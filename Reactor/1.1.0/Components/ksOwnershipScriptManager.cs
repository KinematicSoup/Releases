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
using System.Collections;
using System.Reflection;
using UnityEngine;
using KS.Unity;

namespace KS.Reactor.Client.Unity
{
    /// <summary>
    /// Controls what happens to components and/or child objects when the local player owns or doesn't own the entity.
    /// Attach this script to an object with a <see cref="ksEntityComponent"/> or one of its descendants.
    /// </summary>
    [ksHideInOwnershipManager]
    [DisallowMultipleComponent]
    [AddComponentMenu(ksMenuNames.REACTOR + nameof(ksOwnershipScriptManager))]
    public class ksOwnershipScriptManager : MonoBehaviour
    {
        /// <summary>Object rules to apply based on entity ownership.</summary>
        public enum Rules
        {
            /// <summary>Do nothing to the object.</summary>
            LEAVE_UNCHANGED = 0,
            /// <summary>
            /// Disable the object or script for entities the local player does not own. It will be re-enabled if the
            /// local player gains ownership of the entity. If the component is a rigid body, it will be set to
            /// kinematic instead of disabled.
            /// </summary>
            DISABLE_WHEN_UNOWNED = 1,
            /// <summary>
            /// Disable the object or script for entities the local player does owns. It will be re-enabled if the local
            /// player loses ownership of the entity. If the component is a rigid body, it will be set to kinematic
            /// instead of disabled.
            /// </summary>
            DISABLE_WHEN_OWNED = 2,
            /// <summary>
            /// Destroy the object or script for entities the local player does not own. If will not be recreated if
            /// the local player gains ownership of the entity.
            /// </summary>
            DESTROY_WHEN_UNOWNED = 3,
            /// <summary>
            /// Destroy the object or script for entities the local player owns. If will not be recreated if the local
            /// player loses ownership of the entity.
            /// </summary>
            DESTROY_WHEN_OWNED = 4
        }

        /// <summary>
        /// When happens to the game object. This is only applied if the game object is not the root of the entity.
        /// </summary>
        [ksEnum]
        [Tooltip("What happens to the game object?")]
        public Rules GameObjectRule;

        /// <summary>What happens to components by default.</summary>
        [ksEnum]
        [Tooltip("What happens to components by default?")]
        public Rules DefaultRule;

        /// <summary>
        /// Maps components on this game object to rules to apply. Components that aren't in the dictionary will use
        /// <see cref="DefaultRule"/>.
        /// </summary>
        public ksSerializableDictionary<Component, Rules> ComponentRules
        {
            get { return m_componentRules; }
        }
        [SerializeField]
        private ksSerializableDictionary<Component, Rules> m_componentRules = 
            new ksSerializableDictionary<Component, Rules>();

        private ksEntityComponent m_component;
        private ksEntity m_entity;
        private bool m_isEntityRoot;
        private List<UnityEngine.Object> m_disabledObjects = new List<UnityEngine.Object>();

        /// <summary>
        /// Looks for a <see cref="ksEntityComponent"/> script on this object or one of its ancestors and applies the
        /// rules to the game object/components if an entity is found. Registers a handler to reapply rules when
        /// ownership changes.
        /// </summary>
        private void Awake()
        {
            m_component = GetComponentInParent<ksEntityComponent>();
            if (m_component == null)
            {
                return;
            }
            if (m_component.Entity == null)
            {
                // The entity isn't spawned yet. Initialize once it is spawned.
                m_component.PreAttachScripts += Attached;
                return;
            }
            Initialize();
        }

        /// <summary>
        /// Called when the <see cref="ksEntityComponent"/> is attached to an entity. Applies the rules to the game
        /// object/components. Registers a handler to reapply the rules when ownership changes.
        /// </summary>
        /// <param name="entity">Entity</param>
        private void Attached(ksEntity entity)
        {
            m_component.PreAttachScripts -= Attached;
            Initialize();
        }

        /// <summary>
        /// Applies the rules to the game object/components. Registers a handler to reapply the rules when
        /// ownership changes.
        /// </summary>
        private void Initialize()
        {
            m_isEntityRoot = m_component.gameObject == gameObject;
            m_entity = m_component.Entity;
            m_entity.OnOwnershipChange += OwnershipChanged;
            ApplyRules(m_entity.OwnerId == m_entity.Room.LocalPlayerId);
        }

        /// <summary>
        /// Called when the entity's owner or permissions change. Reapplies the rules to the game object/components if
        /// the local player gained or lost ownership.
        /// </summary>
        /// <param name="oldOwner">Old owner id</param>
        /// <param name="newOwner">New owner id</param>
        /// <param name="oldPermissions">Old permissions</param>
        /// <param name="newPermissions">New permissions</param>
        private void OwnershipChanged(
            uint oldOwner,
            uint newOwner,
            ksOwnerPermissions oldPermissions,
            ksOwnerPermissions newPermissions)
        {
            bool wasOwner = oldOwner == m_entity.Room.LocalPlayerId;
            bool isOwner = newOwner == m_entity.Room.LocalPlayerId;
            if (wasOwner == isOwner)
            {
                return;
            }
            EnableDisabledObjects();
            ApplyRules(isOwner);
        }

        /// <summary>Applies rules to the game object and its scripts based on ownership state.</summary>
        /// <param name="isOwner">Is the local player the owner of the entity?</param>
        private void ApplyRules(bool isOwner)
        {
            // Only apply the game object rule if this is not the root of the entity.
            if (!m_isEntityRoot)
            {
                switch (GameObjectRule)
                {
                    case Rules.DISABLE_WHEN_UNOWNED:
                    {
                        if (!isOwner)
                        {
                            m_disabledObjects.Add(gameObject);
                            gameObject.SetActive(false);
                        }
                        break;
                    }
                    case Rules.DISABLE_WHEN_OWNED:
                    {
                        if (isOwner)
                        {
                            m_disabledObjects.Add(gameObject);
                            gameObject.SetActive(false);
                        }
                        break;
                    }
                    case Rules.DESTROY_WHEN_UNOWNED:
                    {
                        if (!isOwner)
                        {
                            Destroy(gameObject);
                            return;
                        }
                        break;
                    }
                    case Rules.DESTROY_WHEN_OWNED:
                    {
                        if (isOwner)
                        {
                            Destroy(gameObject);
                            return;
                        }
                        break;
                    }
                }
            }
            // Apply script rules.
            foreach (Component component in GetComponents<Component>())
            {
                if (!IsConfigurableComponent(component))
                {
                    continue;
                }
                Rules rule;
                if (!m_componentRules.TryGetValue(component, out rule))
                {
                    rule = DefaultRule;
                }
                switch (rule)
                {
                    case Rules.DISABLE_WHEN_UNOWNED:
                    {
                        if (!isOwner && SetEnabled(component, false))
                        {
                            m_disabledObjects.Add(component);
                        }
                        break;
                    }
                    case Rules.DISABLE_WHEN_OWNED:
                    {
                        if (isOwner && SetEnabled(component, false))
                        {
                            m_disabledObjects.Add(component);
                        }
                        break;
                    }
                    case Rules.DESTROY_WHEN_UNOWNED:
                    {
                        if (!isOwner)
                        {
                            DestroyComponent(component);
                        }
                        break;
                    }
                    case Rules.DESTROY_WHEN_OWNED:
                    {
                        if (isOwner)
                        {
                            DestroyComponent(component);
                        }
                        break;
                    }
                }
            }
        }

        /// <summary>Enables objects that were disabled when we applied script rules.</summary>
        private void EnableDisabledObjects()
        {
            foreach (UnityEngine.Object uobj in m_disabledObjects)
            {
                SetEnabled(uobj, true);
            }
            m_disabledObjects.Clear();
        }

        /// <summary>
        /// Sets an object to be enabled or disabled. If the object is a rigid body, sets the kinematic flag to true
        /// for disabled and false for enabled.
        /// </summary>
        /// <param name="uobj">Object to enable or disable.</param>
        /// <param name="enabled">Enabled state to set.</param>
        /// <returns>True if the enabled state changed.</returns>
        private bool SetEnabled(UnityEngine.Object uobj, bool enabled)
        {
            ksEntityScript script = uobj as ksEntityScript;
            if (script != null)
            {
                if (script.EnableOnAttach == enabled)
                {
                    return false;
                }
                script.EnableOnAttach = enabled;
                if (script.IsAttached)
                {
                    script.enabled = enabled;
                }
                return true;
            }
            Behaviour behaviour = uobj as Behaviour;
            if (behaviour != null)
            {
                if (behaviour.enabled == enabled)
                {
                    return false;
                }
                behaviour.enabled = enabled;
                return true;
            }
            Collider collider = uobj as Collider;
            if (collider != null)
            {
                if (collider.enabled == enabled)
                {
                    return false;
                }
                collider.enabled = enabled;
                return true;
            }
            Renderer renderer = uobj as Renderer;
            if (renderer != null)
            {
                if (renderer.enabled == enabled)
                {
                    return false;
                }
                renderer.enabled = enabled;
                return true;
            }
            Rigidbody rigidbody = uobj as Rigidbody;
            if (rigidbody != null)
            {
                // We force the Unity rigid body that exist on the server to be kinematic for entities that don't have
                // their transform controlled locally. If this rigid body has a wrapper, set the kinematic state through
                // the wrapper instead to ensure it gets set properly.
                if (m_entity != null && m_entity.GameObject == gameObject)
                {
                    ksBaseUnityRigidBody rigidBodyWrapper = m_entity.RigidBody != null ? 
                        m_entity.RigidBody : m_entity.RigidBody2D;
                    if (rigidBodyWrapper != null && rigidBodyWrapper.Rigidbody == rigidbody)
                    {
                        if (rigidBodyWrapper.IsKinematic != enabled)
                        {
                            return false;
                        }
                        rigidBodyWrapper.IsKinematic = !enabled;
                        return true;
                    }
                }

                if (rigidbody.isKinematic != enabled)
                {
                    return false;
                }
                rigidbody.isKinematic = !enabled;
                return true;
            }
            Rigidbody2D rigidbody2D = uobj as Rigidbody2D;
            if (rigidbody2D != null)
            {
                // Do nothing if the rigid body is static.
                if (rigidbody2D.bodyType != (enabled ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic))
                {
                    return false;
                }
                rigidbody2D.bodyType = enabled ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
                return true;
            }
            GameObject gameObj = uobj as GameObject;
            if (gameObj != null)
            {
                if (gameObj.activeSelf == enabled)
                {
                    return false;
                }
                gameObj.SetActive(enabled);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Destroys a component. If the component is a <see cref="Rigidbody"/>, sets
        /// <see cref="ksBaseEntity.RigidBody"/> and <see cref="ksBaseEntity.RigidBody2D"/> to null. If it is a
        /// <see cref="CharacterController"/>, sets <see cref="ksBaseEntity.CharacterController"/> to null.
        /// </summary>
        /// <param name="component">Component to destroy</param>
        private void DestroyComponent(Component component)
        {
            if (component is Rigidbody)
            {
                m_entity.RigidBody = null;
                m_entity.RigidBody2D = null;
            }
            else if (component is CharacterController)
            {
                m_entity.CharacterController = null;
            }
            Destroy(component);
        }

        /// <summary>
        /// Checks if a component can have a configurable ownership rule. Transforms and components tagged with
        /// <see cref="ksHideInOwnershipManagerAttribute"/> that have
        /// <see cref="ksHideInOwnershipManagerAttribute.Hide"/> set to true cannot be configured.
        /// </summary>
        /// <param name="component">Component to check.</param>
        /// <returns>True if the component can have a configurable rule.</returns>
        public static bool IsConfigurableComponent(Component component)
        {
            if (component == null || component is Transform)
            {
                return false;
            }
            ksHideInOwnershipManagerAttribute tag =
                component.GetType().GetCustomAttribute<ksHideInOwnershipManagerAttribute>();
            return tag == null || !tag.Hide;
        }
    }
}