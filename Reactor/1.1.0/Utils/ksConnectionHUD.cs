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
using System.Collections.Generic;
using UnityEngine;

namespace KS.Reactor.Client.Unity
{
    /// <summary>
    /// Self contained GUI designed to help new reactor users establish connection to servers for the first time.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu(ksMenuNames.REACTOR + nameof(ksConnectionHUD))]
    public class ksConnectionHUD : MonoBehaviour
    {
        /// <summary> HUD scroll position </summary>
        private Vector2 m_scrollPosition;
        [Header("Position")]
        [SerializeField, Tooltip("HUD position offset")]
        private Vector2 m_offset = new Vector2(5, 10);

        [Header("Direct Connection")]
        [Tooltip("Default host for direct connection")]
        public string Host = "localhost";

        [Tooltip("Default port for direct connection")]
        public ushort Port = 8000;

        /// <summary> Room to maintain connection and simulation state </summary>
        private ksRoom m_room = null;

        /// <summary>List of available rooms to connect to </summary>
        private List<ksRoomInfo> m_roomList = new List<ksRoomInfo>();

        [Header("Online Connection")]
        [SerializeField, Tooltip("Auto refresh online serverlist every X seconds. Set to 0 to disable auto refresh")]
        private uint m_autoRefreshTimer = 0;
        private float m_currentTimer = 0f;

        //HUD layout varibles
        private const int HUD_BACKGROUND_HEIGHT = 230;
        private const int HUD_CONTENT_WIDTH = 215;
        private const int HUD_CONTENT_HEIGHT_CONNECTING = 50;
        private const int HUD_CONTENT_HEIGHT_CONNECTED = 130;
        private const int HUD_CONTENT_HEIGHT_NOT_CONNECTED = 200;
        private const int HUD_CONNECT_BUTTON_WIDTH = 80;
        private const int HUD_SCROLL_HEIGHT = 75;
        private const int HUD_PADDING_LEFT = 3;
        private const int HUD_PADDING_TOP = 10;
        private GUIStyle m_errorMessageStyle;

        //HUD Position and size
        private Rect m_windowSize;
        
        //Error msg
        private string m_errorMessage;

        //True if there's an active request to fetch rooms
        private bool m_gettingRoom;

        /// <summary>Start is called before the first frame update</summary>
        private void Start()
        {
            //Default size
            m_windowSize = new Rect(m_offset.x, m_offset.y, HUD_BACKGROUND_HEIGHT, HUD_CONTENT_HEIGHT_NOT_CONNECTED);

            //Get a list of rooms on start
            GetRooms();
        }

        /// <summary>Update room list</summary>
        void Update()
        {
            //Auto refresh
            if (m_autoRefreshTimer > 0)
            {
                m_currentTimer += Time.deltaTime;
                if (m_currentTimer > m_autoRefreshTimer)
                {
                    m_currentTimer = 0f;
                    GetRooms();
                }
            }
        }

        /// <summary>Update/Render the user interface.</summary>
        private void OnGUI()
        {
            //Create style. This has to be created from OnGUI.
            if (m_errorMessageStyle == null)
            {
                m_errorMessageStyle = new GUIStyle(GUI.skin.label);
                m_errorMessageStyle.normal.textColor = Color.red;
            }

            GUI.backgroundColor = Color.black;
            m_windowSize = GUI.Window(0, m_windowSize, DrawGUI, "Click here to drag");
        }

        /// <summary>
        /// Draw GUI
        /// </summary>
        /// <param name="windowId">Id of GUI Window</param>
        public void DrawGUI(int windowId)
        {
            if (m_room != null && m_room.IsConnecting)
            {
                m_windowSize = new Rect(m_windowSize.x, m_windowSize.y, m_windowSize.width, HUD_CONTENT_HEIGHT_CONNECTING);
                GUILayout.BeginArea(new Rect(m_offset.x + HUD_PADDING_LEFT, m_offset.y + HUD_PADDING_TOP, HUD_CONTENT_WIDTH, HUD_CONTENT_HEIGHT_CONNECTING));

                GUILayout.Label("Connecting...");
            }
            else if (m_room != null && m_room.IsConnected)
            {
                m_windowSize = new Rect(m_windowSize.x, m_windowSize.y, m_windowSize.width, HUD_CONTENT_HEIGHT_CONNECTED);
                GUILayout.BeginArea(new Rect(m_offset.x + HUD_PADDING_LEFT, m_offset.y + HUD_PADDING_TOP, HUD_CONTENT_WIDTH, HUD_CONTENT_HEIGHT_CONNECTED));

                GUILayout.BeginHorizontal();
                GUILayout.Label($"Connected to {m_room.Address}");
                GUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Disconnect"))
                {
                    m_room.Disconnect();
                }
                GUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();
            }
            else
            {
                m_windowSize = new Rect(m_windowSize.x, m_windowSize.y, m_windowSize.width, HUD_CONTENT_HEIGHT_NOT_CONNECTED);
                GUILayout.BeginArea(new Rect(m_offset.x + HUD_PADDING_LEFT, m_offset.y + HUD_PADDING_TOP, HUD_CONTENT_WIDTH, HUD_CONTENT_HEIGHT_NOT_CONNECTED));

                //Direct Connection
                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>Direct Connection</b>");
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();

                //Host
                Host = GUILayout.TextField(Host);

                //Port
                if (UInt16.TryParse(GUILayout.TextField(Port.ToString()), out ushort port))
                {
                    Port = port;
                }

                //ConnectButton
                if (GUILayout.Button("Connect", GUILayout.Width(HUD_CONNECT_BUTTON_WIDTH)))
                {
                    ksRoomInfo roomInfo = FindAnyObjectByType<ksRoomType>().GetRoomInfo(Host, Port);
                    ConnectToRoom(roomInfo);
                }

                GUILayout.EndHorizontal();
                GUILayout.Label(m_errorMessage, m_errorMessageStyle) ;

                //Online Servers
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>Online Servers ({m_roomList.Count})</b>");
                if (m_autoRefreshTimer == 0)
                { 
                    if (GUILayout.Button("Refresh", GUILayout.Width(HUD_CONNECT_BUTTON_WIDTH)))
                    {
                        GetRooms();
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (String.IsNullOrEmpty(ksReactorConfig.Instance.Build.ImageBinding))
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"<b>No published image</b>");
                    GUILayout.FlexibleSpace();
                }
                GUILayout.EndHorizontal();

                m_scrollPosition = GUILayout.BeginScrollView(m_scrollPosition, GUILayout.Width(HUD_CONTENT_WIDTH), GUILayout.Height(HUD_SCROLL_HEIGHT));
                foreach (ksRoomInfo roomInfo in m_roomList)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"<b>{roomInfo.Id} {roomInfo.Name}</b>");
                    if (GUILayout.Button("Connect", GUILayout.Width(HUD_CONNECT_BUTTON_WIDTH)))
                    {
                        ConnectToRoom(roomInfo);
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
            }
            GUILayout.EndArea();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        #region Connection Logic

        /// <summary>
        /// Get a list of rooms that this project is allowed to connect to.
        /// If the connection type is ONLINE then make an API request for a list of rooms.If a direct connection is required then
        /// use the host and port properties to create a room info connection object and then attempt to connect to the room.
        /// </summary>
        public void GetRooms()
        {
            if (!m_gettingRoom)
            {
                m_gettingRoom = true;
                if (!String.IsNullOrEmpty(ksReactorConfig.Instance.Build.ImageBinding))
                {
                    ksReactor.GetServers(OnGetRooms);
                }
            }
        }

        /// <summary>
        /// Handle the response from a ksReactor.GetServers call.
        /// Store the list of rooms found locally
        /// Override to disable or modify.
        /// </summary>
        /// <param name="rooms">List of available rooms</param>
        /// <param name="error">Description of a Reactor web API service error.</param>
        private void OnGetRooms(List<ksRoomInfo> rooms, string error)
        {
            m_gettingRoom = false;

            //Check for error
            if (!string.IsNullOrEmpty(error))
            {
                ksLog.Error(this, "Error fetching room list. " + error);
                return;
            }

            //Check for empty room list
            if (rooms == null || rooms.Count == 0)
            {
                return;
            }

            m_roomList = rooms;
        }

        /// <summary>
        /// Create and connect to a new room defined in roomInfo.
        /// Registers an OnConnect handler, OnDisconnect handler, 
        /// </summary>
        /// <param name="roomInfo">Information needed to connect to a server room.</param>
        public virtual void ConnectToRoom(ksRoomInfo roomInfo)
        {
            m_errorMessage = null;
            m_room = new ksRoom(roomInfo);
            m_room.OnConnect += OnConnect;
            m_room.OnDisconnect += OnDisconnect;

#if UNITY_WEBGL && !UNITY_EDITOR
            m_room.Protocol = ksConnectionProtocols.WEBSOCKETS;
            ksAddress wsAddress = roomInfo.GetAddress(ksConnectionProtocols.WEBSOCKETS);
            if (wsAddress.IsValid)
            {
                ksWSConnection.Config.Secure = wsAddress.Host != "localhost" && wsAddress.Host == "127.0.0.1";
            }
#endif
            m_room.Connect();
        }

        /// <summary>Log error if connection failed. Override to disable or modify</summary>
        /// <param name="status">Connection status.</param>
        /// <param name="result">Authentication result from user-defined authentication handlers.</param>
        protected virtual void OnConnect(ksBaseRoom.ConnectStatus status, ksAuthenticationResult result)
        {
            if (status == ksBaseRoom.ConnectStatus.SUCCESS)
            {
                ksLog.Info("Connected to " + m_room);
            }
            else
            {
                m_errorMessage = "Connection failed. " + status;
                if (status == ksBaseRoom.ConnectStatus.AUTH_ERR_USER_DEFINED)
                {
                    m_errorMessage += " " + result;
                }
                ksLog.Error(this, "Unable to connect to " + m_room + ". " + status + " " + result);
                m_room.CleanUp();
                m_room = null;
            }
        }

        /// <summary>Cleanup room on disconnect. Override to disable or modify</summary>
        /// <param name="status">Connection status.</param>
        protected virtual void OnDisconnect(ksBaseRoom.ConnectStatus status)
        {
            ksLog.Info("Disconnected from " + m_room + ". Status = " + status);
            m_room.CleanUp();
            m_room = null;
        }

        #endregion
    }
}