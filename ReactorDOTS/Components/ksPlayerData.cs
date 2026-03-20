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
    /// <summary>ECS component struct storing player data.</summary>
    public struct ksPlayerData : IComponentData
    {
        /// <summary>Player id</summary>
        public uint Id
        {
            get { return m_id; }
            internal set { m_id = value; }
        }
        private uint m_id;

        /// <summary>Room id</summary>
        public uint RoomId
        {
            get { return m_roomId; }
            internal set { m_roomId = value; }
        }
        private uint m_roomId;
    }
}