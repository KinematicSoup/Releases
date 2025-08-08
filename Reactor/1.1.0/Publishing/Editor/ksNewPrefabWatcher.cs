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

using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using KS.Unity.Editor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>
    /// Detects when a new prefab is created and clears asset and shape IDs on the prefab. This is to prevent ID
    /// collisions when a new prefab is created from another prefab.
    /// </summary>
    public class ksNewPrefabWatcher : UnityEditor.AssetModificationProcessor
    {
        private static List<string> m_newPrefabPaths = new List<string>();
        private static List<string> m_checkPrefabPaths = new List<string>();
        private static HashSet<string> m_ignorePaths = new HashSet<string>();

        /// <summary>
        /// Tells the watcher to ignore the next time a prefab is created at the given path. Use this to prevent the
        /// asset id of the new prefab from being cleared.
        /// </summary>
        /// <param name="path">Path to ignore.</param>
        public static void Ignore(string path)
        {
            m_ignorePaths.Add(ksPathUtils.Clean(path));
        }

        /// <summary>
        /// Called before a new asset is created. If the asset is a prefab, stores the path so we can clear the IDs
        /// and check the path on the prefab once it is created.
        /// </summary>
        /// <param name="path">Path that will be created.</param>
        public static void OnWillCreateAsset(string path)
        {
            if (m_ignorePaths.Remove(ksPathUtils.Clean(path)))
            {
                return;
            }
            if (path.EndsWith(".prefab"))
            {
                m_newPrefabPaths.Add(path);
                if (m_newPrefabPaths.Count == 1)
                {
                    EditorApplication.update += ClearNewPrefabIds;
                }
                m_checkPrefabPaths.Add(path);
                if (m_checkPrefabPaths.Count == 1)
                {
                    EditorApplication.update += CheckEntityPrefabResourcePaths;
                }
            }
        }

        /// <summary>
        /// Called before an asset is moved. If the asset if a prefab, stores the path so we can check the
        /// path once it is moved.
        /// </summary>
        /// <param name="sourcePath">Original path to the asset.</param>
        /// <param name="destinationPath">New path to the asset.</param>
        /// <returns>Always returns AssetMoveResult.DidNotMove</returns>
        public static AssetMoveResult OnWillMoveAsset(string sourcePath, string destinationPath)
        {
            if (destinationPath.EndsWith(".prefab"))
            {
                m_checkPrefabPaths.Add(destinationPath);
                if (m_checkPrefabPaths.Count == 1)
                {
                    EditorApplication.update += CheckEntityPrefabResourcePaths;
                }
            }
            return AssetMoveResult.DidNotMove;
        }

        /// <summary>Clears asset and shape IDs on all prefabs in the new prefabs path list.</summary>
        private static void ClearNewPrefabIds()
        {
            EditorApplication.update -= ClearNewPrefabIds;
            foreach (string path in m_newPrefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    foreach (ksEntityComponent entity in prefab.GetComponentsInChildren<ksEntityComponent>())
                    {
                        entity.AssetId = 0;
                        foreach (ksColliderData data in entity.gameObject.GetComponents<ksColliderData>())
                        {
                            data.ShapeId = 0;
                            EditorUtility.SetDirty(data);
                        }
                        EditorUtility.SetDirty(entity);
                    }
                }
            }
            m_newPrefabPaths.Clear();
        }


        /// <summary>
        /// Write warnings if entity prefabs are found outside of a Resources folder.
        /// </summary>
        private static void CheckEntityPrefabResourcePaths()
        {
            EditorApplication.update -= CheckEntityPrefabResourcePaths;
            foreach (string path in m_checkPrefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    ksEntityComponent entity = prefab.GetComponentInChildren<ksEntityComponent>();
                    if (entity != null && !string.IsNullOrEmpty(path) && !(
                        path.Contains(Path.DirectorySeparatorChar + "Resources" + Path.DirectorySeparatorChar) ||
                        path.Contains(Path.AltDirectorySeparatorChar + "Resources" + Path.AltDirectorySeparatorChar)))
                    {
                        ksLog.Warning(
                            typeof(ksNewPrefabWatcher).ToString(),
                            "Entity prefabs cannot be instantiated by the server unless they are located under a " +
                            "Resources folder or asset bundle.\n- " + path
                        );
                    }
                }
            }
            m_checkPrefabPaths.Clear();
        }
    }
}
