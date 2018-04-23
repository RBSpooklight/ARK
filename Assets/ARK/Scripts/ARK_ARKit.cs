using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.iOS;

namespace Kouji.ARK 
{
    internal sealed class ARK_ARKit : ARKInterface
    {
        #region Members

        private float m_currentNearClip;
        private float m_currentFarClip;
        
        private int m_currentFrameIndex;
        private int m_cameraWidth;
        private int m_cameraHeight;
        
        private bool m_isBackgroundRendering;
        private bool m_canRenderBackground;
        private bool m_isTexturesInitialized;
 
        private byte[] m_textureYBytes;
        private byte[] m_textureUVBytes;
        private byte[] m_textureYBytes2;
        private byte[] m_textureUVBytes2;
        
        private Camera m_camera;
        
        private ARKitWorldTrackingSessionConfiguration m_config;
        
        private ARBackgroundRenderer m_backgroundRenderer;
    
        private LightEstimate m_lightEstimate;

        private Material m_clearMaterial;
        
        private Vector3[] m_pointCloudData;

        private Matrix4x4 m_displayTransform = new Matrix4x4();
        
        private Dictionary<string, Anchor> m_anchors = new Dictionary<string, Anchor>();

        private Texture2D m_videoTextureY;
        private Texture2D m_videoTextureCbCr;
        
        //Handles
        private GCHandle m_YArrayHandle;
        private GCHandle m_UVArrayHandle;

        #region Accessors

        private UnityARSessionNativeInterface NativeInterface
        {
            get { return UnityARSessionNativeInterface.GetARSessionNativeInterface(); }
        }
    
        public override bool IsSupported
        {
            get { return m_config.IsSupported; }
        }
    
        public override bool IsRenderingBackground
        {
            get { return m_isBackgroundRendering && m_canRenderBackground; }
            set
            {
                if (m_backgroundRenderer == null)
                    return;
    
                m_isBackgroundRendering = value;
    
                var shouldRenderMaterial = (m_isBackgroundRendering && m_canRenderBackground);
                m_backgroundRenderer.mode = 
                    shouldRenderMaterial ? ARRenderMode.MaterialAsBackground : ARRenderMode.StandardBackground;
            }
        }

        #endregion

        #endregion
        
        #region Private_API
        
        private Vector3 GetWorldPosition(ARPlaneAnchor _anchor)
        {
            Debug.Assert(_anchor != null);
            
            var offset = new Vector3(_anchor.center.x, _anchor.center.y, -(_anchor.center.z));
    
            return UnityARMatrixOps.GetPosition(_anchor.transform) + offset;
        }
    
        private TrackedPlane GetTrackedPlane(ARPlaneAnchor _anchor)
        {
            Debug.Assert(_anchor != null);
            
            return new TrackedPlane()
            {
                id = _anchor.identifier,
                center = GetWorldPosition(_anchor),
                rotation = UnityARMatrixOps.GetRotation(_anchor.transform),
                extents = new Vector2(_anchor.extent.x, _anchor.extent.z)
            };
        }
        
        private void AddAnchor(ARPlaneAnchor _anchor)
        {
            Debug.Assert(_anchor != null);
            
            OnPlaneAdded(GetTrackedPlane(_anchor));
        }
        
        private void UpdateAnchor(ARPlaneAnchor _anchor)
        {
            Debug.Assert(_anchor != null);
            
            OnPlaneUpdated(GetTrackedPlane(_anchor));
        }
    
        private void RemoveAnchor(ARPlaneAnchor _anchor)
        {
            Debug.Assert(_anchor != null);
            
            OnPlaneRemoved(GetTrackedPlane(_anchor));
        }
        
        private void UpdateFrame(UnityARCamera _camera)
        {
            if (!m_isTexturesInitialized)
            {
                m_cameraWidth = _camera.videoParams.yWidth;
                m_cameraHeight = _camera.videoParams.yHeight;
    
                int countYBytes = m_cameraWidth * m_cameraHeight;
                int countUVBytes = m_cameraWidth * m_cameraHeight / 2; // 1/4 res, but 2bytes per pixel
                
                m_textureYBytes = new byte[countYBytes];
                m_textureUVBytes = new byte[countUVBytes];
                
                m_textureYBytes2 = new byte[countYBytes];
                m_textureUVBytes2 = new byte[countUVBytes];
                
                m_YArrayHandle = GCHandle.Alloc(m_textureYBytes);
                m_UVArrayHandle = GCHandle.Alloc(m_textureUVBytes);
    
                m_isTexturesInitialized = true;
            }
    
            m_pointCloudData = _camera.pointCloudData;

            m_lightEstimate.mode = LightEstimateMode.AmbientColorTemperature | LightEstimateMode.AmbientIntensity;
            m_lightEstimate.ambientColorTemperature = _camera.lightData.arLightEstimate.ambientColorTemperature;
            
            //Approximatively convert ARKit intensity range (0 - 2000) to Unity intensity range (0 - 8)
            m_lightEstimate.ambientIntensity = _camera.lightData.arLightEstimate.ambientIntensity / 1000f;
            
            //Transform Matrix
            m_displayTransform.SetColumn(0, _camera.displayTransform.column0);
            m_displayTransform.SetColumn(1, _camera.displayTransform.column1);
            m_displayTransform.SetColumn(2, _camera.displayTransform.column2);
            m_displayTransform.SetColumn(3, _camera.displayTransform.column3);
        }

        private void UpdateUserAnchor(ARUserAnchor _userAnchor)
        {
            Anchor anchor;
            if (m_anchors.TryGetValue(_userAnchor.identifier, out anchor))
            {
                anchor.transform.position = _userAnchor.transform.GetColumn(3);
                anchor.transform.rotation = _userAnchor.transform.rotation;
            }
        }
        
        private IntPtr HandleByteArray(ref GCHandle handle, byte[] _array)
        {
            handle.Free();
            handle = GCHandle.Alloc(_array, GCHandleType.Pinned);

            return handle.AddrOfPinnedObject();
        }
        
        private byte[] ByteArrayForFrame(int frame, byte[] array0, byte[] array1)
        {
            return frame == 1 ? array1 : array0;
        }

        private byte[] YByteArrayForFrame(int frame)
        {
            return ByteArrayForFrame(frame, m_textureYBytes, m_textureYBytes2);
        }

        private byte[] UVByteArrayForFrame(int frame)
        {
            return ByteArrayForFrame(frame, m_textureUVBytes, m_textureUVBytes2);
        }
        
        #endregion
    
        public override IEnumerator Start(Configuration _config)
        {
            m_config = new ARKitWorldTrackingSessionConfiguration(
                UnityARAlignment.UnityARAlignmentGravity,
                (UnityARPlaneDetection) _config.mode,
                _config.enablePointCloud,
                _config.enableLightEstimation
            );
    
            if (!IsSupported)
                throw new NotSupportedException("ARKit not supported, your device may not be powerful enough.");
    
            var runOptions =
                UnityARSessionRunOption.ARSessionRunOptionRemoveExistingAnchors | UnityARSessionRunOption.ARSessionRunOptionResetTracking;
            
            NativeInterface.RunWithConfigAndOptions(m_config, runOptions);
    
            //Register Callbacks
            UnityARSessionNativeInterface.ARAnchorAddedEvent += AddAnchor;
            UnityARSessionNativeInterface.ARAnchorUpdatedEvent += UpdateAnchor;
            UnityARSessionNativeInterface.ARAnchorRemovedEvent += RemoveAnchor;
            UnityARSessionNativeInterface.ARFrameUpdatedEvent += UpdateFrame;
            UnityARSessionNativeInterface.ARUserAnchorUpdatedEvent += UpdateUserAnchor;
            
            IsRunning = true;
    
            return null;
        }

        public override void Stop()
        {
            var anchors = m_anchors.Values;
            foreach (var anchor in anchors)
            {
                DestroyAnchor(anchor);
            }
            
            //Unregister Callbacks
            UnityARSessionNativeInterface.ARAnchorAddedEvent -= AddAnchor;
            UnityARSessionNativeInterface.ARAnchorUpdatedEvent -= UpdateAnchor;
            UnityARSessionNativeInterface.ARAnchorRemovedEvent -= RemoveAnchor;
            UnityARSessionNativeInterface.ARFrameUpdatedEvent -= UpdateFrame;
            UnityARSessionNativeInterface.ARUserAnchorUpdatedEvent -= UpdateUserAnchor;
           
            NativeInterface.Pause();
            
            NativeInterface.SetCapturePixelData(false, IntPtr.Zero, IntPtr.Zero);
            m_YArrayHandle.Free();
            m_UVArrayHandle.Free();

            m_isTexturesInitialized = false;
            
            m_isBackgroundRendering = false;
            m_canRenderBackground = false;
            
            m_backgroundRenderer.backgroundMaterial = null;
            m_backgroundRenderer.camera = null;
            m_backgroundRenderer = null;
            
            IsRunning = false;
        }
        
        public override void SetupCamera(Camera _camera)
        {
            m_camera = _camera;
            m_clearMaterial = Resources.Load<Material>("YUVMaterial");
            
            m_backgroundRenderer = new ARBackgroundRenderer();
            m_backgroundRenderer.backgroundMaterial = m_clearMaterial;
            m_backgroundRenderer.camera = m_camera;
        }

        public override void UpdateCamera(Camera _camera)
        {
            _camera.projectionMatrix = NativeInterface.GetCameraProjection();

            if (!m_isBackgroundRendering)
                return;

            var handles = default(ARTextureHandles);

            //Preprocessor locked because 'GetARVideoTextureHandles' is not declared in all platforms
            #if !UNITY_EDITOR && UNITY_IOS
            handles = NativeInterface.GetARVideoTextureHandles();
            #endif

            if (handles.textureY == IntPtr.Zero || handles.textureCbCr == IntPtr.Zero)
            {
                m_canRenderBackground = false;
                return;
            }

            m_canRenderBackground = true;
            IsRenderingBackground = m_isBackgroundRendering;

            Resolution currentResolution = Screen.currentResolution;
            
            //Texture Y
            if (m_videoTextureY == null)
            {
                m_videoTextureY = Texture2D.CreateExternalTexture(
                    currentResolution.width, 
                    currentResolution.height,
                    TextureFormat.R8,
                    false,
                    false,
                    handles.textureY
                );

                m_videoTextureY.filterMode = FilterMode.Bilinear;
                m_videoTextureY.wrapMode = TextureWrapMode.Repeat;
                
                m_clearMaterial.SetTexture("_textureY", m_videoTextureY);
            }
            
            //Texture CbCr
            if (m_videoTextureCbCr == null)
            {
                m_videoTextureCbCr = Texture2D.CreateExternalTexture(
                    currentResolution.width, 
                    currentResolution.height,
                    TextureFormat.RG16,
                    false,
                    false,
                    handles.textureCbCr
                );

                m_videoTextureCbCr.filterMode = FilterMode.Bilinear;
                m_videoTextureCbCr.wrapMode = TextureWrapMode.Repeat;
                
                m_clearMaterial.SetTexture("_textureCbCr", m_videoTextureCbCr);
            }
            
            m_videoTextureY.UpdateExternalTexture(handles.textureY);
            m_videoTextureCbCr.UpdateExternalTexture(handles.textureCbCr);
            
            m_clearMaterial.SetMatrix("_DisplayTransform", m_displayTransform);
        }

        public override void Update()
        {
            //Update camera frustrum if needed
            if (Math.Abs(m_currentNearClip - m_camera.nearClipPlane) > Mathf.Epsilon || 
                Math.Abs(m_currentFarClip - m_camera.farClipPlane) > Mathf.Epsilon)
            {
                m_currentNearClip = m_camera.nearClipPlane;
                m_currentFarClip = m_camera.farClipPlane;
                
                NativeInterface.SetCameraClipPlanes(m_currentNearClip, m_currentFarClip);
            }
        }

        protected override bool TryGetUnscaledPose(ref Pose pose)
        {
            Matrix4x4 matrix = NativeInterface.GetCameraPose();
            pose.position = UnityARMatrixOps.GetPosition(matrix);
            pose.rotation = UnityARMatrixOps.GetRotation(matrix);

            return true;
        }

        public override bool TryGetCameraImage(ref Image image)
        {
            var handles = default(ARTextureHandles);
            
            //Preprocessor locked because 'GetARVideoTextureHandles' is not declared in all platforms
            #if !UNITY_EDITOR && UNITY_IOS
            handles = NativeInterface.GetARVideoTextureHandles();
            #endif
            
            if (!m_isTexturesInitialized || (handles.textureY == IntPtr.Zero || handles.textureCbCr == IntPtr.Zero))
                return false;

            m_currentFrameIndex = (m_currentFrameIndex + 1) % 2;
            
            NativeInterface.SetCapturePixelData(true,
                HandleByteArray(ref m_YArrayHandle, YByteArrayForFrame(m_currentFrameIndex)),
                HandleByteArray(ref m_UVArrayHandle, UVByteArrayForFrame(m_currentFrameIndex))
            );

            image.y = YByteArrayForFrame(1 - m_currentFrameIndex);
            image.uv = UVByteArrayForFrame(1 - m_currentFrameIndex);
            
            image.width = m_cameraWidth;
            image.height = m_cameraHeight;

            return true;
        }

        public override bool TryGetPointCloud(ref PointCloud pointCloud)
        {
            if (m_pointCloudData == null)
                return false;
            
            if (pointCloud.points == null)
                pointCloud.points = new List<Vector3>();
            
            pointCloud.points.Clear();
            pointCloud.points.AddRange(m_pointCloudData);

            return true;
        }

        public override LightEstimate GetLightEstimate()
        {
            return m_lightEstimate;
        }

        public override Matrix4x4 GetDisplayTranform()
        {
            return m_displayTransform;
        }

        public override void ApplyAnchor(Anchor _anchor)
        {
            if (!IsRunning)
                return;

            Transform t = _anchor.transform;
            Matrix4x4 matrix = Matrix4x4.TRS(t.position, t.rotation, t.localScale);
            
            var anchorData = new UnityARUserAnchorData();
            anchorData.transform.column0 = matrix.GetColumn(0);
            anchorData.transform.column1 = matrix.GetColumn(1);
            anchorData.transform.column2 = matrix.GetColumn(2);
            anchorData.transform.column3 = matrix.GetColumn(3);
            anchorData = NativeInterface.AddUserAnchor(anchorData);

            _anchor.id = anchorData.identifierStr;
            m_anchors[_anchor.id] = _anchor;
        }

        public override void DestroyAnchor(Anchor _anchor)
        {
            if (string.IsNullOrEmpty(_anchor.id)) 
                return;
            
            NativeInterface.RemoveUserAnchor(_anchor.id);
            if (m_anchors.ContainsKey(_anchor.id))
                m_anchors.Remove(_anchor.id);

            _anchor.id = null;
        }
    }

}