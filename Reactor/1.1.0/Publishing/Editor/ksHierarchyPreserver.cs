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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using KS.Unity.Editor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>
    /// Provides methods for storing and restoring the state of all hierarchy windows. The state consists of which
    /// scenes are loaded, which scenes/objects are expanded, and which objects are selected. Useful for preserving
    /// hierarchy state after loading different scenes during publishing.
    /// </summary>
    public class ksHierarchyPreserver
    {
        private SceneSetup[] m_sceneSetup;
        private GlobalObjectId[] m_selectedIds;
        private List<HierarchyWindowState> m_hierarchyWindowStates = new List<HierarchyWindowState>();

        /// <summary>Expanded scene and game object state for a hierarchy window.</summary>
        private struct HierarchyWindowState
        {
            public List<string> ExpandedSceneNames;
            public GlobalObjectId[] ExpandedIds;
            public ksReflectionObject SceneHierarchyRO;

            /// <summary>
            /// Constructor that gets the expanded scene and game objects from a scene hierarchy reflection object.
            /// </summary>
            /// <param name="sceneHierarchyRO">Reflection object for a hierarchy window's scene hierarchy.</param>
            public HierarchyWindowState(ksReflectionObject sceneHierarchyRO)
            {
                SceneHierarchyRO = sceneHierarchyRO;
                ExpandedIds = null;

                // Store expanded scene names.
                ExpandedSceneNames = SceneHierarchyRO.GetMethod("GetExpandedSceneNames").Invoke() as List<string>;
                if (ExpandedSceneNames == null)
                {
                    return;
                }
#if UNITY_2020_3_OR_NEWER
                // Store expanded game object ids.
                List<GameObject> expandedObjects = SceneHierarchyRO.GetMethod("GetExpandedGameObjects").Invoke()
                    as List<GameObject>;
                if (expandedObjects == null)
                {
                    return;
                }
                int[] instanceIds = new int[expandedObjects.Count];
                ExpandedIds = new GlobalObjectId[expandedObjects.Count];
                for (int i = 0; i < expandedObjects.Count; i++)
                {
                    instanceIds[i] = expandedObjects[i].GetInstanceID();
                }
                GlobalObjectId.GetGlobalObjectIdsSlow(instanceIds, ExpandedIds);
#endif
            }

            /// <summary>Restores the state of the hierarchy window.</summary>
            public void Restore()
            {
                // Restore expanded scenes.
                if (ExpandedSceneNames != null && ExpandedSceneNames.Count > 0)
                {
                    SceneHierarchyRO.Call("SetScenesExpanded", ExpandedSceneNames);
                }
#if UNITY_2020_3_OR_NEWER
                // Restore expanded game objects.
                if (ExpandedIds != null && ExpandedIds.Length > 0)
                {
                    ksReflectionObject expandMethod = SceneHierarchyRO.GetMethod("ExpandTreeViewItem",
                        paramTypes: new Type[] { typeof(int), typeof(bool) });
                    if (expandMethod.IsVoid)
                    {
                        return;
                    }
                    int[] instanceIds = new int[ExpandedIds.Length];
                    GlobalObjectId.GlobalObjectIdentifiersToInstanceIDsSlow(ExpandedIds, instanceIds);
                    foreach (int id in instanceIds)
                    {
                        expandMethod.Invoke(id, true);
                    }
                }
#endif
            }
        }

        /// <summary>
        /// Stores the state of the hierarchy which can be restored later using <see cref="RestoreHierarchy"/>.
        /// </summary>
        public void StoreHierarchy()
        {
            // Store open scenes and scene loaded states.
            m_sceneSetup = EditorSceneManager.GetSceneManagerSetup();

            // Store selected object ids.
            m_selectedIds = new GlobalObjectId[Selection.objects.Length];
            GlobalObjectId.GetGlobalObjectIdsSlow(Selection.objects, m_selectedIds);

            // Find the hierarchy windows and store their states.
            m_hierarchyWindowStates.Clear();
            foreach (SearchableEditorWindow window in Resources.FindObjectsOfTypeAll<SearchableEditorWindow>())
            {
                if (window.GetType().Name != "SceneHierarchyWindow")
                {
                    continue;
                }
                ksReflectionObject sceneHierachyRO = new ksReflectionObject(window).GetField("m_SceneHierarchy");
                if (sceneHierachyRO.IsVoid)
                {
                    return;
                }
                m_hierarchyWindowStates.Add(new HierarchyWindowState(sceneHierachyRO));
            }
        }

        /// <summary>
        /// Restores the state of the hierarchy that was stored using <see cref="StoreHierarchy"/>. Does nothing if
        /// <see cref="StoreHierarchy"/> was never called.
        /// </summary>
        public void RestoreHierarchy()
        {
            if (m_sceneSetup == null)
            {
                return;
            }
            // Restore scenes and loade scene states.
            EditorSceneManager.RestoreSceneManagerSetup(m_sceneSetup);

            // Restore object selection.
            UnityEngine.Object[] selection = new UnityEngine.Object[m_selectedIds.Length];
            GlobalObjectId.GlobalObjectIdentifiersToObjectsSlow(m_selectedIds, selection);
            Selection.objects = selection;

            // Restore hierarchy window states.
            foreach (HierarchyWindowState state in m_hierarchyWindowStates)
            {
                state.Restore();
            }
        }
    }
}
