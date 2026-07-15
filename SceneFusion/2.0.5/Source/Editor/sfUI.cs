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
using System.Collections;
using UnityEngine;
using UnityEditor;
using KS.SF.Unity.Editor;
using KS.SF.Reactor;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>Scene Fusion UI class.</summary>
    public class sfUI
    {
        public const float DEFAULT_SCENE_VIEW_SIZE = 1f;

        /// <summary>Scene Fusion UI delegates.</summary>
        public class Delegates
        {
            /// <summary>Follow camera callback.</summary>
            /// <param name="id">id of user to follow or unfollow</param>
            /// <param name="isFollowing">if true, follow. Otherwise, unfollow.</param>
            /// <returns>followed user id. 0 means not following any user.</returns>
            public delegate uint FollowCallback(uint userId, bool isFollowing);

            /// <summary>Unfollow camera callback.</summary>
            public delegate void UnfollowCallback();

            /// <summary>Go to camera callback.</summary>
            /// <param name="id">id of user to to to.</param>
            public delegate void GoToCallback(uint userId);

            /// <summary>Delegate to check if the user agree to leave the session.</summary>
            /// <returns></returns>
            public delegate bool AllowLeaveSession();

            /// <summary>Delegate to draw the settings.</summary>
            public delegate void DrawSettings();

            /// <summary>Delegate for getting the viewport rect from a scene view.</summary>
            /// <param name="sceneView">sceneView to get viewport from.</param>
            /// <returns>viewport</returns>
            public delegate Rect ViewportGetter(SceneView sceneView);
        }

        /// <summary></summary>
        /// <returns>singleton instance.</returns>
        public static sfUI Get()
        {
            return m_instance;
        }
        private static sfUI m_instance = new sfUI();

        /// <summary>Delegate for getting the scene viewport rect.</summary>
        public Delegates.ViewportGetter ViewportGetter;

        private bool m_sceneViewStale = false;
        private bool m_inspectorStale = false;
        private bool m_rebuildInspectors = false;
        private bool m_projectBrowserStale = false;
        private ksReflectionObject m_repaintAllInspectorsMethod;
        private ksReflectionObject m_forceRebuildInspectorsMethod;
        private ksReflectionObject m_forceReloadInspectorsMethod;
        private IList m_projectBrowsers;

        /// <summary>Singleton constructor</summary>
        private sfUI()
        {
            ksReflectionObject editorUtilityClass = new ksReflectionObject(typeof(EditorUtility));
            m_forceRebuildInspectorsMethod = editorUtilityClass.GetMethod("ForceRebuildInspectors");
            m_forceReloadInspectorsMethod = editorUtilityClass.GetMethod("ForceReloadInspectors");
            m_repaintAllInspectorsMethod = new ksReflectionObject(typeof(EditorWindow).Assembly, 
                "UnityEditor.InspectorWindow").GetMethod("RepaintAllInspectors");
        }

        /// <summary>Redraws the scene view on the next update.</summary>
        public void MarkSceneViewStale()
        {
            if (!m_sceneViewStale)
            {
                m_sceneViewStale = true;
                EditorApplication.update += RedrawSceneView;
            }
        }

        /// <summary>Redraws the inspector on the next update if the given object is selected.</summary>
        /// <param name="uobj">if this is selected, will redraw the inspector.</param>
        /// <param name="rebuildInspectors">
        /// if true, rebuilds inspectors, which is slower but necessary for it to
        /// display properly if components were added or deleted.
        /// </param>
        public void MarkInspectorStale(UObject uobj, bool rebuildInspectors = false)
        {
            if (m_inspectorStale && rebuildInspectors && !m_rebuildInspectors && 
                sfSelectionWatcher.Get().IsSelected(uobj))
            {
                m_rebuildInspectors = true;
            }
            else if (!m_inspectorStale && sfSelectionWatcher.Get().IsSelected(uobj))
            {
                if (rebuildInspectors)
                {
                    m_rebuildInspectors = rebuildInspectors;
                }
                m_inspectorStale = true;
                EditorApplication.update += RedrawInspector;
            }
        }

        /// <summary>Redraws the project browser on the next update.</summary>
        public void MarkProjectBrowserStale()
        {
            if (!m_projectBrowserStale)
            {
                m_projectBrowserStale = true;
                EditorApplication.update += RedrawProjectBrowser;
            }
        }

        /// <summary>
        /// Redraws the window(s) showing the uobject's icons on the next update. For game objects and components it's
        /// the hierarchy window, and for assets it's the project browser. For prefabs it is both.
        /// </summary>
        /// <param name="uobject"></param>
        public void MarkIconWindowsStale(UObject uobject)
        {
            if (uobject == null)
            {
                return;
            }
            if (uobject is GameObject || uobject is Component)
            {
                sfHierarchyWatcher.Get().MarkHierarchyStale();
            }
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(uobject)))
            {
                MarkProjectBrowserStale();
            }
        }

        /// <summary>Redraws all scene views.</summary>
        private void RedrawSceneView()
        {
            EditorApplication.update -= RedrawSceneView;
            m_sceneViewStale = false;
            SceneView.RepaintAll();
        }

        /// <summary>Redraws the inspector.</summary>
        private void RedrawInspector()
        {
            EditorApplication.update -= RedrawInspector;
            m_inspectorStale = false;
            if (m_rebuildInspectors)
            {
                m_forceRebuildInspectorsMethod.Invoke();
                m_rebuildInspectors = false;
            }
            else
            {
                m_forceReloadInspectorsMethod.Invoke();
            }
            m_repaintAllInspectorsMethod.Invoke();
        }

        /// <summary>Redraws all project browsers.</summary>
        private void RedrawProjectBrowser()
        {
            EditorApplication.update -= RedrawProjectBrowser;
            m_projectBrowserStale = false;
            if (m_projectBrowsers == null)
            {
                m_projectBrowsers = new ksReflectionObject(typeof(EditorWindow).Assembly, "UnityEditor.ProjectBrowser")
                    .GetField("s_ProjectBrowsers").GetValue() as IList;
                if (m_projectBrowsers == null)
                {
                    return;
                }
            }
            for (int i = 0; i < m_projectBrowsers.Count; i++)
            {
                EditorWindow window = m_projectBrowsers[i] as EditorWindow;
                if (window != null)
                {
                    window.Repaint();
                }
            }
        }

        /// <summary>
        /// Finds a window of a type from the UnityEditor namespace if it is open. This can be used to get instances of
        /// internal Unity windows.
        /// </summary>
        /// <param name="className">name of editor window class to find instance of.</param>
        /// <returns>window, or null if the window wasn't found.</returns>
        public EditorWindow FindWindow(string className)
        {
            UObject[] windows = FindWindows(className);
            return windows.Length <= 0 ? null : (EditorWindow)windows[0];
        }

        /// <summary>
        /// Finds all open windows of a type from the UnityEditor namespace. This can be used to get instances of
        /// internal Unity windows.
        /// </summary>
        /// <param name="className">name of editor window class to find instances of.</param>
        /// <returns>windows of the given class name.</returns>
        public UObject[] FindWindows(string className)
        {
            ksReflectionObject reflection = new ksReflectionObject(typeof(EditorWindow).Assembly,
                "UnityEditor." + className);
            return reflection.Type == null ?
                new UObject[] { } : Resources.FindObjectsOfTypeAll(reflection.Type);
        }

        /// <summary>
        /// Gets the width field labels would have in the inspector if it were the given width. The current view width
        /// is used if no width is provided. The width of field labels depends on the size of inspector window, but is
        /// always a fixed size in other windows. Set EditorGUIUtility.labelWidth to the returned value to make the
        /// label width scale with the window width like it does in the inpsector.
        /// </summary>
        /// <param name="width">width of window. If zero or less, the current view width is used.</param>
        public float GetInspectorLabelWidth(float width = 0f)
        {
            if (width <= 0f)
            {
                width = EditorGUIUtility.currentViewWidth;
            }
            // This is the forumla Unity uses to decide the label width in the inspector. We reverse-engineered this 
            // forula.
            return 120f + Mathf.Max(0, width - 355.55555555555555555f) * .45f;
        }

        /// <summary>Gets the viewport rect by calling the ViewportGetter delegate.</summary>
        /// <param name="sceneView">sceneView to get viewport from.</param>
        /// <returns>viewport, or (0, 0, 0, 0) if the ViewportGetter delegate is null.</returns>
        public Rect GetViewport(SceneView sceneView)
        {
            if (ViewportGetter == null)
            {
                ksLog.Warning(this, "ViewportGetter is null, returning (0, 0, 0, 0).");
                return new Rect();
            }
            return ViewportGetter(sceneView);
        }

        // UI event handlers
        public Delegates.GoToCallback OnGotoUserCamera; 
        public Delegates.FollowCallback OnFollowUserCamera;
        public Delegates.UnfollowCallback OnUnfollowUserCamera;

        /// <summary>Invoke a OnGotoUserCamera event</summary>
        public void GotoUserCamera(uint userId)
        {
            if (OnGotoUserCamera != null)
            {
                OnGotoUserCamera(userId);
            }
        }

        /// <summary>Invoke a FollowUserCamera event</summary>
        public uint FollowUserCamera(uint userId, bool isFollowing)
        {
            if (OnFollowUserCamera != null)
            {
                return OnFollowUserCamera(userId, isFollowing);
            }
            return 0;
        }

        /// <summary>Invoke a UnfollowUserCamera event</summary>
        public void UnfollowUserCamera()
        {
            if (OnUnfollowUserCamera != null)
            {
                OnUnfollowUserCamera();
            }
        }
    }
}
