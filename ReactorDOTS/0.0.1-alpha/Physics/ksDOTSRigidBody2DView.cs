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
using Unity.Mathematics;
using UnityEngine;

namespace KS.Reactor.Client.Unity.DOTS
{
    /// <summary>
    /// <see cref="ksIRigidBody2D"/> implementation that provides a 2D view of Unity Physics rigid body data for a DOTS
    /// entity.
    /// </summary>
    public class ksDOTSRigidBody2DView : ksBaseDOTSRigidBody, ksIRigidBody2D
    {
        // We need to track the non-kinematic velocities off of the PhysicsVelocity component to prevent Unity Physics
        // from moving the entity.
        private ksVector2 m_dynamicVelocity = ksVector2.Zero;
        private float m_dynamicAngularVelocity = 0.0f;
        private ksVector2 m_kinematicMovement = ksVector2.Zero;
        private float m_kinematicRotation = 0f;

        /// <summary>Constructor</summary>
        /// <param name="ent">Entity with physics components</param>
        /// <param name="entityManager">Entity manager</param>
        /// <param name="forceKinematic">
        /// If true, sets <see cref="PhysicsMass.InverseMass"/> and <see cref="PhysicsMass.InverseInertia"/> to zero so
        /// Unity Physics doesn't try to move the object.
        /// </param>
        public ksDOTSRigidBody2DView(Entity ent, EntityManager entityManager, bool forceKinematic)
            : base(ent, entityManager, forceKinematic)
        {

        }

        /// <summary>Linear velocity</summary>
        public ksVector2 Velocity
        {
            get
            {
                if (IsKinematic)
                {
                    return ksVector2.Zero;
                }
                if (ForceKinematic)
                {
                    return m_dynamicVelocity;
                }
                if (m_entityManager.HasComponent<PhysicsVelocity>(m_ent))
                {
                    return ((ksVector3)(Vector3)m_entityManager.GetComponentData<PhysicsVelocity>(m_ent).Linear).XY;
                }
                return ksVector2.Zero;
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
                        velocityData.Linear = new float3(value.X, value.Y, 0f);
                        m_entityManager.SetComponentData(m_ent, velocityData);
                    }
                    else if (value != ksVector2.Zero)
                    {
                        m_entityManager.AddComponentData(m_ent, new PhysicsVelocity()
                        {
                            Linear = new float3(value.X, value.Y, 0f)
                        });
                    }
                }
            }
        }

        /// <summary>Angular velocity in degrees per second.</summary>
        public float AngularVelocity
        {
            get
            {
                if (IsKinematic)
                {
                    return 0f;
                }
                if (ForceKinematic)
                {
                    return m_dynamicAngularVelocity;
                }
                if (m_entityManager.HasComponent<PhysicsVelocity>(m_ent))
                {
                    return m_entityManager.GetComponentData<PhysicsVelocity>(m_ent).Linear.z *
                        ksMath.FRADIANS_TO_DEGREES;
                }
                return 0f;
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
                        velocityData.Angular = new float3(0f, 0f, value * ksMath.FDEGREES_TO_RADIANS);
                        m_entityManager.SetComponentData(m_ent, velocityData);
                    }
                    else if (value != 0f)
                    {
                        m_entityManager.AddComponentData(m_ent, new PhysicsVelocity()
                        {
                            Angular = new float3(0f, 0f, value * ksMath.FDEGREES_TO_RADIANS)
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Amount of movement that will be applied to a kinematic rigid body entity during the next physics simulation
        /// step. After the simulation step this value will be reset to <see cref="ksVector2.Zero"/>.
        /// </summary>
        public ksVector2 KinematicMovement
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
        /// Amount of rotation in degrees that will be applied to a kinematic rigid body entity during the next physics
        /// simulation step. After the simulation step this value will be reset to zero.
        /// </summary>
        public float KinematicRotation
        {
            get { return KinematicRotationRadians * ksMath.FRADIANS_TO_DEGREES; }
            set { KinematicRotationRadians = value * ksMath.FDEGREES_TO_RADIANS; }
        }

        /// <summary>
        /// Amount of rotation in radians that will be applied to a kinematic rigid body entity during the next physics
        /// simulation step. After the simulation step this value will be reset to zero.
        /// </summary>
        public float KinematicRotationRadians
        {
            get { return m_kinematicRotation; }
            set
            {
                if (!IsKinematic)
                {
                    ksLog.Warning(this, "Cannot set KinematicRotation on a non-kinematic rigid body.");
                }
                else
                {
                    m_kinematicRotation = value;
                }
            }
        }

        /// <summary>Clear the kinematic movement and rotation values.</summary>
        public void ClearKinematicMotion()
        {
            m_kinematicMovement = ksVector2.Zero;
            m_kinematicRotation = 0f;
        }

        /// <summary>
        /// Controls which degrees of freedom are allowed for the simulation of this rigidbody. Has no effect
        /// when Unity Physics simulates the rigid body.
        /// </summary>
        public ksRigidBody2DConstraints Constraints
        {
            get
            {
                return m_entityManager.HasComponent<ksRigidBodyConstraintData>(m_ent) ?
                    To2DConstraints(m_entityManager.GetComponentData<ksRigidBodyConstraintData>(m_ent).Constraints) :
                    ksRigidBody2DConstraints.NONE;
            }

            set
            {
                if (m_entityManager.HasComponent<ksRigidBodyConstraintData>(m_ent))
                {
                    m_entityManager.SetComponentData(m_ent, new ksRigidBodyConstraintData()
                    {
                        Constraints = To3DConstraints(value)
                    });
                }
                else if (value != ksRigidBody2DConstraints.NONE)
                {
                    m_entityManager.AddComponentData(m_ent, new ksRigidBodyConstraintData()
                    {
                        Constraints = To3DConstraints(value)
                    });
                }
            }
        }

        /// <summary>The rigidbody's center of mass in local space.</summary>
        public new ksVector2 CenterOfMass
        {
            get { return base.CenterOfMass.XY; }
            set { base.CenterOfMass = new ksVector3(value.X, value.Y, base.CenterOfMass.Z); }
        }

        /// <summary>Converts 3D rigid body constraints to 2D constraints.</summary>
        /// <param name="constraints">3D constraints</param>
        /// <returns>2D constraints</returns>
        private ksRigidBody2DConstraints To2DConstraints(ksRigidBodyConstraints constraints)
        {
            return (ksRigidBody2DConstraints)(constraints & ~ksRigidBodyConstraints.FREEZE_POSITION_Z &
                    ~ksRigidBodyConstraints.FREEZE_ROTATION_X & ~ksRigidBodyConstraints.FREEZE_ROTATION_Y);
        }

        /// <summary>Converts 2D rigid body constraints to 3D constraints.</summary>
        /// <param name="constraints">2D constraints</param>
        /// <returns>3D constraints</returns>
        private ksRigidBodyConstraints To3DConstraints(ksRigidBody2DConstraints constraints)
        {
            return (ksRigidBodyConstraints)constraints | ksRigidBodyConstraints.FREEZE_POSITION_Z |
                    ksRigidBodyConstraints.FREEZE_ROTATION_X | ksRigidBodyConstraints.FREEZE_ROTATION_Y;
        }
    }
}
#endif