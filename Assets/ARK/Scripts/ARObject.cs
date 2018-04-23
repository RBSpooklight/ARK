using System.Runtime.Remoting.Messaging;
using UnityEditor;
using UnityEngine;

namespace Kouji.ARK
{
    public class ARObject : MonoBehaviour 
    {
        protected ARKController GetFirstEnabledControllerinChildren()
        {
            foreach (var controller in GetComponentsInChildren<ARKController>())
            {
                if (controller.enabled)
                    return controller;
            }

            return null;
        }
        
        protected Camera GetCamera()
        {
            //Use the same camera as the ARController
            var controller = GetFirstEnabledControllerinChildren();
            if (controller != null)
                return controller.Camera;
            
            //Or.... Is there a Camera in here ?
            var camera = GetComponent<Camera>();
            if (camera != null)
                return camera;
            
            //Or Fallback to the main Camera
            return Camera.main;
        }

        protected Transform GetRoot()
        {
            var camera = GetCamera();
            if (camera != null)
                return camera.transform.parent;

            return null;
        }

        protected float GetScale()
        {
            var root = GetRoot();
            if (root != null)
                return root.transform.localScale.x;

            return 1f;
        }
    }
}
