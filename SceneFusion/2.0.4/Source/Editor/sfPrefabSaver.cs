using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using KS.SceneFusion;
using KS.SF.Reactor;
using KS.SF.Unity.Editor;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>
    /// Provides functions for saving prefabs. Processes imported prefabs to remove <see cref="sfMissingPrefab"/>
    /// components whose path is the same as the prefab's path.
    /// </summary>
    public class sfPrefabSaver
    {
        /// <summary></summary>
        /// <returns>Singleton instance</returns>
        public static sfPrefabSaver Get()
        {
            return m_instance;
        }
        private static sfPrefabSaver m_instance = new sfPrefabSaver();

        private List<GameObject> m_dirtyPrefabs = new List<GameObject>();

        /// <summary>Constructor</summary>
        private sfPrefabSaver()
        {
            ksEditorEvents.OnImportAssets += HandleImportAssets;
        }

        /// <summary>
        /// Marks a prefab dirty and adds it to the list of dirty prefabs to save on the next update. Does nothing
        /// if the <paramref name="uobj"/> is not part of a prefab asset or if the prefab is already dirty.
        /// </summary>
        /// <param name="uobj">Object in prefab to mark dirty.</param>
        public void MarkPrefabDirty(UObject uobj)
        {
            if (PrefabUtility.IsPartOfPrefabAsset(uobj))
            {
                GameObject prefab = sfUnityUtils.GetGameObject(uobj);
                if (prefab != null)
                {
                    prefab = prefab.transform.root.gameObject;
                    if (!EditorUtility.IsDirty(prefab))
                    {
                        if (m_dirtyPrefabs.Count == 0)
                        {
                            sfUnityEventDispatcher.Get().OnUpdate += SaveDirtyPrefabs;
                        }
                        m_dirtyPrefabs.Add(prefab);
                        EditorUtility.SetDirty(prefab);
                    }
                }
            }
        }

        /// <summary>
        /// Saves a prefab asset if the root of the prefab is dirty. Use <see cref="MarkPrefabDirty(UObject)"/> to mark
        /// the root of the prefab as dirty.
        /// </summary>
        /// <param name="uobj">UObject that is part of the prefab asset to save.</param>
        public void SavePrefabIfDirty(UObject uobj)
        {
            if (PrefabUtility.IsPartOfPrefabAsset(uobj))
            {
                GameObject prefab = sfUnityUtils.GetGameObject(uobj);
                if (prefab != null)
                {
                    prefab = prefab.transform.root.gameObject;
                    if (EditorUtility.IsDirty(prefab))
                    {
                        PrefabUtility.SavePrefabAsset(prefab);
                    }
                }
            }
        }

        /// <summary>
        /// Save a prefab instance's source prefab if it is dirty. If the source prefab if also a prefab instance,
        /// saves its source prefab recursively.
        /// /summary>
        /// <param name="uobj">Object to save the source prefab for.</param>
        public void SavePrefabSourceIfDirty(UObject uobj)
        {
            UObject prefabUObj = PrefabUtility.GetCorrespondingObjectFromSource(uobj);
            if (prefabUObj != null)
            {
                SavePrefabSourceIfDirty(prefabUObj);
                GameObject prefab = sfUnityUtils.GetGameObject(prefabUObj);
                if (prefab != null)
                {
                    prefab = prefab.transform.root.gameObject;
                    if (EditorUtility.IsDirty(prefab))
                    {
                        PrefabUtility.SavePrefabAsset(prefab);
                    }
                }
            }
        }

        /// <summary>Saves dirty prefabs and clears the dirty prefab list.</summary>
        /// <param name="deltaTime">Unused</param>
        private void SaveDirtyPrefabs(float deltaTime)
        {
            sfUnityEventDispatcher.Get().OnUpdate -= SaveDirtyPrefabs;
            SaveDirtyPrefabs();
        }

        /// <summary>Saves dirty prefabs and clears the dirty prefab list.</summary>
        public void SaveDirtyPrefabs()
        {
            for (int i = 0; i < m_dirtyPrefabs.Count; i++)
            {
                GameObject prefab = m_dirtyPrefabs[i];
                if (prefab != null && EditorUtility.IsDirty(prefab))
                {
                    PrefabUtility.SavePrefabAsset(prefab);
                }
            }
            m_dirtyPrefabs.Clear();
        }

        /// <summary>
        /// Called when assets are imported. Removes <see cref="sfMissingPrefab"/> from imported prefabs if the missing
        /// prefab path is the same as the prefab's path.
        /// </summary>
        /// <param name="paths">Paths to imported assets</param>
        private void HandleImportAssets(string[] paths)
        {
            foreach (string path in paths)
            {
                if (path.EndsWith(".prefab"))
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null)
                    {
                        bool changed = false;
                        foreach (sfMissingPrefab missingPrefab in prefab.GetComponentsInChildren<sfMissingPrefab>())
                        {
                            if (missingPrefab.PrefabPath == path)
                            {
                                UObject.DestroyImmediate(missingPrefab, true);
                                changed = true;
                            }
                        }
                        if (changed)
                        {
                            sfPrefabLocker.Get().AllowSave(path);
                        }
                    }
                }
            }
        }
    }
}
