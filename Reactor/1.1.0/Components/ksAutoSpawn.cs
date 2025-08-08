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
using System.Threading.Tasks;
using UnityEngine;

namespace KS.Reactor.Client.Unity
{
    /// <summary>
    /// Automatically spawns an entity for the game object when it is instantiated. Can only be used with entity
    /// prefabs in Resources or asset bundles. It first checks it's ancestors for a connected room to spawn in that has
    /// <see cref="ksRoom.AllowPlayerSpawning"/> set to true. If it does not find one, It will look for a room in the
    /// same scene to spawn in. If it does not find one, it will spawn an entity the first time a connection is made to
    /// a room in that scene with auto spawning enabled. If it finds more than one, it will not spawn and will log a
    /// warning. 
    /// 
    /// If placed on a prefab entity that is part of the initial scene and has <see cref="SpawnOwned"/> set to true,
    /// the entity will not be written to the scene config and instead will be spawned per-player when they connect.
    /// 
    /// When the entity it is linked to is destroyed and <see cref="ksEntityComponent.DestroyWithServer"/> is false, if
    /// the entity was destroyed because of a disconnect, it will try to respawn in a different room or wait until there
    /// is a room to respawn in. If it was destroyed for any other reason, the script will disable itself, and you must
    /// reenable it if you want it to respawn.
    /// </summary>
    [ksHideInOwnershipManager]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ksEntityComponent))]
    [AddComponentMenu(ksMenuNames.REACTOR + nameof(ksAutoSpawn))]
    public class ksAutoSpawn : MonoBehaviour
    {
        /// <summary>
        /// If true, the entity is spawned with the local player as the owner. If the entity is part of the initial
        /// scene, it will not be written to the scene config and instead will be spawned per-player when they connect.
        /// </summary>
        public bool SpawnOwned = true;

        /// <summary>Permissions to spawn with if <see cref="SpawnOwned"/> is true.</summary>
        [ksFlags]
        public ksOwnerPermissions OwnerPermissions = ksOwnerPermissions.ALL;

        // Index in m_autoSpawnList
        private int m_index = -1;
        private ksEntityComponent m_component;

        // List of objects that are waiting for room connections to spawn in.
        private static List<ksAutoSpawn> m_autoSpawnList = new List<ksAutoSpawn>();

        /// <summary>
        /// Static initialization. Registers a handler to spawn objects when rooms sync for the first time.</summary>
        static ksAutoSpawn()
        {
            ksReactor.OnRoomInitialized += AutoSpawnInRoomScene;
        }

        /// <summary>
        /// Looks for a <see cref="ksEntityComponent"/> on the game object. If it finds one that does not have a linked
        /// entity, tries to auto-spawn an entity by calling <see cref="Spawn()"/>. If it finds one that is linked
        /// to an entity that was not spawned by the local player, removes this script so it will not try to auto-spawn
        /// the game object gets disconnected from its entity.
        /// </summary>
        private void Start()
        {
            m_component = GetComponent<ksEntityComponent>();
            if (m_component == null)
            {
                return;
            }
            if (m_component.Entity != null)
            {
                if (!m_component.Entity.SpawnedByLocalPlayer)
                {
                    Destroy(this);
                }
                return;
            }
            Spawn();
        }

        /// <summary>
        /// Tries to spawn the entity by calling <see cref="Spawn()"/>. Does nothing if the entity component
        /// reference is not set. It gets set from <see cref="Start"/> or <see cref="Spawn()"/> when they call
        /// <see cref="IsSpawnable"/>.
        /// </summary>
        private void OnEnable()
        {
            if (m_component != null)
            {
                Spawn();
            }
        }

        /// <summary>Removes the entity from the list of entities waiting for a room to spawn in.</summary>
        private void OnDisable()
        {
            RemoveFromAutoSpawnList();
        }

        /// <summary>Removes the entity from the list of entities waiting for a room to spawn in.</summary>
        private void OnDestroy()
        {
            RemoveFromAutoSpawnList();
        }

        /// <summary>
        /// Checks if an entity can be spawned for this game object. To be spawned, it must have a
        /// <see cref="ksEntityComponent"/>, must not already be linked to an entity, must have an asset id, and must
        /// not be permanent.
        /// </summary>
        /// <returns>True if an entity can be spawned for this game object.</returns>
        public bool IsSpawnable()
        {
            if (m_component == null)
            {
                m_component = GetComponent<ksEntityComponent>();
            }
            return m_component != null && m_component.Entity == null && m_component.AssetId != 0 && !m_component.IsPermanent;
        }

        /// <summary>
        /// Tries to spawn an entity for the game object. Will not spawn if <see cref="IsSpawnable"/> returns false or
        /// if it cannot find a room to spawn in. It first looks for a room with auto spawning enabled from the object's
        /// ancestors. If it doesn't find one, it checks for a connected room in the same scene. If it finds more than
        /// one, it will not auto spawn. If it finds none and is enabled, it will add itself to the list of objects
        /// waiting for a room to spawn in and spawn in the first connected room in the same scene.
        /// </summary>
        public void Spawn()
        {
            if (!IsSpawnable())
            {
                return;
            }

            if (transform.parent != null)
            {
                // GetComponentInParent checks the object it is called on and all ancestors.
                ksRoomComponent roomComponent = transform.parent.GetComponentInParent<ksRoomComponent>();
                if (roomComponent != null && roomComponent.Room != null && roomComponent.Room.AllowPlayerSpawning)
                {
                    Spawn(roomComponent.Room);
                }
            }

            bool foundConnectedRoom = false;
            ksRoom room = null;
            foreach (ksBaseRoom baseRoom in ksReactor.Service.Rooms)
            {
                ksRoom currentRoom = baseRoom as ksRoom;
                if (!currentRoom.IsConnected)
                {
                    continue;
                }
                foundConnectedRoom = true;
                if (currentRoom != null && currentRoom.AllowPlayerSpawning &&
                    currentRoom.GameObject != null && currentRoom.GameObject.scene == gameObject.scene)
                {
                    if (room == null)
                    {
                        room = currentRoom;
                    }
                    else
                    {
                        ksLog.Warning(this, "Cannot auto-spawn " + gameObject.name +
                            " because there are multiple connected rooms in the same scene.");
                        return;
                    }
                }
            }
            if (room != null)
            {
                Spawn(room);
            }
            else
            {
                if (foundConnectedRoom)
                {
                    ksLog.Info("Cannot auto-spawn " + gameObject.name + " in any of the connected rooms because " +
                        "none of them are in the same scene with auto-spawning enabled.");
                }
                if (enabled)
                {
                    AddToAutoSpawnList();
                }
            }
        }

        /// <summary>
        /// Tries to spawn an entity for the game object. Will not spawn if <see cref="IsSpawnable"/> returns false.
        /// </summary>
        /// <param name="room">
        /// Room to spawn in. The room does not need to have auto-spawning enabled, but does need player-spawning enabled.
        /// </param>
        public void Spawn(ksRoom room)
        {
            RemoveFromAutoSpawnList();
            if (!IsSpawnable())
            {
                return;
            }
            ksClientSpawnParams spawnParams = new ksClientSpawnParams(gameObject);
            spawnParams.Transform.Position = transform.position;
            spawnParams.Transform.Rotation = transform.rotation;
            spawnParams.Transform.Scale = transform.lossyScale;
            spawnParams.ScaleMode = ksSpawnParams.ScaleModes.ABSOLUTE;
            if (SpawnOwned)
            {
                spawnParams.OwnerId = room.LocalPlayerId;
                spawnParams.Permissions = OwnerPermissions;
            }
            ksEntity entity = room.SpawnEntity(spawnParams);
            if (entity != null)
            {
                entity.OnDestroy += EntityDestroyed;
            }
        }

        /// <summary>
        /// Called when the entity linked to the game object is destroyed. Unregisters the
        /// <see cref="ksBaseEntity.OnDestroy"/> event handler. If <see cref="ksEntityComponent.DestroyWithServer"/>
        /// is true or the script is disabled, does nothing else. If the destroy <paramref name="reason"/> is 
        /// <see cref="ksDestroyReason.DISCONNECT"/>, adds the object to the list of entities waiting for a room to
        /// spawn in, otherwise disables this script so it won't spawn again unless the script is reenabled.
        /// </summary>
        /// <param name="reason">The reason the entity was destroyed.</param>
        private void EntityDestroyed(ksDestroyReason reason)
        {
            if (m_component.Entity != null)
            {
                m_component.Entity.OnDestroy -= EntityDestroyed;
            }
            if (m_component.DestroyWithServer || !enabled)
            {
                return;
            }

            if (reason == ksDestroyReason.DISCONNECT)
            {
                AddToAutoSpawnList();
            }
            else
            {
                enabled = false;
            }
        }

        /// <summary>
        /// Adds the entity to the list of entities waiting for a room to spawn in, if it is not already in the list.
        /// </summary>
        private void AddToAutoSpawnList()
        {
            if (m_index < 0)
            {
                m_index = m_autoSpawnList.Count;
                m_autoSpawnList.Add(this);
            }
        }

        /// <summary>Removes the entity from the list of entities waiting for a room to spawn in.</summary>
        private void RemoveFromAutoSpawnList()
        {
            if (m_index >= 0)
            {
                if (m_index != m_autoSpawnList.Count - 1)
                {
                    // Move the last time object to the removed object's index.
                    ksAutoSpawn last = m_autoSpawnList[m_autoSpawnList.Count - 1];
                    last.m_index = m_index;
                    m_autoSpawnList[m_index] = last;
                }
                m_autoSpawnList.RemoveAt(m_autoSpawnList.Count - 1);
                m_index = -1;
            }
        }

        /// <summary>
        /// Spawns entities for all the objects in the auto spawn list that are in the same scene as the
        /// <paramref name="room"/>. Does nothing if the room does not have auto-spawning enabled.
        /// </summary>
        /// <param name="room">Room to spawn entities in.</param>
        private static void AutoSpawnInRoomScene(ksRoom room)
        {
            if (!room.AllowPlayerSpawning)
            {
                return;
            }
            for (int i = m_autoSpawnList.Count - 1; i >= 0; i--)
            {
                ksAutoSpawn autoSpawn = m_autoSpawnList[i];
                if (autoSpawn.gameObject.scene == room.GameObject.scene)
                {
                    autoSpawn.Spawn(room);
                }
            }
        }
    }
}
