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
using Unity.Entities;

namespace KS.Reactor.Client.Unity.DOTS
{
    /// <summary>
    /// Unity DOTS implementation of <see cref="ksBasePlayer"/>. A player represents a client. Each player has a DOTS
    /// entity spawned for it with a <see cref="ksPlayerData"/>.
    /// </summary>
    public class ksPlayerDOTS : ksBasePlayer
    {
        /// <summary>Room the player is in.</summary>
        public ksRoomDOTS Room
        {
            get { return (ksRoomDOTS)BaseRoom; }
        }

        /// <summary>The DOTS entity for this player.</summary>
        public Entity DOTSEntity
        {
            get { return m_dotsEntity; }
            internal set { m_dotsEntity = value; }
        }
        private Entity m_dotsEntity;

        /// <summary>Constructor</summary>
        /// <param name="id">Player id</param>
        public ksPlayerDOTS(uint id)
            : base(id)
        {

        }

        /// <summary>Destroys the DOTS entity for this player.</summary>
        protected override void Destroy()
        {
            Room.System.EntityManager.DestroyEntity(m_dotsEntity);
            m_dotsEntity = new Entity();// Set to invalid entity
        }
    }
}