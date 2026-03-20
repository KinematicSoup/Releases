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
using System.Collections.Generic;
using System;
using Unity.Entities;
using Unity.Collections;

namespace KS.Reactor.Client.Unity.DOTS
{
    // Needed to prevent 'X does not exist in the namespace KS.Reactor.Client.Unity' build error in generated code.
    using Unity = global::Unity;

    /// <summary>
    /// System for making connections to the Reactor server and registering event handlers. Looks for DOTS entities
    /// with a <see cref="ksConnectComponent"/> and a <see cref="ksAutoConnectTag"/> and will remove the
    /// <see cref="ksAutoConnectTag"/> and make a room connection for any that it finds. You can call
    /// <see cref="BeginConnect()"/> to make a room connection for entities with a <see cref="ksConnectComponent"/> that
    /// don't have a <see cref="ksAutoConnectTag"/> and don't already have an active connection.
    /// </summary>
    public partial class ksConnectSystem : SystemBase
    {
        /// <summary>Gets the connect system from <see cref="World.DefaultGameObjectInjectionWorld"/>.</summary>
        /// <returns></returns>
        public static ksConnectSystem Get()
        {
            return World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<ksConnectSystem>();
        }

        /// <summary>Get rooms event passed to <see cref="OnGetRooms"/> event handlers.</summary>
        public struct GetRoomsEvent
        {
            /// <summary>Component that generated the event.</summary>
            public ksConnectComponent Component;
            /// <summary>Event status</summary>
            public ksBaseRoom.ConnectStatus Status;
            /// <summary>List of rooms found.</summary>
            public List<ksRoomInfo> Rooms;
            /// <summary>Error message returned when Status = <see cref="ksBaseRoom.ConnectStatus.GET_ROOMS_ERROR"/></summary>
            public string Error;
        }

        /// <summary>Connect event passed to <see cref="OnConnect"/> event handlers.</summary>
        public struct ConnectEvent
        {
            /// <summary>Component that generated the event.</summary>
            public ksConnectComponent Component;
            /// <summary>Event status</summary>
            public ksBaseRoom.ConnectStatus Status;
            /// <summary>Authentication result from user-defined authentication handlers.</summary>
            public ksAuthenticationResult Result;
        }
        
        /// <summary>Disconnect event passed to <see cref="OnDisconnect"/> event handlers.</summary>
        public struct DisconnectEvent
        {
            /// <summary>Component that generated the event.</summary>
            public ksConnectComponent Component;
            /// <summary>Event status</summary>
            public ksBaseRoom.ConnectStatus Status;
        }

        /// <summary>
        /// Invoked after getting the list of running rooms. For <see cref="ksConnect.ConnectModes.REMOTE"/> and
        /// <see cref="ksConnect.ConnectModes.LOCAL"/> connections, this is invoked with a single
        /// <see cref="ksRoomInfo"/> with the remote address or localhost respectively. If no handlers are registered,
        /// it will automatically connect to the first room in the list. If any handlers are registered, a connection
        /// will not be made automatically and the handlers will be responsible for connecting to a room. Handlers can
        /// call <see cref="Connect(ksConnectComponent, ksRoomInfo, ksMultiType[])"/> to connect.
        /// </summary>
        public event Action<GetRoomsEvent> OnGetRooms;

        /// <summary>
        /// Invoked after connecting to a room. This will not be invoked for room connections that were made without
        /// using this system.
        /// </summary>
        public event Action<ConnectEvent> OnConnect;

        /// <summary>
        /// Invoked after disconnect from a room. This will not be invoked for room connections that were made without
        /// using this system.
        /// </summary>
        public event Action<DisconnectEvent> OnDisconnect;

        /// <summary>
        /// Set of components to call <see cref="OnGetRooms"/> for when we get the list of online rooms.
        /// </summary>
        private HashSet<ksConnectComponent> m_onlineConnectComponents = new HashSet<ksConnectComponent>();

        /// <summary>
        /// Adds update requirements on <see cref="ksAutoConnectTag"/> and <see cref="ksConnectComponent"/>.
        /// </summary>
        protected override void OnCreate()
        {
            RequireForUpdate<ksAutoConnectTag>();
            RequireForUpdate<ksConnectComponent>();
        }

        /// <summary>
        /// Looks for DOTS entities with a <see cref="ksAutoConnectTag"/> and a <see cref="ksConnectComponent"/>.
        /// Removes the <see cref="ksAutoConnectTag"/> and makes a connection for any that it finds.
        /// </summary>
        protected override void OnUpdate()
        {
            // Because structural changes are not allowed while iterating an entity query, we add the connect components
            // to a list and make the connections when we are done iterating the query. We don't make any stuctural
            // changes when we begin a connection, but it is possible that developer code could in an OnGetRooms
            // handler.
            List<ksConnectComponent> connectComponents = new List<ksConnectComponent>();
            EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach ((ksConnectComponent component, Entity ent) in SystemAPI.Query<ksConnectComponent>()
                .WithAll<ksAutoConnectTag>()
                .WithEntityAccess())
            {
                commandBuffer.RemoveComponent<ksAutoConnectTag>(ent);
                connectComponents.Add(component);
            }
            Dependency.Complete();
            commandBuffer.Playback(EntityManager);

            for (int i = 0; i < connectComponents.Count; i++)
            {
                BeginConnect(connectComponents[i]);
            }
        }


        /// <summary>
        /// Looks for DOTS entities with <see cref="ksConnectComponent"/> that don't have an active connection and makes
        /// connections for them.
        /// </summary>
        public void BeginConnect()
        {
            // Because structural changes are not allowed while iterating an entity query, we add the connect components
            // to a list and make the connections when we are done iterating the query. We don't make any stuctural
            // changes when we begin a connection, but it is possible that developer code could in an OnGetRooms
            // handler.
            List<ksConnectComponent> connectComponents = new List<ksConnectComponent>();
            foreach (ksConnectComponent component in SystemAPI.Query<ksConnectComponent>())
            {
                connectComponents.Add(component);
            }

            for (int i = 0; i < connectComponents.Count; i++)
            {
                BeginConnect(connectComponents[i]);
            }
        }

        /// <summary>
        /// Begins connecting to a room. How it connects is determined by <see cref="ksConnectComponent.Mode"/>.
        /// Does nothing if the <paramref name="component"/> already has an active connection/
        /// </summary>
        /// <param name="component">Component to make the connection with.</param>
        public void BeginConnect(ksConnectComponent component)
        {
            if (component.Room != null && (component.Room.IsConnected || component.Room.IsConnecting))
            {
                return;
            }

            if (component.Mode == ksConnect.ConnectModes.ONLINE)
            {
                if (m_onlineConnectComponents.Count == 0)
                {
                    ksReactor.GetServers(HandleGetRooms);
                }
                m_onlineConnectComponents.Add(component);
            }
            else
            {
                string host = component.Mode == ksConnect.ConnectModes.LOCAL ? "localhost" : component.RemoteHost;
                ksRoomInfo roomInfo = new ksRoomInfo(host, component.Port);
                roomInfo.Scene = component.Scene;
                roomInfo.Type = component.RoomType;
                GetRoomsEvent ev = new GetRoomsEvent()
                {
                    Component = component,
                    Status = ksBaseRoom.ConnectStatus.SUCCESS,
                    Rooms = new List<ksRoomInfo>() { roomInfo }
                };
                HandleGetRooms(ref ev);
            }
        }

        /// <summary>Connects to a room.</summary>
        /// <param name="component">
        /// Component to make the connection with. Does nothing if the component already has an active connection.
        /// <see cref="ksConnectComponent.RemoteHost"/>, <see cref="ksConnectComponent.Port"/>, 
        /// <see cref="ksConnectComponent.Scene"/>, and <see cref="ksConnectComponent.RoomType"/> are ignored and the
        /// values from <paramref name="roomInfo"/> are used instead.
        /// </param>
        /// <param name="roomInfo">Room connection info.</param>
        /// <param name="connectParams">Room connection parameters passed to the server OnAuthenticate handler.</param>
        public void Connect(ksConnectComponent component, ksRoomInfo roomInfo, params ksMultiType[] connectParams)
        {
            if (component.Room != null && (component.Room.IsConnected || component.Room.IsConnecting))
            {
                return;
            }
            component.System = this;
            component.Room = new ksRoomDOTS(roomInfo);
#if UNITY_WEBGL && !UNITY_EDITOR
            // Reactor WebGL builds only support websocket connections.
            component.Room.Protocol = ksConnectionProtocols.WEBSOCKETS;
            if (component.Mode == ConnectModes.LOCAL)
            {
                ksAddress wsAddress = roomInfo.GetAddress(ksConnectionProtocols.WEBSOCKETS);
                if (wsAddress.IsValid)
                {
                    ksWSConnection.Config.Secure = (wsAddress.Host != "localhost" && wsAddress.Host != "127.0.0.1") ||
                        component.SecureLocalWebSockets;
                }
            }
#else
            component.Room.Protocol = component.Protocol;
#endif
            component.Room.Connect(connectParams);
        }

        /// <summary>
        /// Handle a response to a <see cref="ksReactorSystem.GetServers(ksService.RoomListCallback)"/> request.
        /// </summary>
        /// <param name="rooms">List of available rooms.</param>
        /// <param name="error">Request error, null if no error occured.</param>
        private void HandleGetRooms(List<ksRoomInfo> rooms, string error)
        {
            GetRoomsEvent ev = new GetRoomsEvent()
            {
                Status = string.IsNullOrEmpty(error) ? ksBaseRoom.ConnectStatus.SUCCESS : ksBaseRoom.ConnectStatus.GET_ROOMS_ERROR,
                Rooms = rooms,
                Error = error
            };
            foreach (ksConnectComponent component in m_onlineConnectComponents)
            {
                ev.Component = component;
                HandleGetRooms(ref ev);
            }
            m_onlineConnectComponents.Clear();
        }

        /// <summary>
        /// Invokes <see cref="OnGetRooms"/> if there are any handlers, otherwise connects to the first room in the
        /// list or logs an error message.
        /// </summary>
        /// <param name="ev">Get room event parameters</param>
        private void HandleGetRooms(ref GetRoomsEvent ev)
        {
            if (OnGetRooms != null)
            {
                OnGetRooms(ev);
                return;
            }

            // Default handler will connect to the first available room.
            if (!string.IsNullOrEmpty(ev.Error))
            {
                ksLog.Warning(this, $"Unable to fetch online rooms. {ev.Error}");
            }
            else if (ev.Rooms == null || ev.Rooms.Count == 0)
            {
                ksLog.Debug(this, $"No online rooms found.");
            }
            else
            {
                Connect(ev.Component, ev.Rooms[0]);
            }
        }

        /// <summary>Invokes <see cref="OnConnect"/> handlers.</summary>
        /// <param name="ev">On connect event parameters.</param>
        internal void InvokeOnConnect(ref ConnectEvent ev)
        {
            if (OnConnect != null)
            {
                OnConnect(ev);
            }
        }

        /// <summary>Invokes <see cref="OnDisconnect"/> handlers.</summary>
        /// <param name="ev">On disconnect event parameters.</param>
        internal void InvokeOnDisconnect(ref DisconnectEvent ev)
        {
            if (OnDisconnect != null)
            {
                OnDisconnect(ev);
            }
        }
    }
}