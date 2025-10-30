#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using KS.SF.Reactor;
using KS.SceneFusion;

namespace KS.SceneFusion2.Unity
{
    /// <summary>
    /// Stores a list of game object guid pairs. One sfGuidList is saved in each scene with the guids for the game
    /// objects in that scene.
    /// </summary>
    [AddComponentMenu("")]
    [ExecuteInEditMode]
    public class sfGuidList : sfBaseComponent
    {
        /// <summary>Game object + guid pair.</summary>
        [Serializable]
        public struct ObjectGuid
        {
            /// <summary>Game object</summary>
            public GameObject GameObject;

            /// <summary>Guid</summary>
            public string Guid;

            /// <summary>Constructor</summary>
            /// <param name="gameObject"></param>
            /// <param name="guid"></param>
            public ObjectGuid(GameObject gameObject, Guid guid)
            {
                GameObject = gameObject;
                Guid = guid.ToString();
            }
        }

        /// <summary>List of game object + guid pairs.</summary>
        public List<ObjectGuid> ObjectGuids = new List<ObjectGuid>();

        // Maps scenes to sfGuidLists
        private static Dictionary<Scene, sfGuidList> m_sceneToList = new Dictionary<Scene, sfGuidList>();

        /// <summary>
        /// Gets the sfGuidList for a scene. Optionally creates one if there isn't already one in the scene.
        /// </summary>
        /// <param name="scene">scene to get guid list for.</param>
        /// <param name="create">
        /// if true, will create an sfGuidList object is there isn't already one in the scene.
        /// </param>
        /// <returns>for the scene.</returns>
        public static sfGuidList Get(Scene scene, bool create = true)
        {
            if (!scene.isLoaded)
            {
                return null;
            }
            sfGuidList map;
            if (!m_sceneToList.TryGetValue(scene, out map) && create)
            {
                GameObject gameObject = new GameObject("Scene Fusion Guids");
                SceneManager.MoveGameObjectToScene(gameObject, scene);
                map = gameObject.AddComponent<sfGuidList>();
            }
            return map;
        }

        /// <summary>Initialization. Puts this list in the scene to list map.</summary>
        private void OnEnable()
        {
            sfGuidList map;
            if (!m_sceneToList.TryGetValue(gameObject.scene, out map) || map == null)
            {
                m_sceneToList[gameObject.scene] = this;
            }
            else
            {
                ksLog.Warning(this, "Destroying duplicate sfGuidList in scene " + gameObject.scene.name);
                DestroyImmediate(this);
            }
        }

        /// <summary>Deinitialization. Removes this list from the scene to list map.</summary>
        private void OnDisable()
        {
            if (EditorApplication.isUpdating ||
               (!Application.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode))
            {
                return;
            }
            sfGuidList map;
            if (m_sceneToList.TryGetValue(gameObject.scene, out map) && map == this)
            {
                m_sceneToList.Remove(gameObject.scene);
            }
        }
    }
}
#endif
