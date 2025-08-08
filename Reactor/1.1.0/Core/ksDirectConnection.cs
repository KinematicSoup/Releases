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
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using AOT;
using KS.Reactor;
using KS.Reactor.Client;
using UnityEngine;

namespace KS.Reactor.Client.Unity
{
    public class ksDirectConnection : ksConnection
    {
        /// <summary>
        /// Callback registered by client which is invoked when the connection is disconnected.
        /// </summary>
        /// <param name="connectionPtr">Pointer identifying the connection the callback is targeted at.</param>
        private delegate void DirectDisconnectCallback(IntPtr connectionPtr);

        /// <summary>
        /// Callback registered by a client which is invoked to read data from the client connection.
        /// </summary>
        /// <param name="connectionPtr">Pointer identifying the connection the callback is targeted at.</param>
        /// <param name="bufferPtr">Pointer to the buffer to read data into.</param>
        /// <param name="amount">Amount of data to read into the buffer</param>
        /// <returns></returns>
        private delegate int DirectReadCallback(IntPtr connectionPtr, IntPtr bufferPtr, int amount);

        /// <summary>
        /// Callback registered by a client which is invoked when new data is available for the client to read.
        /// </summary>
        /// <param name="connectionPtr">Pointer identifying the connection the callback is targeted at.</param>
        /// <param name="amount">Total amount of bytes of data available to the client.</param>
        private delegate void DirectWriteCallback(IntPtr connectionPtr, int amount);


        [DllImport("ServerModule.dll", EntryPoint = "Connection_Create", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr CreateServerConnection(
            IntPtr serverPtr,
            DirectDisconnectCallback disconnectCallback,
            DirectReadCallback readCallback,
            DirectWriteCallback writeCallback);

        [DllImport("ServerModule.dll", EntryPoint = "Connection_Disconnect", CallingConvention = CallingConvention.Cdecl)]
        private static extern void DisconnectFromServer(IntPtr connectionPtr, bool immediate);

        [DllImport("ServerModule.dll", EntryPoint = "Connection_Write", CallingConvention = CallingConvention.Cdecl)]
        private static extern void WriteToServer(IntPtr connectionPtr, int amount);

        [DllImport("ServerModule.dll", EntryPoint = "Connection_Read", CallingConvention = CallingConvention.Cdecl)]
        private static extern int ReadFromServer(IntPtr connectionPtr, byte[] bufferPtr, int offset, int amount);

        public static IntPtr ServerPtr = IntPtr.Zero;
        private static Dictionary<IntPtr, ksDirectConnection> m_connections = new Dictionary<IntPtr, ksDirectConnection>();

        private IntPtr m_connectionPtr;
        private bool m_isConnected = false;
        private bool m_disconnecting = false;
        private int m_readAvailable = 0;
        private int m_writeAvailable = 0;
        private Queue<IOOperation> m_readQueue = new Queue<IOOperation>();
        private Queue<IOOperation> m_writeQueue = new Queue<IOOperation>();
        private ConnectOperation m_connectState = null;

        /// <summary>Disconnect event to invoke on a connection disconnect.</summary>
        public override event DisconnectHandler OnDisconnect;

        /// <summary>Registers a direct connection factory for all <see cref="ksRoom"/> connections.</summary>
        public static void Register()
        {
            ksBaseRoom.RegisterConnectionFactory(ksConnectionProtocols.DIRECT, () =>
            {
                return new ksDirectConnection();
            });
        }

        /// <summary>Direct connection disconnect handler.</summary>
        /// <param name="connectionPtr">Pointer identifying the connection the callback is targeted at.</param>
        [MonoPInvokeCallback(typeof(DirectDisconnectCallback))]
        private static void HandleDisconnect(IntPtr connectionPtr)
        {
            ksDirectConnection connection = null;
            if (m_connections.TryGetValue(connectionPtr, out connection))
            {
                connection.DirectDisconnect();
            }
        }

        /// <summary>Direct connection read handler.</summary>
        /// <param name="connectionPtr">Pointer identifying the connection the callback is targeted at.</param>
        /// <param name="bufferPtr">Pointer to the buffer to read data into.</param>
        /// <param name="amount">Amount of data to read into the buffer</param>
        [MonoPInvokeCallback(typeof(DirectWriteCallback))]
        private static int HandleRead(IntPtr connectionPtr, IntPtr bufferPtr, int amount)
        {
            ksDirectConnection connection = null;
            if (m_connections.TryGetValue(connectionPtr, out connection))
            {
                return connection.DirectRead(bufferPtr, amount);
            }
            return 0;
        }

        /// <summary>Direct connection write handler.</summary>
        /// <param name="connectionPtr">Pointer identifying the connection the callback is targeted at.</param>
        /// <param name="amount">Total amount of bytes of data available to the client.</param>
        [MonoPInvokeCallback(typeof(DirectWriteCallback))]
        private static void HandleWrite(IntPtr connectionPtr, int amount)
        {
            ksDirectConnection connection = null;
            if (m_connections.TryGetValue(connectionPtr, out connection))
            {
                connection.DirectWrite(amount);
            }
        }

        /// <summary>Is there an open connection to a remote host.</summary>
        public override bool IsConnected
        {
            get { return m_isConnected; }
        }

        /// <summary>Unused</summary>
        public override ksFrameInfo ReadProgress
        {
            get { return new ksFrameInfo(0, 0); }
        }

        /// <summary>Unused</summary>
        public override ksFrameInfo WriteProgress
        {
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
            m_connectState = new ConnectOperation(callback, asyncState);
            m_connectionPtr = CreateServerConnection(ServerPtr, HandleDisconnect, HandleRead, HandleWrite);
            if (m_connectionPtr != IntPtr.Zero)
            {
                m_connections.Add(m_connectionPtr, this);
                m_isConnected = true;
                m_connectState.Callback(this, SocketError.Success, m_connectState.AsyncState);
                m_connectState = null;
            }
            else
            {
                m_connectState.Callback(this, SocketError.AccessDenied, m_connectState.AsyncState);
                m_connectState = null;
            }
        }

        /// <summary>Close an open network connection.</summary>
        /// <param name="immediate">
        /// If false, then the connection will be closed after sending all queued messages.
        /// </param>
        public override void Disconnect(bool immediate)
        {
            m_disconnecting = true;
            DisconnectFromServer(m_connectionPtr, immediate);
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

            lock (m_readQueue)
            {
                if (m_readQueue.Count == 0 && m_readAvailable >= segment.Count)
                {
                    IOOperation op = new IOOperation(SocketAsyncOperation.Receive, segment, callback, asyncState);
                    CompleteReadOp(op);
                }
                else
                {
                    m_readQueue.Enqueue(new IOOperation(SocketAsyncOperation.Receive, segment, callback, asyncState));
                }
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

            lock (m_writeQueue)
            {
                m_writeAvailable += segment.Count;
                m_writeQueue.Enqueue(new IOOperation(SocketAsyncOperation.Send, segment, callback, asyncState));
                WriteToServer(m_connectionPtr, m_writeAvailable);
            }
        }

        /// <summary>Handle disconnect events from a javascript websocket.</summary>
        /// <param name="code">Disconnect error code.</param>
        private void DirectDisconnect()
        {
            SocketError error = SocketError.ConnectionReset;
            m_connections.Remove(m_connectionPtr);
            m_connectionPtr = IntPtr.Zero;
            m_disconnecting = false;
            m_isConnected = false;

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
        /// Direct read request from the connected client. Copy data from the write operation buffers
        /// to the buffer pointer provided.
        /// </summary>
        /// <param name="bufferPtr">Pointer to the buffer</param>
        /// <param name="amount">Amount of data to copy into the buffer.</param>
        /// <returns>Amount of data left in the write operation buffers.</returns>
        private int DirectRead(IntPtr bufferPtr, int amount)
        {
            lock (m_writeQueue)
            {
                while (amount > 0 && m_writeAvailable > 0)
                {
                    IOOperation op = m_writeQueue.Peek();
                    int readAmount = Math.Min(op.Total-op.Processed, amount);
                    Marshal.Copy(op.Segment.Array, op.Segment.Offset + op.Processed, bufferPtr, readAmount);
                    op.Processed += readAmount;
                    bufferPtr += readAmount;
                    amount -= readAmount;
                    m_writeAvailable -= readAmount;

                    if (op.Processed == op.Total)
                    {
                        m_writeQueue.Dequeue();
                        if (op.Callback != null)
                        {
                            op.Callback(this, op.Segment, SocketError.Success, op.AsyncState);
                        }
                    }
                }
                return m_writeAvailable;
            }
        } 

        /// <summary>
        /// Notify the connection that there is data available to read from the direct connection.
        /// If the amount of data is greater than the current read operation, then complete the current
        /// read operation.
        /// </summary>
        /// <param name="amount">Amount of data available to read.</param>
        private void DirectWrite(int amount)
        {
            lock (m_readQueue)
            {
                m_readAvailable += amount;
                while (m_isConnected && m_readQueue.Count > 0 && m_readQueue.Peek().Total <= m_readAvailable)
                {
                    CompleteReadOp(m_readQueue.Dequeue());
                }
            }
        }

        /// <summary>
        /// Attempts to complete a read operation by invoking the read callback with the pointer to the read
        /// operation buffer.
        /// </summary>
        /// <param name="op">Read opertaion to complete.</param>
        private void CompleteReadOp(IOOperation op)
        {
            m_readAvailable = ReadFromServer(m_connectionPtr, op.Segment.Array, op.Segment.Offset, op.Total);
            op.Processed = op.Total;
            if (op.Callback != null)
            {
                op.Callback(this, op.Segment, SocketError.Success, op.AsyncState);
            }
        }
    }
}
