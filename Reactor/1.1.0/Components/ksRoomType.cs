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
    /// <summary>
    /// Room types hold room configuration data. Room and player scripts can be attached to game objects with room types.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu(ksMenuNames.REACTOR + "ksRoomType")]
    public class ksRoomType : MonoBehaviour
    {
        /// <summary>
        /// <see cref="DefaultEntityPredictor"/> is initialized to this when a new room type is constructed.
        /// </summary>
        internal static ksPredictor InitialPredictor;

        /// <summary>
        /// <see cref="DefaultEntityControllerPredictor"/> is initialized to this when a new room type is constructed.
        /// </summary>
        internal static ksPredictor InitialControllerPredictor;

        /// <summary>
        /// Checks if a local server is running for a scene and room type.
        /// </summary>
        /// <param name="scene">scene to check for</param>
        /// <param name="roomType">roomType to check for</param>
        /// <returns>true if a local server is running the room type.</returns>
        internal delegate bool LocalServerChecker(string scene, string roomType);

        /// <summary>
        /// Delegate for checking if a local server is running.
        /// </summary>
        internal static LocalServerChecker LocalServerRunningChecker
        {
            get { return m_localServerChecker; }
            set { m_localServerChecker = value; }
        }
        private static LocalServerChecker m_localServerChecker;

        /// <summary>Number of server frame updates per second.</summary>
        public int ServerFrameRate
        {
            get { return m_serverFrameRate; }
            set { m_serverFrameRate = value; }
        }
        [SerializeField]
        [Range(10, 120)]
        [Tooltip("Number of server frame updates per second.")]
        private int m_serverFrameRate = 60;

        /// <summary>
        /// Number of server frames per network update set from the server to all clients. Eg. if this is two, the
        /// server will send a network update every second frame.
        /// </summary>
        public int ServerFramesPerSync
        {
            get { return m_serverFramesPerSync; }
            set { m_serverFramesPerSync = value; }
        }
        [SerializeField]
        private int m_serverFramesPerSync = 2;

        /// <summary>
        /// Network updates sent from the server to all clients per second. This is calculated from 
        /// <see cref="ServerFrameRate"/> and <see cref="ServerFramesPerSync"/>.
        /// </summary>
        public float NetworkSyncRate
        {
            get { return (float)m_serverFrameRate / Math.Max(1, m_serverFramesPerSync); }
        }

        /// <summary>Number of times per second the client sends data to the server.</summary>
        public int ClientSendRate
        {
            get { return m_clientSendRate; }
            set { m_clientSendRate = value; }
        }

        [SerializeField]
        [Range(ksBaseRoom.MIN_SEND_RATE, ksBaseRoom.MAX_SEND_RATE)]
        [Tooltip("Number of times per second the client sends data to the server.")]
        private int m_clientSendRate = 60;

        /// <summary>
        /// Try to recover lost update time
        /// </summary>
        public bool RecoverUpdateTime
        {
            get { return m_recoverUpdateTime; }
            set { m_recoverUpdateTime = value; }
        }
        [SerializeField]
        [Tooltip("Throttle updates to match elapsed real world times to elapsed simulation time.")]
        private bool m_recoverUpdateTime = true;


        /// <summary>
        /// Apply time scaling received on server frames to the Unity time.
        /// </summary>
        public bool ApplyServerTimeScale
        {
            get { return m_applyServerTimeScale; }
            set { m_applyServerTimeScale = value; }
        }
        [SerializeField]
        [Tooltip("Apply time scaling received on server frames to the Unity time.")]
        private bool m_applyServerTimeScale = true;

        /// <summary>
        /// Is player data synced? You can override this per-player by setting ksIServerPlayer.IsVisible.
        /// </summary>
        public bool PlayersVisible
        {
            get { return m_playersVisible; }
            set { m_playersVisible = value; }
        }
        [SerializeField]
        [Tooltip("Is player data synced? You can override this per-player by setting ksIServerPlayer.IsVisible.")]
        private bool m_playersVisible = true;

        /// <summary>Are players allowed to spawn entities?</summary>
        public bool AllowPlayerSpawning
        {
            get { return m_allowPlayerSpawning; }
        }
        [SerializeField]
        [Tooltip("Are players allowed to spawn entities?")]
        private bool m_allowPlayerSpawning;

        /// <summary>
        /// Player limit
        /// </summary>
        public uint PlayerLimit
        {
            get { return m_playerLimit; }
            set { m_playerLimit = value; }
        }
        [SerializeField]
        [Tooltip("Number of connections the server will accept before blocking connections. Use 0 for unlimited.")]
        private uint m_playerLimit = 0;

        /// <summary>Default predictor for entities that do not use input prediction.</summary>
        public ksPredictor DefaultEntityPredictor
        {
            get { return m_defaultEntityPredictor; }
            set { m_defaultEntityPredictor = value; }
        }
        [SerializeField]
        [Tooltip("Default predictor for entities that do not use input prediction.")]
        private ksPredictor m_defaultEntityPredictor;

        /// <summary>Default predictor for entities with player controllers that use input prediction.</summary>
        public ksPredictor DefaultEntityControllerPredictor
        {
            get { return m_defaultEntityControllerPredictor; }
            set { m_defaultEntityControllerPredictor = value; }
        }
        [SerializeField]
        [Tooltip("Default predictor for entities with player controllers that use input prediction.")]
        private ksPredictor m_defaultEntityControllerPredictor;

        /// <summary>Default predictor for room properties.</summary>
        public ksPredictor DefaultRoomPredictor
        {
            get { return m_defaultRoomPredictor; }
            set { m_defaultRoomPredictor = value; }
        }
        [SerializeField]
        [Tooltip("Default predictor for room properties.")]
        private ksPredictor m_defaultRoomPredictor;

        /// <summary>Default predictor for player properties.</summary>
        public ksPredictor DefaultPlayerPredictor
        {
            get { return m_defaultPlayerPredictor; }
            set { m_defaultPlayerPredictor = value; }
        }
        [SerializeField]
        [Tooltip("Default predictor for player properties.")]
        private ksPredictor m_defaultPlayerPredictor;

        [Tooltip("Override the precision used for all entities when syncing transform data from servers to clients.")]
        public ksTransformPrecisions TransformPrecisionOverrides = new ksTransformPrecisions();

        /// <summary>
        /// Number of seconds between cluster connection attempts.
        /// </summary>
        public byte ClusterReconnectDelay
        {
            get { return m_clusterReconnectDelay; }
            set { m_clusterReconnectDelay = value; }
        }
        [SerializeField]
        [Tooltip("Number of seconds between cluster connection attempts.")]
        private byte m_clusterReconnectDelay = 10;

        [SerializeField]
        [Tooltip("If true, shutsdown the room if it loses its cluster connection and cannot reconnect.")]
        private bool m_shutdownWithoutCluster;

        /// <summary>
        /// Number of failed cluster connection attempts to make before shuttingdown. < 0: unlimited
        /// </summary>
        public int ClusterReconnectAttempts
        {
            get { return m_shutdownWithoutCluster ? m_clusterReconnectAttempts : -1; }
            set 
            {
                if (value < 0)
                {
                    m_shutdownWithoutCluster = false;
                }
                else
                {
                    m_shutdownWithoutCluster = true;
                    m_clusterReconnectAttempts = (byte)Math.Min(value, 255);
                }
            }
        }
        [SerializeField]
        [Tooltip("Number of failed cluster connection attempts to make before shuttingdown.")]
        private byte m_clusterReconnectAttempts = 3;

        public float RequiredCoreCount { 
            get { return m_requiredCoreCount; }
            set
            {
                if (value >= 0.25f && value <= 32f)
                {
                    m_requiredCoreCount = value;
                }
                else
                {
                    ksLog.Warning(this, "Required core count must be in the range of 0.25 to 32");
                }
            }
        }
        [SerializeField]
        [Tooltip("Minimum number of cores required for cloud instances. This value in conjunction with Required Memory will be used to select a recommended instance size during launch.")]
        [ksResourceRange(0.25f, 32f, 0.25f, 8f)]
        private float m_requiredCoreCount = 0.5f;

        public float RequiredMemory { 
            get { return m_requiredMemory; }
            set
            {
                if (value >= 0.25f && value <= 32f)
                {
                    m_requiredMemory = value;
                }
                else
                {
                    ksLog.Warning(this, "Required memory must be in the range of 0.25 to 32");
                }
            }
        }
        [SerializeField]
        [Tooltip("Minimum amount of memory in GB required for cloud instances. This value in conjunction with Required Core Count will be used to select a recommended instance size during launch.")]
        [ksResourceRange(0.25f, 32f, 0.25f, 8f)]
        private float m_requiredMemory = 0.5f;

        public int PhysicsThreads { get { return m_physicsThreads; } }
        [SerializeField]
        [Tooltip("Number of threads to allocate to physics simulations. Live servers will limit the total number of concurrent threads to 32.")]
        [Range(1, 32)]
        private int m_physicsThreads = 4;

        public int NetworkThreads { get { return m_networkThreads; } }
        [SerializeField]
        [Tooltip("Number of threads to allocate to network connections. Live servers will limit the total number of concurrent threads to 32.")]
        [Range(1, 32)]
        private int m_networkThreads = 4;

        public int EncodingThreads { get { return m_encodingThreads; } }
        [SerializeField]
        [Tooltip("Number of threads to allocate to encoding. Live servers will limit the total number of concurrent threads to 32.")]
        [Range(1, 32)]
        private int m_encodingThreads = 4;

        /// <summary>
        /// Max number of concurrent authentications. 0 for unlimited.
        /// </summary>
        public int MaxConcurrentAuthentications
        {
            get { return m_maxConcurrentAuthentications; }
        }
        [SerializeField]
        [Tooltip("Max number of concurrent authentications. 0 for unlimited.")]
        [Range(0, 32)]
        private int m_maxConcurrentAuthentications = 4;

        /// <summary>When true, player controllers update use multithreading to update in parallel.</summary>
        public bool ParallelControllerUpdates { get { return m_parallelControllerUpdates; } }
        [SerializeField]
        [Tooltip("When checked, player controllers update use multithreading to update in parallel.")]
        private bool m_parallelControllerUpdates = false;

        /// <summary>
        /// When true, entity transform and property updates for client-owned entities are applied in parallel on the
        /// server using multithreading.
        /// </summary>
        public bool ParallelOwnedEntityUpdates { get { return m_parallelOwnedEntityUpdates; } }
        [SerializeField]
        [Tooltip("When checked, entity transform and property updates for client-owned entities are applied in parallel on " +
            "the server using multithreading.")]
        private bool m_parallelOwnedEntityUpdates;

        /// <summary>
        /// Port to use for local server.
        /// </summary>
        public ushort LocalServerPort
        {
            get { return m_port; }
            set { m_port = value; }
        }
        [Header("Local Server Testing")]
        [SerializeField]
        [Tooltip("Port used when starting a local server.")]
        private ushort m_port = 8000;

        public ksRoomType()
        {
            m_defaultEntityPredictor = InitialPredictor;
            m_defaultEntityControllerPredictor = InitialControllerPredictor;
        }

        /// <summary>
        /// Gets room info for connecting to a room of this type on localhost.
        /// </summary>
        /// <param name="host">Host to connect to</param>
        /// <param name="port">Port to connect to. If zero, <see cref="LocalServerPort"/> is used.</param>
        /// <returns>Connection info</returns>
        public ksRoomInfo GetRoomInfo(string host = "127.0.0.1", ushort port = 0)
        {
            if (port == 0)
            {
                port = m_port;
            }
            ksRoomInfo roomInfo = new ksRoomInfo(host, port);
            roomInfo.Type = gameObject.name;
            roomInfo.Scene = gameObject.scene.name;
            return roomInfo;
        }

        /// <summary>
        /// Checks if a local server is running this room type. This can only detect local servers started through
        /// Unity and can only be used in the editor.
        /// </summary>
        /// <returns>true if a local server is running this room type.</returns>
        public bool IsLocalServerRunning()
        {
            return m_localServerChecker == null ? false : m_localServerChecker(gameObject.scene.name, gameObject.name);
        }
    }
}
