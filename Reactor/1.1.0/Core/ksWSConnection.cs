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

// Websockets are only used for WEBGL builds and not in the editor
#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using AOT;

namespace KS.Reactor.Client.Unity
{
    /// <summary>Network stream connection using a KinematicSoup javascript websocket library.</summary>
    public class ksWSConnection : ksConnection
    {
        /// <summary>Javascript WebSocket connect handler.</summary>
        /// <param name="id">WebSocket ID</param>
        private delegate void JSConnectHandler(int id);

        /// <summary>Javascript WebSocket disconnect handler.</summary>
        /// <param name="id">WebSocket ID</param>
        /// <param name="code">Diconnect error code.</param>
        private delegate void JSDisconnectHandler(int id, int code);

        /// <summary>Javascript WebSocket message handler.</summary>
        /// <param name="id">WebSocket ID</param>
        /// <param name="available">Amount of data available.</param>
        private delegate void JSMessageHandler(int id, int available);

        /// <summary>Create a javascript WebSocket.</summary>
        /// <param name="connectCallback">Callback to invoke when a connection is established.</param>
        /// <param name="disconnectCallback">Callback to invoke when a connection is disconnected.</param>
        /// <param name="messageCallback">Callback to invoke when a new message is received.</param>
        /// <returns>WebSocket ID</returns>
        [DllImport("__Internal")]
        private static extern int KSCreateWS(
            JSConnectHandler connectCallback,
            JSDisconnectHandler disconnectCallback,
            JSMessageHandler messageCallback
        );

        /// <summary>Cleanup the javascript websocket object.</summary>
        /// <param name="id">WebSocket ID</param>
        [DllImport("__Internal")]
        private static extern void KSDisposeWS(int id);

        /// <summary>Connect a javascript websocket to a remote host.</summary>
        /// <param name="id">WebSocket ID</param>
        /// <param name="url">Server URL</param>
        [DllImport("__Internal")]
        private static extern void KSConnectWS(int id, string url);

        /// <summary>Disconnect a javascript websocket.</summary>
        /// <param name="id">WebSocket ID</param>
        [DllImport("__Internal")]
        private static extern void KSDisconnectWS(int id);

        /// <summary>Copy received data from a javascript WebSocket into a buffer.</summary>
        /// <param name="id">WebSocket ID</param>
        /// <param name="data">Data buffer</param>
        /// <param name="offset">Position in the buffer to start reading data into.</param>
        /// <param name="count">Amount of data to read.</param>
        /// <returns>Amount of data remaining in the WebSocket buffer.</returns>
        [DllImport("__Internal")]
        private static extern int KSReadWS(int id, byte[] data, int offset, int count);

        /// <summary>Write data from a buffer into a javascript WebSocket send message.</summary>
        /// <param name="id">WebSocket ID</param>
        /// <param name="data">Data buffer</param>
        /// <param name="offset">Position in the buffer to start reading data from.</param>
        /// <param name="count">Amount of data to write.</param>
        [DllImport("__Internal")]
        private static extern void KSWriteWS(int id, byte[] data, int offset, int count);

        // Collection of javascript websocket IDs to c# websocket objects.
        private static Dictionary<int, ksWSConnection> m_websockets = new Dictionary<int, ksWSConnection>();

        /**
         * Configuration options for Websocket connections
         */
        public class ConfigSettings
        {
            public bool Secure = true;
        }

        /**
         * Configuration settings used for new websocket connections.
         */
        public static ConfigSettings Config
        {
            get { return m_config; }
        }
        private static ConfigSettings m_config = new ConfigSettings();

        private int m_instanceId = 0;
        private int m_available = 0;
        private bool m_disconnecting = false;
        private Queue<IOOperation> m_readQueue = new Queue<IOOperation>();
        private ConnectOperation m_connectState = null;

        /// <summary>Disconnect event to invoke on a connection disconnect.</summary>
        public override event DisconnectHandler OnDisconnect;

        /// <summary>Registers a websocket factory for all <see cref="ksRoom"/> connections.</summary>
        public static void Register()
        {
            ksBaseRoom.RegisterConnectionFactory(ksConnectionProtocols.WEBSOCKETS, () => { return new ksWSConnection(); });
        }

        /// <summary>WebSocket connect handler.</summary>
        /// <param name="id">WebSocket ID</param>
        [MonoPInvokeCallback(typeof(JSConnectHandler))]
        private static void JSHandleConnect(int id)
        {
            ksWSConnection ws = null;
            if (m_websockets.TryGetValue(id, out ws))
            {
                ws.HandleConnect();
            }
        }

        /// <summary>WebSocket disconnect handler.</summary>
        /// <param name="id">WebSocket ID</param>
        /// <param name="code">Disconnect error code</param>
        [MonoPInvokeCallback(typeof(JSDisconnectHandler))]
        private static void JSHandleDisconnect(int id, int code)
        {
            ksWSConnection ws = null;
            if (m_websockets.TryGetValue(id, out ws))
            {
                ws.HandleDisconnect(code);
            }
        }

        /// <summary>Websocket message handler.</summary>
        /// <param name="id">WebSocket ID</param>
        /// <param name="length">Amount of data available.</param>
        [MonoPInvokeCallback(typeof(JSMessageHandler))]
        private static void JSHandleMessage(int id, int length)
        {
            ksWSConnection ws = null;
            if (m_websockets.TryGetValue(id, out ws))
            {
                ws.HandleMessage(length);
            }
        }

        /// <summary>Is there an open connection to a remote host.</summary>
        public override bool IsConnected
        {
            get { return m_instanceId > 0; }
        }

        /// <summary>
        /// Unused on Websocket connections.
        /// </summary>
        public override ksFrameInfo ReadProgress {
            get { return new ksFrameInfo(0, 0); }
        }

        /// <summary>
        /// Unused on Websocket connections.
        /// </summary>
        public override ksFrameInfo WriteProgress {
            get { return new ksFrameInfo(0, 0); }
        }

        /// <summary>Begin connecting to a websocket server using the url wss://host:port</summary>
        /// <param name="host">Host name</param>
        /// <param name="port">Host port</param>
        /// <param name="callback">Callback to invoke when a connection attempt completes.</param>
        /// <param name="asyncState">Aysnc state data.</param>
        public override void Connect(string host, ushort port, ConnectHandler callback, object asyncState = null)
        {
            if (callback == null)
            {
                throw new ArgumentNullException("Connection callback cannot be null");
            }

            m_host = host;
            m_port = port;
            m_instanceId = KSCreateWS(JSHandleConnect, JSHandleDisconnect, JSHandleMessage);
            m_websockets.Add(m_instanceId, this);
            m_connectState = new ConnectOperation(callback, asyncState);

            KSConnectWS(m_instanceId, (Config.Secure ? "wss://" : "ws://") + host + ":" + port);
        }

        /// <summary>Close an open network connection.</summary>
        /// <param name="immediate">
        /// If false, then the connection will be closed after sending all queued messages.
        /// </param>
        public override void Disconnect(bool immediate)
        {
            m_disconnecting = true;
            if (!immediate)
            {
                KSDisconnectWS(m_instanceId);
            }
            else
            {
                HandleDisconnect(1005);
            }
        }

        /// <summary>Read data from the WebSocket connection.</summary>
        /// <param name="segment"><see cref="ksStreamBuffer.Segment"/> to receive data into.</param>
        /// <param name="callback">Callback to invoke when the read is complete.</param>
        /// <param name="asyncState">Aysnc state data.</param>
        public override void Read(ksStreamBuffer.Segment segment, IOHandler callback, object asyncState = null)
        {
            if (callback == null)
            {
                throw new ArgumentNullException("IOHandler callback cannot be null");
            }

            if (!IsConnected)
            {
                callback(this, segment, SocketError.NotConnected, asyncState);
                return;
            }
            if (m_disconnecting)
            {
                callback(this, segment, SocketError.Disconnecting, asyncState);
                return;
            }

            bool immediate = false;
            lock (m_readQueue)
            {
                if (m_readQueue.Count == 0 && m_available >= segment.Count)
                {
                    m_available = KSReadWS(m_instanceId, segment.Array, segment.Offset, segment.Count);
                    immediate = true;
                }
                else
                {
                    m_readQueue.Enqueue(new IOOperation(SocketAsyncOperation.Receive, segment, callback, asyncState));
                }
            }
            if (immediate)
            {
                callback(this, segment, SocketError.Success, asyncState);
            }
        }

        /// <summary>Write data to the websocket.</summary>
        /// <param name="segment"><see cref="ksStreamBuffer.Segment"/> to write data from.</param>
        /// <param name="callback">Callback to invoke when the write is complete.</param>
        /// <param name="asyncState">Async state data.</param>
        public override void Write(ksStreamBuffer.Segment segment, IOHandler callback, object asyncState = null)
        {
            if (callback == null)
            {
                throw new ArgumentNullException("IOHandler callback cannot be null");
            }

            if (!IsConnected)
            {
                callback(this, segment, SocketError.NotConnected, asyncState);
                return;
            }
            if (m_disconnecting)
            {
                callback(this, segment, SocketError.Disconnecting, asyncState);
                return;
            }

            KSWriteWS(m_instanceId, segment.Array, segment.Offset, segment.Count);
            callback(this, segment, SocketError.Success, asyncState);
        }

        /// <summary>
        /// Handle a connect event from the javascript Websocket by calling the connect handler.
        /// </summary>
        private void HandleConnect()
        {
            if (m_connectState != null)
            {
                m_connectState.Callback(this, SocketError.Success, m_connectState.AsyncState);
            }
            m_connectState = null;
        }
        
        /// <summary>Handle disconnect events from a javascript websocket.</summary>
        /// <param name="code">Disconnect error code.</param>
        private void HandleDisconnect(int code)
        {
            SocketError error = SocketError.Success;
            switch (code)
            {
                case 1001: error = SocketError.ConnectionReset; break; //Endpoint going away
                case 1002: error = SocketError.ProtocolType; break; // Protocol error
                case 1005: error = SocketError.ConnectionAborted; break; // No Status
                case 1006: error = SocketError.ConnectionReset; break; // Abnormal disconnect (server disconnected)
                case 1009: error = SocketError.MessageSize; break; // Data frame too large
                default: error = SocketError.Fault; break; // Unexpected error
            }
            KSDisposeWS(m_instanceId);
            m_websockets.Remove(m_instanceId);
            m_instanceId = -1;
            m_disconnecting = false;

            // This is a connect attempt failure if the connect callback is still defined
            if (m_connectState != null)
            {
                m_connectState.Callback(this, error, m_connectState.AsyncState);
                m_connectState = null;
            }
            else
            {
                if (OnDisconnect != null)
                {
                    OnDisconnect(this, error);
                }
                lock (m_readQueue)
                {
                    while (m_readQueue.Count > 0)
                    {
                        IOOperation request = m_readQueue.Dequeue();
                        request.Callback(this, request.Segment, error, request.AsyncState);
                    }
                }
            }
        }

        /// <summary>
        /// Handle socket read events by updating the amount of data available in the javascript buffer.
        /// </summary>
        /// <param name="avaiable">Amount of data availabe.</param>
        private void HandleMessage(int avaiable)
        {
            m_available = avaiable;
            while (TryRead());
        }

        /// <summary>
        /// Attempt to complete queued read requests by reading data from the javascript buffer.
        /// </summary>
        /// <returns>True if a read request was completed.</returns>
        private bool TryRead()
        {
            IOOperation request = null;
            lock (m_readQueue)
            {
                if (m_readQueue.Count > 0 && m_available >= m_readQueue.Peek().Segment.Count)
                {
                    request = m_readQueue.Dequeue();
                    m_available = KSReadWS(m_instanceId, request.Segment.Array, request.Segment.Offset, request.Segment.Count);
                }
            }
            if (request != null)
            {
                request.Callback(this, request.Segment, SocketError.Success, request.AsyncState);
                return true;
            }
            return false;
        }
    }
}
#endif