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
    /// Shared data component storing the room id that is added to room/player/ksentity entities in connected rooms.
    /// </summary>
    public struct ksSharedRoomId : ISharedComponentData
    {
        /// <summary>Room id</summary>
        public uint Id
        {
            get { return m_id; }
        }
        private uint m_id;

        /// <summary>Constructor</summary>
        /// <param name="id">Room id</param>
        public ksSharedRoomId(uint id)
        {
            m_id = id;
        }
    }
}