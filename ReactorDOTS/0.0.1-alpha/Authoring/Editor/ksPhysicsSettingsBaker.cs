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
using KS.Reactor.Client.Unity;

namespace KS.Reactor.Client.Unity.DOTS.Editor
{
    /// <summary>
    /// Baker for <see cref="ksPhysicsSettings"/> that converts the <see cref="ksEntityPhysicsSettings.RigidBodyMode"/>
    /// to a <see cref="ksRigidBodyConstraintData"/>.
    /// </summary>
    public class ksPhysicsSettingsBaker : Baker<ksPhysicsSettings>
    {
        /// <summary>Bakes a <see cref="ksRigidBodyConstraintData"/> from a <see cref="ksPhysicsSettings"/>.</summary>
        /// <param name="authoring"></param>
        public override void Bake(ksPhysicsSettings authoring)
        {
            Entity ent = GetEntity(TransformUsageFlags.None);
            AddComponent(ent, new ksRigidBodyModeData()
            {
                Mode = authoring.DefaultEntityPhysics.RigidBodyMode
            });
        }
    }
}
#endif