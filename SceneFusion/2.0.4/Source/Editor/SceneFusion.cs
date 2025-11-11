using System;
using UnityEngine;
using UnityEditor;
using KS.SceneFusion2.Client;
using KS.SceneFusion;
using KS.SF.Unity.Editor;
using KS.SF.Reactor;
using KS.SF.Reactor.Client;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>Scene Fusion entry point. Does initialization and runs the update loop.</summary>
    public class SceneFusion : ksSingleton<SceneFusion>
    {
        /// <summary>Service</summary>
        public sfService Service
        {
            get { return m_service; }
        }
        private sfService m_service;

        /// <summary>Scene Fusion version</summary>
        public ksVersion Version
        {
            get { return sfConfig.Get().Version; }
        }

        /// <summary>
        /// Did we reconnect to the current session after disconnecting temporarily to recompile or enter play mode?
        /// </summary>
        public bool Reconnected
        {
            get { return m_isReconnect && m_running; }
        }

        private sfLogFile m_fileLogger;
        [SerializeField]
        private string m_sessionLogId;

        [NonSerialized]
        private long m_lastTime;
        [NonSerialized]
        private bool m_running = false;
        [NonSerialized]
        private bool m_isReconnect = false;
        [SerializeField]
        private bool m_isFirstLoad = true;
        [SerializeField]
        private sfSessionInfo m_reconnectInfo;
        [SerializeField]
        private string m_reconnectToken;
        [SerializeField]
        private sfOnlineMenuUI m_activeSessionUI;

        /// <summary>Initialization</summary>
        protected override void Initialize()
        {
            m_service = new sfService();
            m_service.OnConnect += OnConnect;
            m_service.OnDisconnect += OnDisconnect;
#if MOCK_WEB_SERVICE
            m_service.WebService = new sfMockWebService();
#else
            m_service.WebService = sfWebService.Get();
#endif
            sfGuidManager.Get().RegisterEventListeners();

            if (m_activeSessionUI == null)
            {
                m_activeSessionUI = new sfOnlineMenuUI();
            }
            sfOnlineMenu.DrawSettings = m_activeSessionUI.Draw;
            sfSessionsMenu.PreSessionCheck = PreSessionCheck;
            sfUI.Get().ViewportGetter = GetViewport;

            // Set icons for our scripts.
            if (sfTextures.Question == null || sfTextures.Logo == null)
            {
                // When reinstalling the package, texture may not be loaded yet so we delay...
                EditorApplication.delayCall += SetScriptIcons;
            }
            else
            {
                SetScriptIcons();
            }

            // Register the config translator first so the config sfObject is created before anything else.
            sfConfigTranslator configTranslator = new sfConfigTranslator();
            configTranslator.OnConfigSynced += StartConfigDependentManagers;
            sfObjectEventDispatcher.Get().Register(sfType.Config, configTranslator);

            sfSceneTranslator sceneTranslator = new sfSceneTranslator();
            sfObjectEventDispatcher.Get().Register(sfType.Scene, sceneTranslator);
            sfObjectEventDispatcher.Get().Register(sfType.SceneLock, sceneTranslator);
            sfObjectEventDispatcher.Get().Register(sfType.SceneSubscriptions, sceneTranslator);
            sfObjectEventDispatcher.Get().Register(sfType.Hierarchy, sceneTranslator);

            sfLightingTranslator lightingTranslator = new sfLightingTranslator();
            sfObjectEventDispatcher.Get().Register(sfType.LightmapSettings, lightingTranslator);
            sfObjectEventDispatcher.Get().Register(sfType.RenderSettings, lightingTranslator);

            sfObjectEventDispatcher.Get().Register(sfType.OcclusionSettings, new sfOcclusionTranslator());
            sfObjectEventDispatcher.Get().Register(sfType.GameObject, new sfGameObjectTranslator());
            sfObjectEventDispatcher.Get().Register(sfType.Component, new sfComponentTranslator());
            sfObjectEventDispatcher.Get().Register(sfType.Terrain, new sfTerrainTranslator());
            sfObjectEventDispatcher.Get().Register(sfType.TerrainBrush, new sfTerrainBrushTranslator());
            sfObjectEventDispatcher.Get().Register(sfType.Prefab, new sfPrefabTranslator());

            // The asset translator should be registered after all other UObject translators so other translators get a
            // chance to handle sfObjectEventDispatcher.Create events first.
            sfObjectEventDispatcher.Get().Register(sfType.Asset, new sfAssetTranslator());

            sfObjectEventDispatcher.Get().Register(sfType.AssetPath, new sfAssetPathTranslator());

            sfAvatarTranslator avatarTranslator = new sfAvatarTranslator();
            sfObjectEventDispatcher.Get().Register(sfType.Avatar, avatarTranslator);
            sfUI.Get().OnFollowUserCamera = avatarTranslator.OnFollow;
            sfUI.Get().OnGotoUserCamera = avatarTranslator.OnGoTo;
            avatarTranslator.OnUnfollow = sfUI.Get().UnfollowUserCamera;

            sfObjectEventDispatcher.Get().InitializeTranslators();

            if (m_reconnectInfo != null && m_reconnectInfo.ProjectId != -1)
            {
                sfConfig.Get().Logging.OnLevelChange += SetLogLevel;
                SetLogLevel(sfConfig.Get().Logging.Level);

                // It is not safe to join a session until all ksSingletons are finished initializing, so we wait till
                // the end of the frame.
                EditorApplication.delayCall += () =>
                {
                    m_isReconnect = true;
                    if (m_service.WebService != null)
                    {
                        m_service.WebService.SFToken = m_reconnectToken;
                    }
                    sfActivityIndicator.Get().AddTask();
                    m_service.JoinSession(m_reconnectInfo);
                    m_reconnectInfo = null;
                    m_reconnectToken = null;
                };
            }

            m_lastTime = DateTime.Now.Ticks;
            EditorApplication.update += Update;
            EditorApplication.quitting += OnQuit;

            // Show getting started window
            if ((sfConfig.Get().ShowGettingStartedScreen && m_isFirstLoad) ||
                sfConfig.Get().Version.ToString() != sfConfig.Get().LastVersion)
            {
                sfGettingStartedWindow.Get().Open();
            }
            m_isFirstLoad = false;
        }

        /// <summary>Called when the editor is closing. Disconnects from the session.</summary>
        private void OnQuit()
        {
            if (m_service != null && m_service.IsConnected)
            {
                m_service.LeaveSession();
                Stop();
            }
        }

        /// <summary>Unity on disable. Disconnects from the session.</summary>
        private void OnDisable()
        {
            if (m_service != null && m_service.IsConnected)
            {
                if (EditorApplication.isCompiling)
                {
                    ksLog.Debug(this, "Disconnecting temporarily to recompile.");
                }
                else if (!EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    ksLog.Debug(this, "Disconnecting temporarily to enter play mode.");
                }
                else
                {
                    ksLog.Debug(this, "Disconnecting temporarily.");
                }
                m_reconnectInfo = m_service.SessionInfo;
                if (m_service.WebService != null)
                {
                    m_reconnectToken = m_service.WebService.SFToken;
                }
                m_service.LeaveSession(true);
                Stop();
            }
        }

        /// <summary>Sets icons on our scripts.</summary>
        private void SetScriptIcons()
        {
            ksIconUtility.Get().SetIcon<sfMissingPrefab>(sfTextures.Question);
            ksIconUtility.Get().SetIcon<sfMissingComponent>(sfTextures.Question);
            // Set the icon even if it already has an icon, as the logo icon will change if the editor theme changes.
            ksIconUtility.Get().SetDisabledIcon<sfGuidList>(sfTextures.Logo, true);
            ksIconUtility.Get().SetDisabledIcon<sfIgnore>(sfTextures.Logo, true);
        }

        /// <summary>
        /// If the user is starting a session, prompts the user to save the untitled scene if there is one. Displays a
        /// dialog asking the user if they want to change serialization modes if the serialization mode is not force
        /// text. Starts file logging for the session.
        /// </summary>
        /// <param name="sessionInfo">sessionInfo for the session to join, or null if starting a new session.</param>
        /// <returns>
        /// true to start/join the session. False if the user had an untitled scene they did not save.
        /// </returns>
        private bool PreSessionCheck(sfSessionInfo sessionInfo)
        {
            if (sessionInfo == null)
            {
                sfSceneTranslator translator = sfObjectEventDispatcher.Get().GetTranslator<sfSceneTranslator>(
                    sfType.Scene);
                if (translator != null && !translator.PromptSaveUntitledScene())
                {
                    return false;
                }
            }
            if (EditorSettings.serializationMode != SerializationMode.ForceText)
            {
                if (!PromptChangeSerializationMode())
                {
                    return false;
                }
                EditorSettings.serializationMode = SerializationMode.ForceText;
            }

            // Room Id is 0 when doing a manual LAN connection.
            m_sessionLogId = sessionInfo == null ? "new." + DateTime.Now.Ticks : 
                sessionInfo.RoomInfo.Id == 0 ? "manual." + DateTime.Now.Ticks : sessionInfo.RoomInfo.Id.ToString();
            sfConfig.Get().Logging.OnLevelChange += SetLogLevel;
            SetLogLevel(sfConfig.Get().Logging.Level);

            sfActivityIndicator.Get().AddTask();

            return true;
        }

        /// <summary>Prompts the user to change the serialization mode to ForceText.</summary>
        /// <returns>true if the user agreed to change the serialization mode.</returns>
        private bool PromptChangeSerializationMode()
        {
            return EditorUtility.DisplayDialog("Change Serialization Mode",
                "Serialization mode 'ForceText' is required to use Scene Fusion. The current serialization mode is '" +
                EditorSettings.serializationMode + "'. Do you want to continue and change the serialization mode?",
                "OK", "Cancel");
        }

        /// <summary>Gets the the viewport rect from a scene view.</summary>
        /// <param name="sceneView">sceneView to viewport from.</param>
        /// <returns>viewport.</returns>
        private Rect GetViewport(SceneView sceneView)
        {
#if UNITY_2022_2_OR_NEWER
            return sceneView.cameraViewport;
#else
            Rect rect = sceneView.camera.pixelRect;
            rect.width = sceneView.position.width;
            // Remove the height of the scene view toolbar (26.2) from the height of the scene view.
            rect.height = sceneView.position.height - 26.2f;
            return rect;
#endif
        }

        /// <summary>Sets log level for the file logger.</summary>
        /// <param name="level">level to set.</param>
        private void SetLogLevel(sfConfig.LogLevel level)
        {
            if (level == sfConfig.LogLevel.NONE)
            {
                if (m_fileLogger != null)
                {
                    ksLog.UnregisterHandler(m_fileLogger.Write);
                    Application.logMessageReceived -= m_fileLogger.LogUnityException;
                    m_fileLogger.Close();
                }
                return;
            }
            if (m_fileLogger == null)
            {
                m_fileLogger = new sfLogFile();
                m_fileLogger.StartSessionLog(m_sessionLogId);
            }
            ksLog.Level levelFlags;
            switch (level)
            {
                case sfConfig.LogLevel.ERRORS: 
                    levelFlags = ksLog.Level.ERROR | ksLog.Level.FATAL; break;
                case sfConfig.LogLevel.WARNINGS:
                    levelFlags = ksLog.Level.WARNING | ksLog.Level.ERROR | ksLog.Level.FATAL; break;
                case sfConfig.LogLevel.INFO:
                    levelFlags = ksLog.Level.INFO | ksLog.Level.WARNING | ksLog.Level.ERROR | ksLog.Level.FATAL; break;
                case sfConfig.LogLevel.DEBUG:
                default:
                    levelFlags = ksLog.Level.ALL; break;
            }
            ksLog.RegisterHandler(m_fileLogger.Write, levelFlags);
            Application.logMessageReceived += m_fileLogger.LogUnityException;
        }

        /// <summary>Called after connecting to a session.</summary>
        /// <param name="session"></param>
        /// <param name="errorMessage"></param>
        public void OnConnect(sfSession session, string errorMessage)
        {
            sfActivityIndicator.Get().RemoveTask();
            if (session == null)
            {
                ksLog.Error(this, errorMessage);

                // Stop the file logger.
                SetLogLevel(sfConfig.LogLevel.NONE);
                sfConfig.Get().Logging.OnLevelChange -= SetLogLevel;
                m_fileLogger = null;
                return;
            }
            if (m_running)
            {
                return;
            }
            m_running = true;
            // When starting a new session we don't know the session id until we connect, so we temporarly use id 0 for
            // the session log file. Now that we know the id, we rename the log file with the new session id.
            if (m_sessionLogId != null && (m_sessionLogId.StartsWith("new") || m_sessionLogId.StartsWith("manual")))
            {
                m_sessionLogId = session.Info.RoomInfo.Id.ToString();
                if (m_fileLogger != null)
                {
                    m_fileLogger.RenameSessionLog(ref m_sessionLogId);
                }
            }

            ksLog.Info(this, "Connected to Scene Fusion session.");

            sfLoader.Get().Initialize();
            sfIconDrawer.Get().Start();
            sfHierarchyWatcher.Get().Start();
            sfUnityEventDispatcher.Get().PreUpdate += sfHierarchyWatcher.Get().PreUpdate;
            sfUnityEventDispatcher.Get().OnUpdate += sfHierarchyWatcher.Get().Update;
            sfSelectionWatcher.Get().Start();
            sfUndoManager.Get().Start();
            sfLockManager.Get().Start();
            sfMissingScriptSerializer.Get().Start();
            sfPropertyManager.Get().Start();
            sfSessionFooterUI.Get().ShowUpgradeLink = session.GetObjectLimit(sfType.GameObject) != uint.MaxValue;
            if (!EditorApplication.isPlaying)
            {
                sfObjectEventDispatcher.Get().Start(m_service.Session);
            }
        }

        /// <summary>
        /// Called after config settings for the session are synced. Starts managers that depend on session config settings.
        /// </summary>
        private void StartConfigDependentManagers()
        {
            if (sfConfig.Get().SyncPrefabs == sfConfig.PrefabSyncMode.FULL)
            {
                sfPrefabStageMap.Get().Start();
                sfPrefabEventManager.Get().Start();
            }
            else
            {
                sfPrefabLocker.Get().Start();
            }
        }

        /// <summary>Called after disconnecting from a session.</summary>
        /// <param name="session"></param>
        /// <param name="errorMessage"></param>
        public void OnDisconnect(sfSession session, string errorMessage)
        {
            if (errorMessage != null)
            {
                ksLog.Error(this, errorMessage);
            }
            ksLog.Info(this, "Disconnected from Scene Fusion session.");
            Stop();
        }

        /// <summary>Stops running Scene Fusion.</summary>
        private void Stop()
        {
            if (!m_running)
            {
                return;
            }
            m_running = false;
            m_isReconnect = false;
            sfUnityEventDispatcher.Get().Disable();
            sfGuidManager.Get().SaveGuids();
            sfGuidManager.Get().Clear();
            if (sfConfig.Get().SyncPrefabs == sfConfig.PrefabSyncMode.FULL)
            {
                sfPrefabStageMap.Get().Stop();
                sfPrefabEventManager.Get().Stop();
            }
            else
            {
                sfPrefabLocker.Get().Stop();
            }

            sfObjectEventDispatcher.Get().Stop(m_service.Session);
            sfIconDrawer.Get().Stop();
            sfHierarchyWatcher.Get().Stop();
            sfUnityEventDispatcher.Get().PreUpdate += sfHierarchyWatcher.Get().PreUpdate;
            sfUnityEventDispatcher.Get().OnUpdate += sfHierarchyWatcher.Get().Update;
            sfSelectionWatcher.Get().Stop();
            sfUndoManager.Get().Stop();
            sfLockManager.Get().Stop();
            sfMissingScriptSerializer.Get().Stop();
            sfPropertyManager.Get().Stop();
            sfLoader.Get().CleanUp();
            sfObjectMap.Get().Clear();
            sfNotificationManager.Get().Clear();

            // Stop the file logger.
            SetLogLevel(sfConfig.LogLevel.NONE);
            sfConfig.Get().Logging.OnLevelChange -= SetLogLevel;
            m_fileLogger = null;
        }

        /// <summary>Called every frame.</summary>
        private void Update()
        {
            // Time.deltaTime is not accurate in the editor so we track it ourselves.
            long ticks = DateTime.Now.Ticks;
            float dt = (ticks - m_lastTime) / (float)TimeSpan.TicksPerSecond;
            m_lastTime = ticks;

            // Start the object event dispatcher when we leave play mode.
            if (!sfObjectEventDispatcher.Get().IsActive && m_running && m_service.Session != null && !EditorApplication.isPlaying)
            {
                sfObjectEventDispatcher.Get().Start(m_service.Session);
                // Create all the objects
                foreach (sfObject obj in m_service.Session.GetRootObjects())
                {
                    sfObjectEventDispatcher.Get().OnCreate(obj, -1);
                }
            }

            if (m_running && m_service.Session != null && !EditorApplication.isPlaying)
            {
                // Disable Unity events while SF is changing the scene
                sfUnityEventDispatcher.Get().Disable();
            }
            sfUnityEventDispatcher.Get().InvokePreUpdate(dt);
            m_service.Update(dt);
            sfUnityEventDispatcher.Get().InvokeOnUpdate(dt);
            if (m_running && m_service.Session != null && !EditorApplication.isPlaying)
            {
                // Reenable Unity events when SF is done changing the scene
                sfUnityEventDispatcher.Get().Enable();
            }

            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                sfCameraManager.Get().LastSceneCamera = view.camera;
            }

            if (Application.isPlaying && Camera.allCamerasCount > 0)
            {
                sfCameraManager.Get().LastGameCamera = Camera.allCameras[0];
            }
        }
    }
}
