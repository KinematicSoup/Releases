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
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KS.Reactor.Client.Unity
{
    /// <summary>
    /// Maps entity ids to game objects that existed in the scene before joining a room, and room type names to room 
    /// types.
    /// </summary>
    public class ksEntityLinker : ksEntityFactory
    {
        private Scene m_scene;
        private List<ksEntityComponent> m_permanentObjects = new List<ksEntityComponent>();
        private Dictionary<uint, GameObject> m_map = new Dictionary<uint, GameObject>();
        private Dictionary<string, ksRoomType> m_roomTypes = new Dictionary<string, ksRoomType>();
        private GameObject m_container;

        /// <summary>Are the permanent objects loaded?</summary>
        public bool IsLoaded
        {
            get { return m_container != null; }
        }

        /// <summary>Constructor</summary>
        /// <param name="scene">Unity scene this entity linker tracks objects for.</param>
        internal ksEntityLinker(Scene scene)
        {
            m_scene = scene;
        }

        /// <summary>
        /// Creates a map of entity IDs to game objects with <see cref="ksEntityComponent"/> components, and a map of
        /// room type names to <see cref="ksRoomType"/> components. All entities are then added to a game object that
        /// tracks inactive entities in a scene.
        /// </summary>
        public void Initialize()
        {
            if (m_container != null || !m_scene.IsValid())
            {
                return;
            }
            m_container = new GameObject("Inactive Reactor Entities");
            SceneManager.MoveGameObjectToScene(m_container, m_scene);
            foreach (GameObject gameObject in m_scene.GetRootGameObjects())
            {
                // Do not track/link the container or inactive gameobjects
                if (gameObject == m_container || !gameObject.activeSelf)
                {
                    continue;
                }
                foreach (ksEntityComponent entityComponent in gameObject.GetComponentsInChildren<ksEntityComponent>())
                {
                    TrackEntity(entityComponent);
                }
                foreach (ksRoomType roomType in gameObject.GetComponentsInChildren<ksRoomType>())
                {
                    AddRoomType(roomType);
                }
            }
            m_container.SetActive(false);
        }

        /// <summary>
        /// Add a <see cref="ksRoomType"/> to the collection of rooms the linker tracks.
        /// </summary>
        /// <param name="roomType">ksRoomType component attached to a gameobject.</param>
        public void AddRoomType(ksRoomType roomType)
        {
            if (roomType == null || m_roomTypes.ContainsKey(roomType.gameObject.name) || roomType.gameObject.scene != m_scene)
            {
                return;
            }
            m_roomTypes[roomType.gameObject.name] = roomType;
        }

        /// <summary>
        /// Remove a <see cref="ksRoomType"/> from the collection of rooms the linker tracks.
        /// </summary>
        /// <param name="roomType">ksRoomType component attached to a gameobject.</param>
        public void RemoveRoomType(ksRoomType roomType)
        {
            if (roomType != null)
            {
                m_roomTypes.Remove(roomType.gameObject.name);
            }
        }

        /// <summary>Add an entity to the linker.</summary>
        /// <param name="entityComponent"></param>
        public void TrackEntity(ksEntityComponent entityComponent)
        {
            if (entityComponent.EntityId != 0)
            {
                if (entityComponent.IsPermanent)
                {
                    entityComponent.enabled = false;
                    m_permanentObjects.Add(entityComponent);
                    entityComponent.IsSceneEntity = true;
                    return;
                }
                m_map[entityComponent.EntityId] = entityComponent.gameObject;
            }
            // Do not add the object if it is auto-spawned per player.
            else if (entityComponent.AssetId != 0)
            {
                ksAutoSpawn autoSpawn = entityComponent.GetComponent<ksAutoSpawn>();
                if (autoSpawn != null && autoSpawn.SpawnOwned && autoSpawn.enabled)
                {
                    return;
                }
            }
            entityComponent.IsSceneEntity = true;
            entityComponent.transform.SetParent(m_container.transform);
        }

        /// <summary>
        /// Get a list of entities that have been marked permanent. Permanent entities exist on both the client and
        /// server and will not move. These entities will never receive transform updates from the server.
        /// </summary>
        /// <returns>
        /// List of <see cref="ksEntityComponent"/> components whose <see cref="ksEntityComponent.IsPermanent"/>
        /// proeprty is true.
        /// </returns>
        public List<ksEntityComponent> GetPermanentObjects()
        {
            return m_permanentObjects;
        }

        /// <summary>
        /// Gets the game object for the <paramref name="entity"/>'s id and removes it from the map.
        /// </summary>
        /// <param name="entity">Entity to get game object for.</param>
        /// <param name="prefab">Prefab associated with the entity's asset id. Unused.</param>
        /// <returns>Game object for the entity, or null if none was found.</returns>
        public override GameObject GetGameObject(ksEntity entity, GameObject prefab)
        {
            return GetGameObjectForEntity(entity.Id);
        }

        /// <summary>Returns an entity to the linker.</summary>
        /// <param name="entityComponent"></param>
        public override void ReleaseGameObject(ksEntityComponent entity)
        {
            TrackEntity(entity);
        }

        /// <summary>Gets the game object with the given entity id and removes it from the map.</summary>
        /// <param name="entityId">Entity id</param>
        /// <returns>GameObject for the entity, or null if none was found.</returns>

        public GameObject GetGameObjectForEntity(uint entityId)
        {
            GameObject gameObject;
            if (m_map.TryGetValue(entityId, out gameObject))
            {
                m_map.Remove(entityId);
            }
            return gameObject;
        }

        /// <summary>Gets a room type from the scene by name.</summary>
        /// <param name="name">Name of the room type</param>
        /// <param name="roomType">Set to the room type component, or null if none is found.</param>
        /// <returns>True if a room type with the given name was found in the scene.</returns>
        public bool GetRoomType(string name, out ksRoomType roomType)
        {
            return m_roomTypes.TryGetValue(name, out roomType);
        }

        /// <summary>Checks if a game object is a child of the linker's container object.</summary>
        /// <param name="gameObject"></param>
        /// <returns>True if the game object is a child of the container object.</returns>
        public bool Contains(GameObject gameObject)
        {
            return m_container != null && gameObject.transform.parent == m_container.transform;
        }
    }
}
