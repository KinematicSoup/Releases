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
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace KS.Reactor.Client.Unity
{
    /// <summary>
    /// Reactor Connection component for connecting to Reactor server rooms and registering event handlers.
    /// </summary>
    public class ksConnect : MonoBehaviour
    {
        /// <summary>List of protocols the connection script uses to connect to a server.</summary>
        public enum ConnectProtocols
        {
            /// <summary>TCP</summary>
            TCP = 0,
            /// <summary>Reliable UDP</summary>
            RUDP = 2
        };

        /// <summary>List of methods the connection script uses to find a server.</summary>
        public enum ConnectModes
        {
            /// <summary>Use the Reactor API to find and connect to a public server.</summary>
            ONLINE,
            /// <summary>Connect directly to a remote server using a host and port.</summary>
            REMOTE,
            /// <summary>Connect to the localhost using the ksRoomType configuration.</summary>
            LOCAL
        };

        /// <summary> Get rooms event passed to OnGetRooms event handlers.</summary>
        public struct GetRoomsEvent
        {
            /// <summary><see cref="ksConnect"/> instance which generated the event.</summary>
            public ksConnect Caller;
            /// <summary>Event status</summary>
            public ksBaseRoom.ConnectStatus Status;
            /// <summary>List of rooms found.</summary>
            public List<ksRoomInfo> Rooms;
            /// <summary>Error message returned when Status = <see cref="ksBaseRoom.ConnectStatus.GET_ROOMS_ERROR"/></summary>
            public string Error;
        }

        /// <summary> Connect event passed to OnConnect event handlers.</summary>
        public struct ConnectEvent
        {
            /// <summary><see cref="ksConnect"/> instance which generated the event.</summary>
            public ksConnect Caller;
            /// <summary>Event status</summary>
            public ksBaseRoom.ConnectStatus Status;
            /// <summary>Authentication result from user-defined authentication handlers.</summary>
            public ksAuthenticationResult Result;

            /// <summary>Custom status returned by a failed server authentication method when Status = <see cref="ksBaseRoom.ConnectStatus.AUTH_ERR_USER_DEFINED"/>.</summary>
            [Obsolete("Use Result.Code")]
            public uint CustomStatus
            {
                get { return Result.Code; }
            }
        }

        /// <summary> Disconnect event passed to OnDisconnect event handlers.</summary>
        public struct DisconnectEvent
        {
            /// <summary><see cref="ksConnect"/> instance which generated the event.</summary>
            public ksConnect Caller;
            /// <summary>Event status</summary>
            public ksBaseRoom.ConnectStatus Status;
        }


        [Tooltip("When enabled, the script will attempt to connect to a room when the script is started.")]
        public bool ConnectOnStart = true;

        [Tooltip("Connection protocol to use for connections.")]
        public ConnectProtocols ConnectProtocol = ConnectProtocols.TCP;

        [ksEnum]
        [FormerlySerializedAs("ConnectionMethod")]
        [Tooltip("Process used by the script to find a room.")]
        public ConnectModes ConnectMode = ConnectModes.LOCAL;

        [FormerlySerializedAs("Host")]
        [Tooltip("Remote host address used for connections using the REMOTE connect mode.")]
        public string RemoteHost = "localhost";
        
        [FormerlySerializedAs("Port")]
        [Tooltip("Remote port used for connections using the REMOTE connect mode.")]
        public ushort RemotePort = 8000;

        [Tooltip("Use a secure web socket connection protocol for connecting to localhost in webgl builds.")]
        public bool SecureLocalWebSockets = false;

        [Tooltip("Event handlers called when using the ONLINE connect mode.")]
        public UnityEvent<GetRoomsEvent> OnGetRooms = new UnityEvent<GetRoomsEvent>();

        [Tooltip("Event handlers called when connecting to a room.")]
        public UnityEvent<ConnectEvent> OnConnect = new UnityEvent<ConnectEvent>();

        [Tooltip("Event handlers called when disconnected from a room.")]
        public UnityEvent<DisconnectEvent> OnDisconnect = new UnityEvent<DisconnectEvent>();

        [HideInInspector] public ksRoom Room = null;

        /// <summary>Check if the room connection should start automatically.</summary>
        void Start()
        {
            if (ConnectOnStart)
            {
                BeginConnect();
            }
        }

        /// <summary>Begin connecting to a room.</summary>
        public void BeginConnect()
        {
            if (ConnectMode == ConnectModes.ONLINE)
            {
                ksLog.Debug(this, $"Fetching online room list.");
                ksReactor.GetServers(HandleGetRooms);
            }
            else
            {
                ksRoomType roomType = GetComponent<ksRoomType>();
                string host = ConnectMode == ConnectModes.REMOTE ? RemoteHost : "127.0.0.1";
                ushort port = ConnectMode == ConnectModes.REMOTE ? RemotePort :
                    roomType == null ? (ushort)8000 : roomType.LocalServerPort;
                ksRoomInfo roomInfo = roomType != null ? roomType.GetRoomInfo(host, port) : new ksRoomInfo(host, port);
                HandleGetRooms(new List<ksRoomInfo>{ roomInfo }, null);
            }
        }

        /// <summary>Connect to a room.</summary>
        /// <param name=""roomInfo"">Room connection paremeters.</param>
        public void Connect(ksRoomInfo roomInfo, params ksMultiType[] connectParams)
        {
            if (Room != null)
            {
                return;
            }

            Room = new ksRoom(roomInfo);
            Room.OnConnect += HandleConnect;
            Room.OnDisconnect += HandleDisconnect;
#if UNITY_WEBGL && !UNITY_EDITOR
            // Reactor WebGL builds only support websocket connections.
            Room.Protocol = ksConnectionProtocols.WEBSOCKETS;
            if (ConnectMode == ConnectModes.LOCAL)
            {
                ksAddress wsAddress = roomInfo.GetAddress(ksConnectionProtocols.WEBSOCKETS);
                if (wsAddress.IsValid)
                {
                    ksWSConnection.Config.Secure = (wsAddress.Host != "localhost" && wsAddress.Host != "127.0.0.1") ||
                        SecureLocalWebSockets;
                }
            }
#else
            Room.Protocol = (ksConnectionProtocols)ConnectProtocol;
#endif
            Room.Connect(connectParams);
        }

        /// <summary> Disconnect from a connected room.</summary>
        public void Disconnect(bool immediate)
        {
            if (Room != null)
            {
                Room.Disconnect(immediate);
            }
        }

        /// <summary>Handle a response to a KSReactor.GetServer request./// </summary>
        /// <param name=""rooms"">List of available rooms.</param>
        /// <param name=""error"">Request error, null if no error occured.</param>
        private void HandleGetRooms(List<ksRoomInfo> rooms, string error)
        {
            if (GetRegisteredEventHandlerCount(OnGetRooms) > 0)
            {
                OnGetRooms.Invoke(new GetRoomsEvent()
                {
                    Caller = this,
                    Status = string.IsNullOrEmpty(error) ? ksBaseRoom.ConnectStatus.SUCCESS : ksBaseRoom.ConnectStatus.GET_ROOMS_ERROR,
                    Rooms = rooms,
                    Error = error
                });
            }
            else
            {
                // Default handler will connect to the first available room.
                if (!string.IsNullOrEmpty(error))
                {
                    ksLog.Warning(this, $"Unable to fetch online rooms. {error}");
                }
                else if (rooms == null || rooms.Count == 0)
                {
                    ksLog.Debug(this, $"No online rooms found.");
                }
                else
                {
                    Connect(rooms[0]);
                }
            }
        }

        /// <summary>Handle room connection events.</summary>
        /// <param name=""status"">Connection status.</param>
        /// <param name="result">Authentication result from user-defined authentication handlers.</param>
        private void HandleConnect(ksBaseRoom.ConnectStatus status, ksAuthenticationResult result)
        {
            if (status == ksBaseRoom.ConnectStatus.SUCCESS)
            {
                ksLog.Debug(this, $"Connected to room {Room.Address}");
                OnConnect.Invoke(new ConnectEvent()
                {
                    Caller = this,
                    Status = status,
                    Result = result
                });
            }
            else
            {
                ksLog.Warning(this, $"Unable to connect to room {Room.Address}. {status} {result}");
                OnConnect.Invoke(new ConnectEvent()
                {
                    Caller = this,
                    Status = status,
                    Result = result
                });
                Room.CleanUp();
                Room = null;
            }
        }

        /// <summary>Handle room disconnect events.</summary>
        /// <param name=""status"">Disconnect reason.</param>
        private void HandleDisconnect(ksBaseRoom.ConnectStatus status)
        {
            ksLog.Info(this, $"Disconnected from room {Room.Address}. Status = {status}");
            OnDisconnect.Invoke(new DisconnectEvent()
            {
                Caller = this,
                Status = status
            });
            Room.CleanUp();
            Room = null;
        }

        /// <summary>Get the number of registered event handlers on a unity event object.</summary>
        /// <param name="unityEvent">Unity event object.</param>
        /// <returns>Number of registered unity event handlers.</returns>
        private int GetRegisteredEventHandlerCount(UnityEventBase unityEvent)
        {
            if (unityEvent == null)
            {
                return 0;
            }

            FieldInfo field = typeof(UnityEventBase).GetField("m_Calls", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null)
            {
                object invokeCallList = field.GetValue(unityEvent);
                if (invokeCallList != null)
                {
                    PropertyInfo property = invokeCallList.GetType().GetProperty("Count");
                    if (property != null)
                    {
                        return unityEvent.GetPersistentEventCount() + (int)property.GetValue(invokeCallList);
                    }
                }
            }

            return unityEvent.GetPersistentEventCount();
        }
    }
}
