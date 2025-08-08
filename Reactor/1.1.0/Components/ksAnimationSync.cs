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
using UnityEngine;

namespace KS.Reactor.Client.Unity
{
    /// <summary>
    /// Syncs animation parameters and optionally layer states for an entity that has an owner with the
    /// <see cref="ksOwnerPermissions.PROPERTIES"/> permission. Each animation parameter (and layer if
    /// <see cref="SyncLayerStates"/> is true) is synced as an entity property, starting at id 
    /// <see cref="AnimationPropertiesStart"/>.
    /// </summary>
    [ksHideInOwnershipManager]
    [RequireComponent(typeof(ksEntityComponent))]
    [AddComponentMenu(ksMenuNames.REACTOR + nameof(ksAnimationSync))]
    public class ksAnimationSync : ksEntityScript
    {
        /// <summary>The animator to sync animation state for.</summary>
        public Animator Animator
        {
            get { return m_animator; }
            set
            {
                if (m_animator == value)
                {
                    return;
                }
                Animator oldValue = m_animator;
                m_animator = value;

                if ((object)value == null)
                {
                    Detached();
                }
                else if ((object)oldValue == null)
                {
                    Initialize();
                }
                else if (!Entity.HasOwnerPermission(ksOwnerPermissions.PROPERTIES))
                {
                    UnbindAnimationProperties();
                    if (m_animator != null)
                    {
                        RemoteSetup();
                    }
                }
            }
        }
        [Tooltip("The animator to sync animation properties for. If null, will look for an animator on the same game object.")]
        [SerializeField]
        private Animator m_animator;

        /// <summary>
        /// The start of the property ids used to sync animation data. Each animation parameter (and layer if 
        /// <see cref="SyncLayerStates"/> is true) will use one property.
        /// </summary>
        public uint AnimationPropertiesStart
        {
            get { return m_animationPropertiesStart; }
        }
        [Tooltip("The start of the property ids used to sync animation data. Each animation parameter " +
            "(and layer if Sync Layer States is checked) will use one property.")]
        [SerializeField]
        private uint m_animationPropertiesStart = 1000;

        /// <summary>
        /// Should the animation layer states be synced? If your layer states are driven by animation properties, this
        /// should be false. If they are set programmatically, this should be true.
        /// </summary>
        public bool SyncLayerStates
        {
            get { return m_syncLayerStates; }
            set
            {
                if (m_syncLayerStates == value)
                {
                    return;
                }
                m_syncLayerStates = value;
                if (!Entity.HasOwnerPermission(ksOwnerPermissions.PROPERTIES) && m_animator != null)
                {
                    // Rebind animation properties
                    UnbindAnimationProperties();
                    BindAnimationProperties();
                }
            }
        }
        [Tooltip("Should the animation layer states be synced? If your layer states are driven by animation " +
            "properties, leave this unchecked. If they are set programmatically, check this box.")]
        [SerializeField]
        private bool m_syncLayerStates = false;

        /// <summary>The cross fade duration in seconds when switching layer states.</summary>
        public float CrossFadeDuration
        {
            get { return m_crossFadeDuration; }
            set { m_crossFadeDuration = value; }
        }
        [Tooltip("The cross fade duration in seconds when switching layer states.")]
        [SerializeField]
        private float m_crossFadeDuration = .2f;

        private bool m_applyRootMotion;
        private List<KeyValuePair<uint, ksDelegates.PropertyChangeHandler>> m_propertyChangeHandlers;

        /// <summary>Initialization. Registers event listeners for syncing animation data.</summary>
        public override void Initialize()
        {
            if (m_animator == null)
            {
                m_animator = GetComponent<Animator>();
                if (m_animator == null)
                {
                    return;
                }
            }

            Entity.OnOwnershipChange += OwnershipChanged;
            if (Entity.HasOwnerPermission(ksOwnerPermissions.PROPERTIES))
            {
                OwnerSetup();
            }
            else
            {
                RemoteSetup();
            }
        }

        /// <summary>Unregisters event listeners.</summary>
        public override void Detached()
        {
            if (!Entity.HasOwnerPermission(ksOwnerPermissions.PROPERTIES))
            {
                // Set apply root motion to what it was before we disabled it.
                if (m_animator != null)
                {
                    m_animator.applyRootMotion = m_applyRootMotion;
                }
            }
            UnbindAnimationProperties();
            Room.PreSendFrame -= UpdateAnimationProperties;
            Entity.OnOwnershipChange -= OwnershipChanged;
        }

        /// <summary>
        /// Does setup for the owner to send animation property data to the server.
        /// </summary>
        private void OwnerSetup()
        {
            Room.PreSendFrame += UpdateAnimationProperties;
            enabled = true;// So LateUpdate gets called to sync the animation data.
        }

        /// <summary>
        /// Does set up for applying animation property datas received from the server.
        /// </summary>
        private void RemoteSetup()
        {
            enabled = false;// Prevent LateUpdate from being called.

            // Prevent the animator from fighting with the server over the transform and store the current apply root
            // motion value so we can restore it later.
            m_applyRootMotion = m_animator.applyRootMotion;
            m_animator.applyRootMotion = false;

            BindAnimationProperties();
        }

        /// <summary>Binds event handlers for applying animation property changes to the animator.</summary>
        private void BindAnimationProperties()
        {
            m_propertyChangeHandlers = new List<KeyValuePair<uint, ksDelegates.PropertyChangeHandler>>();

            // Bind property change event handlers for animation parameters.
            for (int i = 0; i < m_animator.parameterCount; i++)
            {
                BindAnimatorProperty(i);
            }
            if (m_syncLayerStates)
            {
                // Bind property change event handers for animation layer states.
                for (int i = 0; i < m_animator.layerCount; i++)
                {
                    BindLayerState(i);
                }
            }
        }

        /// <summary>
        /// Called every frame. Updates properties for triggered animation triggers so they will be sent to the server.
        /// </summary>
        private void LateUpdate()
        {
            if (m_animator == null)
            {
                return;
            }
            for (int i = 0; i < m_animator.parameterCount; i++)
            {
                AnimatorControllerParameter parameter = m_animator.GetParameter(i);
                if (parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    if (m_animator.GetBool(parameter.nameHash))
                    {
                        uint propertyId = m_animationPropertiesStart + (uint)i;

                        // Animation triggers are synced as a byte property. Each time the trigger is trigerred, we
                        // increment the value. Anytime the value changes, other players will trigger the trigger.
                        if (!Properties.Contains(propertyId))
                        {
                            Properties[propertyId] = (byte)0;
                        }
                        else
                        {
                            unchecked
                            {
                                Properties[propertyId]++;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>Binds a property change handler to set a layer state when the property changes.</summary>
        /// <param name="index">Index of layer state</param>
        private void BindLayerState(int index)
        {
            uint propertyId = m_animationPropertiesStart + (uint)m_animator.parameterCount + (uint)index;
            ksDelegates.PropertyChangeHandler handler = (ksMultiType oldValue, ksMultiType newValue) =>
                m_animator.CrossFade(newValue.Int, m_crossFadeDuration, index);
            m_propertyChangeHandlers.Add(new KeyValuePair<uint, ksDelegates.PropertyChangeHandler>(
                propertyId, handler));
            Entity.OnPropertyChange[propertyId] += handler;
            m_animator.Play(Properties[propertyId].Int, index);
        }

        /// <summary>Binds a property change handler to set an animation parameter when the property changes.</summary>
        /// <param name="index">Index of animation parameter</param>
        private void BindAnimatorProperty(int index)
        {
            uint propertyId = m_animationPropertiesStart + (uint)index;
            ksDelegates.PropertyChangeHandler handler = (ksMultiType oldValue, ksMultiType newValue) =>
                SetParameterValue(index, ref newValue);
            m_propertyChangeHandlers.Add(new KeyValuePair<uint, ksDelegates.PropertyChangeHandler>(
                propertyId, handler));
            Entity.OnPropertyChange[propertyId] += handler;
            ksMultiType value = Properties[propertyId];

            // Set the parameter to the current value, ignoring triggers.
            if (!value.IsNull)
            {
                SetParameterValue(index, ref value, false);
            }
        }

        /// <summary>Gets the value of an animation parameter as a <see cref="ksMultiType"/>.</summary>
        /// <param name="parameter">Animation parameter</param>
        /// <returns>Value of the animation parameter</returns>
        private ksMultiType GetParameterValue(AnimatorControllerParameter parameter)
        {
            switch (parameter.type)
            {
                case AnimatorControllerParameterType.Bool:
                {
                    return m_animator.GetBool(parameter.nameHash);
                }
                case AnimatorControllerParameterType.Float:
                {
                    return m_animator.GetFloat(parameter.nameHash);
                }
                case AnimatorControllerParameterType.Int:
                {
                    return m_animator.GetInteger(parameter.nameHash);
                }
            }
            return ksMultiType.Null;
        }

        /// <summary>Sets an animation parameter value.</summary>
        /// <param name="index">Index of the animation parameter to set</param>
        /// <param name="value">Value to set</param>
        /// <param name="setTrigger">
        /// If true and the parameter is a trigger, sets the trigger. <paramref name="value"/> is ignored for triggers.
        /// </param>
        private void SetParameterValue(int index, ref ksMultiType value, bool setTrigger = true)
        {
            AnimatorControllerParameter parameter = m_animator.GetParameter(index);
            switch (parameter.type)
            {
                case AnimatorControllerParameterType.Bool:
                {
                    m_animator.SetBool(parameter.nameHash, value);
                    break;
                }
                case AnimatorControllerParameterType.Int:
                {
                    m_animator.SetInteger(parameter.nameHash, value);
                    break;
                }
                case AnimatorControllerParameterType.Float:
                {
                    m_animator.SetFloat(parameter.nameHash, value);
                    break;
                }
                case AnimatorControllerParameterType.Trigger:
                {
                    if (setTrigger)
                    {
                        m_animator.SetTrigger(parameter.nameHash);
                    }
                    break;
                }
            }
        }

        /// <summary>Updates changed animation properties that will be sent to the server.</summary>
        private void UpdateAnimationProperties()
        {
            // Sync changed animation parameters
            for (int i = 0; i < m_animator.parameterCount; i++)
            {
                AnimatorControllerParameter parameter = m_animator.GetParameter(i);
                if (parameter.type != AnimatorControllerParameterType.Trigger)
                {
                    ksMultiType value = GetParameterValue(parameter);
                    uint propertyId = m_animationPropertiesStart + (uint)i;
                    if (!value.Equals(Properties[propertyId]))
                    {
                        Properties[propertyId] = value;
                    }
                }
            }

            if (!m_syncLayerStates)
            {
                return;
            }
            // Sync changed animation layer states
            for (int i = 0; i < m_animator.layerCount; i++)
            {
                int state = m_animator.GetCurrentAnimatorStateInfo(i).fullPathHash;
                uint propertyId = m_animationPropertiesStart + (uint)m_animator.parameterCount + (uint)i;
                if (state != Properties[propertyId].Int)
                {
                    Properties[propertyId] = state;
                }
            }
        }

        /// <summary>Removes animation property change handlers.</summary>
        private void UnbindAnimationProperties()
        {
            if (m_propertyChangeHandlers == null)
            {
                return;
            }
            foreach (KeyValuePair<uint, ksDelegates.PropertyChangeHandler> handler in m_propertyChangeHandlers)
            {
                Entity.OnPropertyChange[handler.Key] -= handler.Value;
            }
            m_propertyChangeHandlers = null;
        }

        /// <summary>
        /// Called when the entity's owner or owner permissions change. If the local player gained or lost property
        /// ownership, does setup to send or receive animation properties respectively.
        /// </summary>
        /// <param name="oldOwner"></param>
        /// <param name="newOwner"></param>
        /// <param name="oldPermissions"></param>
        /// <param name="newPermissions"></param>
        private void OwnershipChanged(
            uint oldOwner,
            uint newOwner,
            ksOwnerPermissions oldPermissions,
            ksOwnerPermissions newPermissions)
        {
            if (m_animator == null)
            {
                return;
            }
            bool wasPropertyOwner = enabled;
            bool isPropertyOwner = Entity.HasOwnerPermission(ksOwnerPermissions.PROPERTIES);
            if (wasPropertyOwner == isPropertyOwner)
            {
                return;
            }
            if (isPropertyOwner)
            {
                UnbindAnimationProperties();
                // Set apply root motion to what it was before we disabled it.
                m_animator.applyRootMotion = m_applyRootMotion;
                OwnerSetup();
            }
            else
            {
                Room.PreSendFrame -= UpdateAnimationProperties;
                RemoteSetup();
            }
        }
    }
}