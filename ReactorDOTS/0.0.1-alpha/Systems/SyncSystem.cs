/*
KINEMATICSOUP CONFIDENTIAL
 Copyright(c) 2014-2026 KinematicSoup Technologies Incorporated 
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
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

#if REACTOR_DOTS_PHYSICS
using Unity.Physics;
#endif
using Hash128 = Unity.Entities.Hash128;

namespace KS.Reactor.Client.Unity.DOTS
{
    // Needed to prevent 'X does not exist in the namespace KS.Reactor.Client.Unity' build error in generated code.
    using Unity = global::Unity;

    /// <summary>
    /// This provides functions for spawning and updates DOTS entities for rooms/player/entities. These functions use
    /// <see cref="SystemAPI"/> functions that can only be used from within a system. Auto-creation is disabled and
    /// instead rooms will create the system if one does not already exist in their <see cref="World"/>.
    /// </summary>
    [DisableAutoCreation]
    internal partial class SyncSystem : SystemBase
    {
        private bool m_loggedMissingPrefabMapError = false;

        /// <summary>Does nothing.</summary>
        protected override void OnUpdate()
        {
            
        }

        /// <summary>
        /// Spawns a DOTS entity for a room with a <see cref="ksRoomData"/>. Looks for a DOTS entity with a 
        /// <see cref="ksRoomData"/> that matches the room's scene name and room type and isn't already linked to a
        /// room, or spawns one if none is found.
        /// </summary>
        /// <param name="room">Room to spawn entity for.</param>
        /// <returns>DOTS entity for the room.</returns>
        public Entity FindOrCreateRoom(ksRoomDOTS room)
        {
            ksRoomData data;
            Hash128 sceneGuid;
            Entity ent = GetRoomEntityAndSceneGuid(room.Info, out data, out sceneGuid);
            room.SceneGuid = sceneGuid;
            if (!EntityManager.Exists(ent))
            {
                room.IsTempEntity = true;
                ent = EntityManager.CreateEntity(typeof(ksRoomData));
                data = new ksRoomData
                {
                    Id = room.Id,
                    Scene = room.Info.Scene == null ? "" : room.Info.Scene,
                    RoomType = room.Info.Type == null ? "" : room.Info.Type,
                    SendRate = room.SendRate
                };
                room.DefaultEntityPredictor = ScriptableObject.CreateInstance<ksLinearPredictorAsset>();
                room.DefaultEntityControllerPredictor = 
                    ScriptableObject.CreateInstance<ksConvergingInputPredictorAsset>();
            }
            else
            {
                room.IsTempEntity = false;
                data.Id = room.Id;
                room.DefaultEntityPredictor = data.DefaultEntityPredictor;
                room.DefaultEntityControllerPredictor = data.DefaultEntityControllerPredictor;
                room.DefaultPlayerPredictor = data.DefaulPlayerPredictor;
                if (room.Predictor == null && data.DefaultRoomPredictor != null)
                {
                    room.Predictor = data.DefaultRoomPredictor.CreateInstance();
                }
                try
                {
                    room.SendRate = data.SendRate;
                }
                catch (ArgumentException ex)
                {
                    ksLog.LogException(this, ex);
                }
#if REACTOR_DOTS_PHYSICS
                if (SystemAPI.HasComponent<ksRigidBodyModeData>(ent))
                {
                    ksBaseDOTSRigidBody.DefaultType = SystemAPI.GetComponentRO<ksRigidBodyModeData>(ent).ValueRO.Mode;
                }
#endif
            }
            SystemAPI.SetComponent(ent, data);
            EntityManager.AddSharedComponent(ent, new ksSharedRoomId(room.Id));
            return ent;
        }

        /// <summary>Spawns a DOTS entity for a player with a <see cref="ksPlayerData"/>.</summary>
        /// <param name="player">Player to spawn entity for.</param>
        /// <returns>DOTS entity for the player.</returns>
        public Entity CreatePlayer(ksPlayerDOTS player)
        {
            Entity ent = EntityManager.CreateEntity(typeof(ksPlayerData));
            SystemAPI.SetComponent(ent, new ksPlayerData()
            {
                Id = player.Id,
                RoomId = player.Room.Id
            });
            EntityManager.AddSharedComponent(ent, new ksSharedRoomId(player.Room.Id));
            return ent;
        }

        /// <summary>
        /// Spawns a DOTS entity for an entity from the entity prefab for the entity's asset id, or by finding a
        /// scene entity from the scene's <see cref="ksSceneEntityMap"/> with the entity's id. If no scene entity is
        /// found and no prefab is found, creates a DOTS entity without a prefab.
        /// </summary>
        /// <param name="entity">Entity to spawn DOTS entity for.</param>
        /// <returns>Dots entity for the entity.</returns>
        public Entity FindOrCreateEntity(ksEntityDOTS entity)
        {
            entity.IsSceneEntity = false;

            // Check if there is a DOTS entity in the scene for this entity id.
            Entity ent = new Entity();
            ksSceneEntityMap map = GetSceneEntityMap(entity.Room.SceneGuid);
            if (map != null && map.TryGetValue(entity.Id, out ent))
            {
                // If it has a ksSharedRoomId, it's already linked to another entity and we can't use it.
                if (EntityManager.HasComponent<ksSharedRoomId>(ent))
                {
                    ent = new Entity();
                }
                else
                {
                    EntityManager.RemoveComponent<Disabled>(ent);
                    entity.IsSceneEntity = true;
                }
            }

            // Instantiate a DOTS entity from the prefab for the asset id if we didn't find a scene DOTS entity.
            if (!EntityManager.Exists(ent) && entity.AssetId != 0)
            {
                ksPrefabCacheSystem prefabSystem = ksPrefabCacheSystem.Get();
                if (prefabSystem != null)
                {
                    Entity prefab;
                    if (!prefabSystem.TryGet(entity.AssetId, out prefab))
                    {
                        ksLog.Error(this, "Could not find entity prefab with asset id " + entity.AssetId + ".");
                    }
                    else
                    {
                        ent = EntityManager.Instantiate(prefab);
                    }
                }
                else if (!m_loggedMissingPrefabMapError)
                {
                    m_loggedMissingPrefabMapError = true;
                    ksLog.Error(this, "Cannot spawn entity prefabs because there is no entity prefab map. Did you add a " +
                        typeof(ksRoomType) + " to your subscene?");
                }
            }

            // If we didn't find a prefab to instantiate from, create a DOTS entity without a prefab.
            if (!EntityManager.Exists(ent))
            {
                ent = EntityManager.CreateEntity(typeof(LocalTransform), typeof(LocalToWorld), typeof(ksEntityData));
            }

            // Set the entity data.
            ksEntityData data = new ksEntityData()
            {
                Id = entity.Id,
                RoomId = entity.Room.Id
            };
            if (!SystemAPI.HasComponent<ksEntityData>(ent))
            {
                EntityManager.AddComponentData(ent, data);
            }
            else
            {
                SystemAPI.SetComponent(ent, data);
            }

            // Set the shared room id.
            EntityManager.AddSharedComponent(ent, new ksSharedRoomId(entity.Room.Id));

            // Set the transform.
            UpdateTransform(entity, ksReadOnlyTransformState.DirtyFlag.ALL);

#if REACTOR_DOTS_PHYSICS
            if (SystemAPI.HasComponent<PhysicsVelocity>(ent))
            {
                ksRigidBodyModes mode = ksBaseDOTSRigidBody.DefaultType;
                if (SystemAPI.HasComponent<ksRigidBodyModeData>(ent))
                {
                    mode = SystemAPI.GetComponentRO<ksRigidBodyModeData>(ent).ValueRO.Mode;
                }
                bool forceKinematic = !entity.HasOwnerPermission(ksOwnerPermissions.TRANSFORM);
                if (mode == ksRigidBodyModes.RIGID_BODY_2D)
                {
                    entity.RigidBody2D = new ksDOTSRigidBody2DView(ent, EntityManager, forceKinematic);
                }
                else
                {
                    entity.RigidBody = new ksDOTSRigidBody(ent, EntityManager, forceKinematic);
                }
            }
#endif
            return ent;
        }

        /// <summary>Applies an entity's transform to its DOTS entity.</summary>
        /// <param name="entity">Entity to apply transform update for.</param>
        /// <param name="flags">Flags indicating which tranform components (position/rotation/scale) to update.</param>
        public void UpdateTransform(ksEntityDOTS entity, ksReadOnlyTransformState.DirtyFlag flags)
        {
            if (flags == ksReadOnlyTransformState.DirtyFlag.NONE)
            {
                return;
            }
            RefRW<LocalTransform> transform;
            if (!TryGetTransformRW(entity.DOTSEntity, out transform))
            {
                return;
            }
            if ((flags & ksReadOnlyTransformState.DirtyFlag.POSITION) != 0)
            {
                transform.ValueRW.Position = entity.Transform.Position.ToFloat3();
            }
            if ((flags & ksReadOnlyTransformState.DirtyFlag.ROTATION) != 0)
            {
                transform.ValueRW.Rotation = entity.Transform.Rotation.ToQuaternion();
            }
            if ((flags & ksReadOnlyTransformState.DirtyFlag.SCALE) != 0)
            {
                ksVector3 scale = entity.Transform.Scale;
                if (SystemAPI.HasComponent<PostTransformMatrix>(entity.DOTSEntity))
                {
                    // Set scale on the post transform matrix, preserving any position/rotation offset.
                    RefRW<PostTransformMatrix> matrix = SystemAPI.GetComponentRW<PostTransformMatrix>(entity.DOTSEntity);
                    float4x4 m = matrix.ValueRO.Value;

                    Vector3 v = new Vector3(m.c0.x, m.c0.y, m.c0.z).normalized * scale.X;
                    m.c0 = new float4(v.x, v.y, v.z, m.c0.w);

                    v = new Vector3(m.c1.x, m.c1.y, m.c1.z).normalized * scale.Y;
                    m.c1 = new float4(v.x, v.y, v.z, m.c1.w);

                    v = new Vector3(m.c2.x, m.c2.y, m.c2.z).normalized * scale.Z;
                    m.c2 = new float4(v.x, v.y, v.z, m.c2.w);

                    matrix.ValueRW.Value = m;
                    transform.ValueRW.Scale = 1f;
                }
                else if (scale.X == scale.Y && scale.X == scale.Z)
                {
                    transform.ValueRW.Scale = entity.Transform.Scale.X;
                }
                else
                {
                    // Add a post transform matrix with the non-uniform scale.
                    transform.ValueRW.Scale = 1f;
                    EntityManager.AddComponentData(entity.DOTSEntity, new PostTransformMatrix()
                    {
                        Value = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, scale)
                    });
                }
            }
        }

        /// <summary>
        /// Updates <paramref name="transform"/> with the values from the DOTS entity's transform, setting dirty flags
        /// for values that are different.
        /// </summary>
        /// <param name="transform">Transform to update.</param>
        public void GetEntityTransformUpdate(ksEntityDOTS entity, ksTransformState transform)
        {
            LocalTransform localTransform = SystemAPI.GetComponentRO<LocalTransform>(entity.DOTSEntity).ValueRO;

            // Calling the transform setters will set a dirty flag even if the value did not change, so we check if
            // the values changed before calling the setter.
            if (!transform.Position.EqualsFloat3(localTransform.Position))
            {
                transform.Position = localTransform.Position.ToKSVector3();
            }

            if (!transform.Rotation.EqualsQuaternion(localTransform.Rotation.value))
            {
                transform.Rotation = localTransform.Rotation.ToKSQuaternion();
            }

            ksVector3 scale;
            if (SystemAPI.HasComponent<PostTransformMatrix>(entity.DOTSEntity))
            {
                float4x4 matrix = SystemAPI.GetComponentRO<PostTransformMatrix>(entity.DOTSEntity).ValueRO.Value;
                scale = new ksVector3(
                    math.length(matrix.c0.xyz),
                    math.length(matrix.c1.xyz),
                    math.length(matrix.c2.xyz)) * localTransform.Scale;
            }
            else
            {
                scale = new ksVector3(localTransform.Scale, localTransform.Scale, localTransform.Scale);
            }
            if (transform.Scale != scale)
            {
                transform.Scale = scale;
            }
        }

        /// <summary>
        /// Gets the map of entity ids the scene entities for the scene with the given <paramref name="sceneGuid"/>, or
        /// null if none is found.
        /// </summary>
        /// <param name="sceneGuid">Scene guid</param>
        /// <returns>Scene entity map, or null if none is found.</returns>
        private ksSceneEntityMap GetSceneEntityMap(in Hash128 sceneGuid)
        {
            if (!sceneGuid.IsValid)
            {
                return null;
            }
            SceneSection scene = new SceneSection()
            {
                SceneGUID = sceneGuid
            };
            foreach (var entityMap in SystemAPI.Query<ksSceneEntityMap>().WithSharedComponentFilter(scene))
            {
                return entityMap;
            }
            return null;
        }

        /// <summary>
        /// Looks for a DOTS entity with a <see cref="SceneSection"/> and a <see cref="ksRoomData"/> that matches the
        /// scene name from <paramref name="roomInfo"/> and sets <paramref name="sceneGuid"/> to the guid from the
        /// <see cref="SceneSection"/>. If the type from the <see cref="ksRoomData"/> also matches the type from the
        /// <paramref name="roomInfo"/> and it is not already linked to another room (doesn't have a
        /// <see cref="ksSharedRoomId"/>), sets <paramref name="roomData"/> to it.
        /// </summary>
        /// <param name="roomInfo">
        /// Room info with scene and type string to look for in a <see cref="ksRoomData"/>.
        /// </param>
        /// <param name="roomData">
        /// Set to the <see cref="ksRoomData"/> with room type and scene name that matched the
        /// <paramref name="roomInfo"/> and wasn't already linked to room, or default struct if none was found.
        /// </param>
        /// <param name="sceneGuid">
        /// Set to the scene guid from the <see cref="SceneSection"/> on the dots entity with a
        /// <see cref="ksRoomData"/> that matched the scene name from the <paramref name="roomInfo"/>, or default
        /// struct if none was found.
        /// </param>
        /// <returns>
        /// The DOTS entity with the <see cref="ksRoomData"/> that matched the scene name and room type from
        /// <paramref name="roomInfo"/> and wasn't already linked to a room, or invalid entity if none was found.
        /// </returns>
        private Entity GetRoomEntityAndSceneGuid(ksRoomInfo roomInfo, out ksRoomData roomData, out Hash128 sceneGuid)
        {
            roomData = new ksRoomData();
            sceneGuid = new Hash128();
            if (string.IsNullOrEmpty(roomInfo.Scene))
            {
                return new Entity();
            }
            foreach (var (data, scene, ent) in SystemAPI.Query<ksRoomData, SceneSection>().WithEntityAccess())
            {
                if (data.Scene == roomInfo.Scene)
                {
                    sceneGuid = scene.SceneGUID;
                    if (!string.IsNullOrEmpty(roomInfo.Type) && data.RoomType == roomInfo.Type)
                    {
                        // If it has a ksSharedRoomId, it's already linked to a room.
                        if (EntityManager.HasChunkComponent<ksSharedRoomId>(ent))
                        {
                            return new Entity();
                        }
                        roomData = data;
                        return ent;
                    }
                }
            }
            return new Entity();
        }

        /// <summary>Tries to get a read-write reference to a local transform from a DOTS entity.</summary>
        /// <param name="ent">DOTS entity to get read-write local transform reference from.</param>
        /// <param name="transformRef">Set to the local transform reference</param>
        /// <returns>True if the DOTS entity had the local transform.</returns>
        private bool TryGetTransformRW(Entity ent, out RefRW<LocalTransform> transformRef)
        {
            try
            {
                transformRef = SystemAPI.GetComponentRW<LocalTransform>(ent);
                return true;
            }
            catch (Exception e)
            {
                ksLog.LogException(e);
                transformRef = new RefRW<LocalTransform>();
                return false;
            }
        }
    }
}