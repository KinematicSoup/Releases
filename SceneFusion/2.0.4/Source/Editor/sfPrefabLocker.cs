using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using KS.SF.Reactor;
using KS.SceneFusion;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>Prevents saving of prefabs when <see cref="AllowSavingPrefabs"/> is false.</summary>
    public class sfPrefabLocker : AssetModificationProcessor
    {
        /// <summary></summary>
        /// <returns>Singleton instance</returns>
        public static sfPrefabLocker Get()
        {
            return m_instance;
        }
        private static sfPrefabLocker m_instance = new sfPrefabLocker();

        /// <summary>If false, prevents prefabs from being saved.</summary>
        public bool AllowSavingPrefabs
        {
            get { return m_allowSavingPrefabs; }
        }

        private bool m_allowSavingPrefabs = true;
        private bool m_cancellingPrefabSave = false;
        private HashSet<string> m_toReload = new HashSet<string>();
        private HashSet<string> m_canSaveOncePaths = new HashSet<string>();

        /// <summary>
        /// Called before assets are saved. Prevents saving prefabs if <see cref="AllowSavingPrefabs"/> is false.
        /// </summary>
        /// <param name="paths">Paths to assets being saved.</param>
        /// <returns>Paths to save.</returns>
        public static string[] OnWillSaveAssets(string[] paths)
        {
            return m_instance.PreSaveAssets(paths);
        }

        /// <summary>Starts preventing prefabs from being saved.</summary>
        public void Start()
        {
            m_allowSavingPrefabs = false;
            
            // If a prefab is selected, refresh the inspector so it gets locked.
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            foreach (GameObject gameObject in Selection.gameObjects)
            {
                if (PrefabUtility.IsPartOfPrefabAsset(gameObject) ||
                    (stage != null && stage.IsPartOfPrefabContents(gameObject)))
                {
                    sfUI.Get().MarkInspectorStale(gameObject);
                    break;
                }
            }
        }

        /// <summary>Stops preventing prefabs from being saved.</summary>
        public void Stop()
        {
            m_allowSavingPrefabs = true;
            m_canSaveOncePaths.Clear();

            // Unlock prefab stage game objects.
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                foreach (GameObject prefab in sfUnityUtils.IterateSelfAndDescendants(stage.prefabContentsRoot))
                {
                    sfUnityUtils.RemoveFlags(prefab, HideFlags.NotEditable);
                    sfUI.Get().MarkInspectorStale(prefab);
                }
            }
            // If a prefab is selected, refresh the inspector so it gets unlocked.
            foreach (GameObject gameObject in Selection.gameObjects)
            {
                if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
                {
                    sfUI.Get().MarkInspectorStale(gameObject);
                }
            }
        }

        /// <summary>
        /// Allows the prefab at the given path to be saved. This must be called before saving the prefab each time
        /// you want it to be saved.
        /// </summary>
        /// <param name="path">Prefab path to allow to be saved.</param>
        public void AllowSave(string path)
        {
            if (!m_allowSavingPrefabs)
            {
                m_canSaveOncePaths.Add(path);
            }
        }

        /// <summary>
        /// Called before assets are saved. Prevents saving prefabs if <see cref="AllowSavingPrefabs"/> is false. New
        /// prefabs can be saved.
        /// </summary>
        /// <param name="paths">Paths to assets being saved.</param>
        /// <returns>Paths to save.</returns>
        private string[] PreSaveAssets(string[] paths)
        {
            if (m_allowSavingPrefabs)
            {
                return paths;
            }
            List<string> toSave = new List<string>();
            foreach (string path in paths)
            {
                if (!path.EndsWith(".prefab") || !File.Exists(path) || m_canSaveOncePaths.Remove(path))
                {
                    toSave.Add(path);
                    continue;
                }
                m_toReload.Add(path);
                if (m_cancellingPrefabSave)
                {
                    continue;
                }
                m_cancellingPrefabSave = true;
                ksLog.Warning(this, "Prevented saving prefab '" + path + "'.");
                EditorUtility.DisplayDialog(
                    "Prefab Editing Disabled",
                    "Editing prefabs during a Scene Fusion session is not supported. "
                    + "Please disconnect before editing prefabs and redistribute your prefabs with "
                    + "your team before starting a new session.",
                    "OK");
                // Some actions that modify prefabs--such as applying prefab instance modifications to a prefab or
                // removing a component from a prefab--will also modify prefab instances even though we cancelled
                // saving the prefab. We undo those prefab instance modifications by reverting the next operation
                // recorded on the undo stack, if one is recorded by the end of the frame.
                sfUndoManager.Get().UndoNextOperation = true;
                EditorApplication.delayCall += PostCancelSave;
            }
            return toSave.ToArray();
        }

        /// <summary>
        /// Called after preventing prefabs from saving. Cancels undoing the next operation on the undo stack if non
        /// occurred between beginning the save and now. Closes the prefab stage if it is open, and reloads prefabs
        /// that were modified to get rid of the modifications and clear the dirty state.
        /// </summary>
        private void PostCancelSave()
        {
            m_cancellingPrefabSave = false;
            // Cancel undoing the next undo operation.
            sfUndoManager.Get().UndoNextOperation = false;
            // Close the prefab stage if it is open.
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                stage.ClearDirtiness();
                StageUtility.GoToMainStage();
            }
            // Reload prefabs that were modified.
            foreach (string assetPath in m_toReload)
            {
                AssetDatabase.ImportAsset(assetPath);
            }
            m_toReload.Clear();
        }
    }
}
