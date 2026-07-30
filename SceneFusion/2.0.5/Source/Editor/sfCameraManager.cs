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
using UnityEngine;
using UnityEditor;
using KS.SF.Reactor;
using System.Linq;
using KS.SF.Unity.Editor;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>Class that holds references to last active cameras.</summary>
    public class sfCameraManager
    {
        private ksReflectionObject m_viewIsLockedToObjectProperty = null;

        /// <summary>Singleton instance.</summary>
        public static sfCameraManager Get()
        {
            return m_instance;
        }
        private static sfCameraManager m_instance = new sfCameraManager();

        /// <summary>Last active scene view camera</summary>
        public Camera LastSceneCamera
        {
            get
            {
                return m_lastSceneCamera;
            }

            set
            {
                m_lastSceneCamera = value;
            }
        }
        private Camera m_lastSceneCamera = null;

        /// <summary>Last active game view camera</summary>
        public Camera LastGameCamera
        {
            get
            {
                return m_lastGameCamera;
            }

            set
            {
                m_lastGameCamera = value;
            }
        }
        private Camera m_lastGameCamera = null;

        /// <summary>Last active camera</summary>
        public Camera LastActiveCamera
        {
            get
            {
                if (sfVRManager.Instance.Camera != null)
                {
                    return sfVRManager.Instance.Camera;
                }

                if (Application.isPlaying
                    && EditorWindow.focusedWindow != null
                    && EditorWindow.focusedWindow.titleContent.text == "Game")
                {
                    return m_lastGameCamera;
                }
                else
                {
                    return m_lastSceneCamera;
                }
            }
        }

        /// <summary>
        /// Calls look at method on current scene view, then fixes size if the given size is negative.
        /// </summary>
        /// <param name="pivot"></param>
        /// <param name="rotation"></param>
        /// <param name="size">distance between pivot and camera position in perspective mode.</param>
        /// <param name="orthographic"></param>
        /// <param name="instant">If true, look at the given pivot immediately.</param>
        public void SceneViewLookAt(Vector3 pivot, Quaternion rotation, float size, bool orthographic, bool instant)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null)
            {
                return;
            }

            // Unlocks scene view from object.
            if (m_viewIsLockedToObjectProperty == null)
            {
                m_viewIsLockedToObjectProperty = new ksReflectionObject(
                    typeof(SceneView)).GetProperty("viewIsLockedToObject");
            }
            m_viewIsLockedToObjectProperty.SetValue(view, false);

            view.LookAt(pivot, rotation, size, orthographic, instant);
            if (size < 0f)
            {
                view.size = size;
            }
        }
    }
}
