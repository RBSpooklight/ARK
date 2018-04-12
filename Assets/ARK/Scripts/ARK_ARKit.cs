using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.iOS;

namespace Kouji.ARK 
{
    internal class ARK_ARKit : ARKInterface
    {
        private Camera m_camera;
        
        private ARKitWorldTrackingSessionConfiguration m_config;
        
        private ARBackgroundRenderer m_backgroundRenderer;
    
        private LightEstimate m_lightEstimate;

        private Material m_clearMaterial;
    
        private bool m_isBackgroundRendering;
        private bool m_canRenderBackground;
        private bool m_isTexturesInitialized;

        private int m_currentFrameIndex;
        
        private int m_cameraWidth;
        private int m_cameraHeight;
        
        private byte[] m_textureYBytes;
        private byte[] m_textureUVBytes;
        private byte[] m_textureYBytes2;
        private byte[] m_textureUVBytes2;
        
        private Vector3[] m_pointCloudData;

        private Matrix4x4 m_displayTransform;
        
        private Dictionary<string, Anchor> m_anchors = new Dictionary<string, Anchor>();
        
        //Handles
        private GCHandle m_YArrayHandle;
        private GCHandle m_UVArrayHandle;
    
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
    
        public override IEnumerator Start(Configuration _config)
        {
            m_config = new ARKitWorldTrackingSessionConfiguration(
                UnityARAlignment.UnityARAlignmentGravity,
                (UnityARPlaneDetection) _config.mode,
                _config.enablePointCloud,
                _config.enableLightEstimation
            );
    
            if (!IsSupported)
                throw new NotSupportedException("ARKit not supported, is your iOS version too old?");
    
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
           
            UnityARSessionNativeInterface.GetARSessionNativeInterface().Pause();
            
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

        protected override bool TryGetUnscaledPose(ref Pose pose)
        {
            Matrix4x4 matrix = NativeInterface.GetCameraPose();
            pose.position = UnityARMatrixOps.GetPosition(matrix);
            pose.rotation = UnityARMatrixOps.GetRotation(matrix);

            return true;
        }

        public override bool TryGetCameraImage(ref Image image)
        {
            ARTextureHandles handles;
            
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

            #if !UNITY_EDITOR && UNITY_IOS
            ARTextureHandles handles =
                UnityARSessionNativeInterface.GetARSessionNativeInterface().GetARVideoTextureHandles();
            #endif
        }

        //blabla
        private Vector3 GetWorldPosition(ARPlaneAnchor _anchor)
        {
            var offset = new Vector3(_anchor.center.x, _anchor.center.y, -(_anchor.center.z));
    
            return UnityARMatrixOps.GetPosition(_anchor.transform) + offset;
        }
    
        private TrackedPlane GetTrackedPlane(ARPlaneAnchor _anchor)
        {
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
            OnPlaneAdded(GetTrackedPlane(_anchor));
        }
        
        private void UpdateAnchor(ARPlaneAnchor _anchor)
        {
            OnPlaneUpdated(GetTrackedPlane(_anchor));
        }
    
        private void RemoveAnchor(ARPlaneAnchor _anchor)
        {
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
        
        //blabla
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
    }

}