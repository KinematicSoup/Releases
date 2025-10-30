using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using KS.SF.Unity.Editor;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>
    /// Detects and invokes an event for new prefabs created in Unity. Does not detect new prefab files written
    /// externally.
    /// </summary>
    public class sfNewPrefabWatcher : AssetModificationProcessor
    {
        /// <summary>Gets the singleton instance.</summary>
        /// <returns>Singleton instance</returns>
        public static sfNewPrefabWatcher Get()
        {
            return m_instance;
        }
        private static sfNewPrefabWatcher m_instance = new sfNewPrefabWatcher();

        /// <summary>New prefab event handler.</summary>
        /// <param name="path">Path to the new prefab.</param>
        public delegate void NewPrefabHandler(string path);

        /// <summary>
        /// Invoked when a new prefab is created in Unity. Not invoked for new prefab files written externally.
        /// </summary>
        public event NewPrefabHandler OnNewPrefab;

        private List<string> m_newPrefabPaths = new List<string>();

        /// <summary>
        /// Adds a new prefab path to invoke <see cref="OnNewPrefab"/> for when assets are imported. We wait until
        /// assets are imported because you cannot load new assets before then.
        /// </summary>
        /// <param name="path">Path to the new prefab.</param>
        private void AddNewPrefabPath(string path)
        {
            if (OnNewPrefab == null)
            {
                return;
            }
            if (m_newPrefabPaths.Count == 0)
            {
                ksEditorEvents.OnImportAssets += InvokeNewPrefabEvents;
            }
            m_newPrefabPaths.Add(path);
        }

        /// <summary>Invokes <see cref="OnNewPrefab"/> for paths in the new prefab paths list.</summary>
        /// <param name="paths">Imported asset paths. Unused.</param>
        private void InvokeNewPrefabEvents(string[] paths)
        {
            ksEditorEvents.OnImportAssets -= InvokeNewPrefabEvents;
            if (OnNewPrefab != null)
            {
                foreach (string path in m_newPrefabPaths)
                {
                    OnNewPrefab(path);
                }
            }
            m_newPrefabPaths.Clear();
        }

        /// <summary>
        /// Unity calls this before creating a new asset. If the asset is a prefab, adds it to the new prefab paths
        /// list to invoke events for when assets are imported. We wait until assets are imported because you cannot
        /// load new assets before then.
        /// </summary>
        /// <param name="path">Path to new asset.</param>
        private static void OnWillCreateAsset(string path)
        {
            if (path.EndsWith(".prefab"))
            {
                Get().AddNewPrefabPath(path);
            }
        }
    }
}
