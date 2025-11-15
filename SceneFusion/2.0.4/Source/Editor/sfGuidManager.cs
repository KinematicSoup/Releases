using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using KS.SceneFusionCommon;
using KS.SF.Reactor;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>
    /// Manages generation of guids for game objects, and stores a mapping between game objects and guids. The guids are
    /// persistent ids that uniquely identify game objects.
    /// </summary>
    public class sfGuidManager
    {
        /// <summary></summary>
        /// <returns>singleton instance</returns>
        public static sfGuidManager Get()
        {
            return m_instance;
        }
        private static sfGuidManager m_instance = new sfGuidManager();

        private Dictionary<Guid, GameObject> m_idToObjectMap = new Dictionary<Guid, GameObject>();
        private Dictionary<GameObject, Guid> m_objectToIdMap = new Dictionary<GameObject, Guid>();
        private bool m_saved = false;

        /// <summary>Registers event listeners</summary>
        public void RegisterEventListeners()
        {
            sfSceneSaveWatcher.Get().PreSave += PreSave;
            sfSceneSaveWatcher.Get().PostSave += PostSave;
        }

        /// <summary>
        /// Gets the guid for a game object. Assigns a guid if one does not already exist. There are two strategies for
        /// assigning a guid:
        /// 1. Create a deterministic guid by hashing the game object's scene, name, parent guid, and transform. This
        /// allows multiple users starting with the same scene to generate the same guids.
        /// 2. Use System.Guid to create a guid.
        /// </summary>
        /// <param name="gameObject">gameObject to get or create guid for.</param>
        /// <param name="deterministic">
        /// determines if we should use the deterministic guid or system.guid to create a
        /// guid if the game object does not already have one.
        /// </param>
        /// <returns>for the game object.</returns>
        public Guid GetGuid(GameObject gameObject, bool deterministic = false)
        {
            Guid guid;
            if (m_objectToIdMap.TryGetValue(gameObject, out guid))
            {
                return guid;
            }
            if (deterministic)
            {
                guid = CreateDeterministicGuid(gameObject);
            }
            else
            {
                guid = Guid.NewGuid();
            }
            ResolveCollisions(ref guid);
            m_idToObjectMap[guid] = gameObject;
            m_objectToIdMap[gameObject] = guid;
            return guid;
        }

        /// <summary>Sets the guid for a game object.</summary>
        /// <param name="gameObject">gameObject to set guid for.</param>
        /// <param name="guid">guid to set.</param>
        public void SetGuid(GameObject gameObject, Guid guid)
        {
            ResolveCollisions(ref guid);
            m_idToObjectMap[guid] = gameObject;
            m_objectToIdMap[gameObject] = guid;
        }

        /// <summary>Gets a game object by its guid.</summary>
        /// <param name="guid">guid to get game object for.</param>
        /// <returns>for the guid, or null if none is found.</returns>
        public GameObject GetGameObject(Guid guid)
        {
            GameObject gameObject;
            m_idToObjectMap.TryGetValue(guid, out gameObject);
            return gameObject;
        }

        /// <summary>Removes a game object and its guid from the map.</summary>
        /// <returns>gameObject to remove.</returns>
        public void Remove(GameObject gameObject)
        {
            Guid guid;
            if (m_objectToIdMap.TryGetValue(gameObject, out guid))
            {
                m_objectToIdMap.Remove(gameObject);
                m_idToObjectMap.Remove(guid);
            }
        }

        /// <summary>Load guids for a scene from its sfGuidList.</summary>
        /// <param name="scene">scene to load guids from.</param>
        public void LoadGuids(Scene scene)
        {
            sfGuidList map = sfGuidList.Get(scene);
            if (map == null)
            {
                return;
            }
            foreach (sfGuidList.ObjectGuid pair in map.ObjectGuids)
            {
                Guid guid = new Guid(pair.Guid);
                ResolveCollisions(ref guid);
                m_idToObjectMap[guid] = pair.GameObject;
                m_objectToIdMap[pair.GameObject] = guid;
            }
        }

        /// <summary>Saves guids into the sfGuidList for each scene.</summary>
        public void SaveGuids()
        {
            sfSceneTranslator translator = sfObjectEventDispatcher.Get().GetTranslator<sfSceneTranslator>(
                sfType.Scene);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && translator.GetSceneObject(scene) != null)
                {
                    sfGuidList map = sfGuidList.Get(scene);
                    map.ObjectGuids.Clear();
                }
            }
            foreach (KeyValuePair<Guid, GameObject> pair in m_idToObjectMap)
            {
                if (pair.Value != null)
                {
                    sfGuidList map = sfGuidList.Get(pair.Value.scene);
                    if (map != null)
                    {
                        map.ObjectGuids.Add(new sfGuidList.ObjectGuid(pair.Value, pair.Key));
                    }
                }
            }
        }

        /// <summary>Clears the mapping between guids and objects.</summary>
        public void Clear()
        {
            m_idToObjectMap.Clear();
            m_objectToIdMap.Clear();
        }

        /// <summary>
        /// Called before saving a scene. Writes guid/game object pairs to the sfGuidList to be saved in the scene.
        /// </summary>
        /// <param name="scene">scene that will be saved.</param>
        private void PreSave(Scene scene)
        {
            if (SceneFusion.Get().Service.Session == null)
            {
                // If we are not in a session, we want to remove any deleted game objects from the sfGuidList before
                // saving.
                sfGuidList map = sfGuidList.Get(scene, false);
                if (map != null)
                {
                    for (int i = map.ObjectGuids.Count - 1; i >= 0; i--)
                    {
                        if (map.ObjectGuids[i].GameObject == null)
                        {
                            map.ObjectGuids.RemoveAt(i);
                        }
                    }
                }
            }
            else if (!m_saved)
            {
                // Set saved flag to avoid saving multiple times if we are saving multiple scenes at once.
                m_saved = true;
                SaveGuids();
            }
        }

        /// <summary>Called after scenes are saved.</summary>
        private void PostSave()
        {
            m_saved = false;
        }

        /// <summary>
        /// Checks for a guild collision and resolves it by incrementing the guid until there is no collision.
        /// </summary>
        /// <param name="guid">guid to resolve collisions for.</param>
        private void ResolveCollisions(ref Guid guid)
        {
            GameObject gameObject;
            while (m_idToObjectMap.TryGetValue(guid, out gameObject))
            {
                byte[] bytes = guid.ToByteArray();
                int i = 0;
                while (bytes[i] == 255)
                {
                    bytes[i] = 0;
                    i++;
                    i %= 16;
                }
                bytes[i]++;
                guid = new Guid(bytes);
            }
        }

        /// <summary>Creates a deterministic guid for a game object.</summary>
        /// <param name="gameObject">gameObject to create deterministic guid for.</param>
        /// <returns>for the gameObject.</returns>
        private Guid CreateDeterministicGuid(GameObject gameObject)
        {
            int[] nums = new int[4];
            unchecked
            {
                bool isPrefab = PrefabUtility.IsPartOfPrefabAsset(gameObject);
                string sceneNameOrPrefabPath = isPrefab ?
                    AssetDatabase.GetAssetPath(gameObject) : gameObject.scene.name;
                nums[0] = (sceneNameOrPrefabPath + "." + gameObject.name).GetHashCode();
                // The root of a prefab asset doesn't get a guid, so don't include the parent in the guid if the parent
                // is the root of a prefab asset.
                nums[1] = ((gameObject.transform.parent == null ||
                    isPrefab && gameObject.transform.parent.parent == null) ?
                    0 : GetGuid(gameObject.transform.parent.gameObject).GetHashCode()) + 
                    7 * gameObject.transform.localPosition.GetHashCode();
                nums[2] = gameObject.transform.localRotation.GetHashCode();
                nums[3] = gameObject.transform.localScale.GetHashCode();
            }
            byte[] bytes = new byte[16];
            Buffer.BlockCopy(nums, 0, bytes, 0, sizeof(int) * 4);
            return new Guid(bytes);
        }
    }
}
