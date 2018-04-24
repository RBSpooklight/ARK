using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kouji.ARK 
{
    public abstract class ARKInterface
    {   
        #region Enumerators
        
        /// <summary>
        /// Enumerator used in Configuration structure to specify plane detection capabilities.
        /// </summary>
        [Flags]
        public enum PlaneDetectionMode
        {
            None         = 0,
            Horizontal   = 1 << 0,
            Vertical     = 1 << 1,
            HorizontalAndVertical = (1 << 1) | (1 << 0)
        }

        /// <summary>
        /// Enumerator used in LightEstimate structure to specify light estimation capabilities.
        /// </summary>
        [Flags]
        public enum LightEstimateMode
        {
            None                     = 0,
            AmbientIntensity         = 1 << 0,
            AmbientColorTemperature  = 1 << 1
        }
        
        #endregion
        
        #region Structures
        
        /// <summary>
        /// Structure used to specify what features you want to enable for the AR session.
        /// </summary>
        public struct Configuration
        {
            public bool enablePointCloud;
            public bool enableLightEstimation;
            public PlaneDetectionMode mode;
        }

        /// <summary>
        /// Structure containing the Camera Image.
        /// </summary>
        public struct Image
        {
            public byte[] y;
            public byte[] uv;
            public int width;
            public int height;
        }

        /// <summary>
        /// Structure containing the tracked feature points.
        /// </summary>
        public struct PointCloud
        {
            public List<Vector3> points;
        }

        /// <summary>
        /// Structure containing the current light estimate values.
        /// </summary>
        public struct LightEstimate
        {
            public float ambientIntensity;
            public float ambientColorTemperature;
            public LightEstimateMode mode;
        }

        #endregion

        #region Members

        /// <summary>
        /// Current used interface, either ARKit or ARCore depending on the device.
        /// </summary>
        private static ARKInterface m_interface;

        //Events
        public static Action<TrackedPlane> onPlaneAdded;
        public static Action<TrackedPlane> onPlaneUpdated;
        public static Action<TrackedPlane> onPlaneRemoved;
        
        /// <summary>
        /// Returns true if ARK is started and active, false otherwise.
        /// </summary>
        public virtual bool IsRunning
        {
            get;
            protected set;
        }
        
        /// <summary>
        /// Returns true if the current used Interface is supported on the device, false otherwise.
        /// </summary>
        public virtual bool IsSupported
        {
            get { return true; }
        }
        
        /// <summary>
        /// Get: Returns true if background rendering is enabled, false otherwise.
        /// Set: Either you should render the background material or not.
        /// </summary>
        public virtual bool IsRenderingBackground
        {
            get { return false; }
            set { throw new NotSupportedException("'IsRenderingBackground' called on ARK abstract class."); }
        }

        #endregion

        #region Abstract_API
        
        //TODO: ARK PAUSE?

        /// <summary>
        /// Starts ARK with a specified Configuration.
        /// </summary>
        /// <remarks>You can use this method to unpause ARK.</remarks>
        public abstract IEnumerator Start(Configuration _config);
        
        /// <summary>
        /// Stops ARK.
        /// </summary>
        public abstract void Stop();

        /// <summary>
        /// Called to specify to ARK that you want to define '_camera' as the AR Camera.
        /// </summary>
        public abstract void SetupCamera(Camera _camera);
        
        /// <summary>
        /// Called to perform any change on '_camera'.
        /// e.g: Projection Matrix or even Field Of View
        /// </summary>
        public abstract void UpdateCamera(Camera _camera);
        
        /// <summary>
        /// Called to do per-frame updates.
        /// e.g: Update tracked planes or augmentations.
        /// </summary>
        public abstract void Update();

        /// <summary>
        /// Populates 'pose' with the current camera pose (Location and Rotation).
        /// </summary>
        protected abstract bool TryGetUnscaledPose(ref Pose pose);
        
        /// <summary>
        /// Populates 'image' with the current camera image.
        /// </summary>
        public abstract bool TryGetCameraImage(ref Image image);
        
        /// <summary>
        /// Populates 'pointCloud' with currently tracked features.
        /// </summary>
        public abstract bool TryGetPointCloud(ref PointCloud pointCloud);

        /// <summary>
        /// Returns a struct containing estimated light exposure and estimated color temperature.
        /// </summary>
        public abstract LightEstimate GetLightEstimate();
        
        /// <summary>
        /// Returns the projection Matrix.
        /// </summary>
        public abstract Matrix4x4 GetDisplayTranform();

        #endregion

        #region Virtual_API
        
        /// <summary>
        /// Adds an Anchor to the world.
        /// </summary>
        public virtual void ApplyAnchor(Anchor _anchor) {}
        
        /// <summary>
        /// Destroys the specified Anchor.
        /// </summary>
        public virtual void DestroyAnchor(Anchor _anchor) {}

        #endregion

        #region Static_API

        /// <summary>
        /// Returns the currently used interface.
        /// If no interface has been specified, creates the correct one depending on the device.
        /// </summary>
        public static ARKInterface GetInterface()
        {
            if (m_interface == null)
            {
                //TODO: SPECIFIC INTERFACES, FACTORY REFACTO?
                
                #if UNITY_IOS
                    m_interface = new ARK_ARKit();
                #endif
//                #if UNITY_EDITOR
//                    m_Interface = new AREditorInterface();
//                #elif UNITY_IOS
//                    m_Interface = new ARKitInterface();
//                #elif UNITY_ANDROID
//                    m_Interface = new ARCoreInterface();
//                #endif
            }

            return m_interface;
        }
        
        //TODO: DOC, is this method useless?
        public static void SetInterface(ARKInterface _interface)
        {
            m_interface = _interface;
        }

        #endregion
        
        public bool TryGetPose(ref Pose pose)
        {
            return TryGetUnscaledPose(ref pose);
        }

        /// <summary>
        /// Method used to do a safe call of the 'onPlaneAdded' delegate.
        /// </summary>
        protected void OnPlaneAdded(TrackedPlane _plane)
        {
            if (onPlaneAdded != null)
                onPlaneAdded(_plane);
        }
        
        /// <summary>
        /// Method used to do a safe call of the 'onPlaneUpdated' delegate.
        /// </summary>
        protected void OnPlaneUpdated(TrackedPlane _plane)
        {
            if (onPlaneUpdated != null)
                onPlaneUpdated(_plane);
        }
        
        /// <summary>
        /// Method used to do a safe call of the 'onPlaneRemoved' delegate.
        /// </summary>
        protected void OnPlaneRemoved(TrackedPlane _plane)
        {
            if (onPlaneRemoved != null)
                onPlaneRemoved(_plane);
        }
    }
}
