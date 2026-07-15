/*
KINEMATICSOUP CONFIDENTIAL
 Copyright(c) 2014-2026 KinematicSoup Technologies Incorporated 
 All Rights Reserved.

NOTICE:  All information contained herein is, and remains the property of 
KinematicSoup Technologies Incorporated and its suppliers, if any. The 
intellectual and technical concepts contained herein are proprietary to 
KinematicSoup Technologies Incorporated and its suppliers and may be covered by
U.S. and Foreign Patents, patents in process, and are protected by trade secret
or copyright law. Dissemination of this information or reproduction of this
material is strictly forbidden unless prior written permission is obtained from
KinematicSoup Technologies Incorporated.
*/
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using KS.SF.Unity.Editor;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>Class that holds vr objects and reflection info.</summary>
    internal class sfVRManager 
    {
        public enum VRDeviceType
        {
            NOT_DEFINED,
            VIVE,
            OCULUS
        }
        /// <summary>VRView type reflection object.</summary>
        public ksReflectionObject VRViewType
        {
            get
            {
                return m_vrViewType;
            }
        }
        private ksReflectionObject m_vrViewType = null;
        private ksReflectionObject m_vrCameraProperty = null;

        private Type m_editorVRType = null;
        private Type m_spatialHashModuleType = null;
        private Type m_twoHandedProxyBaseType = null;
        private MethodInfo m_getModuleMethod = null;
        private ksReflectionObject m_addObjectMethod = null;
        private ksReflectionObject m_leftHandField = null;
        private ksReflectionObject m_rightHandField = null;

        private MonoBehaviour m_spatialHashModule = null;

        /// <summary>Singleton instance.</summary>
        public static sfVRManager Instance
        {
            get { return m_instance; }
        }
        private static sfVRManager m_instance = new sfVRManager();

        /// <summary>VR device type</summary>
        public VRDeviceType DeviceType
        {
            get
            {
                string model = GetFirstXRInputDeviceName();
                if (model == "Oculus Rift")
                {
                    return VRDeviceType.OCULUS;
                }
                else if (model == "Vive MV")
                {
                    return VRDeviceType.VIVE;
                }
                return VRDeviceType.NOT_DEFINED;
            }
        }


        /// <summary>VR camera</summary>
        public Camera Camera
        {
            get
            {
                if (m_camera == null && m_vrCameraProperty != null)
                {
                    m_camera = (Camera)m_vrCameraProperty.GetValue();
                }
                return m_camera;
            }
        }
        private Camera m_camera = null;



        /// <summary>VR camera transform</summary>
        public Transform CameraTransform
        {
            get
            {
                if (m_cameraTransform == null && Camera != null)
                {
                    m_cameraTransform = Camera.transform;
                }
                return m_cameraTransform;
            }
        }
        private Transform m_cameraTransform = null;

        /// <summary>VR left controller.</summary>
        public Transform LeftController
        {
            get
            {
                if (m_leftController == null)
                {
                    FindControllers();
                }
                return m_leftController;
            }
        }
        private Transform m_leftController = null;

        /// <summary>VR right controller.</summary>
        public Transform RightController
        {
            get
            {
                if (m_rightController == null)
                {
                    FindControllers();
                }
                return m_rightController;
            }
        }
        private Transform m_rightController = null;

        /// <summary>Is in VR mode?</summary>
        public bool InVRMode
        {
            get { return Camera != null; }
        }

        /// <summary>Constructor</summary>
        private sfVRManager()
        {
            m_editorVRType = sfTypeCache.Get().Load("UnityEditor.Experimental.EditorVR.Core.EditorVR");
            if (m_editorVRType == null)
            {
                return;
            }

            Type vrViewType = sfTypeCache.Get().Load("UnityEditor.Experimental.EditorVR.Core.VRView");
            if (vrViewType == null)
            {
                return;
            }
            m_vrViewType = new ksReflectionObject(vrViewType);
            m_vrCameraProperty = m_vrViewType.GetProperty("viewerCamera");

            m_spatialHashModuleType = sfTypeCache.Get().Load("UnityEditor.Experimental.EditorVR.Modules.SpatialHashModule");
            if (m_spatialHashModuleType == null)
            {
                return;
            }
            m_addObjectMethod = new ksReflectionObject(m_spatialHashModuleType).GetMethod("AddObject",
                BindingFlags.Public | BindingFlags.Instance,
                new Type[] { typeof(GameObject) });

            Type viveProxyType = sfTypeCache.Get().Load("UnityEditor.Experimental.EditorVR.Proxies.ViveProxy");
            m_twoHandedProxyBaseType = viveProxyType.BaseType;
            if (m_twoHandedProxyBaseType == null)
            {
                return;
            }
            ksReflectionObject twoHandedProxyBaseType = new ksReflectionObject(m_twoHandedProxyBaseType);
            m_leftHandField = twoHandedProxyBaseType.GetField("m_LeftHand");
            m_rightHandField = twoHandedProxyBaseType.GetField("m_RightHand");
        }

        /// <summary>
        /// Add game object to spatial hash. Only game object added can be selected in VR mode with controller.
        /// </summary>
        /// <param name="gameObject"></param>
        public void AddToSpatialHash(GameObject gameObject)
        {
            if (m_addObjectMethod != null && GetSpatialHashModule() != null)
            {
                m_addObjectMethod.InstanceInvoke(m_spatialHashModule, gameObject);
            }
        }

        /// <summary>Get spatial hash module by reflection.</summary>
        /// <returns>SpatialHashModule object</returns>
        private MonoBehaviour GetSpatialHashModule()
        {
            if (!InVRMode)
            {
                return null;
            }

            if (m_spatialHashModule != null)
            {
                return m_spatialHashModule;
            }

            if (m_getModuleMethod == null)
            {
                if (m_editorVRType == null)
                {
                    return null;
                }
                else if (m_spatialHashModuleType != null)
                {
                    MethodInfo getModuleMethod = m_editorVRType.GetMethod("GetModule",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (getModuleMethod != null)
                    {
                        m_getModuleMethod = getModuleMethod.MakeGenericMethod(m_spatialHashModuleType);
                    }
                }
            }

            if (m_getModuleMethod != null)
            {
                GameObject editorVRGameObject = Camera.gameObject.transform.root.gameObject;
                MonoBehaviour editorVR = (MonoBehaviour)editorVRGameObject.GetComponent(m_editorVRType);
                object obj = m_getModuleMethod.Invoke(editorVR, null);

                m_spatialHashModule = (MonoBehaviour)obj;
            }
            return m_spatialHashModule;
        }

        /// <summary>Finds controllers' transforms.</summary>
        private void FindControllers()
        {
            if (CameraTransform != null)
            {
                Transform editorVRCameraPivot = CameraTransform.parent;
                if (editorVRCameraPivot == null || m_leftHandField == null || m_rightHandField == null)
                {
                    return;
                }

                m_leftController = null;
                m_rightController = null;

                Transform touchProxy = editorVRCameraPivot.Find("TouchProxy");
                if (touchProxy != null && FindControllersOnProxy(touchProxy.gameObject))
                {
                    return;
                }

                Transform viveProxy = editorVRCameraPivot.Find("ViveProxy");
                if (viveProxy != null && FindControllersOnProxy(viveProxy.gameObject))
                {
                    return;
                }

                Transform sixenseProxy = editorVRCameraPivot.Find("SixenseProxy");
                if (sixenseProxy != null && FindControllersOnProxy(sixenseProxy.gameObject))
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Tries to finds controllers' transforms on given proxy game object.
        /// Returns true if found active controller transform.
        /// </summary>
        /// <param name="proxyGameObject"></param>
        /// <returns></returns>
        private bool FindControllersOnProxy(GameObject proxyGameObject)
        {
            if (proxyGameObject != null)
            {
                Component proxy = proxyGameObject.GetComponent(m_twoHandedProxyBaseType);
                if (proxy != null)
                {
                    Transform leftController = m_leftHandField.GetValue(proxy) as Transform;
                    Transform rightController = m_rightHandField.GetValue(proxy) as Transform;
                    if (leftController != null && leftController.gameObject.activeSelf)
                    {
                        m_leftController = leftController;
                    }
                    if (rightController != null && rightController.gameObject.activeSelf)
                    {
                        m_rightController = rightController;
                    }
                    if (m_leftController != null || m_rightController != null)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary></summary>
        /// <returns>first XR input device's name</returns>
        private string GetFirstXRInputDeviceName()
        {
            List<UnityEngine.XR.InputDevice> inputDevices = new List<UnityEngine.XR.InputDevice>();
            UnityEngine.XR.InputDevices.GetDevices(inputDevices);
            if (inputDevices.Count > 0)
            {
                return inputDevices[0].name;
            }
            return null;
        }
    }
}
