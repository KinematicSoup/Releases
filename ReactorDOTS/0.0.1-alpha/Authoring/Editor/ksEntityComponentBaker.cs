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
using UnityEditor;
using UnityEngine;
using Unity.Entities;

namespace KS.Reactor.Client.Unity.DOTS.Editor
{
    /// <summary>Baker for <see cref="ksEntityComponent"/> that converts to a <see cref="ksEntityData"/>.</summary>
    public class ksEntityComponentBaker : Baker<ksEntityComponent>
    {
        /// <summary>
        /// Bakes a <see cref="ksEntityData"/> from a <see cref="ksEntityComponent"/>. If the ks entity is permanent
        /// and is not a prefab asset, adds a <see cref="ksPermanentTag"/>. If the Unity Physics package is installed
        /// and the entity has a <see cref="Rigidbody"/>, adds a <see cref="ksRigidBodyModeData"/> with the rigid body
        /// mode value if the entity overrides the rigid body mode, and adds a <see cref="ksNoRigidBodyTag"/>
        /// if the <see cref="ksEntityComponent.RigidBodyExistence"/> is not
        /// <see cref="ksExistenceModes.CLIENT_AND_SERVER"/>. If the ks entity is not a prefab, adds a
        /// <see cref="ksSceneEntityTag"/>.
        /// </summary>
        /// <param name="authoring"></param>
        public override void Bake(ksEntityComponent authoring)
        {
            bool isPrefab = PrefabUtility.IsPartOfPrefabAsset(authoring);
            bool isPermanent = authoring.IsPermanent && !isPrefab;
            Entity ent = GetEntity(isPermanent ? TransformUsageFlags.None : TransformUsageFlags.Dynamic);
            AddComponent(ent, new ksEntityData()
            {
                Id = authoring.EntityId
            });
            if (isPermanent)
            {
                AddComponent<ksPermanentTag>(ent);
            }
#if REACTOR_DOTS_PHYSICS
            else if (authoring.GetComponent<Rigidbody>() != null)
            {
                if (authoring.RigidBodyExistence == ksExistenceModes.SERVER_ONLY)
                {
                    AddComponent(ent, new ksNoRigidBodyTag());
                }
                else if (authoring.PhysicsOverrides.RigidBodyMode != ksRigidBodyModes.DEFAULT)
                {
                    AddComponent(ent, new ksRigidBodyModeData()
                    {
                        Mode = authoring.PhysicsOverrides.RigidBodyMode
                    });
                }
            }
#endif
            if (!isPrefab)
            {
                AddComponent<ksSceneEntityTag>(ent);
            }
            if (!authoring.DestroyWithServer)
            {
                AddComponent<ksNoDestroyTag>(ent);
            }

            if (authoring.OverridePredictor)
            {
                AddSharedComponent(ent, new ksSharedPredictorOverride(authoring.Predictor));
            }
            if (authoring.OverrideControllerPredictor)
            {
                AddSharedComponent(ent, new ksSharedControllerPredictorOverride(authoring.ControllerPredictor));
            }
        }
    }
}