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
using UnityEngine;
using Unity.Entities;
using Unity.Physics;
using Unity.Mathematics;

namespace KS.Reactor.Client.Unity.DOTS
{
    /// <summary>
    /// Base class for classes that provide access to Unity Physics rigid bodies in the format expected by Reactor.
    /// Derived classes implement either <see cref="ksIRigidBody"/> or <see cref="ksIRigidBody2D"/>.
    /// </summary>
    public class ksBaseDOTSRigidBody
    {
        /// <summary>
        /// Determines if rigid bodies are 3D or 2D by default when spawning prefabs or loading the scene.
        /// </summary>
        public static ksRigidBodyModes DefaultType
        {
            get { return m_defaultType; }
            set { m_defaultType = value; }
        }
        private static ksRigidBodyModes m_defaultType = ksRigidBodyModes.RIGID_BODY_3D;

        /// <summary>
        /// If true, <see cref="PhysicsMass.InverseMass"/> and <see cref="PhysicsMass.InverseInertia"/> will be set to
        /// zero regardless of the value of <see cref="IsKinematic"/> to prevent Unity Physics from affecting the rigid
        /// body. Setting to false will set <see cref="PhysicsMass.InverseMass"/> back to the inverse of <see cref="Mass"/>
        /// (or zero if mass is zero) and <see cref="PhysicsMass.InverseInertia"/> back to the original value if
        /// <see cref="IsKinematic"/> is also false.
        /// </summary>
        public bool ForceKinematic
        {
            get { return m_forceKinematic; }
            set
            {
                if (m_forceKinematic != value)
                {
                    m_forceKinematic = value;
                    if (!m_isKinematic)
                    {
                        if (value)
                        {
                            MakeKinematic();
                        }
                        else
                        {
                            MakeDynamic();
                        }
                    }
                }
            }
        }

        protected Entity m_ent;
        protected EntityManager m_entityManager;
        private bool m_forceKinematic;
        private bool m_isKinematic;
        // Unity Physics rigid bodies are made kinematic by setting inverse mass and inertia tensor to zero. We store
        // the original values so we can restore them if we need to make the rigid body dynamic again.
        private float m_invMass;
        private float3 m_invInertia;

        /// <summary>Constructor</summary>
        /// <param name="ent">Entity with physics components</param>
        /// <param name="entityManager">Entity manager</param>
        /// <param name="forceKinematic">
        /// If true, sets <see cref="PhysicsMass.InverseMass"/> and <see cref="PhysicsMass.InverseInertia"/> to zero so
        /// Unity Physics doesn't try to move the object.
        /// </param>
        public ksBaseDOTSRigidBody(Entity ent, EntityManager entityManager, bool forceKinematic)
        {
            m_ent = ent;
            m_entityManager = entityManager;
            m_forceKinematic = forceKinematic;
            if (forceKinematic)
            {
                if (m_entityManager.HasComponent<PhysicsMass>(ent))
                {
                    PhysicsMass massData = m_entityManager.GetComponentData<PhysicsMass>(ent);
                    m_isKinematic = massData.IsKinematic;
                    m_invMass = massData.InverseMass;
                    m_invInertia = massData.InverseInertia;
                    massData.InverseMass = 0f;
                    massData.InverseInertia = float3.zero;
                    m_entityManager.SetComponentData(m_ent, massData);
                }
                else
                {
                    m_isKinematic = true;
                }
            }
        }

        /// <summary>
        /// Mass of the rigid body. If <see cref="ForceKinematic"/> and <see cref="IsKinematic"/> are false, setting
        /// this will set <see cref="PhysicsMass.InverseMass"/> to the inverse, or zero if mass is zero.
        /// </summary>
        public float Mass
        {
            get
            {
                if (!m_forceKinematic && !m_isKinematic)
                {
                    if (!m_entityManager.HasComponent<PhysicsMass>(m_ent))
                    {
                        return 0f;
                    }
                    m_invMass = m_entityManager.GetComponentData<PhysicsMass>(m_ent).InverseMass;
                }
                return m_invMass == 0f ? 0f : (1f / m_invMass);
            }

            set
            {
                m_invMass = value == 0f ? 0f : (1f / value);
                if (m_forceKinematic)
                {
                    m_isKinematic = m_invMass == 0f && m_invInertia.Equals(float3.zero);
                }
                else if (!m_isKinematic)
                {
                    if (m_entityManager.HasComponent<PhysicsMass>(m_ent))
                    {
                        PhysicsMass massData = m_entityManager.GetComponentData<PhysicsMass>(m_ent);
                        massData.InverseMass = m_invMass;
                        m_entityManager.SetComponentData(m_ent, massData);
                    }
                    else if (value != 0f)
                    {
                        m_entityManager.SetComponentData(m_ent, new PhysicsMass()
                        {
                            InverseMass = m_invMass,
                            InertiaOrientation = quaternion.identity
                        });
                    }
                }
            }
        }

        /// <summary>Linear damping</summary>
        public float Drag
        {
            get
            {
                return m_entityManager.HasComponent<PhysicsDamping>(m_ent) ?
                    m_entityManager.GetComponentData<PhysicsDamping>(m_ent).Linear : 0f;
            }

            set
            {
                PhysicsDamping dampingData;
                if (!m_entityManager.HasComponent<PhysicsDamping>(m_ent))
                {
                    if (value == 0f)
                    {
                        return;
                    }
                    dampingData = new PhysicsDamping();
                    dampingData.Linear = value;
                    m_entityManager.AddComponentData(m_ent, dampingData);
                }
                else
                {
                    dampingData = m_entityManager.GetComponentData<PhysicsDamping>(m_ent);
                    dampingData.Linear = value;
                    m_entityManager.SetComponentData(m_ent, dampingData);
                }
            }
        }

        /// <summary>Angular damping</summary>
        public float AngularDrag
        {
            get
            {
                return m_entityManager.HasComponent<PhysicsDamping>(m_ent) ?
                    m_entityManager.GetComponentData<PhysicsDamping>(m_ent).Angular : 0f;
            }

            set
            {
                PhysicsDamping dampingData;
                if (!m_entityManager.HasComponent<PhysicsDamping>(m_ent))
                {
                    if (value == 0f)
                    {
                        return;
                    }
                    dampingData = new PhysicsDamping();
                    dampingData.Angular = value;
                    m_entityManager.AddComponentData(m_ent, dampingData);
                }
                else
                {
                    dampingData = m_entityManager.GetComponentData<PhysicsDamping>(m_ent);
                    dampingData.Angular = value;
                    m_entityManager.SetComponentData(m_ent, dampingData);
                }
            }
        }

        /// <summary>
        /// If true, the entity will be affected by the scene gravity. Setting to false will set
        /// <see cref="PhysicsGravityFactor.Value"/> to zero, and setting to true will set it to one.
        /// </summary>
        public bool UseGravity
        {
            get
            {
                return !m_entityManager.HasComponent<PhysicsGravityFactor>(m_ent) ||
                    m_entityManager.GetComponentData<PhysicsGravityFactor>(m_ent).Value != 0f;
            }

            set
            {
                if (!m_entityManager.HasComponent<PhysicsGravityFactor>(m_ent))
                {
                    if (value)
                    {
                        return;
                    }
                    m_entityManager.AddComponentData(m_ent, new PhysicsGravityFactor() { Value = 0f });
                }
                else
                {
                    m_entityManager.SetComponentData(m_ent, new PhysicsGravityFactor() { Value = value ? 1f : 0f });
                }
            }
        }

        /// <summary>
        /// If true, the entity will not be effected by gravity or other impulses, but may be moved around by setting
        /// translation and rotation from scripts.  If set to true, <see cref="PhysicsMass.InverseMass"/> and 
        /// <see cref="PhysicsMass.InverseInertia"/> will be set to zero to prevent Unity Physics from affecting the
        /// rigid body. Setting to false will set <see cref="PhysicsMass.InverseMass"/> back to the inverse of <see cref="Mass"/>
        /// (or zero if mass is zero) and <see cref="PhysicsMass.InverseInertia"/> back to the original value if
        /// <see cref="ForceKinematic"/> is also false.
        /// </summary>
        public bool IsKinematic
        {
            get
            {
                if (m_forceKinematic)
                {
                    return m_isKinematic;
                }
                return !m_entityManager.HasComponent<PhysicsMass>(m_ent) ||
                    m_entityManager.GetComponentData<PhysicsMass>(m_ent).IsKinematic;
            }

            set
            {
                if (m_isKinematic != value)
                {
                    if (!value && m_invMass == 0 && m_invInertia.Equals(float3.zero))
                    {
                        ksLog.Warning(this, "Cannot set IsKinematic to false when Mass and InertiaTensor are zero.");
                        return;
                    }
                    m_isKinematic = value;
                    if (!m_forceKinematic)
                    {
                        if (value)
                        {
                            MakeKinematic();
                        }
                        else
                        {
                            MakeDynamic();
                        }
                    }
                }
            }
        }

        /// <summary>The rigidbody's center of mass in local space.</summary>
        public ksVector3 CenterOfMass
        {
            get
            {
                return m_entityManager.HasComponent<PhysicsMass>(m_ent) ?
                    (ksVector3)(Vector3)m_entityManager.GetComponentData<PhysicsMass>(m_ent).CenterOfMass :
                    ksVector3.Zero;
            }

            set
            {
                if (m_entityManager.HasComponent<PhysicsMass>(m_ent))
                {
                    PhysicsMass massData = m_entityManager.GetComponentData<PhysicsMass>(m_ent);
                    massData.CenterOfMass = (Vector3)value;
                    m_entityManager.SetComponentData(m_ent, massData);
                }
                else if (value != ksVector3.Zero)
                {
                    m_entityManager.AddComponentData(m_ent, new PhysicsMass()
                    {
                        CenterOfMass = (Vector3)value,
                        InertiaOrientation = quaternion.identity
                    });
                }
            }
        }

        /// <summary>
        /// Makes the rigid body dynamic by restoring <see cref="PhysicsMass.InverseMass"/> and
        /// <see cref="PhysicsMass.InverseInertia"/> to their original values.
        /// </summary>
        private void MakeDynamic()
        {
            if (m_entityManager.HasComponent<PhysicsMass>(m_ent))
            {
                PhysicsMass massData = m_entityManager.GetComponentData<PhysicsMass>(m_ent);
                massData.InverseMass = m_invMass;
                massData.InverseInertia = m_invInertia;
                m_entityManager.AddComponentData(m_ent, massData);
            }
            else if (m_invMass != 0 || !m_invInertia.Equals(float3.zero))
            {
                m_entityManager.SetComponentData(m_ent, new PhysicsMass()
                {
                    InverseMass = m_invMass,
                    InverseInertia = m_invInertia,
                    InertiaOrientation = quaternion.identity
                });
            }
        }

        /// <summary>
        /// Makes the rigid body kinematic by setting <see cref="PhysicsMass.InverseMass"/> and
        /// <see cref="PhysicsMass.InverseInertia"/> to zero and storing the original values so they can bre restored
        /// when <see cref="MakeDynamic"/> is called.
        /// </summary>
        private void MakeKinematic()
        {
            if (m_entityManager.HasComponent<PhysicsMass>(m_ent))
            {
                PhysicsMass massData = m_entityManager.GetComponentData<PhysicsMass>(m_ent);
                m_invMass = massData.InverseMass;
                m_invInertia = massData.InverseInertia;
                massData.InverseMass = 0f;
                massData.InverseInertia = float3.zero;
                m_entityManager.SetComponentData(m_ent, massData);
            }
            else
            {
                m_invMass = 0f;
                m_invInertia = float3.zero;
            }
        }
    }
}
#endif