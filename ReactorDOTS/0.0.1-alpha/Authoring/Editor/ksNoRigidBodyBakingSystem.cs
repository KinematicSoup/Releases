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
#if REACTOR_DOTS_PHYSICS
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Reflection;
using Unity.Physics;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;
using KS.Reactor.Client.Unity;
using System.Collections.Generic;

namespace KS.Reactor.Client.Unity.DOTS
{
    // Needed to prevent 'X does not exist in the namespace KS.Reactor.Client.Unity' build error in generated code.
    using Unity = global::Unity;

    /// <summary>
    /// Baking system that removes physics components from DOTS entities with a <see cref="ksNoRigidBodyTag"/>.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct ksNoRigidBodyBakingSystem : ISystem
    {
        /// <summary>
        /// Removes <see cref="PhysicsVelocity"/>, <see cref="PhysicsDamping"/>, <see cref="PhysicsGravityFactor"/>,
        /// <see cref="PhysicsMass"/>, and <see cref="ksRigidBodyConstraintData"/> from DOTS entities with a
        /// <see cref="ksNoRigidBodyTag"/> and a <see cref="PhysicsVelocity"/>.
        /// </summary>
        /// <param name="state"></param>
        public void OnUpdate(ref SystemState state)
        {
            // All entities that were baked from game objects with a RigidBody have a PhysicsVelocity component, so we
            // look for entities with a ksNoRigidBodyTag and a PhysicsVelocity and remove physics components from them.
            EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach ((PhysicsVelocity velocity, Entity ent) in SystemAPI.Query<PhysicsVelocity>()
                .WithAll<ksNoRigidBodyTag>()
                .WithOptions(EntityQueryOptions.IncludePrefab)
                .WithEntityAccess())
            {
                commandBuffer.RemoveComponent<PhysicsVelocity>(ent);
                if (SystemAPI.HasComponent<PhysicsDamping>(ent))
                {
                    commandBuffer.RemoveComponent<PhysicsDamping>(ent);
                }
                if (SystemAPI.HasComponent<PhysicsGravityFactor>(ent))
                {
                    commandBuffer.RemoveComponent<PhysicsGravityFactor>(ent);
                }
                if (SystemAPI.HasComponent<PhysicsMass>(ent))
                {
                    commandBuffer.RemoveComponent<PhysicsMass>(ent);
                }
                if (SystemAPI.HasComponent<ksRigidBodyConstraintData>(ent))
                {
                    commandBuffer.RemoveComponent<ksRigidBodyConstraintData>(ent);
                }
            }
            commandBuffer.Playback(state.EntityManager);
        }
    }
}
#endif