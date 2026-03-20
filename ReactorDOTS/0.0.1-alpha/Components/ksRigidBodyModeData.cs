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
using Unity.Entities;
using Unity.Physics;

namespace KS.Reactor.Client.Unity.DOTS
{
    /// <summary>
    /// Determines if rigid bodies should be 2D or 3D. If attached to an entity a <see cref="ksEntityData"/> and a
    /// <see cref="PhysicsVelocity"/>, the ks entity for the DOTS entity will spawns with a
    /// <see cref="ksDOTSRigidBody"/> if <see cref="Mode"/> is <see cref="ksRigidBodyModes.RIGID_BODY_3D"/>, or a
    /// <see cref="ksDOTSRigidBody2DView"/> if <see cref="Mode"/> is <see cref="ksRigidBodyModes.RIGID_BODY_2D"/>. If
    /// attached to the entity for the room, sets <see cref="ksBaseDOTSRigidBody.DefaultType"/> to <see cref="Mode"/>
    /// when the room connects, which determines which rigid body to spawn for ks entities without a
    /// <see cref="ksRigidBodyModeData"/>.
    /// </summary>
    public struct ksRigidBodyModeData : IComponentData
    {
        /// <summary>Rigid body mode</summary>
        public ksRigidBodyModes Mode;
    }
}
#endif
