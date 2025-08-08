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

using UnityEngine;

namespace KS.Reactor.Client.Unity
{
    /// <summary>Unity implementation of <see cref="ksBasePlayer"/>. A player represents a client.</summary>
    public class ksPlayer : ksBasePlayer
    {
        /// <summary>Game object for attaching player scripts.</summary>
        public GameObject GameObject
        {
            get { return m_gameObject; }
        }
        private GameObject m_gameObject;

        /// <summary>Room the player belongs to.</summary>
        public ksRoom Room
        {
            get { return (ksRoom)BaseRoom; }
        }

        private ksPlayerComponent m_component;

        /// <summary>The player component</summary>
        internal ksPlayerComponent Component
        {
            get { return m_component; }
        }

        /// <summary>Constructor</summary>
        /// <param name="id">Player ID</param>
        public ksPlayer(uint id)
            : base(id)
        {
        }

        /// <summary>Initializes all <see cref="ksPlayerScript"/> attached to this player.</summary>
        protected override void InitializeScripts()
        {
            m_component.InitializeScripts();
        }

        /// <summary>Destroys this player.</summary>
        protected override void Destroy()
        {
            GameObject.Destroy(m_gameObject);
        }

        /// <summary>Sets the game object and adds a <see cref="ksPlayerComponent"/></summary>
        /// <param name="gameObject">Game object associated with this player.</param>
        internal void SetGameObject(GameObject gameObject)
        {
            m_gameObject = gameObject;
            m_component = m_gameObject.AddComponent<ksPlayerComponent>();
            m_component.ParentObject = this;
        }

        /// <summary>
        /// Checks if a player script should be attached to this player by checking which types (local, remote)
        /// the script attaches to and which type of player this is.
        /// </summary>
        /// <param name="script">Script to validate.</param>
        /// <returns>True if the script should be attached.</returns>
        internal bool ValidateScript(ksPlayerScript script)
        {
            ksPlayerScript.PlayerTypes playerType = IsLocal ?
                ksPlayerScript.PlayerTypes.LOCAL_PLAYER : ksPlayerScript.PlayerTypes.REMOTE_PLAYERS;
            return (script.PlayerType & playerType) != 0;
        }
    }
}
