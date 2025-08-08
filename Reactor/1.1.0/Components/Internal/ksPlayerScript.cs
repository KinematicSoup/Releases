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
    /// <summary>
    /// Base class for scripts attached to players. These can be attached to game objects with <see cref="ksRoomType"/>
    /// in the editor, but they won't run on those game objects. They are copied to player game objects for players
    /// in rooms of the given room type.
    /// </summary>
    [AddComponentMenu(ksMenuNames.HIDDEN)]
    public class ksPlayerScript : ksMonoScript<ksPlayer, ksPlayerScript>
    {
        /// <summary>Player types</summary>
        public enum PlayerTypes
        {
            LOCAL_PLAYER = 1,
            REMOTE_PLAYERS = 2,
            ALL_PLAYERS = LOCAL_PLAYER | REMOTE_PLAYERS
        }

        /// <summary>Player the script is attached to.</summary>
        public ksPlayer Player
        {
            get { return ParentObject; }
        }

        /// <summary>Room the player is in.</summary>
        public override sealed ksRoom Room
        {
            get { return Player == null ? null : Player.Room; }
        }

        /// <summary>Room properties</summary>
        public ksPropertyMap Properties
        {
            get { return Player == null ? null : Player.Properties; }
        }

        /// <summary>Which player types should this script be attached to?</summary>
        public PlayerTypes PlayerType
        {
            get { return m_playerType; }
            set { m_playerType = value; }
        }
        [SerializeField][ksEnum]
        PlayerTypes m_playerType = PlayerTypes.ALL_PLAYERS;

        /// <summary>Initialize internal components.</summary>
        protected virtual void OnEnable()
        {
            if (!SetupComponent() && GetComponent<ksRoomType>() == null)
            {
                ksLog.Warning(this, "No ksRoomType or Player found on game object " +
                    gameObject.name + " with ksPlayerScript");
            }
        }
    }
}
