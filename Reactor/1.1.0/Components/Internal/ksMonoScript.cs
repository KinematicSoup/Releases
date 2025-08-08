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
    /// <summary>Base class for Reactor MonoBehaviour scripts.</summary>
    /// <typeparam name="Parent">Type the script can be attached to.</typeparam>
    /// <typeparam name="Script">Script type</typeparam>
    public abstract class ksMonoScript<Parent, Script> : MonoBehaviour
        where Parent : class
        where Script : ksMonoScript<Parent, Script>
    {
        /// <summary>Get the most derived type of this script.</summary>
        public Type InstanceType
        {
            get
            {
                if (m_type == null)
                {
                    m_type = GetType();
                }
                return m_type;
            }
        }
        private Type m_type;

        /// <summary>Room the script is in.</summary>
        public abstract ksRoom Room { get; }

        /// <summary>Server time and local frame delta.</summary>
        public ksClientTime Time
        {
            get { return Room == null ? ksClientTime.Zero : Room.Time; }
        }

        /// <summary>
        /// Is this script attached to a Reactor object (entity/room/player)? Scripts that are not attached are always
        /// disabled. If you enable an unattached script, it will look for a 
        /// <see cref="ksScriptsComponent{Parent, Script}"/> with an entity/room/player to attach to and disable itself
        /// if it does not find one.
        /// </summary>
        public bool IsAttached
        {
            get { return ParentObject != null; }
        }

        /// <summary>Has <see cref="Initialize"/> been called on this script?</summary>
        public bool IsInitialized
        {
            get { return m_initialized; }
            internal set { m_initialized = value; }
        }
        private bool m_initialized = false;

        /// <summary>Loader for <see cref="ksScriptAsset"/>s in Resources or asset bundles.</summary>
        public ksBaseAssetLoader Assets
        {
            get { return ksScriptAsset.Assets; }
        }

        /// <summary>Component the script is attached to.</summary>
        internal ksScriptsComponent<Parent, Script> Component
        {
            get { return m_component; }
            set { m_component = value; }
        }
        private ksScriptsComponent<Parent, Script> m_component;

        /// <summary>The parent (room, entity, or player) for this script.</summary>
        internal Parent ParentObject
        {
            get{ return m_component == null ? null : m_component.ParentObject; }
        }

        /// <summary>
        /// Should this script be enabled when it is attached to the Reactor object (entity/room/player)?
        /// </summary>
        public bool EnableOnAttach
        {
            get { return m_enableOnAttach; }
            set { m_enableOnAttach = value; }
        }
        private bool m_enableOnAttach = true;

        /// <summary>
        /// Unity OnDestroy. Removes the script from the component and, if the script was is in the component script
        /// list, calls <see cref="Detached()"/>. If you override this method, your override should call the base method
        /// to ensure the script is cleaned up properly.
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (m_component != null && m_component.ScriptsAttached && m_component.Scripts.Remove((Script)this))
            {
                Detached();
            }
        }

        /// <summary>
        /// Called immediately when the script/gameobject is attached to its Reactor object (Entity/Room/Player). Other
        /// entities may not be spawned yet, and other Reactor scripts may not be attached yet, meaning their
        /// Entity/Room/Player reference will be null and they won't be in the 
        /// <see cref="ksScriptsComponent{Parent, Script}.Scripts"/> list yet. Initialization that depends on other
        /// scripts or entities should be done from <see cref="Initialize"/>. Attached is called on disabled scripts.
        /// </summary>
        public virtual void Attached()
        {

        }

        /// <summary>
        /// Called after all other scripts are attached and entities are spawned. Initialize is not called on disabled
        /// scripts.
        /// </summary>
        public virtual void Initialize()
        {

        }

        /// <summary>
        /// Called when the script/gameobject is detached from its Reactor object (Entity/Room/Player). Remove event
        /// listeners and perform other clean up logic here.
        /// </summary>
        public virtual void Detached()
        {

        }

        /// <summary>
        /// Called when a script wakes. This method stores the initial enabled state of a script.  If the
        /// script was not enabled, then we set a value indicating that the script should not be included in the
        /// list of scripts on <see cref="ksScriptsComponent{Parent, Script}"/>
        /// </summary>
        protected virtual void Awake()
        {
            m_enableOnAttach = enabled;
        }

        /// <summary>Finds the script component and performs initialization.</summary>
        /// <returns>True if script component for this script type was found.</returns>
        internal bool SetupComponent()
        {
            if (m_component != null)
            {
                if (m_component.IsInitialized && !m_initialized)
                {
                    m_initialized = true;
                    Initialize();
                }
                return true;
            }
            m_component = GetComponent<ksScriptsComponent<Parent, Script>>();
            if (m_component != null)
            {
                if (!m_component.ScriptsAttached)
                {
                    m_component = null;
                    enabled = false;
                }
                else if (!m_component.Scripts.Contains((Script)this))
                {
                    m_component.Scripts.Add((Script)this);
                    // Set enabled to true before calling Attached so if Attached sets it to false, it won't be set
                    // back to true after.
                    enabled = true;
                    Attached();
                    if (m_component.IsInitialized)
                    {
                        ksRPCManager<ksRPCAttribute>.Instance.RegisterTypeRPCs(InstanceType);
                        m_initialized = true;
                        Initialize();
                    }
                }
                return true;
            }
            enabled = false;
            return false;
        }
    }
}
