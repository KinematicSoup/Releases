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

namespace KS.Reactor.Client.Unity.DOTS.Editor
{
    /// <summary>
    /// Baker for <see cref="Rigidbody"/> that converts the <see cref="Rigidbody.constraints"/> to a
    /// <see cref="ksRigidBodyConstraintData"/>.
    /// </summary>
    public class ksRigidBodyConstraintBaker : Baker<Rigidbody>
    {
        /// <summary>
        /// Bakes a <see cref="ksRigidBodyConstraintData"/> from <see cref="Rigidbody.constraints"/> if the constraints
        /// are not none.
        /// </summary>
        /// <param name="authoring"></param>
        public override void Bake(Rigidbody authoring)
        {
            if (authoring.constraints != RigidbodyConstraints.None)
            {
                Entity ent = GetEntity(TransformUsageFlags.None);
                AddComponent(ent, new ksRigidBodyConstraintData()
                {
                    Constraints = (ksRigidBodyConstraints)authoring.constraints
                });
            }
        }
    }
}
#endif