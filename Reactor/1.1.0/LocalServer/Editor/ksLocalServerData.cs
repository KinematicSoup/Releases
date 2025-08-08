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

using System.Collections.Generic;
using UnityEngine;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>
    /// Holds a list of running local servers to serialize during Unity serialization.
    /// </summary>
    public class ksLocalServerData : ScriptableObject
    {
        /// <summary>Iterator delegate </summary>
        /// <param name="server">Object to pass to the iterator.</param>
        public delegate void Iterator(ksLocalServer server);

        [SerializeField]
        private List<ksLocalServer> m_servers = new List<ksLocalServer>();
        private int m_index = -1;

        /// <summary>
        /// Called after Unity serialization (or when the object is created).
        /// </summary>
        /// <remarks>
        /// Calls LocalServer.Load() <see cref="ksLocalServer.Load(ksLocalServerData)"/>
        /// </remarks>
        public void OnEnable()
        {
            hideFlags = HideFlags.HideAndDontSave;
            for (int i = 0; i < m_servers.Count; ++i)
            {
                m_servers[i].Init();
            }
            ksLocalServer.Load(this);
        }

        /// <summary>Adds a local server to the list of tracked servers.</summary>
        /// <param name="server">Server to add.</param>
        public void Add(ksLocalServer server)
        {
            lock (m_servers)
            {
                if (!m_servers.Contains(server))
                {
                    m_servers.Add(server);
                }
            }
        }

        /// <summary>Removes a local server from the list of tracked servers.</summary>
        /// <param name="server">Server to remove.</param>
        public void Remove(ksLocalServer server)
        {
            lock (m_servers)
            {
                int index = m_servers.IndexOf(server);
                if (index >= 0)
                {
                    m_servers.RemoveAt(index);
                    if (index < m_index)
                    {
                        m_index--;
                    }
                }
            }
        }

        /// <summary>Iterates local servers.</summary>
        /// <param name="iter">An Iterator function to call on each server.</param>
        public void Iterate(Iterator iter)
        {
            lock (m_servers)
            {
                for (m_index = m_servers.Count - 1; m_index >= 0; m_index--)
                {
                    iter(m_servers[m_index]);
                }
            }
        }

        /// <summary>Stops iterating. Call this while iterating to stop.</summary>
        public void StopIterating()
        {
            lock (m_servers)
            {
                m_index = -1;
            }
        }

        /// <summary>
        /// Removes the server currently being iterated. Does nothing if not iterating.
        /// </summary>
        public void RemoveCurrent()
        {
            lock (m_servers)
            {
                if (m_index >= 0 && m_index < m_servers.Count)
                {
                    m_servers.RemoveAt(m_index);
                }
            }
        }
    }
}
