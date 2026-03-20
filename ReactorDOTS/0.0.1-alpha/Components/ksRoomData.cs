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
using Unity.Collections;
using KS.Reactor.Client.Unity;

namespace KS.Reactor.Client.Unity.DOTS
{
    /// <summary>ECS component struct storing room data.</summary>
    public struct ksRoomData : IComponentData
    {
        /// <summary>Room id</summary>
        public uint Id
        {
            get { return m_id; }
            internal set { m_id = value; }
        }
        private uint m_id;

        /// <summary>Scene name</summary>
        public FixedString512Bytes Scene
        {
            get { return m_scene; }
            internal set { m_scene = value; }
        }
        private FixedString512Bytes m_scene;

        /// <summary>Room type</summary>
        public FixedString512Bytes RoomType
        {
            get { return m_roomType; }
            internal set { m_roomType = value; }
        }
        private FixedString512Bytes m_roomType;

        /// <summary>
        /// How many updates we send to the server in one second. Must be between
        /// <see cref="ksBaseRoom.MIN_SEND_RATE"/> and <see cref="ksBaseRoom.MAX_SEND_RATE"/>.
        /// </summary>
        public int SendRate
        {
            get { return m_sendRate; }
            internal set { m_sendRate = value; }
        }
        private int m_sendRate;

        /// <summary>
        /// Are players allowed to spawn entities? If you change the value to true at runtime, you must also change
        /// Room.AllowPlayerSpawns on the server.
        /// </summary>
        public bool AllowPlayerSpawning
        {
            get { return m_allowPlayerSpawning; }
            internal set { m_allowPlayerSpawning = value; }
        }
        private bool m_allowPlayerSpawning;

        /// <summary>Default predictor for entities that do not use input prediction.</summary>
        public ksPredictor DefaultEntityPredictor
        {
            get { return m_defaultEntityPredictor.Value; }
            internal set { m_defaultEntityPredictor.Value = value; }
        }
        private UnityObjectRef<ksPredictor> m_defaultEntityPredictor;

        /// <summary>Default predictor for entities with player controllers that use input prediction.</summary>
        public ksPredictor DefaultEntityControllerPredictor
        {
            get { return m_defaultEntityControllerPredictor.Value; }
            internal set { m_defaultEntityControllerPredictor.Value = value; }
        }
        private UnityObjectRef<ksPredictor> m_defaultEntityControllerPredictor;

        /// <summary>Default predictor for room properties.</summary>
        public ksPredictor DefaultRoomPredictor
        {
            get { return m_defaultRoomPredictor.Value; }
            internal set { m_defaultRoomPredictor.Value = value; }
        }
        private UnityObjectRef<ksPredictor> m_defaultRoomPredictor;

        /// <summary>Default predictor for player properties.</summary>
        public ksPredictor DefaulPlayerPredictor
        {
            get { return m_defaultPlayerPredictor.Value; }
            internal set { m_defaultPlayerPredictor.Value = value; }
        }
        private UnityObjectRef<ksPredictor> m_defaultPlayerPredictor;
    }
}