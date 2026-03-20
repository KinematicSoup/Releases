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
using Unity.Entities;
using KS.Reactor.Client.Unity;

namespace KS.Reactor.Client.Unity.DOTS
{
    /// <summary>
    /// Unity DOTS implementation of ksBaseEntity. Entities are objects create, updated, and destroyed by a 
    /// server and replicated to clients. Each entity has a DOTS entity spawned for it with a 
    /// <see cref="ksEntityData"/>.
    /// </summary>
    public class ksEntityDOTS : ksBaseEntity
    {
        /// <summary>Room the entity is in.</summary>
        public ksRoomDOTS Room
        {
            get { return (ksRoomDOTS)BaseRoom; }
        }

        /// <summary>The DOTS entity for this entity.</summary>
        public Entity DOTSEntity
        {
            get { return m_dotsEntity; }
        }
        private Entity m_dotsEntity;

        /// <summary>Was this entity part of the initial scene? If false, the entity was spawned at runtime.</summary>
        public bool IsSceneEntity
        {
            get { return m_isSceneEntity; }
            internal set { m_isSceneEntity = value; }
        }
        private bool m_isSceneEntity;

        /// <summary>Creates and initializes the DOTS entity for this entity.</summary>
        /// <param name="spawnParams">
        /// If the entity was spawned by the local player, this is the spawn parameters object used to spawn the
        /// entity, otherwise it is null.
        /// </param>
        protected override void Initialize(ksSpawnParams spawnParams)
        {
            m_dotsEntity = Room.System.FindOrCreateEntity(this);
        }

        /// <summary>Applies the entity's transform to its DOTS entity.</summary>
        /// <param name="flags">Flags indicating which tranform components (position/rotation/scale) to update.</param>
        protected override void UpdateTransform(
            ksReadOnlyTransformState.DirtyFlag flags = ksReadOnlyTransformState.DirtyFlag.ALL)
        {
            Room.System.UpdateTransform(this, flags);
        }

        /// <summary>
        /// Updates <paramref name="transform"/> with the values from the DOTS entity's transform, setting dirty flags
        /// for values that are different. This is called on locally-owned entities with the 
        /// <see cref="ksOwnerPermissions.TRANSFORM"/> permission to get a transform update to send to the server.
        /// </summary>
        /// <param name="transform">Transform to update.</param>
        protected override void GetTransformUpdate(ksTransformState transform)
        {
            Room.System.GetEntityTransformUpdate(this, transform);
        }

        /// <summary>Cleans up the entity and returns it to a pool of reusable entity resources.</summary>
        /// <param name="reason">The reason the entity was destroyed.</param>
        protected override void Destroy(ksDestroyReason reason)
        {
            if (!Room.System.EntityManager.HasComponent<ksNoDestroyTag>(m_dotsEntity))
            {
                if (m_isSceneEntity)
                {
                    Room.System.EntityManager.AddComponent<Disabled>(m_dotsEntity);
                    Room.System.EntityManager.RemoveComponent<ksSharedRoomId>(m_dotsEntity);
                }
                else
                {
                    Room.System.EntityManager.DestroyEntity(m_dotsEntity);
                }
            }
            else
            {
                Room.System.EntityManager.RemoveComponent<ksSharedRoomId>(m_dotsEntity);
            }
            m_dotsEntity = new Entity();// Set to invalid entity
            if (!ksObjectPool<ksEntity>.Instance.IsFull)
            {
                CleanUp();
                ksObjectPool<ksEntityDOTS>.Instance.Return(this);
            }
        }

        /// <summary>
        /// Creates a predictor to use when the entity does not have a player controller with input prediction enabled.
        /// If the entity has a <see cref="ksSharedPredictorOverride"/>, create's an instance from the referenced
        /// predictor, otherwise creates an instance from <see cref="ksRoom.DefaultEntityPredictor"/>.
        /// </summary>
        /// <param name="predictor">The created predictor.</param>
        /// <returns>Always true</returns>
        protected override bool CreateNonInputPredictor(out ksIPredictor predictor)
        {
            ksPredictor predictorAsset = GetPredictorAsset();
            if (predictorAsset != null)
            {
                predictor = predictorAsset.CreateInstance();

                // If the input predictor is not assigned and it uses the same predictor asset, assign the
                // instance to be the input predictor as well.
                if (!IsInputPredictorAssigned)
                {
                    ksPredictor controllerPredictor = GetControllerPredictorAsset();
                    if (predictorAsset == controllerPredictor)
                    {
                        InputPredictor = predictor;
                    }
                }
            }
            else
            {
                predictor = null;
            }
            return true;
        }

        /// <summary>
        /// Creates a predictor to use when the entity has a player controller with input prediction enabled. If the
        /// entity has a <see cref="ksSharedControllerPredictorOverride"/>, create's an instance from the referenced
        /// predictor, otherwise creates an instance from <see cref="ksRoom.DefaultEntityControllerPredictor"/>.
        /// </summary>
        /// <param name="predictor">The created predictor.</param>
        /// <returns>Always true</returns>
        protected override bool CreateInputPredictor(out ksIPredictor predictor)
        {
            ksPredictor predictorAsset = GetControllerPredictorAsset();
            if (predictorAsset != null)
            {
                predictor = predictorAsset.CreateInstance();

                // If the non-input predictor is not assigned and it uses the same predictor asset, assign the
                // instance to be the non-input predictor as well.
                if (!IsNonInputPredictorAssigned)
                {
                    ksPredictor noControllerPredictor = GetPredictorAsset();
                    if (predictorAsset == noControllerPredictor)
                    {
                        InputPredictor = predictor;
                    }
                }
            }
            else
            {
                predictor = null;
            }
            return true;
        }

        /// <summary>
        /// Gets the predictor asset to use for this entity without a player controller that uses input prediction.
        /// </summary>
        /// <returns>Non-input predictor asset</returns>
        private ksPredictor GetPredictorAsset()
        {
            if (Room.System.EntityManager.HasChunkComponent<ksSharedPredictorOverride>(m_dotsEntity))
            {
                return Room.System.EntityManager.GetSharedComponent<ksSharedPredictorOverride>(m_dotsEntity).Predictor;
            }
            return Room.DefaultEntityPredictor;
        }

        /// <summary>
        /// Gets the predictor asset to use for this entity with a player controller that uses input prediction.
        /// </summary>
        /// <returns>Input predictor asset</returns>
        private ksPredictor GetControllerPredictorAsset()
        {
            if (Room.System.EntityManager.HasChunkComponent<ksSharedControllerPredictorOverride>(m_dotsEntity))
            {
                return Room.System.EntityManager.GetSharedComponent<ksSharedControllerPredictorOverride>(m_dotsEntity)
                    .Predictor;
            }
            return Room.DefaultEntityControllerPredictor;
        }

#if REACTOR_DOTS_PHYSICS
        /// <summary>
        /// Called when the owner or owner permissions change. If the entity has a rigid body, sets 
        /// <see cref="ksBaseDOTSRigidBody.ForceKinematic"/> to false if the local player owns the entity with the
        /// <see cref="ksOwnerPermissions.TRANSFORM"/> permission, otherwise sets it to true.
        /// </summary>
        protected override void OwnershipChanged()
        {
            ksBaseDOTSRigidBody rigidBody = RigidBody != null ?
                (RigidBody as ksBaseDOTSRigidBody) : (RigidBody2D as ksBaseDOTSRigidBody);
            if (rigidBody != null)
            {
                rigidBody.ForceKinematic = !HasOwnerPermission(ksOwnerPermissions.TRANSFORM);
            }
        }
#endif
    }
}