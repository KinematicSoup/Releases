using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using KS.SF.Reactor;
using KS.SceneFusion;
using KS.SceneFusion2.Client;
using KS.SF.Unity.Editor;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>
    /// Maintains a dictionary mapping prefab stage objects to prefab objects. The prefab stage is where you edit a
    /// prefab when you open a prefab. The objects in the prefab stage are not the real prefab; they are copies of the
    /// prefab objects which we call stage objects. Unity does not provide a mapping between stage objects and prefab
    /// objects so we build one ourselves with this class. Only one prefab stage can be active at a time, so we never
    /// need to map multiple prefab stages at the same time.
    /// </summary>
    public class sfPrefabStageMap
    {
        /// <summary></summary>
        /// <returns>Singleton instance</returns>
        public static sfPrefabStageMap Get()
        {
            return m_instance;
        }
        private static sfPrefabStageMap m_instance = new sfPrefabStageMap();

        // Maps stage object instance ids to prefab objects.
        private Dictionary<int, UObject> m_stageToPrefabMap = new Dictionary<int, UObject>();
        private PrefabStage m_currentStage;

        /// <summary>Private constructor</summary>
        private sfPrefabStageMap()
        {

        }

        /// <summary>
        /// Constructor used for testing that builds a map between a test stage object and test prefab object, which do
        /// not need to be real stage or prefab objects.
        /// </summary>
        /// <param name="stageObject">Stage object to build map with. Need not be a real stage object.</param>
        /// <param name="prefabObject">Prefab object to build map with. Need not be a real prefab object.</param>
        internal sfPrefabStageMap(GameObject stageObject, GameObject prefabObject)
        {
            AddMapping(stageObject, prefabObject);
        }

        /// <summary>
        /// Registers event listeners to build/clear the map when a prefab stage is opened/closed. If a prefab stage is
        /// already open, prompts the user to save unsaved changes before building the map.
        /// </summary>
        public void Start()
        {
            sfUnityEventDispatcher.Get().OnOpenPrefabStage += BuildMap;
            sfUnityEventDispatcher.Get().OnClosePrefabStage += HandleCloseStage;

            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                // Prompt the user to save or discard prefab stage changes if there are any. We do this because we need
                // the prefab stage objects and the prefab to be in the same state to build the map.
                object result = new ksReflectionObject(stage)
                    .Call("AskUserToSaveDirtySceneBeforeDestroyingScene").GetValue();
                if (result == null || !(bool)result)
                {
                    // The user chose cancel. Close the prefab stage.
                    StageUtility.GoToMainStage();
                }
                else
                {
                    BuildMap(stage);
                }
            }
        }

        /// <summary>Removes event listeners and clears the map.</summary>
        public void Stop()
        {
            sfUnityEventDispatcher.Get().OnOpenPrefabStage -= BuildMap;
            sfUnityEventDispatcher.Get().OnClosePrefabStage -= HandleCloseStage;
            ClearMap();
        }

        /// <summary>Gets the prefab object for a stage object.</summary>
        /// <typeparam name="T">Type of object</typeparam>
        /// <param name="stageObject">Stage object to get prefab object for.</param>
        /// <returns>The prefab object for the stage object, or null if none was found.</returns>
        public T GetPrefabObject<T>(T stageObject) where T : UObject
        {
            return stageObject == null ? null : GetPrefabObject<T>(stageObject.GetInstanceID());
        }

        /// <summary>Gets the prefab object for a stage object.</summary>
        /// <param name="instanceId">Instance id of stage object to get prefab object for.</param>
        /// <returns>The prefab object for the stage object, or null if none was found.</returns>
        public T GetPrefabObject<T>(int instanceId) where T : UObject
        {
            UObject prefab;
            m_stageToPrefabMap.TryGetValue(instanceId, out prefab);
            return prefab as T;
        }

        /// <summary>Rebuilds the map if a prefab stage is open.</summary>
        public void Rebuild()
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                BuildMap(stage);
            }
        }

        /// <summary>Builds the map for a prefab stage.</summary>
        /// <param name="stage">Stage to build map for.</param>
        private void BuildMap(PrefabStage stage)
        {
            ClearMap();
            m_currentStage = stage;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(stage.assetPath);
            AddMapping(stage.prefabContentsRoot, prefab);
        }

        /// <summary>
        /// Called when a prefab stage is closed. If the closed stage is the current stage the map was built for,
        /// clears the map and rebuilds it if another stage is open.
        /// </summary>
        /// <param name="stage">Stage that was closed.</param>
        private void HandleCloseStage(PrefabStage stage)
        {
            // We need to check if this is the current stage we built the map from because when you switch stages,
            // Unity fires the open event before the close event, in which case the current stage will be the newly
            // opened one and we don't want to clear the map.
            if (m_currentStage == stage)
            {
                ClearMap();
                // If you open a prefab stage for a nested prefab from within a prefab stage, Unity will keep both
                // stages open but only one wll be active. When you close the second stage, Unity returns to the first
                // stage without firing an open event for it, so we need to rebuild the map if there is a stage still
                // open.
                Rebuild();
            }
        }

        /// <summary>Clears the map.</summary>
        private void ClearMap()
        {
            m_stageToPrefabMap.Clear();
            m_currentStage = null;
        }

        /// <summary>
        /// Adds a mapping between a stage object and a prefab object, and all of their components and children
        /// recursively.
        /// </summary>
        /// <param name="stageObject">Stage object</param>
        /// <param name="prefabObject">Prefab object</param>
        private void AddMapping(GameObject stageObject, GameObject prefabObject)
        {
            if (stageObject == null || prefabObject == null)
            {
                return;
            }
            Component[] stageComponents = stageObject.GetComponents<Component>();
            Component[] prefabComponents = prefabObject.GetComponents<Component>();
            if (stageComponents.Length != prefabComponents.Length)
            {
                ksLog.Warning(this, "Prefab stage object " + stageObject.name + " has " + stageComponents.Length +
                    " components, but the prefab asset object has " + prefabComponents.Length + 
                    ". It will be excluded from the map.");
                return;
            }
            m_stageToPrefabMap[stageObject.GetInstanceID()] = prefabObject;

            for (int i = 0; i < stageComponents.Length; i++)
            {
                if (stageComponents[i].GetType() != prefabComponents[i].GetType())
                {
                    ksLog.Warning(this, "Prefab stage component on object " + stageObject.name + " at index " + i +
                        " is type " + stageComponents[i].GetType() + ", but the prefab asset component is type " +
                        prefabComponents[i].GetType() + ". It will be excluded from the map.");
                }
                else
                {
                    m_stageToPrefabMap[stageComponents[i].GetInstanceID()] = prefabComponents[i];
                }
            }

            if (stageObject.transform.childCount != prefabObject.transform.childCount)
            {
                ksLog.Warning(this, "Prefab stage object " + stageObject.name + " has " +
                    stageObject.transform.childCount + " children, but the prefab asset object has " +
                    prefabObject.transform.childCount + ". Children will be excluded from the map.");
                return;
            }
            for (int i = 0; i < stageObject.transform.childCount; i++)
            {
                AddMapping(stageObject.transform.GetChild(i).gameObject,
                    prefabObject.transform.GetChild(i).gameObject);
            }
        }
    }
}
