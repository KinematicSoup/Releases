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
using Unity.Entities;

namespace KS.Reactor.Client.Unity.DOTS
{
    /// <summary>
    /// Managed component that stores data the <see cref="ksConnectSystem"/> uses to connect and stores a 
    /// <see cref="ksRoomDOTS"/> once a connection is made.
    /// </summary>
    public class ksConnectComponent : IComponentData
    {
        /// <summary>Connection protocol to use for connections.</summary>
        public ksConnectionProtocols Protocol;

        /// <summary>Process used by the system to find a room.</summary>
        public ksConnect.ConnectModes Mode;

        /// <summary>
        /// Remote host address used for connections using the <see cref="ksConnect.ConnectModes.REMOTE"/> connect mode.
        /// </summary>
        public string RemoteHost = "localhost";

        /// <summary>
        /// Remote port used for connections using the <see cref="ksConnect.ConnectModes.REMOTE"/> or 
        /// <see cref="ksConnect.ConnectModes.LOCAL"/> connect mode.
        /// </summary>
        public ushort Port = 8000;

        /// <summary>
        /// Use a secure web socket connection protocol for connecting to localhost in webgl builds using the
        /// <see cref="ksConnect.ConnectModes.LOCAL"/> connect mode.
        /// </summary>
        public bool SecureLocalWebSockets = false;

        /// <summary>
        /// Scene used for connections using the <see cref="ksConnect.ConnectModes.REMOTE"/> or
        /// <see cref="ksConnect.ConnectModes.LOCAL"/> connect mode. Should match the name of the authoring sub scene
        /// the <see cref="ksRoomType"/> component was in.
        /// </summary>
        public string Scene;

        /// <summary>
        /// Room type used for connections using the <see cref="ksConnect.ConnectModes.REMOTE"/> or
        /// <see cref="ksConnect.ConnectModes.LOCAL"/> connect mode. Should match the name of a game object in the 
        /// authoring sub scene that had a <see cref="ksRoomType"/> component.
        /// </summary>
        public string RoomType;

        /// <summary>Connect system that is handling this connection.</summary>
        internal ksConnectSystem System;

        /// <summary>The room connection. Null before a connection is started and after disconnecting.</summary>
        public ksRoomDOTS Room
        {
            get { return m_room; }
            internal set
            {
                if (m_room == value)
                {
                    return;
                }
                if (m_room != null)
                {
                    m_room.OnConnect -= HandleConnect;
                    m_room.OnDisconnect -= HandleDisconnect;
                }
                m_room = value;
                if (m_room != null)
                {
                    m_room.OnConnect += HandleConnect;
                    m_room.OnDisconnect += HandleDisconnect;
                }
            }
        }

        private ksRoomDOTS m_room;

        /// <summary>Handle room connection events.</summary>
        /// <param name=""status"">Connection status.</param>
        /// <param name="result">Authentication result from user-defined authentication handlers.</param>
        private void HandleConnect(ksBaseRoom.ConnectStatus status, ksAuthenticationResult result)
        {
            if (status == ksBaseRoom.ConnectStatus.SUCCESS)
            {
                ksLog.Debug(this, $"Connected to room {Room.Address}");
                ksConnectSystem.ConnectEvent ev = new ksConnectSystem.ConnectEvent()
                {
                    Component = this,
                    Status = status,
                    Result = result
                };
                System.InvokeOnConnect(ref ev);
            }
            else
            {
                ksLog.Warning(this, $"Unable to connect to room {Room.Address}. {status} {result}");
                ksConnectSystem.ConnectEvent ev = new ksConnectSystem.ConnectEvent()
                {
                    Component = this,
                    Status = status,
                    Result = result
                };
                System.InvokeOnConnect(ref ev);
                Room.CleanUp();
                Room = null;
            }
        }

        /// <summary>Handle room disconnect events.</summary>
        /// <param name=""status"">Disconnect reason.</param>
        private void HandleDisconnect(ksBaseRoom.ConnectStatus status)
        {
            ksLog.Info(this, $"Disconnected from room {Room.Address}. Status = {status}");
            ksConnectSystem.DisconnectEvent ev = new ksConnectSystem.DisconnectEvent()
            {
                Component = this,
                Status = status
            };
            System.InvokeOnDisconnect(ref ev);
            Room.CleanUp();
            Room = null;
        }
    }
}