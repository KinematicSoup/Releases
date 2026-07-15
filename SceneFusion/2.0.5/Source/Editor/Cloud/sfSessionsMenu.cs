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
using UnityEngine;
using UnityEditor;
using KS.SF.Reactor;
using KS.SF.Unity.Editor;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Linq;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>GUI that displays the list of available sessions.</summary>
    public class sfSessionsMenu : ksAuthenticatedMenu
    {
        /// <summary>
        /// Callback for checking if a game object can be synced. Used for counting the number of syncable game objects
        /// in open scenes.
        /// </summary>
        /// <param name="gameObject">gameObject to check.</param>
        /// <returns>true if the game object can be synced.</returns>
        public delegate bool SyncChecker(GameObject gameObject);

        /// <summary>Callback before starting or joining a session.</summary>
        /// <param name="sessionInfo">sessionInfo for the session to join, or null if starting a new session.</param>
        /// <returns>false to cancel joining or starting the session.</returns>
        public delegate bool PreSessionChecker(sfSessionInfo sessionInfo);

        /// <summary>
        /// Callback for checking if a game object can be synced. Used for counting the number of syncable game objects
        /// in open scenes.
        /// </summary>
        public static SyncChecker CanSync;

        /// <summary>
        /// Callback before a session is joined or started. Return false to prevent session from starting or being
        /// joined.
        /// </summary>
        public static PreSessionChecker PreSessionCheck;

        // Width in pixels of buttons that appear on the same line
        private const float BUTTON_WIDTH = 120;
        private const float LABEL_PADDING = 5;
        private const float PORT_LABEL_WIDTH = 30;
        private const float PORT_INPUT_WIDTH = 75;
        private const float ADDRESS_INPUT_WIDTH = 150;
        private const ushort LAN_BROADCAST_PORT = 8000;
        
        private List<sfProjectInfo> m_projects = null;
        private List<sfSessionInfo> m_sessions = null;
        [NonSerialized]
        private string m_errorMessage = null;
        [NonSerialized]
        private string m_newErrorMessage = null;
        [NonSerialized]
        private string m_lanWarningMessage = null;
        [NonSerialized]
        private string m_refreshErrorMessage = null;
        [NonSerialized]
        private string m_refreshMessage = null;
        [NonSerialized]
        private string m_newRefreshErrorMessage = null;
        [NonSerialized]
        private string m_newRefreshMessage = null;
        [NonSerialized]
        private bool m_hasNewRefreshErrorMesage = false;
        [NonSerialized]
        private bool m_hasNewRefreshMesage = false;
        private int m_refreshFailCount = 0;
        private bool m_showRefreshButton = false;
        private bool m_hasNewSessions = false;
        private int m_otherProjectSessionCount = 0;
        private const int LOADING = 1;  // Session list is loading.
        private const int REFRESHING = 0;   // Session list is quietly refreshing.
        private const int LOADED = -1;  // Session list is loaded.
        [NonSerialized]
        private int m_loadState = LOADED;
        private bool m_fetchingToken = false;
        private bool m_hasToken = false;
        private HashSet<uint> m_stoppingRoomIds = new HashSet<uint>();
        private Vector2 m_scrollPosition;
        private sfSessionTableGUI m_sessionTable = sfSessionTableGUI.Instance;
        private long m_nextRefreshTime;
        private const long REFRESH_TIME_PERIOD = 10 * TimeSpan.TicksPerSecond;

        private ksWindow m_window;
        private sfIdleChecker m_idleChecker = new sfIdleChecker();

        private const float IDLE_TIME_LIMIT = 300f; // When mouse is outside of focused window or not moving
                                                    // for this time in seconds, stop refreshing session list.
        private bool m_canUseInterface = true;
        private string m_cannotUseInterfaceReason = null;
        private MessageType m_reasonType = MessageType.None;
        private sfProjectInfo m_project = null;
        private int m_objectCount = 0;
        private bool m_objectCountStale = true;
        private bool m_wasInPlaymode = false;

        private UdpClient m_broadcastClient;
        private Dictionary<uint, sfSessionInfo> m_lanSessionDict = new Dictionary<uint, sfSessionInfo>();
        private List<sfSessionInfo> m_lanSessionList = new List<sfSessionInfo>();
        private bool m_lanSessionRefresh = false;
        private ushort m_lanServerPort = 8000;
        private bool m_downloadingLAN = false;
        private string m_lanHostAddress = "";
        private ushort m_lanHostPort = 8000;
        private sfLocalSession.ServerState m_lanServerState = sfLocalSession.ServerState.MISSING;
        private long m_setupBroadcastReceiverTimer = 0;
        private const long SETUP_BROADCAST_RECEIVER_INTERVAL = 3 * TimeSpan.TicksPerSecond;
        private const string UNABLE_TO_RECEIVE_LAN_SESSION_WARNING =
            "Unable to receive LAN session information. Use direct connections instead.";

        private bool m_expandOnlineSessions = true;
        private bool m_expandLanSessions = true;
        private bool m_isCompiling = false;
        private bool m_isInitialized = false;

        /// <summary>Scene Fusion service</summary>
        private static sfService Service
        {
            get { return SceneFusion.Get().Service; }
        }

        /// <summary>Error message</summary>
        private string ErrorMessage
        {
            get { return m_newErrorMessage; }

            set
            {
                m_newErrorMessage = value;
            }
        }

        /// <summary>Error message when refreshing</summary>
        private string RefreshErrorMessage
        {
            get { return m_newRefreshErrorMessage; }

            set
            {
                m_newRefreshErrorMessage = value;
                m_hasNewRefreshErrorMesage = true;
            }
        }


        /// <summary>Message when refreshing</summary>
        private string RefreshMessage
        {
            get { return m_newRefreshMessage; }

            set
            {
                m_newRefreshMessage = value;
                m_hasNewRefreshMesage = true;
            }
        }

        /// <summary>Check if the SF webservice and SF token used for session managment are avialable.</summary>
        private bool HasSFToken
        {
            get { return Service.WebService != null && Service.WebService.SFToken != null; }
        }

        /// <summary>Icon</summary>
        override public Texture Icon
        {
            get { return sfTextures.Logo; }
        }

        /// <summary>Unity on enable</summary>
        private void OnEnable()
        {
            SetLoginMenu(typeof(sfLoginMenu));
            hideFlags = HideFlags.HideAndDontSave;
            m_nextRefreshTime = 0;
        }

        /// <summary>Unity update</summary>
        private void Update()
        {
            if (!m_idleChecker.Checking)
            {
                m_idleChecker.Start();
            }

            // Refresh the session list and LAN servers state periodically
            if (m_wasInPlaymode || (m_idleChecker.GetIdlingTime() < IDLE_TIME_LIMIT
                && DateTime.Now.Ticks >= m_nextRefreshTime))
            {
                m_wasInPlaymode = false;
                m_lanServerState = sfLocalSession.ServerCheck();
                QuietRefresh();
            }
            CheckLanSessions();
            string cannotUseInterfaceReason = null;
            m_canUseInterface = CanUseInterface(out cannotUseInterfaceReason);
            if (m_cannotUseInterfaceReason != cannotUseInterfaceReason)
            {
                m_window.Repaint();
                m_cannotUseInterfaceReason = cannotUseInterfaceReason;
                if (m_canUseInterface)
                {
                    QuietRefresh();
                }
            }

            SetupBroadcastReceiver();

            if (Service.ConnectionError != null)
            {
                ErrorMessage = Service.ConnectionError;
                Service.ConnectionError = null;
            }
        }

        /// <summary>Called when the menu is opened.</summary>
        /// <param name="window">window the opened the menu.</param>
        public override void OnOpen(ksWindow window)
        {
            m_idleChecker.Start();
            m_window = window;
            m_refreshFailCount = 0;
            ErrorMessage = null;
            m_lanServerState = sfLocalSession.ServerCheck();
            m_setupBroadcastReceiverTimer = 0;
            EditorApplication.update += Update;
            EditorApplication.hierarchyChanged += OnHierarchyChange;
        }

        /// <summary>Destroy this object when the menu is closed.</summary>
        /// <param name="window">window that closed the menu.</param>
        public override void OnClose(ksWindow window)
        {
            m_idleChecker.Stop();
            EditorApplication.update -= Update;
            EditorApplication.hierarchyChanged -= OnHierarchyChange;
            if (m_broadcastClient != null)
            {
                m_broadcastClient.Close();
                m_broadcastClient = null;
            }
        }

        /// <summary>Creates the GUI.</summary>
        /// <param name="window">window the GUI is for.</param>
        public override void OnDraw(ksWindow window)
        {
            if (Event.current.type == EventType.Layout && !UpdateLayout(window))
            {
                return;
            }

            if (m_isCompiling)
            {
                EditorGUILayout.HelpBox("Compiling scripts...", MessageType.Warning);
                return;
            }

            if (!m_isInitialized)
            {
                EditorGUILayout.HelpBox("Initializing service...", MessageType.Info);
                return;
            }

            if (m_objectCountStale)
            {
                m_objectCountStale = false;
                m_objectCount = CountSyncableObjects();
            }
            
            ksStyle.ProjectSelector(ksEditorWebService.Email, m_project, m_projects, Logout, SelectProject);

            if (!m_hasToken)
            {
                if (m_fetchingToken)
                {
                    EditorGUILayout.HelpBox("Loading sessions...", MessageType.Info);
                }
                return;
            }

            try
            {
                m_scrollPosition = EditorGUILayout.BeginScrollView(m_scrollPosition);
            }
            catch (InvalidCastException)
            {
                // There is a Unity bug where the first time this get executed after Unity started, it will throw an
                // InvalidCastException. We just catch the exception and do nothing so that Unity does not log out
                // an error.
            }
            if (!m_canUseInterface)
            {
                EditorGUILayout.HelpBox(m_cannotUseInterfaceReason, m_reasonType);
            }
            else if (m_project == null)
            {
                QuietRefresh();
            }
            else
            {
                string cannotStartReason = null;
                bool canStartSession = CanStartSession(out cannotStartReason);
                string cannotJoinReason = null;
                bool canJoinSession = CanJoinSession(out cannotJoinReason);

                DrawUsageInfo(cannotJoinReason, cannotStartReason);
                DrawErrorMessages();

                // Draw Online Sessions
                m_expandOnlineSessions = EditorGUILayout.Foldout(
                    m_expandOnlineSessions, 
                    "Online Sessions (" + m_sessions.Count + ")"
                );
                if (m_expandOnlineSessions)
                {
                    DrawNewSessionButtons(canStartSession);
                    DrawSessionList(canJoinSession, m_sessions);
                }
                EditorGUILayout.Space();

                // Draw LAN Sessions
                if (m_project.LANEnabled)
                {
                    m_expandLanSessions = EditorGUILayout.Foldout(
                        m_expandLanSessions, 
                        "LAN Sessions (" + m_lanSessionList.Count() + ")"
                    );
                    if (m_expandLanSessions)
                    {
                        DrawLANSessionButtons(canStartSession);
                        DrawSessionList(canJoinSession, m_lanSessionList, true);
                        DrawManualLanConnect(canJoinSession);
                    }
                }
                else
                {
                    ksStyle.HelpBoxLink(
                        MessageType.Info,
                        "<a>Contact us</a> to learn how to enable LAN servers for this project.",
                        "https://www.kinematicsoup.com/contact");
                }
            }
            EditorGUILayout.EndScrollView();
            sfSessionFooterUI.Get().DrawFooter();
        }

        /// <summary>
        /// Handle the result of a fetch token request. If the token is successfully fetched then
        /// refresh the list of sessions.
        /// </summary>
        /// <param name="-">success, true if a new SF session token was retrieved.</param>
        /// <param name="-">username (unused)</param>
        /// <param name="-">response (unused)</param>
        private void HandleFetchToken(bool success, string username, string response)
        {
            m_fetchingToken = false;
            if (success)
            {
                QuietRefresh();
            }
        }

        /// <summary>Draw the SceneFusion usage information</summary>
        /// <param name="-">reason the user cannot join sessions.</param>
        /// <param name="-">reason the user cannot start sessions.</param>
        private void DrawUsageInfo(string cannotJoinReason, string cannotStartReason)
        {
            if (m_project.SessionLimit == 0 || m_project.UserLimit == 0)
            {
                string msg = "There is no active subscription for this project.\n Please contact your account admin to fix it.";
                EditorGUILayout.HelpBox(msg, MessageType.Warning);
                if (GUILayout.Button("Open Console", GUILayout.MaxWidth(BUTTON_WIDTH)))
                {
                    string url = sfConfig.Get().Urls.WebConsole;
                    if (url != null)
                    {
                        Application.OpenURL(url);
                    }
                }
            }
            else
            {
                // Usage messages

                string sessionlimits = "Sessions: " + m_project.SessionCount + " of "
                    + ((m_project.SessionLimit < 0) ? "Unlimited" : m_project.SessionLimit.ToString());
                string userlimits = "Users: " + m_project.UserCount + " of "
                    + ((m_project.UserLimit < 0) ? "Unlimited" : m_project.UserLimit.ToString());
                string objectLimits = "Objects: " + m_objectCount.ToString() + " of "
                    + ((m_project.ObjectLimit < 0) ? "Unlimited" : m_project.ObjectLimit.ToString());

                string msg = "Subscription: " + m_project.SubscriptionName + "\n";
                MessageType msgType = MessageType.Info;
                msg += sessionlimits + "\n" + userlimits;
                if (m_project.ObjectLimit >= 0)
                {
                    msg += "\n" + objectLimits;
                }
                EditorGUILayout.HelpBox(msg, msgType);

                string warningMessage = null;
                if (!string.IsNullOrEmpty(cannotJoinReason))
                {
                    warningMessage = cannotJoinReason;
                }
                else if (!string.IsNullOrEmpty(cannotStartReason))
                {
                    warningMessage = cannotStartReason;
                }

                if (string.IsNullOrEmpty(warningMessage) &&
                    m_project.ObjectLimit > 0 &&
                    m_objectCount > (m_project.ObjectLimit / 10 * 9))
                {
                    warningMessage = "You are about to reach your " + m_project.ObjectLimit +
                        " object limit.";
                }

                if (!string.IsNullOrEmpty(warningMessage))
                {
                    ksStyle.HelpBoxLink(
                        MessageType.Warning,
                        warningMessage + " <a>Click here to upgrade.</a>",
                        sfConfig.Get().Urls.Upgrade);
                }
            }
        }

        /// <summary>Draw the buttons used to start new sessions.</summary>
        /// <param name="-">can the user start sessions</param>
        private void DrawNewSessionButtons(bool canStartSession)
        {
            if (m_project.SessionLimit != 0 && m_project.UserLimit != 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                // Start sessions
                EditorGUI.BeginDisabledGroup(!canStartSession);
                {
                    if (GUILayout.Button("New Session", GUILayout.MaxWidth(BUTTON_WIDTH)) &&
                        (PreSessionCheck == null || PreSessionCheck(null)))
                    {
                        ksLog.Info("Launching Session: " + sfPaths.SceneName);
                        sfAnalytics.Get().TrackEvent(sfAnalytics.Events.ONLINE_SESSION_START);
                        ErrorMessage = null;
                        Service.StartSession(
                            m_project.Id,
                            "Unity " + Application.unityVersion,
                            Application.productName + ";" + sfPaths.SceneName);
                    }
                }
                EditorGUI.EndDisabledGroup();
                GUILayout.EndHorizontal();
            }
        }


        /// <summary>Draw the buttons used to start new LAN sessions.</summary>
        /// <param name="-">can the user start sessions</param>
        private void DrawLANSessionButtons(bool canStartSession)
        {
            EditorGUI.BeginDisabledGroup(!canStartSession);
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                if (m_lanServerState == sfLocalSession.ServerState.OK)
                {
                    if (GUILayout.Button("New Local Session", GUILayout.MaxWidth(BUTTON_WIDTH)) &&
                        (PreSessionCheck == null || PreSessionCheck(null)))
                    {
                        ksLog.Info("Launching LAN Session: " + sfPaths.SceneName);
                        ErrorMessage = null;

                        sfSessionInfo sessionInfo = WriteLANConfig();
                        if (sessionInfo != null)
                        {
                            string sessionId = "sf_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                            sfAnalytics.Get().TrackEvent(sfAnalytics.Events.LOCAL_SESSION_START);
                            Service.StartLocalSession(sessionId, sessionInfo);
                        }
                        else
                        {
                            ErrorMessage = "An unexpected error occured while launching and joining the LAN session.";
                        }
                    }

                    Rect r = GUILayoutUtility.GetLastRect();
                    r.x = r.xMax + LABEL_PADDING;
                    r.width = PORT_LABEL_WIDTH;
                    EditorGUI.LabelField(r, "Port");

                    r.x = r.xMax + LABEL_PADDING;
                    r.width = PORT_INPUT_WIDTH;
                    m_lanServerPort = (ushort)EditorGUI.IntField(r, m_lanServerPort);
                }
                else
                {
                    if (GUILayout.Button("Install Scene Fusion LAN Server"))
                    {
                        ErrorMessage = null;
                        EditorUtility.DisplayProgressBar("Installing SceneFusion LAN server", "Downloading package.", 0.33f);
                        m_downloadingLAN = true;
                        Service.WebService.DownloadLANServer(m_project, OnDownloadLANServer);
                    }
                    GUILayout.FlexibleSpace();
                }
                GUILayout.EndHorizontal();
            }
            EditorGUI.EndDisabledGroup();
            
        }

        /// <summary>Handle the response from a DownloadLANServer request</summary>
        /// <param name="-">LAN server file data</param>
        /// <param name="-">Error message</param>
        private void OnDownloadLANServer(byte[] data, string error)
        {
            try
            {
                if (string.IsNullOrEmpty(error) && (data == null || data.Length == 0))
                {
                    error = "Failed to download server";
                }

                // Report error and exit
                if (!string.IsNullOrEmpty(error))
                {
                    EditorUtility.ClearProgressBar();
                    m_downloadingLAN = false;
                    ErrorMessage = error;
                    m_window.Repaint();
                    return;
                }

                EditorUtility.DisplayProgressBar("Installing SceneFusion LAN server", "Installing server.", 0.66f);
                string from = sfPaths.External + "SceneFusionServer.zip";
                string to = sfPaths.ExternalServer;
                ksPathUtils.Create(sfPaths.External, true);

                // Delete the old server
                if (File.Exists(from))
                {
                    File.Delete(from);
                }

                if (Directory.Exists(to))
                {
                    Directory.Delete(to, true);
                }

                // Save ZIP archive and unzip it.
                File.WriteAllBytes(from, data);
                ksPathUtils.Unzip(from, to);

                EditorUtility.DisplayProgressBar("Installing SceneFusion LAN server", "Validating server.", 1.0f);
                m_lanServerState = sfLocalSession.ServerCheck();
                if (m_lanServerState == sfLocalSession.ServerState.MISSING)
                {
                    ErrorMessage = "Error extracting '" + from + "' to '" + to + "'";
                }
                else
                {
                    File.Delete(from);
                }
            }
            catch (Exception ex)
            {
                ksLog.Error("Unexpected error installing LAN server.", ex);
            }
            m_downloadingLAN = false;
            EditorUtility.ClearProgressBar();
            m_window.Repaint();
        }

        /// <summary>Draw the list of SceneFusion sessions</summary>
        /// <param name="-">can the user join sessions</param>
        /// <param name="-">sessions list</param>
        /// <param name="isLANSessions"></param>
        private void DrawSessionList(
            bool canJoinSession,
            IEnumerable<sfSessionInfo> sessions,
            bool isLANSessions = false)
        {
            EditorGUI.indentLevel = 1;
            if (isLANSessions && !string.IsNullOrEmpty(m_lanWarningMessage))
            {
                EditorGUILayout.HelpBox(m_lanWarningMessage, MessageType.Warning);
            }
            else if (!string.IsNullOrEmpty(m_refreshErrorMessage))
            {
                EditorGUILayout.HelpBox("Failed to refresh session list. " + m_refreshErrorMessage, MessageType.Error);
                if (m_showRefreshButton)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Try Again", GUILayout.MaxWidth(BUTTON_WIDTH)))
                    {
                        Refresh();
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            else if (sessions != null)
            {
                if (sessions.Count() == 0)
                {
                    string message;
                    if (isLANSessions)
                    {
                        message = "No LAN sessions are currently running.";
                    }
                    else
                    {
                        message = "No sessions are currently running";
                        if (m_otherProjectSessionCount == 0)
                        {
                            message += ".";
                        }
                        else
                        {
                            message += " for the active project. ";
                            if (m_otherProjectSessionCount == 1)
                            {
                                message += "1 session is running in another project.";
                            }
                            else
                            {
                                message += m_otherProjectSessionCount + " sessions are running in other projects.";
                            }
                            message += " Change the active project using the top-right dropdown.";
                        }
                    }
                    ksStyle.WordWrapLabel(message);
                }
                else
                {
                    m_sessionTable.DrawHeader("Project", "Scene", "Creator");
                    EditorGUI.BeginDisabledGroup(!canJoinSession);
                    foreach (sfSessionInfo session in sessions)
                    {
                        DrawRow(session);
                    }
                    EditorGUI.EndDisabledGroup();
                }
            }
            EditorGUI.indentLevel = 0;
        }

        /// <summary>Draw a manual connection foldout</summary>
        private void DrawManualLanConnect(bool canJoinSession)
        {
            EditorGUILayout.Space();
            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            if (GUILayout.Button("Manual Connect", GUILayout.MaxWidth(BUTTON_WIDTH)))
            {
                sfSessionInfo session = sfSessionInfo.CreateLANInfo(
                    1,
                    m_project.Name,
                    Application.productName,
                    "Unity " + Application.unityVersion,
                    sfPaths.SceneName,
                    sfConfig.Get().Version.ToString(),
                    "",
                    new ksRoomInfo(m_lanHostAddress, m_lanHostPort));
                if (PreSessionCheck == null || PreSessionCheck(session))
                {
                    Service.JoinSession(session);
                    GUIUtility.ExitGUI();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUILayout.Label("Host", GUILayout.Width(PORT_LABEL_WIDTH));
            m_lanHostAddress = GUILayout.TextField(m_lanHostAddress, GUILayout.Width(ADDRESS_INPUT_WIDTH));
            GUILayout.Label("Port", GUILayout.Width(PORT_LABEL_WIDTH));
            m_lanHostPort = ushort.Parse(GUILayout.TextField(m_lanHostPort.ToString(), GUILayout.Width(PORT_INPUT_WIDTH)));
            GUILayout.EndHorizontal();
        }

        /// <summary>Draw error messages.</summary>
        private void DrawErrorMessages()
        {
            if (!string.IsNullOrEmpty(m_errorMessage))
            {
                EditorGUILayout.HelpBox(m_errorMessage, MessageType.Error);
            }
        }

        /// <summary>
        /// Updates the GUI layout with new data, possibly switching to a different menu
        /// if we've logged out or connected to a room.
        /// </summary>
        /// <param name="window">window the GUI is for.</param>
        /// <returns>false if we switched menus.</returns>
        private bool UpdateLayout(ksWindow window)
        {
            m_isInitialized = Service != null && Service.WebService != null;
            m_isCompiling = EditorApplication.isCompiling;
            if (!m_isInitialized || m_isCompiling)
            {
                return true;
            }

            if (Service.IsConnected)
            {
                window.Menu = ScriptableObject.CreateInstance<sfOnlineMenu>();
                window.Menu.Draw(window);
                return false;
            }

            m_hasToken = HasSFToken;
            if (!m_hasToken)
            {
                if (!string.IsNullOrEmpty(ksEditorWebService.Email) && !string.IsNullOrEmpty(ksEditorWebService.Token))
                {
                    if (!m_fetchingToken)
                    {
                        m_fetchingToken = true;
                        Service.WebService.Authenticate(
                            ksEditorWebService.Email,
                            ksEditorWebService.Token,
                            HandleFetchToken);
                    }
                }
            }

            if (m_newErrorMessage != m_errorMessage)
            {
                m_errorMessage = m_newErrorMessage;
            }
            if (m_hasNewRefreshErrorMesage)
            {
                m_hasNewRefreshErrorMesage = false;
                m_refreshErrorMessage = m_newRefreshErrorMessage;
            }
            if (m_hasNewRefreshMesage)
            {
                m_hasNewRefreshMesage = false;
                m_refreshMessage = m_newRefreshMessage;
            }
            if (m_hasNewSessions && m_projects != null)
            {
                m_hasNewSessions = false;
                m_otherProjectSessionCount = 0;
                if (m_projects.Count == 0)
                {
                    m_sessions = new List<sfSessionInfo>();
                }
                else
                {
                    m_project = m_projects[0];
                    foreach(sfProjectInfo project in m_projects)
                    {
                        if (project.Id == sfConfig.Get().ProjectId)
                        {
                            m_project = project;
                        }
                        else
                        {
                            m_otherProjectSessionCount += project.Sessions.Count;
                        }
                    }
                    sfConfig.Get().ProjectId = m_project.Id;
                    m_sessions = m_project.Sessions;
                    CheckSubscription();
                }
            }
            if (m_lanSessionRefresh)
            {
                m_lanSessionRefresh = false;
                m_lanSessionList.Clear();
                m_lanSessionList.AddRange(m_lanSessionDict.Values);
                m_window.Repaint();
            }
            if (m_refreshFailCount >= 3)
            {
                m_showRefreshButton = true;
            }
            else
            {
                m_showRefreshButton = false;
            }
            return true;
        }

        /// <summary>Logout of the service and reset the menu.</summary>
        private void Logout()
        {
            Service.Logout();
            SetLoginMenu(typeof(sfLoginMenu));
        }

        /// <summary>Select a project from the project list</summary>
        /// <param name="project">project that was selected.</param>
        private void SelectProject(sfProjectInfo project)
        {
            m_project = project;
            sfConfig.Get().ProjectId = m_project.Id;
            CheckSubscription();
            m_hasNewSessions = true;
        }

        /// <summary>
        /// Shows or hides the footer upgrade link depending on the subscription of the selected project.
        /// </summary>
        private void CheckSubscription()
        {
            sfSessionFooterUI.Get().ShowUpgradeLink = m_project != null && m_project.SubscriptionName == "Scene Fusion Free";
        }

        /// <summary>Creates the GUI for a row in the session table.</summary>
        /// <param name="session"></param>
        private void DrawRow(sfSessionInfo session)
        {
            // If the session was not launched by Unity, the do not show it in the session list
            if (!session.LaunchApplication.StartsWith("Unity"))
            {
                return;
            }

            string currentUnityVersion = "Unity " + Application.unityVersion.Substring(0, Application.unityVersion.LastIndexOf("."));
            string expectedUnityVersion = session.LaunchApplication.Substring(0, session.LaunchApplication.LastIndexOf("."));
            string joinError = null;
            if (session.RequiredVersion != sfConfig.Get().ServerVersion.ToString())
            {
                joinError = "Requires Scene Fusion " + session.RequiredVersion;
            }
            else if (expectedUnityVersion != currentUnityVersion)
            {
                joinError = "Unity versions do not match; Expected = " + expectedUnityVersion + ", Current = " + currentUnityVersion;
            }
            string stopError = session.CanStop ? null : "You do not have permission to stop this session.";

            EditorGUI.BeginDisabledGroup(m_stoppingRoomIds.Contains(session.RoomInfo.Id));
            m_sessionTable.DrawRow(
                session,
                joinError,
                stopError,
                delegate ()
                {
                    if (PreSessionCheck == null || PreSessionCheck(session))
                    {
                        Service.JoinSession(session);
                        GUIUtility.ExitGUI();
                    }
                },
                delegate () {
                    string title = "Confirm Stop Session";
                    string message = "Stop the session " + session.SceneName + " launched by " + session.Creator + "?";
                    if (EditorUtility.DisplayDialog(title, message, "OK", "Cancel"))
                    {
                        m_stoppingRoomIds.Add(session.RoomInfo.Id);
                        m_window.Repaint();
                        Service.StopSession(session, OnStopSession);
                    }
                }
            );
            EditorGUI.EndDisabledGroup();
        }

        /// <summary>Sends a get-sessions request to the web server.</summary>
        private void Refresh()
        {
            RefreshErrorMessage = null;
            m_loadState = LOADING;
            m_window.Repaint();
            Service.GetSessions("Unity " + Application.unityVersion, OnGetSessions);
        }

        /// <summary>
        /// Sends a get-sessions request to the web server.
        /// This method does not show "loading" message or disable refresh button.
        /// </summary>
        private void QuietRefresh()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode 
                || !ksEditorWebService.IsLoggedIn || ksEditorWebService.IsLoggingIn || !HasSFToken
                || EditorApplication.isPlaying || m_loadState == REFRESHING || m_loadState == LOADING)
            {
                return;
            }

            RefreshErrorMessage = null;
            RefreshMessage = null;
            m_loadState = REFRESHING;
            m_nextRefreshTime = DateTime.Now.Ticks + REFRESH_TIME_PERIOD;
            Service.GetSessions("Unity " + Application.unityVersion, OnGetSessions);
        }

        /// <summary>Called when game objects are added, removed, renamed, or reordered/reparented.</summary>
        private void OnHierarchyChange()
        {
            m_objectCountStale = true;
        }

        /// <summary>Called when a get-sessions request completes. Updates the GUI with the new data.</summary>
        /// <param name="projects">projects the user belongs to and session data. Null if an error occurred.</param>
        /// <param name="response">response from the server.</param>
        private void OnGetSessions(List<sfProjectInfo> projects, string response)
        {
            if (projects != null)
            {
                m_projects = projects;
                m_hasNewSessions = true;
                m_refreshFailCount = 0;
            }
            else
            {
                // Handle "Invalid Token" errors gracefully.
                if (response == "Invalid token")
                {
                    if (m_refreshFailCount < 10)
                    {
                        // Refresh again in 1 second
                        m_nextRefreshTime = DateTime.Now.Ticks + TimeSpan.TicksPerSecond;
                        RefreshMessage = "Fetching session list...";
                    }
                    else
                    {
                        Service.Logout();
                    }
                }
                else
                {
                    RefreshErrorMessage = response;
                }
                m_projects = null;
                m_refreshFailCount++;
            }
            m_loadState = LOADED;
            m_window.Repaint();
        }

        /// <summary>
        /// Called when a stop-session request completes. Removes the session from the GUI if the request was
        /// successful. Otherwise displays an error message.
        /// </summary>
        /// <param name="success">true if the session was stopped successfully.</param>
        /// <param name="sessionInfo">sessionInfo of the session the request was for.</param>
        /// <param name="response">response from the server.</param>
        private void OnStopSession(bool success, sfSessionInfo sessionInfo, string response)
        {
            m_stoppingRoomIds.Remove(sessionInfo.RoomInfo.Id);
            m_window.Repaint();
            if (!success)
            {
                ErrorMessage = response;
            }
            else
            {
                sfProjectInfo project = GetActiveProject();
                if (project != null && project.Sessions == m_sessions)
                {
                    if (m_sessions == project.Sessions)
                    {
                        // Copy the sessions list so changing the project sessions list does not update the GUI. The
                        // GUI can only be updated from a Layout event.
                        m_sessions = new List<sfSessionInfo>(project.Sessions);
                    }
                    for (int i = 0; i < project.Sessions.Count; i++)
                    {
                        if (project.Sessions[i].RoomInfo.Id == sessionInfo.RoomInfo.Id)
                        {
                            project.Sessions.RemoveAt(i);
                            m_hasNewSessions = true;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>Gets the project we're currently viewing.</summary>
        /// <returns>project being viewed.</returns>
        private sfProjectInfo GetActiveProject()
        {
            return m_project;
        }

        /// <summary>Check if the editor is in a state that we should stop the user from using the interface.</summary>
        /// <param name="reson">reson why the result returned false.</param>
        /// <returns>false if the interface should be disabled.</returns>
        private bool CanUseInterface(out string reason)
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                reason = "Cannot create or join sessions while the editor is in play mode.";
                m_reasonType = MessageType.Warning;
                m_wasInPlaymode = true;
                return false;
            }

            if (m_downloadingLAN)
            {
                reason = "Installing Scene Fusion LAN server...";
                m_reasonType = MessageType.Info;
                return false;
            }

            if (m_loadState == LOADING)
            {
                reason = "Loading...";
                m_reasonType = MessageType.Info;
                return false;
            }

            if (!string.IsNullOrEmpty(m_refreshErrorMessage))
            {
                reason = m_refreshErrorMessage;
                m_reasonType = MessageType.Error;
                return false;
            }

            if (!string.IsNullOrEmpty(m_refreshMessage))
            {
                reason = m_refreshMessage;
                m_reasonType = MessageType.Info;
                return false;
            }
            
            if (m_projects == null || m_projects.Count == 0)
            {
                reason = "You are not an editor in any projects. You must be invited to a project " +
                        "before you can use " + Product.NAME + ".";
                m_reasonType = MessageType.Warning;
                return false;
            }

            if (Service.IsConnecting)
            {
                reason = "Connecting to session.";
                m_reasonType = MessageType.Info;
                return false;
            }
            
            if (Service.IsStartingSession)
            {
                reason = "Starting session.";
                m_reasonType = MessageType.Info;
                return false;
            }
            
            reason = null;
            return true;
        }

        /// <summary>Can this user join an editing session.</summary>
        /// <param name="reson">reson why the result returned false.</param>
        /// <returns>false if user cannot join a session</returns>
        private bool CanJoinSession(out string reason)
        {
            if (m_project.UserLimit >= 0 && m_project.UserCount >= m_project.UserLimit)
            {
                reason = "Cannot create or join sessions because the user limit for the company has been reached.";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>Can this user start an editing session.</summary>
        /// <param name="reson">reson why the result returned false.</param>
        /// <returns>false if user cannot start a session</returns>
        private bool CanStartSession(out string reason)
        {
            if (!CanJoinSession(out reason))
            {
                return false;
            }

            if (m_project.SessionLimit >= 0 && m_project.SessionCount >= m_project.SessionLimit)
            {
                reason = "Cannot create sessions because the session limit for the company has been reached.";
                return false;
            }

            if (m_project.ObjectLimit > 0 && m_objectCount > m_project.ObjectLimit)
            {
                reason = "Cannot create sessions because you have more than " + m_project.ObjectLimit + " objects.";
                return false;
            }

            return true;
        }

        /// <summary>Parse session information from a broadcast message.</summary>
        /// <param name="-">async result</param>
        private void OnBroadcastReceived(IAsyncResult ar)
        {
            if (m_broadcastClient == null)
            {
                return;
            }
            IPEndPoint ip = new IPEndPoint(IPAddress.Any, LAN_BROADCAST_PORT);
            byte[] bytes = m_broadcastClient.EndReceive(ar, ref ip);
            // Parse the message data
            int offset = 0;
            uint id = BitConverter.ToUInt32(bytes, offset);
            offset += sizeof(uint);
            ushort port = BitConverter.ToUInt16(bytes, offset);
            offset += sizeof(ushort);
            int strlen = (int)bytes[offset++];
            string projectName = System.Text.Encoding.ASCII.GetString(bytes, offset, strlen);
            offset += strlen;
            strlen = (int)bytes[offset++];
            string sceneName = System.Text.Encoding.ASCII.GetString(bytes, offset, strlen);
            offset += strlen;
            strlen = (int)bytes[offset++];
            string creator = System.Text.Encoding.ASCII.GetString(bytes, offset, strlen);
            offset += strlen;
            strlen = (int)bytes[offset++];
            string version = System.Text.Encoding.ASCII.GetString(bytes, offset, strlen);
            offset += strlen;
            strlen = (int)bytes[offset++];
            string application = System.Text.Encoding.ASCII.GetString(bytes, offset, strlen);
            offset += strlen;

            string editorProjectName = "";
            string[] strArray = sceneName.Split(new char[] { ';' });
            if (strArray.Length > 1)
            {
                editorProjectName = strArray[0];
                sceneName = strArray[1];
            }

            // Create session info
            ksRoomInfo roomInfo = new ksRoomInfo(ip.Address.ToString(), port);
            roomInfo.Id = id;
            roomInfo.Scene = "Scene Fusion";
            roomInfo.Type = "Scene Fusion";
            sfSessionInfo info = sfSessionInfo.CreateLANInfo(
                1,
                projectName,
                editorProjectName,
                application,
                sceneName,
                version,
                creator,
                roomInfo);
            m_lanSessionRefresh = !m_lanSessionDict.ContainsKey(id);
            m_lanSessionDict[id] = info;

            // Receive again!
            m_broadcastClient.BeginReceive(OnBroadcastReceived, new object());
        }

        /// <summary>Clear the LAN session list of session older than 5 seconds.</summary>
        private void CheckLanSessions()
        {
            List<uint> removals = new List<uint>();
            foreach(KeyValuePair<uint, sfSessionInfo> pair in m_lanSessionDict)
            {
                if (pair.Value.Time < DateTime.Now - TimeSpan.FromSeconds(5.0))
                {
                    removals.Add(pair.Key);
                    m_lanSessionRefresh = true;
                }
            }
            foreach(uint id in removals)
            {
                m_lanSessionDict.Remove(id);
            }
            if (m_lanSessionRefresh)
            {
                m_window.Repaint();
            }
        }

        /// <summary>
        /// Write a config file to the server directory for a new LAN session then
        /// return the project and session information for the new session.
        /// </summary>
        /// <returns></returns>
        private sfSessionInfo WriteLANConfig()
        {
            ksRoomInfo roomInfo = new ksRoomInfo("localhost", m_lanServerPort);
            roomInfo.Scene = "SceneFusion";
            roomInfo.Id = 0;

            sfSessionInfo sessionInfo = sfSessionInfo.CreateLANInfo(
                m_project.Id,
                m_project.Name,
                Application.productName,
                "Unity " + Application.unityVersion,
                sfPaths.SceneName,
                sfConfig.Get().Version.ToString(),
                Service.LocalUsername,
                roomInfo);

            // Write LAN config
            ksJSON jsonConfig = new ksJSON();
            jsonConfig["projectId"] = sfConfig.Get().ProjectId;
            jsonConfig["projectName"] = sessionInfo.ProjectName;
            jsonConfig["scene"] = sessionInfo.EditorProjectName + ";" + sessionInfo.SceneName;
            jsonConfig["creator"] = sessionInfo.Creator;
            jsonConfig["version"] = sessionInfo.RequiredVersion;
            jsonConfig["port"] = (int)m_lanServerPort;
            jsonConfig["token"] = Service.WebService.SFToken;
            jsonConfig["application"] = sessionInfo.LaunchApplication;
            if (!ksAnalytics.Enabled)
            {
                jsonConfig["analytics"] = false;
            }

            try
            {
                File.WriteAllText(sfPaths.ExternalServer + "config.json", jsonConfig.Print(true));
                return sessionInfo;
            }
            catch(Exception ex)
            {
                ksLog.Error(this, "Error writing LAN configs.", ex);
            }
            return null;
        }

        /// <summary>
        /// Setup broadcast receiver if the broadcast client is null.
        /// Show a warning message on the sessions panel if it failed.
        /// </summary>
        private void SetupBroadcastReceiver()
        {
            if (m_broadcastClient == null && m_setupBroadcastReceiverTimer <= DateTime.Now.Ticks)
            {
                try
                {
                    IPEndPoint ep = new IPEndPoint(IPAddress.Any, LAN_BROADCAST_PORT);
                    m_broadcastClient = new UdpClient(ep);
                    m_broadcastClient.BeginReceive(OnBroadcastReceived, new object());
                    ksLog.Info(this, "Listening for Session broadcasts on port " + LAN_BROADCAST_PORT);
                    if (m_lanWarningMessage == UNABLE_TO_RECEIVE_LAN_SESSION_WARNING)
                    {
                        m_lanWarningMessage = null;
                    }
                }
                catch
                {
                    m_lanWarningMessage = UNABLE_TO_RECEIVE_LAN_SESSION_WARNING;
                    m_setupBroadcastReceiverTimer = DateTime.Now.Ticks + SETUP_BROADCAST_RECEIVER_INTERVAL;
                    // Do nothing, we will try to setup the UDP socket next update.
                }
            }
        }

        /// <summary>Iterates all game objects and counts the number of syncable objects.</summary>
        /// <returns>number of syncable game objects.</returns>
        private int CountSyncableObjects()
        {
            if (CanSync == null)
            {
                return 0;
            }
            int count = 0;
            sfUnityUtils.ForEachGameObject((GameObject gameObject) =>
            {
                if (CanSync(gameObject))
                {
                    count++;
                    return true;
                }
                return false;
            });
            return count;
        }
    }
}
