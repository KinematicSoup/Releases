/*
KINEMATICSOUP CONFIDENTIAL
 Copyright(c) 2014-2025 KinematicSoup Technologies Incorporated 
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KS.Reactor.Client.Unity
{
    /// <summary>Wraps a Unity <see cref="Rigidbody"/> for use with Reactor using a 2D interface.</summary>
    public class ksUnityRigidBody2DView : ksBaseUnityRigidBody, ksIRigidBody2D
    {
        // All unity rigid bodies used in reactor are kinematic (to prevent client simulation from moving the entity)
        // However Unity and PhysX clear the velocities on kinematic rigid bodies, so we need to track the non-kinematic
        // velocities off of the unity rigid body.
        private ksVector2 m_dynamicVelocity = ksVector2.Zero;
        private float m_dynamicAngularVelocity = 0.0f;
        private ksVector2 m_kinematicMovement = ksVector2.Zero;
        private float m_kinematicRotation = 0f;

        /// <summary>Constructor</summary>
        /// <param name="rigidBody">Unity rigid body.</param>
        /// <param name="forceKinematic">
        /// If true, sets the Unity rigidbody to kinematic so Unity physics doesn't try to move the object.
        /// </param>
        public ksUnityRigidBody2DView(Rigidbody rigidBody, bool forceKinematic) : base(rigidBody, forceKinematic)
        {

        }

        /// <summary>Linear velocity</summary>
        public ksVector2 Velocity
        {
            get {
                return IsKinematic == m_rigidBody.isKinematic
#if UNITY_6000_0_OR_NEWER
                    ? ((ksVector3)m_rigidBody.linearVelocity).XY
#else
                    ? ((ksVector3)m_rigidBody.velocity).XY
#endif
                    : m_dynamicVelocity;
            }
            set
            {
                if (IsKinematic)
                {
                    ksLog.Warning(this, "Setting velocity on a kinematic rigid body is not supported.");
                    return;
                }
                m_dynamicVelocity = value;
                if (!m_rigidBody.isKinematic)
                {
#if UNITY_6000_0_OR_NEWER
                    m_rigidBody.linearVelocity = new Vector3(value.X, value.Y, 0f);
#else
                    m_rigidBody.velocity = new Vector3(value.X, value.Y, 0f);
#endif
                }
            }
        }

        /// <summary>Angular velocity. Component values are degrees per second.</summary>
        public float AngularVelocity
        {
            get {
                return IsKinematic == m_rigidBody.isKinematic
                  ? m_rigidBody.angularVelocity.z * ksMath.FRADIANS_TO_DEGREES
                  : m_dynamicAngularVelocity;
            }
            set 
            {
                if (IsKinematic)
                {
                    ksLog.Warning(this, "Setting velocity on a kinematic rigid body is not supported.");
                    return;
                }
                m_dynamicAngularVelocity = value;
                if (!m_rigidBody.isKinematic)
                {
                    m_rigidBody.angularVelocity = new Vector3(0f, 0f, value * ksMath.FDEGREES_TO_RADIANS);
                }
            }
        }

        /// <summary>
        /// Amount of movement that will be applied to a kinematic rigid body entity 
        /// during the next physics simulation step. After the simulation step this 
        /// value will be reset to <see cref="ksVector2.Zero"/>.
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
        /// Amount of rotation in degrees that will be applied to a kinematic rigid body entity 
        /// during the next physics simulation step. After the simulation step this 
        /// value will be reset to zero.
        /// </summary>
        public float KinematicRotation
        {
            get { return KinematicRotationRadians * ksMath.FRADIANS_TO_DEGREES; }
            set { KinematicRotationRadians = value * ksMath.FDEGREES_TO_RADIANS; }
        }

        /// <summary>
        /// Amount of rotation in radians that will be applied to a kinematic rigid body entity 
        /// during the next physics simulation step. After the simulation step this 
        /// value will be reset to zero.
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

        /// <summary>Clear the Kinematic movement and rotation values.</summary>
        public void ClearKinematicMotion()
        {
            m_kinematicMovement = ksVector2.Zero;
            m_kinematicRotation = 0f;
        }

        /// <summary>Controls which degrees of freedom are allowed for the simulation of this rigidbody.</summary>
        public ksRigidBody2DConstraints Constraints
        {
            get
            {
                return (ksRigidBody2DConstraints)(m_rigidBody.constraints & ~RigidbodyConstraints.FreezePositionZ &
                    ~RigidbodyConstraints.FreezeRotationX & ~RigidbodyConstraints.FreezeRotationY);
            }
            set
            {
                m_rigidBody.constraints = (RigidbodyConstraints)value | RigidbodyConstraints.FreezePositionZ |
                    RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
            }
        }

        /// <summary>The rigidbody's center of mass in local space.</summary>
        public new ksVector2 CenterOfMass
        {
            get { return base.CenterOfMass.XY; }
            set { base.CenterOfMass = new ksVector3(value.X, value.Y, base.CenterOfMass.Z); }
        }
    }
}
