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
using UnityEngine;

namespace KS.Reactor.Client.Unity.DOTS
{
    /// <summary>
    /// <see cref="ksIRigidBody"/> implementation that accesses Unity Physics rigid body data for a DOTS entity.
    /// </summary>
    public class ksDOTSRigidBody : ksBaseDOTSRigidBody, ksIRigidBody
    {
        // We need to track the non-kinematic velocities off of the PhysicsVelocity component to prevent Unity Physics
        // from moving the entity.
        private ksVector3 m_dynamicVelocity = ksVector3.Zero;
        private ksVector3 m_dynamicAngularVelocity = ksVector3.Zero;
        private ksVector3 m_kinematicMovement = ksVector3.Zero;
        private ksQuaternion m_kinematicRotation = ksQuaternion.Identity;

        /// <summary>Constructor</summary>
        /// <param name="ent">Entity with physics components</param>
        /// <param name="entityManager">Entity manager</param>
        /// <param name="forceKinematic">
        /// If true, sets <see cref="PhysicsMass.InverseMass"/> and <see cref="PhysicsMass.InverseInertia"/> to zero so
        /// Unity Physics doesn't try to move the object.
        /// </param>
        public ksDOTSRigidBody(Entity ent, EntityManager entityManager, bool forceKinematic) 
            : base(ent, entityManager, forceKinematic)
        {

        }

        /// <summary>Linear velocity</summary>
        public ksVector3 Velocity
        {
            get
            {
                if (IsKinematic)
                {
                    return ksVector3.Zero;
                }
                if (ForceKinematic)
                {
                    return m_dynamicVelocity;
                }
                if (m_entityManager.HasComponent<PhysicsVelocity>(m_ent))
                {
                    return (Vector3)m_entityManager.GetComponentData<PhysicsVelocity>(m_ent).Linear;
                }
                return ksVector3.Zero;
            }

            set
            {
                if (IsKinematic)
                {
                    ksLog.Warning(this, "Setting velocity on a kinematic rigid body is not supported.");
                    return;
                }
                m_dynamicVelocity = value;
                if (!ForceKinematic)
                {
                    if (m_entityManager.HasComponent<PhysicsVelocity>(m_ent))
                    {
                        PhysicsVelocity velocityData = m_entityManager.GetComponentData<PhysicsVelocity>(m_ent);
                        velocityData.Linear = (Vector3)value;
                        m_entityManager.SetComponentData(m_ent, velocityData);
                    }
                    else if (value != ksVector3.Zero)
                    {
                        m_entityManager.AddComponentData(m_ent, new PhysicsVelocity()
                        {
                            Linear = (Vector3)value
                        });
                    }
                }
            }
        }

        /// <summary>Angular velocity. Component values are degrees per second.</summary>
        public ksVector3 AngularVelocity
        {
            get
            {
                if (IsKinematic)
                {
                    return ksVector3.Zero;
                }
                if (ForceKinematic)
                {
                    return m_dynamicAngularVelocity;
                }
                if (m_entityManager.HasComponent<PhysicsVelocity>(m_ent))
                {
                    return (Vector3)m_entityManager.GetComponentData<PhysicsVelocity>(m_ent).Angular *
                        ksMath.FRADIANS_TO_DEGREES;
                }
                return ksVector3.Zero;
            }

            set
            {
                if (IsKinematic)
                {
                    ksLog.Warning(this, "Setting angular velocity on a kinematic rigid body is not supported.");
                    return;
                }
                m_dynamicAngularVelocity = value;
                if (!ForceKinematic)
                {
                    if (m_entityManager.HasComponent<PhysicsVelocity>(m_ent))
                    {
                        PhysicsVelocity velocityData = m_entityManager.GetComponentData<PhysicsVelocity>(m_ent);
                        velocityData.Angular = (Vector3)(value * ksMath.FDEGREES_TO_RADIANS);
                        m_entityManager.SetComponentData(m_ent, velocityData);
                    }
                    else if (value != ksVector3.Zero)
                    {
                        m_entityManager.AddComponentData(m_ent, new PhysicsVelocity()
                        {
                            Angular = (Vector3)(value * ksMath.FDEGREES_TO_RADIANS)
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Amount of movement that will be applied to a kinematic rigid body entity during the next physics simulation
        /// step. After the simulation step this value will be reset to <see cref="ksVector3.Zero"/>.
        /// </summary>
        public ksVector3 KinematicMovement
        {
            get { return m_kinematicMovement; }
            set
            {
                if (IsKinematic)
                {
                    m_kinematicMovement = value;
                }
                else
                {
                    ksLog.Warning(this, "Cannot set KinematicMovement on a non-kinematic rigid body.");
                }
            }
        }

        /// <summary>
        /// Amount of rotation that will be applied to a kinematic rigid body entity during the next physics simulation
        /// step. After the simulation step this value will be reset to <see cref="ksQuaternion.Identity"/>.
        /// </summary>
        public ksQuaternion KinematicRotation
        {
            get { return m_kinematicRotation; }
            set
            {
                if (!IsKinematic)
                {
                    ksLog.Warning(this, "Cannot set KinematicRotation on a non-kinematic rigid body.");
                }
                else if (value.IsValid)
                {
                    m_kinematicRotation = value;
                }
                else
                {
                    ksLog.Warning(this, "KinematicRotation must be a valid quaternion.");
                }
            }
        }

        /// <summary>Clear the kinematic movement and rotation values.</summary>
        public void ClearKinematicMotion()
        {
            m_kinematicMovement = ksVector3.Zero;
            m_kinematicRotation = ksQuaternion.Identity;
        }

        /// <summary>
        /// Controls which degrees of freedom are allowed for the simulation of this rigidbody. Has no effect
        /// when Unity Physics simulates the rigid body.
        /// </summary>
        public ksRigidBodyConstraints Constraints
        {
            get 
            {
                return m_entityManager.HasComponent<ksRigidBodyConstraintData>(m_ent) ?
                    m_entityManager.GetComponentData<ksRigidBodyConstraintData>(m_ent).Constraints :
                    ksRigidBodyConstraints.NONE;
            }
            set 
            {
                if (m_entityManager.HasComponent<ksRigidBodyConstraintData>(m_ent))
                {
                    m_entityManager.SetComponentData(m_ent, new ksRigidBodyConstraintData()
                    {
                        Constraints = value
                    });
                }
                else if (value != ksRigidBodyConstraints.NONE)
                {
                    m_entityManager.AddComponentData(m_ent, new ksRigidBodyConstraintData()
                    {
                        Constraints = value
                    });
                }
            }
        }
    }
}
#endif