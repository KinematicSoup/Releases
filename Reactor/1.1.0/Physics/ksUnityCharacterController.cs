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

namespace KS.Reactor.Client.Unity
{
    /// <summary>
    /// Class that wraps a Unity character controller and implements the ksICharacterController interface.
    /// </summary>
    public class ksUnityCharacterController : ksICharacterController
    {
        private ksEntity m_entity;
        private event ksCharacterControllerEvents.ColliderHitHandler m_onColliderHit;

        /// <summary>Unity character controller.</summary>
        public CharacterController UnityCharacterController
        {
            get { return m_characterController; }
        }
        private CharacterController m_characterController;

        /// <summary>Slope limit in degrees.</summary>
        public float SlopeLimit
        {
            get { return m_characterController.slopeLimit; }
            set { m_characterController.slopeLimit = value; }
        }

        /// <summary>Step offset in meters.</summary>
        public float StepOffset
        {
            get { return m_characterController.stepOffset; }
            set { m_characterController.stepOffset = value; }
        }

        /// <summary>Collision skin width.</summary>
        public float SkinWidth
        {
            get { return m_characterController.skinWidth; }
            set { m_characterController.skinWidth = value; }
        }

        /// <summary>Gets or sets the minimum move distance of the character controller.</summary>
        public float MinMoveDistance
        {
            get { return m_characterController.minMoveDistance; }
            set { m_characterController.minMoveDistance = value; }
        }

        /// <summary>The offset of the character's capsule relative to the transform's position.</summary>
        public ksVector3 Offset
        {
            get { return m_characterController.center; }
            set { m_characterController.center = value; }
        }

        /// <summary>The radius of the character's capsule.</summary>
        public float Radius
        {
            get { return m_characterController.radius; }
            set { m_characterController.radius = value; }
        }

        /// <summary>The height of the character's capsule.</summary>
        public float Height
        {
            get { return m_characterController.height; }
            set { m_characterController.height = value; }
        }

        /// <summary>
        /// Enables or disables overlap recovery. Used to depenetrate character controllers from static objects when an
        /// overlap is detected.
        /// </summary>
        public bool EnableOverlapRecovery
        {
            get { return m_characterController.enableOverlapRecovery; }
            set { m_characterController.enableOverlapRecovery = value; }
        }

        /// <summary>
        /// Determines whether other rigidbodies or character controllers collide with this character controller.
        /// </summary>
        public bool DetectCollisions
        {
            get { return m_characterController.detectCollisions; }
            set { m_characterController.detectCollisions = value; }
        }

        /// <summary>
        /// What part of the capsule collided with the environment during the last CharacterController.Move call.
        /// </summary>
        public ksCharacterControllerCollisionFlags CollisionFlags
        {
            get { return (ksCharacterControllerCollisionFlags)m_characterController.collisionFlags; }
        }

        /// <summary>Was the CharacterController touching the ground during the last move?</summary>
        public bool IsGrounded
        {
            get { return m_characterController.isGrounded; }
        }

        /// <summary>The current relative velocity of the character.</summary>
        public ksVector3 Velocity
        {
            get { return m_velocity; }
        }
        private ksVector3 m_velocity;

        /// <summary>Invoked when this character controller hit a collider.</summary>
        public event ksCharacterControllerEvents.ColliderHitHandler OnColliderHit
        {
            add
            {
                m_onColliderHit += value;
            }
            remove
            {
                m_onColliderHit -= value;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="entity">Entity the character controller attached to</param>
        /// <param name="characterController">Unity character controller component</param>
        public ksUnityCharacterController (ksEntity entity, CharacterController characterController)
        {
            m_entity = entity;
            m_characterController = characterController;
        }

        /// <summary>
        /// Moves the character with velocity. Gravity is automatically applied. Returns if the character is grounded.
        /// </summary>
        /// <param name="velocity"></param>
        /// <returns>True if the character is grounded.</returns>
        public bool SimpleMove(ksVector3 velocity)
        {
            if (m_entity == null || m_entity.PlayerController == null || m_entity.PlayerController.Transform == null ||
                Time.deltaTime == 0f)
            {
                return m_characterController.isGrounded;
            }

            // Because we may be using a different time delta than Unity and Unity doesn't let us change the time scale
            // until the following frame, we use apply velocity using our time delta and use Move to emulate
            // SimpleMove.
            m_velocity.X = velocity.X;
            m_velocity.Z = velocity.Z;
            ksVector3 gravity = m_entity.Room.Physics.Gravity;
            float deltaTime = m_entity.Room.Time.Delta;
            ksVector3 delta = m_velocity * deltaTime + 0.5f * gravity * deltaTime * deltaTime;
            m_velocity += gravity * deltaTime;

            // On the client side, the actual game object is moved in the SyncObject.UpdateClientTransform but not the
            // player controller. What gets updated in the player controller is its ksTransform, which is used by the
            // predictors to decide what the client state should be. Therefore, when the character controller's Move
            // method is called, we move the character controller, copy the position to the player controller's
            // transform and then reset the character controller's position.
            ksTransform transform = m_entity.PlayerController.Transform;
            Vector3 originalPosition = m_characterController.transform.position;// Store character controller position
            Teleport(transform.Position);// Teleport character controller to the player controller position
            // Move the unity character controller
            CollisionFlags flags = m_characterController.Move(delta);

            transform.Position = m_characterController.transform.position;// Move the player controller
            Teleport(originalPosition);// Reset the Unity character controller position
            // Zero the y-velocity if on ground.
            if ((flags & UnityEngine.CollisionFlags.Below) != 0)
            {
                m_velocity.Y = 0f;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Moves the character with a displacement vector. Users are responsible for applying gravity to characters.
        /// </summary>
        /// <param name="motion"></param>
        /// <returns></returns>
        public ksCharacterControllerCollisionFlags Move(ksVector3 motion)
        {
            if (m_entity == null || m_entity.PlayerController == null || m_entity.PlayerController.Transform == null)
            {
                return ksCharacterControllerCollisionFlags.NONE;
            }

            // On the client side, the actual game object is moved in the SyncObject.UpdateClientTransform but not the
            // player controller. What gets updated in the player controller is its ksTransform, which is used by the
            // predictors to decide what the client state should be. Therefore, when the character controller's Move
            // method is called, we move the character controller, copy the position to the player controller's
            // transform and then reset the character controller's position.
            ksTransform transform = m_entity.PlayerController.Transform;
            Vector3 originalPosition = m_characterController.transform.position;// Store character controller position
            Teleport(transform.Position);// Teleport character controller to the player controller position
            // Move the unity character controller
            ksCharacterControllerCollisionFlags flags =
                (ksCharacterControllerCollisionFlags)m_characterController.Move(motion);

            // We have to calculate velocity since we may use a different time delta from Unity.
            float dt = m_entity.Room.Time.Delta;
            if (dt > 0f)
            {
                m_velocity = (m_characterController.transform.position - transform.Position) / dt;
            }

            transform.Position = m_characterController.transform.position;// Move the player controller
            Teleport(originalPosition);// Reset the Unity character controller position
            return flags;
        }

        /// <summary>
        /// Teleports to the given position.
        /// </summary>
        /// <param name="position">Position to teleport to.</param>
        public void Teleport(Vector3 position)
        {
            // To properly teleport a game object, we have to diable the character controller on it.
            m_characterController.enabled = false;
            m_characterController.transform.position = position;
            m_characterController.enabled = true;
        }

        /// <summary>Process collision events with character controller.</summary>
        internal void InvokeColliderHitHandler(ControllerColliderHit hit)
        {
            if (m_onColliderHit != null)
            {
                ksEntityComponent entityComponent = null;
                ksIUnityCollider hitCollider = null;
                ksPhysicsAdaptor.TryGetKSCollider(hit.collider, out hitCollider, out entityComponent);
                ksCharacterControllerColliderHit colliderHit = new ksCharacterControllerColliderHit(
                        hitCollider,
                        hit.point,
                        hit.normal,
                        hit.moveDirection,
                        hit.moveLength);
                try
                {
                    m_onColliderHit(colliderHit);
                }
                catch (Exception ex)
                {
                    ksExceptionHandler.Handle(
                        "Exception caught while calling character controller OnColliderHit handlers.", ex);
                }
            }
        }
    }
}