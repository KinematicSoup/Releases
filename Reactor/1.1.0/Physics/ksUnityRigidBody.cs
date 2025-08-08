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
    /// <summary>Wraps a Unity <see cref="Rigidbody"/> for use with Reactor.</summary>
    public class ksUnityRigidBody : ksBaseUnityRigidBody, ksIRigidBody
    {
        // All unity rigid bodies used in reactor are kinematic (to prevent client simulation from moving the entity)
        // However Unity and PhysX clear the velocities on kinematic rigid bodies, so we need to track the non-kinematic
        // velocities off of the unity rigid body.
        private ksVector3 m_dynamicVelocity = ksVector3.Zero;
        private ksVector3 m_dynamicAngularVelocity = ksVector3.Zero;
        private ksVector3 m_kinematicMovement = ksVector3.Zero;
        private ksQuaternion m_kinematicRotation = ksQuaternion.Identity;

        /// <summary>Constructor</summary>
        /// <param name="rigidBody">Unity rigid body.</param>
        /// <param name="forceKinematic">
        /// If true, sets the Unity rigidbody to kinematic so Unity physics doesn't try to move the object.
        /// </param>
        public ksUnityRigidBody(Rigidbody rigidBody, bool forceKinematic) : base(rigidBody, forceKinematic)
        {

        }

        /// <summary>Linear velocity</summary>
        public ksVector3 Velocity
        {
            get {
                return IsKinematic == m_rigidBody.isKinematic
#if UNITY_6000_0_OR_NEWER
                    ? (ksVector3)m_rigidBody.linearVelocity
#else
                    ? (ksVector3)m_rigidBody.velocity
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
                    m_rigidBody.linearVelocity = value;
#else
                    m_rigidBody.velocity = value;
#endif
                }
            }
        }

        /// <summary>Angular velocity. Component values are degrees per second.</summary>
        public ksVector3 AngularVelocity
        {
            get {
                return IsKinematic == m_rigidBody.isKinematic
                    ? (ksVector3)m_rigidBody.angularVelocity 
                    : m_dynamicAngularVelocity;
            }
            set 
            {
                if (IsKinematic)
                {
                    ksLog.Warning(this, "Setting angular velocity on a kinematic rigid body is not supported.");
                    return;
                }
                m_dynamicAngularVelocity = value;
                if (!m_rigidBody.isKinematic)
                {
                    m_rigidBody.angularVelocity = value;
                }
            }
        }

        /// <summary>
        /// Amount of movement that will be applied to a kinematic rigid body entity 
        /// during the next physics simulation step. After the simulation step this 
        /// value will be reset to <see cref="ksVector3.Zero"/>.
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
        /// Amount of rotation that will be applied to a kinematic rigid body entity 
        /// during the next physics simulation step. After the simulation step this 
        /// value will be reset to <see cref="ksQuaternion.Identity"/>.
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

        /// <summary>Clear the Kinematic movement and rotation values.</summary>
        public void ClearKinematicMotion()
        {
            m_kinematicMovement = ksVector3.Zero;
            m_kinematicRotation = ksQuaternion.Identity;
        }

        /// <summary>Controls which degrees of freedom are allowed for the simulation of this rigidbody.</summary>
        public ksRigidBodyConstraints Constraints
        {
            get { return (ksRigidBodyConstraints)m_rigidBody.constraints; }
            set { m_rigidBody.constraints = (RigidbodyConstraints)value; }
        }
    }
}
