/*
KINEMATICSOUP CONFIDENTIAL
 Copyright(c) 2014-2025 KinematicSoup Technologies Incorporated 
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
using System.Reflection;
using KS.Unity.Editor;
using UnityEditor;
using UnityEngine;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>
    /// Base class that enables Reactor to define new editors which extend or override the rendering of Unity inspector editors.
    /// </summary>
    public class ksOverrideEditor : UnityEditor.Editor
    {
        [NonSerialized]
        protected UnityEditor.Editor m_baseEditor;
        private ksReflectionObject m_reflectionEditor = ksReflectionObject.Void;
        protected ksReflectionObject m_roOnEnable;
        protected ksReflectionObject m_roOnSceneGUI;
        protected ksReflectionObject m_roOnPreSceneGUI;
        protected ksReflectionObject m_roOnHeaderGUI;
        protected ksReflectionObject m_roOnDestroy;

        /// <summary>
        /// Base editor we're overriding.
        /// </summary>
        public UnityEditor.Editor BaseEditor
        {
            get { return m_baseEditor; }
        }

        /// <summary>
        /// Base editor reflection object.
        /// </summary>
        public ksReflectionObject ReflectionEditor
        {
            get { return m_reflectionEditor; }
        }

        /// <summary>
        /// Unity OnEnable.
        /// </summary>
        protected virtual void OnEnable()
        {
            m_roOnEnable.Invoke();
        }

        /// <summary>
        /// Destroy the base editor when we are destroyed.
        /// </summary>
        protected virtual void OnDisable()
        {
            if (m_baseEditor != null)
            {
                DestroyImmediate(m_baseEditor);
            }
        }

        /// <summary>
        /// Unity OnDestroy.
        /// </summary>
        protected virtual void OnDestroy()
        {
            m_roOnDestroy.Invoke();
        }

        /// <summary>
        /// Calls the base editor's OnSceneGUI.
        /// </summary>
#if UNITY_2019_3_OR_NEWER
        /// <param name="sceneView"></param>
        protected virtual void OnSceneGUI(SceneView sceneView)
#else
        protected virtual void OnSceneGUI()
#endif
        {
            m_roOnSceneGUI.Invoke();
        }

        /// <summary>
        /// Calls the base editor's OnPreSceneGUI.
        /// </summary>
        protected virtual void OnPreSceneGUI()
        {
            m_roOnPreSceneGUI.Invoke();
        }

        /// <summary>
        /// Calls the base editor's OnHeaderGUI.
        /// </summary>
        protected override void OnHeaderGUI()
        {
            m_roOnHeaderGUI.Invoke();
        }

        /// <summary>
        /// Calls the base editor's DrawPreview.
        /// </summary>
        /// <param name="previewArea"></param>
        public override void DrawPreview(Rect previewArea)
        {
            if (m_baseEditor != null)
            {
                m_baseEditor.DrawPreview(previewArea);
            }
        }

        /// <summary>
        /// Calls the base editor's GetPreviewTitle.
        /// </summary>
        /// <returns></returns>
        public override GUIContent GetPreviewTitle()
        {
            return m_baseEditor == null ? null : m_baseEditor.GetPreviewTitle();
        }

        /// <summary>
        /// Calls the base editor's GetInfoString.
        /// </summary>
        /// <returns></returns>
        public override string GetInfoString()
        {
            return m_baseEditor == null ? "" : m_baseEditor.GetInfoString();
        }

        /// <summary>
        /// Calls the base editor's HasPreviewGUI.
        /// </summary>
        /// <returns></returns>
        public override bool HasPreviewGUI()
        {
            return m_baseEditor == null ? false : m_baseEditor.HasPreviewGUI();
        }

        /// <summary>
        /// Calls the base editor's OnInspectorGUI.
        /// </summary>
        public override void OnInspectorGUI()
        {
            if (m_baseEditor != null)
            {
                m_baseEditor.OnInspectorGUI();
            }
        }

        /// <summary>
        /// Calls the base editor's OnInteractivePreviewGUI.
        /// </summary>
        /// <param name="r"></param>
        /// <param name="background"></param>
        public override void OnInteractivePreviewGUI(Rect r, GUIStyle background)
        {
            if (m_baseEditor != null)
            {
                m_baseEditor.OnInteractivePreviewGUI(r, background);
            }
        }

        /// <summary>
        /// Calls the base editor's OnPreviewGUI.
        /// </summary>
        /// <param name="r"></param>
        /// <param name="background"></param>
        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            if (m_baseEditor != null)
            {
                m_baseEditor.OnPreviewGUI(r, background);
            }
        }

        /// <summary>
        /// Calls the base editor's OnPreviewSettings.
        /// </summary>
        public override void OnPreviewSettings()
        {
            if (m_baseEditor != null)
            {
                m_baseEditor.OnPreviewSettings();
            }
        }

        /// <summary>
        /// Calls the base editor's RequiresConstantRepaint.
        /// </summary>
        /// <returns></returns>
        public override bool RequiresConstantRepaint()
        {
            return m_baseEditor == null ? false : m_baseEditor.RequiresConstantRepaint();
        }

        /// <summary>
        /// Calls the base editor's UseDefaultMargins.
        /// </summary>
        /// <returns></returns>
        public override bool UseDefaultMargins()
        {
            return m_baseEditor == null ? true : m_baseEditor.UseDefaultMargins();
        }

        /// <summary>
        /// Calls the base editor's RenderStaticPreview.
        /// </summary>
        /// <param name="assetPath"></param>
        /// <param name="subAssets"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public override Texture2D RenderStaticPreview(string assetPath, UnityEngine.Object[] subAssets, int width, int height)
        {
            return m_baseEditor == null ? null : m_baseEditor.RenderStaticPreview(assetPath, subAssets, width, height);
        }

        /// <summary>
        /// Loads a Unity editor class via reflection to be the base editor. Does nothing if the base editor 
        /// is already set.
        /// </summary>
        /// <param name="className"></param>
        /// <param name="assemblyName">Name of assembly to load from.</param>
        protected void LoadBaseEditor(string className, string assemblyName = "UnityEditor")
        {
            if (m_baseEditor != null)
            {
                return;
            }
            try
            {
                Assembly unityAssembly = Assembly.Load(assemblyName);
                if (unityAssembly == null)
                {
                    ksLog.Error(this, "Unable to load assembly " + assemblyName);
                    return;
                }
                string typeName = "UnityEditor." + className;
                Type type = unityAssembly.GetType(typeName);
                if (type == null)
                {
                    ksLog.Error(this, "Unable to load type " + typeName);
                }
                else
                {
                    m_baseEditor = CreateEditor(targets, type);
                    if (m_baseEditor == null)
                    {
                        ksLog.Error(this, "Unable to create Editor " + typeName);
                    }
                    else
                    {
                        m_reflectionEditor = new ksReflectionObject(m_baseEditor);
                        // Methods below may not be defined, so we log no errors if they're not found.
                        m_roOnEnable = m_reflectionEditor.GetMethod("OnEnable", true);
                        m_roOnSceneGUI = m_reflectionEditor.GetMethod("OnSceneGUI", true);
                        m_roOnPreSceneGUI = m_reflectionEditor.GetMethod("OnPreSceneGUI", true);
                        m_roOnHeaderGUI = m_reflectionEditor.GetMethod("OnHeaderGUI", true);
                        m_roOnDestroy = m_reflectionEditor.GetMethod("OnDestroy", true);
                    }
                }
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Error loading type " + className, e);
            }
        }
    }
}