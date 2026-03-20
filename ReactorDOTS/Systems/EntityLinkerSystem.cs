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
using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;

namespace KS.Reactor.Client.Unity.DOTS
{
    // Needed to prevent 'X does not exist in the namespace KS.Reactor.Client.Unity' build error in generated code.
    using Unity = global::Unity;

    /// <summary>
    /// System that builds maps of ks entity ids to scene entities for each subscene by checking for DOTS entities
    /// with <see cref="ksEntityData"/>, <see cref="SceneSection"/> (automatically added by Unity to scene entities),
    /// and without <see cref="ksPermanentTag"/> and <see cref="ksSharedRoomId"/> (indicating it's already linked to a
    /// ksEntity). Each map is stored in a <see cref="ksSceneEntityMap"/> component on a DOTS entity in scene section 0
    /// of the subscene the map is for.
    /// </summary>
    internal partial struct EntityLinkerSystem : ISystem
    {
        private static Dictionary<Hash128, ksSceneEntityMap> m_newMaps = new Dictionary<Hash128, ksSceneEntityMap>();

        /// <summary>
        /// Sets the update requirement that there be an entity with a <see cref="ksEntityData"/> and a
        /// <see cref="SceneSection"/>, but not a <see cref="ksPermanentTag"/> or a <see cref="ksSharedRoomId"/>.
        /// </summary>
        /// <param name="state"></param>
        public void OnCreate(ref SystemState state)
        {
            EntityQuery query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ksEntityData, SceneSection, ksSceneEntityTag>()
                .WithAbsent<ksPermanentTag, ksSharedRoomId>()
                .Build(ref state);
            state.RequireForUpdate(query);
        }

        /// <summary>
        /// Finds enabled DOTS entities with a <see cref="ksEntityData"/>, a <see cref="SceneSection"/>, and a
        /// <see cref="ksSceneEntityTag"/>that aren't  permanent (no <see cref="ksPermanentTag"/>) and are not linked
        /// to a ks entity (no <see cref="ksSharedRoomId"/>), and disables them and puts them in a
        /// <see cref="ksSceneEntityMap"/> so they can be retrieved by their id and linked to the corresponding ks
        /// entity it is first synced.
        /// </summary>
        /// <param name="state"></param>
        public void OnUpdate(ref SystemState state)
        {
            ksSceneEntityMap entityMap = null;
            Hash128 sceneGuid = new Hash128();
            // Structural changes are not allowed when iterating entities, so we use a command buffer to delay the
            // changes.
            EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (entityData, scene, ent) in SystemAPI.Query<RefRO<ksEntityData>, SceneSection>()
                .WithAbsent<ksPermanentTag, ksSharedRoomId>()
                .WithAll<ksSceneEntityTag>()
                .WithEntityAccess())
            {
                uint entityId = entityData.ValueRO.Id;
                if (entityId == 0)
                {
                    continue;
                }
                if (scene.SceneGUID != sceneGuid)
                {
                    sceneGuid = scene.SceneGUID;
                    entityMap = FindOrCreateEntityMap(ref state, ref commandBuffer, in sceneGuid);
                }
                entityMap[entityId] = ent;
                commandBuffer.AddComponent<Disabled>(ent);
            }
            commandBuffer.Playback(state.EntityManager);
            commandBuffer.Dispose();
            m_newMaps.Clear();
        }

        /// <summary>
        /// Finds the <see cref="ksSceneEntityMap"/> for the scene with the given <paramref name="sceneGuid"/>, or
        /// creates one and attaches it to a DOTS entity in section 0 of the scene if none is found.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="commandBuffer">
        /// Command buffer to buffer entity creation commands in if a new scene map is created.
        /// </param>
        /// <param name="sceneGuid">Identifies the scene to get or create the map for.</param>
        /// <returns>Scene entity map</returns>
        private ksSceneEntityMap FindOrCreateEntityMap(
            ref SystemState state, 
            ref EntityCommandBuffer commandBuffer, 
            in Hash128 sceneGuid) 
        {
            ksSceneEntityMap entityMap;
            if (m_newMaps.TryGetValue(sceneGuid, out entityMap))
            {
                return entityMap;
            }
            SceneSection scene = new SceneSection()
            {
                SceneGUID = sceneGuid
            };
            foreach (var map in SystemAPI.Query<ksSceneEntityMap>().WithSharedComponentFilter(scene))
            {
                return map;
            }
            entityMap = new ksSceneEntityMap();
            m_newMaps[sceneGuid] = entityMap;
            Entity ent = commandBuffer.CreateEntity();
            commandBuffer.AddComponent(ent, entityMap);
            commandBuffer.AddSharedComponent(ent, scene);
            return entityMap;
        }
    }
}