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
using UnityEngine;
using UnityEngine.Serialization;

namespace KS.Reactor.Client.Unity
{
    /// <summary>
    /// PhysX settings common to static, kinematic, and dynamic entities.
    /// These values may be set on a scene or entity. A value of -1f, or 0 for iteration values,
    /// means that the value should be inherited. If set on an <see cref="ksEntityComponent"/> component
    /// then the value will be inherited from the room <see cref="ksPhysicsSettings"/> component.  If set 
    /// on a <see cref="ksPhysicsSettings"/> component, then the default values will be used.
    /// </summary>
    [Serializable]
    public class ksEntityPhysicsSettings
    {
        /// <summary>
        /// Rigid body modes shown in the inspector. This is the same as <see cref="ksRigidBodyModes"/>, but without
        /// the <see cref="ksRigidBodyModes.DEFAULT"/> option so it cannot be selected in the inspector. We also use
        /// the InsectorName attribute to change the names to start with a digit, which we cannot do where the other
        /// enum is defined in Common.
        /// </summary>
        private enum RigidBodyModes
        {
            [InspectorName("3D")]
            RIGID_BODY_3D = ksRigidBodyModes.RIGID_BODY_3D,
            [InspectorName("2D")]
            RIGID_BODY_2D = ksRigidBodyModes.RIGID_BODY_2D
        }

        /// <summary>Contact offset value of all colliders on this entity.</summary>
        [Tooltip("Contact offset value of all colliders on this entity. (Unchecked = Use default room settings)")]
        [Min(-1.0f)]
        public float ContactOffset = 0.01f;

        /// <summary>The mass-normalized energy threshold, below which objects start going to sleep.</summary>
        [Tooltip("The mass-normalized energy threshold, below which objects start going to sleep. (Unchecked = Use default room settings)")]
        [Min(-1.0f)]
        public float SleepThreshold = 0.005f;

        /// <summary>The maximimum linear speed of the entity.</summary>
        [Tooltip("The maximimum linear speed of the entity. (Unchecked = Use default room settings)")]
        [Min(-1.0f)]
        public float MaxLinearSpeed = 1000f;

        /// <summary>The maximimum angular speed of the entity measured in degrees per second.</summary>
        [Tooltip("The maximimum angular speed of the entity measured in degrees per second. (Unchecked = Use default room settings)")]
        [Min(-1.0f)]
        public float MaxAngularSpeed = 50000f;

        /// <summary>Maximum speed of a rigidbody when moving out of penetrating state.</summary>
        [Tooltip("Maximum speed of a rigidbody when moving out of penetrating state. (Unchecked = Use default room settings)")]
        [Min(-1.0f)]
        public float MaxDepenetrationSpeed = 1000f;

        /// <summary>Determines how accurately Rigidbody joints and collision contacts are resolved.</summary>
        [Tooltip("The PositionIterations determines how accurately Rigidbody joints and collision contacts are resolved. (Unchecked = Use default room settings)")]
        [Range(1, 10)]
        public int PositionIterations = 6;

        /// <summary>Affects how how accurately entity collision contacts are resolved.</summary>
        [Tooltip("The VelocityIterations affects how how accurately entity collision contacts are resolved. (Unchecked = Use default room settings)")]
        [Range(0, 10)]
        public int VelocityIterations = 1;

        /// <summary>Is the rigid body 3D or 2D?</summary>
        public ksRigidBodyModes RigidBodyMode
        {
            get { return (ksRigidBodyModes)m_rigidBodyMode; }
            set { m_rigidBodyMode = (RigidBodyModes)value; }
        }

        /// <summary>Is the rigid body 3D or 2D?</summary>
        [Tooltip("Is the rigid body 3D or 2D? (Unchecked = Use default room settings)")]
        [SerializeField]
        [FormerlySerializedAs("m_rigidBodyType")]
        [ksDisplayName("2D/3D Mode")]
        private RigidBodyModes m_rigidBodyMode = RigidBodyModes.RIGID_BODY_3D;

        /// <summary>Constructor</summary>
        /// <param name="sleepThreshold">
        /// The mass-normalized energy threshold, below which objects start going to sleep.
        /// </param>
        /// <param name="contactOffset">Contact offset value of all colliders on this entity.</param>
        /// <param name="maxLinearSpeed">The maximimum linear speed of the entity.</param>
        /// <param name="maxAngularSpeed">
        /// The maximimum angular speed of the entity measured in degrees per second.
        /// </param>
        /// <param name="maxDepenetrationSpeed">
        /// Maximum speed of a rigidbody when moving out of penetrating state.
        /// </param>
        /// <param name="positionIterations">
        /// Determines how accurately Rigidbody joints and collision contacts are resolved.
        /// </param>
        /// <param name="velocityIterations">
        /// Determines how how accurately entity collision contacts are resolved.
        /// </param>
        /// <param name="type">Rigid body type.</param>
        public ksEntityPhysicsSettings(
            float sleepThreshold = 0.005f,
            float contactOffset = 0.01f,
            float maxLinearSpeed = 1000f,
            float maxAngularSpeed = 50000f,
            float maxDepenetrationSpeed = 1000f,
            int positionIterations = 6,
            int velocityIterations = 1,
            ksRigidBodyModes type = ksRigidBodyModes.RIGID_BODY_3D)
        {
            SleepThreshold = sleepThreshold;
            ContactOffset = contactOffset;
            MaxLinearSpeed = maxLinearSpeed;
            MaxAngularSpeed = maxAngularSpeed;
            MaxDepenetrationSpeed = maxDepenetrationSpeed;
            PositionIterations = positionIterations;
            VelocityIterations = velocityIterations;
            RigidBodyMode = type;
        }

        /// <summary>
        /// Check if the values are valid. If a value is a negative float, then snap it to -1f. 
        /// During publishing a values of -1f is used to indicate that a parent or default value should be used.
        /// </summary>
        public void Validate()
        {
            if (SleepThreshold < 0f) SleepThreshold = -1f;
            if (ContactOffset < 0f) ContactOffset = -1f;
            if (MaxLinearSpeed < 0f) MaxLinearSpeed = -1f;
            if (MaxAngularSpeed < 0f) MaxAngularSpeed = -1f;
            if (MaxDepenetrationSpeed < 0f) MaxDepenetrationSpeed = -1f;
            if (PositionIterations <= 0) PositionIterations = -1;
            if (VelocityIterations < 0) VelocityIterations = -1;
        }
    }
}
