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
using UnityEngine;

using Hash128 = Unity.Entities.Hash128;

namespace KS.Reactor.Client.Unity.DOTS
{
    /// <summary>
    /// Unity DOTS implementation of ksBaseRoom. Rooms are used to connect to server rooms. The room maintains a
    /// simulation state that is regularly synced with the server. Each connected room as a DOTS entity with a
    /// <see cref="ksRoomData"/>.
    /// </summary>
    public class ksRoomDOTS : ksBaseRoom<ksPlayerDOTS, ksEntityDOTS>
    {
        /// <summary>This is not implemented and returns null.</summary>
        public override ksBasePhysics Physics
        {
            get { return null; }
        }

        /// <summary>This is not implemented and is always zero.</summary>
        protected override ksVector3 Gravity 
        {
            get { return ksVector3.Zero; }
            set { }
        }

        /// <summary>The DOTS entity for this room.</summary>
        public Entity DOTSEntity
        {
            get { return m_dotsEntity; }
        }
        private Entity m_dotsEntity;

        /// <summary>
        /// True if the room's DOTS entity is a temporary entity that will be destroyed when we disconnect because a
        /// <see cref="ksRoomData"/> with a matching scene and type that wasn't linked to a different room was not 
        /// found.
        /// </summary>
        internal bool IsTempEntity
        {
            get { return m_isTempEntity; }
            set { m_isTempEntity = value; }
        }
        private bool m_isTempEntity;

        /// <summary>The <see cref="World"/> the entities in this room belong to.</summary>
        public World World
        {
            get { return m_system.World; }
        }

        /// <summary>
        /// Guid of the scene the room is in. All zeroes if a <see cref="ksRoomData"/> with a matching scene name and a
        /// <see cref="SceneSection"/> was not found when the room first connected.
        /// </summary>
        public Hash128 SceneGuid
        {
            get { return m_sceneGuid; }
            internal set { m_sceneGuid = value; }
        }
        private Hash128 m_sceneGuid;

        /// <summary>Default predictor for entities that do not use input prediction.</summary>
        public ksPredictor DefaultEntityPredictor
        {
            get { return m_defaultEntityPredictor; }
            set { m_defaultEntityPredictor = value; }
        }
        private ksPredictor m_defaultEntityPredictor;

        /// <summary>Default predictor for entities with player controllers that use input prediction.</summary>
        public ksPredictor DefaultEntityControllerPredictor
        {
            get { return m_defaultEntityControllerPredictor; }
            set { m_defaultEntityControllerPredictor = value; }
        }
        private ksPredictor m_defaultEntityControllerPredictor;

        /// <summary>Default predictor for player properties.</summary>
        public ksPredictor DefaultPlayerPredictor
        {
            get { return m_defaultPlayerPredictor; }
            set { m_defaultPlayerPredictor = value; }
        }
        private ksPredictor m_defaultPlayerPredictor;

        /// <summary>System for spawning and updating DOTS entities.</summary>
        internal SyncSystem System
        {
            get { return m_system; }
        }
        private SyncSystem m_system;

        /// <summary>Constructor</summary>
        /// <param name="world">World to spawn entities in.</param>
        /// <param name="roomInfo">Determines where we connect.</param>
        public ksRoomDOTS(World world, ksRoomInfo roomInfo)
            : base(roomInfo)
        {
            if (world == null)
            {
                world = World.DefaultGameObjectInjectionWorld;
            }
            m_system = world.GetOrCreateSystemManaged<SyncSystem>();
        }

        /// <summary>Constructor</summary>
        /// <param name="world">World to spawn entities in.</param>
        public ksRoomDOTS(World world)
            : this(world, new ksRoomInfo())
        {

        }

        /// <summary>Constructor</summary>
        /// <param name="roomInfo">Determines where we connect.</param>
        public ksRoomDOTS(ksRoomInfo roomInfo)
            : this(null, roomInfo)
        {
            
        }

        /// <summary>Constructor</summary>
        public ksRoomDOTS()
            : this(null, new ksRoomInfo())
        {

        }

        /// <summary>Connect to the server room.</summary>
        /// <param name="session">Player session</param>
        /// <param name="authArgs">Room authentication arguments.</param>
        public void Connect(ksPlayerAPI.Session session, params ksMultiType[] authArgs)
        {
            if (!Application.isPlaying)
            {
                ksLog.Warning(this, "You can only connect to a room while the game is playing.");
                return;
            }
            if (ksReactor.Service != null)
            {
                ksReactor.Service.JoinRoom(this, session, authArgs);
            }
        }

        /// <summary>Connect to the server room.</summary>
        /// <param name="authArgs">Room authentication arguments.</param>
        public void Connect(params ksMultiType[] authArgs)
        {
            if (!Application.isPlaying)
            {
                ksLog.Warning(this, "You can only connect to a room while the game is playing.");
                return;
            }
            if (ksReactor.Service != null)
            {
                ksReactor.Service.JoinRoom(this, null, authArgs);
            }
        }

        /// <summary>Disconnect from the server.</summary>
        /// <param name="immediate">
        /// If immediate is false, then disconnection will be delayed until all queued RPC calls have been sent.
        /// </param>
        public void Disconnect(bool immediate = false)
        {
            if (ksReactor.Service != null)
            {
                ksReactor.Service.LeaveRoom(this, immediate);
            }
        }

        /// <summary>Called when connected to the server. Spawns the DOTS entity for this room.</summary>
        protected override void Connected()
        {
            m_dotsEntity = System.FindOrCreateRoom(this);
        }

        /// <summary>Called when disconnected from the server. Destroys the DOTS entity for this room.</summary>
        protected override void Disconnected()
        {
            if (m_isTempEntity)
            {
                System.EntityManager.DestroyEntity(m_dotsEntity);
            }
            else
            {
                System.EntityManager.RemoveComponent<ksSharedRoomId>(m_dotsEntity);
            }
            m_dotsEntity = new Entity();// Set to invalid entity
        }

        /// <summary>Fetches a <see cref="ksEntityDOTS"/> from the pool.</summary>
        /// <returns>Entity object</returns>
        protected override ksEntityDOTS CreateEntity()
        {
            return ksObjectPool<ksEntityDOTS>.Instance.Fetch();
        }

        /// <summary>Creates a player object.</summary>
        /// <param name="id">Id of the player.</param>
        /// <returns>Player object</returns>
        protected override ksPlayerDOTS CreatePlayer(uint id)
        {
            return new ksPlayerDOTS(id);
        }

        /// <summary>Does nothing.</summary>
        /// <param name="rpcId"></param>
        /// <param name="rpcArgs"></param>
        /// <returns>False</returns>
        protected override bool InvokeRPC(uint rpcId, ksMultiType[] rpcArgs)
        {
            return false;
        }

        /// <summary>Creates the DOTS entity for a player.</summary>
        /// <param name="player">Player to create DOTS entity for.</param>
        protected override void LoadPlayerScripts(ksPlayerDOTS player)
        {
            player.DOTSEntity = System.CreatePlayer(player);
            if (DefaultPlayerPredictor != null)
            {
                player.Predictor = DefaultPlayerPredictor.CreateInstance();
            }
        }
    }
}