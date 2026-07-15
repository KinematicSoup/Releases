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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.Serialization;
using KS.SF.Reactor;
using KS.SF.Reactor.Client;
using KS.SF.Unity.Editor;
using KS.SF.Unity;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>Scene Fusion config.</summary>
    public partial class sfConfig : ScriptableObject
    {
        /// <summary>Options for when to show user terrain brushes.</summary>
        public enum ShowTerrainBrushOptions
        {
            [InspectorName("Always")]
            ALWAYS = 0,
            [InspectorName("When Terrain Is Selected")]
            WHEN_TERRAIN_SELECTED = 1,
            [InspectorName("Never")]
            NEVER = 2
        }

        /// <summary>Prefab sync mode.</summary>
        public enum PrefabSyncMode
        {
            /// <summary>Prefab editing during a session is disabled.</summary>
            [InspectorName("Off")]
            OFF = 0,
            /// <summary>
            /// Prefab editing during a session is disabled. New prefabs created during a session are synced.
            /// </summary>
            [InspectorName("Create Only")]
            CREATE_ONLY = 1,
            /// <summary>
            /// (Experimental) Prefab editing during a session is enabled. All referenced prefabs and prefabs modified
            /// during a session are synced. Prefab deletion will not sync.
            /// </summary>
            [InspectorName("Full (Experimental)")]
            FULL = 2
        }

        /// <summary>Log level.</summary>
        public enum LogLevel
        {
            [InspectorName("None")]
            NONE = 0,
            [InspectorName("Errors")]
            ERRORS = 1,
            [InspectorName("Warnings")]
            WARNINGS = 2,
            [InspectorName("Info")]
            INFO = 3,
            [InspectorName("Debug")]
            DEBUG = 4
        }

        /// <summary>Logging flags to control which sfObject events are logged.</summary>
        [Flags]
        public enum EventLoggingFlags : ushort
        {
            [InspectorName("None")]
            NONE = 0,
            [InspectorName("Create")]
            CREATE = 1 << 0,
            [InspectorName("Confirm Create")]
            CONFIRM_CREATE = 1 << 1,
            [InspectorName("Delete")]
            DELETE = 1 << 2,
            [InspectorName("Confirm Delete")]
            CONFIRM_DELETE = 1 << 3,
            [InspectorName("Parent Change")]
            PARENT_CHANGE = 1 << 4,
            [InspectorName("Lock")]
            LOCK = 1 << 5,
            [InspectorName("Unlock")]
            UNLOCK = 1 << 6,
            [InspectorName("Lock Owner Change")]
            LOCK_OWNER_CHANGE = 1 << 7,
            [InspectorName("Direct Lock Change")]
            DIRECT_LOCK_CHANGE = 1 << 8,
            [InspectorName("Property Change")]
            PROPERTY_CHANGE = 1 << 9,
            [InspectorName("Remove Field")]
            REMOVE_FIELD = 1 << 10,
            [InspectorName("List Add")]
            LIST_ADD = 1 << 11,
            [InspectorName("List Remove")]
            LIST_REMOVE = 1 << 12,
            [InspectorName("All Hierarchy")]
            ALL_HIERARCHY = CREATE | CONFIRM_CREATE | DELETE | CONFIRM_DELETE | PARENT_CHANGE,
            [InspectorName("All Locks")]
            ALL_LOCKS = LOCK | UNLOCK | DIRECT_LOCK_CHANGE | LOCK_OWNER_CHANGE,
            [InspectorName("All Properties")]
            ALL_PROPERTIES = PROPERTY_CHANGE | REMOVE_FIELD | LIST_ADD | LIST_REMOVE,
            [InspectorName("All")]
            ALL = 0xFFFF
        }

        /// <summary>Event handler for when a value of type T changes.</summary>
        /// <param name="value">value that changed.</param>
        public delegate void ChangeHandler<T>(T value);

        /// <summary>URLs</summary>
#if KS_DEVELOPMENT
        [Serializable]
#endif
        public class URLConfigs
        {
            public string WebAPI = "https://matchmaker-console.kinematicsoup.com/api";
            public string WebConsole = "https://console.kinematicsoup.com";
            public string Downloads = "https://download.kinematicsoup.com/scene-fusion";
            public string Documentation = "https://docs.kinematicsoup.com/SceneFusion/unity/getting_started.html";
            public string Upgrade = "https://console.kinematicsoup.com";
            public string Discord = "https://discord.gg/u3wSJGZZ76";
            public string Youtube = "https://www.youtube.com/kinematicsoup";
            public string SupportEmail = "support@kinematicsoup.com";
        };

        /// <summary>Icon, widget offsets and online indicator toggle.</summary>
        [Serializable]
        public class UISettings
        {
            [Tooltip("Show user cameras showing the location of other users.")]
            public bool ShowUserCameras = true;
            [SerializeField]
            [Tooltip("Render objects selected by other users with a lock shader in the user's color.")]
            private bool m_showLockShaders = true;
            // Used to detect changes in OnValidate.
            private bool m_oldShowLockShaders = true;
            public bool ShowLockShaders 
            { 
                get 
                { 
                    return m_showLockShaders;
                }

                set
                {
                    if (m_showLockShaders != value)
                    {
                        m_showLockShaders = value;
                        m_oldShowLockShaders = value;
                        if (OnToggleLockShaders != null)
                        {
                            OnToggleLockShaders(value);
                        }
                    }
                } 
            }

            public ShowTerrainBrushOptions ShowUserTerrainBrushes
            {
                get { return m_showUserTerrainBrushes; }
                set
                {
                    if (m_showUserTerrainBrushes != value)
                    {
                        m_showUserTerrainBrushes = value;
                    }
                }
            }

            [SerializeField]
            [Tooltip("When to show the terrain brushes of other users who are editing terrain.")]
            private ShowTerrainBrushOptions m_showUserTerrainBrushes = ShowTerrainBrushOptions.WHEN_TERRAIN_SELECTED;
            // Used to detect changes in OnValidate.
            private ShowTerrainBrushOptions m_oldShowUserTerrainBrushes = ShowTerrainBrushOptions.WHEN_TERRAIN_SELECTED;
            /// <summary>Offset from the right side for Scene Fusion icons in the hierarchy window.</summary>
            public float HierarchyIconOffset
            {
                get { return m_hierarchyIconOffset; }
                set
                {
                    if (m_hierarchyIconOffset != value)
                    {
                        m_hierarchyIconOffset = value;
                        m_oldHierarchyIconOffset = value;
                        if (OnHierarchyIconOffsetChange != null)
                        {
                            OnHierarchyIconOffsetChange(value);
                        }
                    }
                }
            }

            [Tooltip("Offset from the right side for Scene Fusion icons in the hierarchy window.")]
            [FormerlySerializedAs("HierarchyIconOffset")]
            [SerializeField]
            private float m_hierarchyIconOffset;
            private float m_oldHierarchyIconOffset;// For detecting changes in OnValidate.

            /// <summary>Offset from the lower right corner for Scene Fusion icons in the project browser.</summary>
            public Vector2 ProjectBrowserIconOffset
            {
                get { return m_projectBrowserIconOffset; }
                set
                {
                    if (m_projectBrowserIconOffset != value)
                    {
                        m_projectBrowserIconOffset = value;
                        m_oldProjectBrowserIconOffset = value;
                        if (OnProjectBrowserIconOffsetChange != null)
                        {
                            OnProjectBrowserIconOffsetChange(value);
                        }
                    }
                }
            }

            [Tooltip("Offset from the lower right corner for Scene Fusion icons in the project browser.")]
            [SerializeField]
            private Vector2 m_projectBrowserIconOffset;
            private Vector2 m_oldProjectBrowserIconOffset;// For detecting changes in OnValidate.

            // Events

            /// <summary>Invoked when ShowLockShaders is toggled.</summary>
            public event ChangeHandler<bool> OnToggleLockShaders;

            /// <summary>Invoked when ShowUserTerrainBrushes changes.</summary>
            public event ChangeHandler<ShowTerrainBrushOptions> OnShowUserTerrainBrushesChange;

            /// <summary>Invoked when HierarchyIconOffset changes.</summary>
            public event ChangeHandler<float> OnHierarchyIconOffsetChange;

            /// <summary>Invoked when ProjectIconOffset changes.</summary>
            public event ChangeHandler<Vector2> OnProjectBrowserIconOffsetChange;

            /// <summary>Initialization</summary>
            internal void Initialize()
            {
                m_oldShowLockShaders = m_showLockShaders;
                m_oldShowUserTerrainBrushes = m_showUserTerrainBrushes;
                m_oldHierarchyIconOffset = m_hierarchyIconOffset;
                m_oldProjectBrowserIconOffset = m_projectBrowserIconOffset;
            }

            /// <summary>Invokes change events for property values that changed.</summary>
            internal void InvokeChangeEvents()
            {
                if (m_showLockShaders != m_oldShowLockShaders)
                {
                    m_oldShowLockShaders = m_showLockShaders;
                    if (OnToggleLockShaders != null)
                    {
                        OnToggleLockShaders(m_showLockShaders);
                    }
                }
                if (m_showUserTerrainBrushes != m_oldShowUserTerrainBrushes)
                {
                    m_oldShowUserTerrainBrushes = m_showUserTerrainBrushes;
                    if (OnShowUserTerrainBrushesChange != null)
                    {
                        OnShowUserTerrainBrushesChange(m_showUserTerrainBrushes);
                    }
                }
                if (m_hierarchyIconOffset != m_oldHierarchyIconOffset)
                {
                    m_oldHierarchyIconOffset = m_hierarchyIconOffset;
                    if (OnHierarchyIconOffsetChange != null)
                    {
                        OnHierarchyIconOffsetChange(m_hierarchyIconOffset);
                    }
                }
                if (m_projectBrowserIconOffset != m_oldProjectBrowserIconOffset)
                {
                    m_oldProjectBrowserIconOffset = m_projectBrowserIconOffset;
                    if (OnProjectBrowserIconOffsetChange != null)
                    {
                        OnProjectBrowserIconOffsetChange(m_projectBrowserIconOffset);
                    }
                }
            }
        }

        /// <summary>Network timeout and receive buffer settings</summary>
        [Serializable]
        public partial class NetworkSettings
        {
            [Tooltip("TCP Timeout in seconds (minimum 5 seconds).")]
            public int Timeout = 30;

            [Tooltip("Receive buffer size in KB (minimum 8 KB).")]
            public int ReceiveBufferSize = 1024;
        }

        [Serializable]
        public class PerformanceSettings
        {
            [Tooltip("Maximum number of times per second to sync property value updates.")]
            [Range(1,60)]
            public uint PropertySyncRate = 10;
            private uint m_oldPropertySyncRate;
            [Tooltip("Maximum number of times per second to sync terrain updates.")]
            [Range(1, 60)]
            public float TerrainSyncRate = 2;

            /// <summary>Invoked when <see cref="PropertySyncRate"/> changes.</summary>
            public event ChangeHandler<uint> OnPropertySyncRateChange;

            /// <summary>Initialization</summary>
            internal void Initialize()
            {
                m_oldPropertySyncRate = PropertySyncRate;
            }

            /// <summary>Invokes change events for property values that changed.</summary>
            internal void InvokeChangeEvents()
            {
                if (PropertySyncRate != m_oldPropertySyncRate)
                {
                    m_oldPropertySyncRate = PropertySyncRate;
                    if (OnPropertySyncRateChange != null)
                    {
                        OnPropertySyncRateChange(PropertySyncRate);
                    }
                }
            }
        }

        [Serializable]
        public class LogSettings
        {
            /// <summary>Which sfObject events to log.</summary>
            [Tooltip("Which sfObject events to log.")]
            public EventLoggingFlags ObjectEventLogging = EventLoggingFlags.NONE;

            /// <summary>Controls what is written in client log files.</summary>
            public LogLevel Level
            {
                get { return m_level; }
                set
                {
                    if (m_level != value)
                    {
                        m_level = value;
                        m_oldLevel = value;
                        if (OnLevelChange != null)
                        {
                            OnLevelChange(value);
                        }
                    }
                }
            }
            [SerializeField]
            private LogLevel m_level = LogLevel.WARNINGS;
            // Used to detect changes in OnValidate.
            private LogLevel m_oldLevel = LogLevel.WARNINGS;

            /// <summary>Amount of log files to keep.</summary>
            [Tooltip("Ammount of log files to keep.")]
            public int LogHistorySize = 5;

            /// <summary>Is verbose logging enabled?</summary>
            public bool Verbose
            {
                get { return m_verbose; }
                set
                {
                    if (m_verbose != value)
                    {
                        m_verbose = value;
                        m_oldVerbose = value;
                        if (OnVerboseChange != null)
                        {
                            OnVerboseChange(value);
                        }
                    }
                }
            }

            /// <summary>Is verbose logging enabled?</summary>
            [SerializeField]
            [Tooltip("Enable verbose logging")]
            private bool m_verbose = false;
            // Used to detect changes in OnValidate.
            private bool m_oldVerbose = false;

            /// <summary>Invoked when Level changes.</summary>
            public event ChangeHandler<LogLevel> OnLevelChange;

            /// <summary>Invoked when Verbose changes.</summary>
            public event ChangeHandler<bool> OnVerboseChange;

            /// <summary>Checks if an EventLoggingFlags is set.</summary>
            /// <param name="flag">flag to check for.</param>
            /// <returns>true if the flag is set.</returns>
            public bool HasFlag(EventLoggingFlags flag)
            {
                return (ObjectEventLogging & flag) != EventLoggingFlags.NONE;
            }

            /// <summary>Initialization</summary>
            internal void Initialize()
            {
                m_oldLevel = m_level;
                m_oldVerbose = m_verbose;
            }

            /// <summary>Invokes change events for property values that changed.</summary>
            internal void InvokeChangeEvents()
            {
                if (m_level != m_oldLevel)
                {
                    m_oldLevel = m_level;
                    if (OnLevelChange != null)
                    {
                        OnLevelChange(m_level);
                    }
                }
                if (m_verbose != m_oldVerbose)
                {
                    m_oldVerbose = m_verbose;
                    if (OnVerboseChange != null)
                    {
                        OnVerboseChange(m_verbose);
                    }
                }
            }
        }

        /// <summary>
        /// If true, prevents changing the following settings that should not change during a session:
        /// - SyncPrefabs
        /// </summary>
        [NonSerialized]
        public bool SessionSettingsLocked = false;

        /// <summary>Scene Fusion version</summary>
        public ksVersion Version
        {
            get { return m_version; }
        }

#if KS_DEVELOPMENT
        [SerializeField]
#endif
        private ksSerializableVersion m_version = new ksVersion(2, 0, 5);

        /// <summary>Scene Fusion server version</summary>
        public ksVersion ServerVersion
        {
            get { return m_serverVersion; }
        }

#if KS_DEVELOPMENT
        [SerializeField]
#endif
        private ksSerializableVersion m_serverVersion = new ksVersion(2, 5, 4);

        public bool Debug = true;
        public string LastVersion = "0.0.0";
#if U2U
        public string UnrealVersion = "4.22.0";
#endif

        /// <summary>Full version string including of the form [Version][Build Identifier].</summary>
        public string FullVersion
        {
            get { return Version + sfPackageInfo.BUILD_IDENTIFIER; }
        }

        public bool ShowGettingStartedScreen = true;

        /// <summary>
        /// If true, referenced materials and materiald modified during a session will be synced. The value for this
        /// setting is determined by the session creator and cannot be changed during a session.
        /// </summary>
        public bool SyncMaterials
        {
            get { return m_syncMaterials; }
            set
            {
                if (SessionSettingsLocked)
                {
                    ksLog.Warning(this, "Cannot set SyncMaterials while in a session.");
                }
                else
                {
                    m_syncMaterials = value;
                }
            }
        }
        [Tooltip("If checked, referenced materials and materials modified during a session will be synced. The value " +
            "for this setting is determined by the session creator and cannot be changed during a session.")]
        [SerializeField]
        private bool m_syncMaterials = true;

        /// <summary>
        /// Prefab sync mode. The value for this setting is determined by the session creator and cannot be changed
        /// during a session.
        /// </summary>
        public PrefabSyncMode SyncPrefabs
        {
            get { return m_syncPrefabs; }
            set
            {
                if (SessionSettingsLocked)
                {
                    ksLog.Warning(this, "Cannot set SyncPrefabs while in a session.");
                }
                else
                {
                    m_syncPrefabs = value;
                }
            }
        }
        [Tooltip(
@"Prefab sync mode. The value for this setting is determined by the session creator and cannot be changed during a session.

- Off: Prefab editing during a session is disabled.
- Create Only: Prefab editing during a session is disabled. New prefabs created during a session are synced.
- Full (Experimental): Prefab editing during a session is enabled. All referenced prefabs and prefabs modified during a session are synced. Prefab deletion will not sync.
")]
        [SerializeField]
        private PrefabSyncMode m_syncPrefabs = PrefabSyncMode.CREATE_ONLY;

        public URLConfigs Urls = new URLConfigs();
        public UISettings UI = new UISettings();
        public NetworkSettings Network = new NetworkSettings();
        public PerformanceSettings Performance = new PerformanceSettings();
        public LogSettings Logging = new LogSettings();

        private const string CONFIG_PATH = "Editor/SceneFusionConfig.asset";

        /// <summary></summary>
        /// <returns>
        /// singleton instance. Load the config from the asset database. If it does not exist, then
        /// create a new config.
        /// </returns>
        public static sfConfig Get()
        {
            if (m_instance == null)
            {
                string path = sfPaths.AssetRoot + CONFIG_PATH;
                ksLog.Debug("Loading Scene Fusion Config from: " + path);
                m_instance = AssetDatabase.LoadAssetAtPath<sfConfig>(path);
                if (m_instance == null)
                {
                    ksLog.Debug("Creating Scene Fusion Config at: " + path);
                    m_instance = CreateInstance<sfConfig>();
                    ksPathUtils.Create(path);
                    AssetDatabase.CreateAsset(m_instance, path);
                }
            }
            return m_instance;
        }
        private static sfConfig m_instance;

        /// <summary>Create an instance of this class</summary>
        private void OnEnable()
        {
            if (EditorUtility.IsPersistent(this))
            {
                hideFlags = HideFlags.None;
                if (m_instance == null)
                {
                    m_instance = this;
                }
            }
            UI.Initialize();
            Logging.Initialize();
            Performance.Initialize();
        }

        /// <summary>Invokes change events for properties that changed.</summary>
        private void OnValidate()
        {
            // We sometimes destroy game objects in response to these events and Unity does not let you destroy objects
            // from OnValidate, so we delay until the end of the frame.
            EditorApplication.delayCall += InvokeChangeEvents;
        }

        /// <summary>Invokes change events for property values that changed.</summary>
        private void InvokeChangeEvents()
        {
            UI.InvokeChangeEvents();
            Logging.InvokeChangeEvents();
            Performance.InvokeChangeEvents();
        }
    }
}
