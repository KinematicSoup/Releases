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
using UnityEngine;

namespace KS.Reactor.Client.Unity.DOTS
{
    /// <summary>
    /// Unity does not bake <see cref="Rigidbody.constraints"/>, so we bake it to this component so it can be exposed to
    /// predictors and player controllers as <see cref="ksIRigidBody.Constraints"/> or
    /// <see cref="ksIRigidBody2D.Constraints"/>. This does not actually constrain the rigid body during Unity Physics
    /// simulation.
    /// </summary>
    public struct ksRigidBodyConstraintData : IComponentData
    {
        /// <summary>
        /// Rigid body constraints. This does not affect the rigid body during Unity Physics simulation.
        /// </summary>
        public ksRigidBodyConstraints Constraints;
    }
}
#endif
