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
using System.IO;
using UnityEngine;
using KS.Unity.Editor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Tracks local Reactor server instances.</summary>
    public class ksLocalServerManager
    {
        /// <summary>
        /// List of running server instances to serialize when Unity restarts.
        /// </summary>
        private ksLocalServerData m_data;

        /// <summary>List of local server instances.</summary>
        public ksLocalServerData Servers
        {
            get
            {
                if (m_data == null)
                {
                    m_data = ScriptableObject.CreateInstance<ksLocalServerData>();
                }
                return m_data;
            }
        }

        /// <summary>Number of running local server instances.</summary>
        public int RunningServerCount
        {
            get
            {
                int count = 0;
                Servers.Iterate((ksLocalServer server) =>
                {
                    if (server.IsRunning)
                    {
                        count++;
                    }
                });
                return count;
            }
        }

        private ksAtomicDictionary<string, ksLocalServer> m_serverMap = new ksAtomicDictionary<string, ksLocalServer>();

        /// <summary>Loads running local server instances.</summary>
        /// <param name="data">Local server instances data to load.</param>
        public void Load(ksLocalServerData data)
        {
            if (m_data != null)
            {
                ScriptableObject.DestroyImmediate(m_data);
            }
            m_data = data;
            ksPathUtils.Create(ksPaths.LogDir, true);
            // Monitor for new lock files created by the local cluster
            new ksFileWatcher(OnCreateFile, "*.log.lock", ksFileWatcher.Flags.FILE_CREATED, ksPaths.LogDir).Start();
            // Create a hash set of existing lock files
            HashSet<string> lockFiles = new HashSet<string>();
            try
            {
                foreach (string path in Directory.GetFiles(ksPaths.LogDir, "*.log.lock"))
                {
                    lockFiles.Add(ksPathUtils.Clean(ksPathUtils.ToAbsolutePath(path)));
                }
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Error getting files from '" + ksPaths.LogDir + "'", e);
            }
            // Find server processes
            Servers.Iterate((ksLocalServer server) =>
            {
                if (server.FindProcess())
                {
                    // We found the process for this lock file, so we don't need to process it.
                    lockFiles.Remove(ksPathUtils.Clean(server.LogFile + ".lock"));
                    if (!string.IsNullOrEmpty(server.Id))
                    {
                        m_serverMap[server.Id] = server;
                    }
                }
                else
                {
                    Servers.RemoveCurrent();
                }
            });

            // Check if lock files have running server processes
            foreach (string path in lockFiles)
            {
                ksLocalServer server = new ksLocalServer();
                server.LoadLockFile(path);
                if (server.FindProcess())
                {
                    AddServer(server);
                }
                else
                {
                    // Delete lock files for processes that have stopped
                    server.DeleteLockFile();
                }
            }
        }

        /// <summary>Stops running local server instances.</summary>
        /// <returns>Number of server instances stopped.</returns>
        public int StopServers()
        {
            int stopCount = 0;
            Servers.Iterate((ksLocalServer server) =>
            {
                if (server.IsRunning)
                {
                    server.Stop(server.IsClusterInstance);
                    stopCount++;
                }
            });
            return stopCount;
        }

        /// <summary>
        /// Stops running local server instances that were started by a cluster.
        /// </summary>
        public void StopClusterInstances()
        {
            Servers.Iterate((ksLocalServer server) =>
            {
                if (server.IsRunning && server.IsClusterInstance)
                {
                    server.Stop();
                }
            });
        }

        /// <summary>Start all stopped local server instances.</summary>
        /// <returns>Number of server instances started.</returns>
        public int StartServers()
        {
            int startCount = 0;
            Servers.Iterate((ksLocalServer server) =>
            {
                if (!server.IsRunning)
                {
                    server.Restart();
                    startCount++;
                }
            });
            return startCount;
        }

        /// <summary>Remove stopped local server instances from the manager.</summary>
        /// <returns>Number of server instances removed from the manager.</returns>
        public int RemoveStoppedServers()
        {
            int count = 0;
            Servers.Iterate((ksLocalServer server) =>
            {
                if (!server.IsRunning)
                {
                    Servers.RemoveCurrent();
                    count++;
                }
            });
            return count;
        }

        /// <summary>
        /// Restarts or removes stopped local server instances, depending on the config setting 
        /// <see cref="ksReactorConfig.ServerConfigs.RestartOnUpdate"/>.
        /// </summary>
        public void RestartOrRemoveStoppedServers()
        {
            if (ksReactorConfig.Instance.Server.RestartOnUpdate)
            {
                int count = StartServers();
                if (count > 0)
                {
                    ksLog.Info(this, "Restarted " + count + " server instances.");
                }
            }
            else
            {
                RemoveStoppedServers();
            }
        }

        /// <summary>
        /// Adds a server instance to the list of running local server instances.
        /// </summary>
        /// <param name="server">Local server instance to load.</param>
        public void AddServer(ksLocalServer server)
        {
            Servers.Add(server);
            if (!string.IsNullOrEmpty(server.Id))
            {
                m_serverMap[server.Id] = server;
            }
        }

        /// <summary>
        /// Removes a server instance from the list of running local server instances.
        /// </summary>
        /// <param name="server">Local server instance to remove.</param>
        public void RemoveServer(ksLocalServer server)
        {
            Servers.Remove(server);
            if (!string.IsNullOrEmpty(server.Id))
            {
                m_serverMap.Remove(server.Id);
            }
        }

        /// <summary>
        /// Gets a local server instance for a room type. If a local server instance of the room type
        /// is running, that instance will be returned. If not, a new instance is created (but not started).
        /// GetServer is not aware of local server processes started externally and will not return
        /// a local server instance for those processes.
        /// </summary>
        /// <param name="serverType">Type of the local server instance to get or create.</param>
        /// <returns>Local server instance of the appropriate room type.</returns>
        public ksLocalServer GetServer(string serverType = "")
        {
            ksLocalServer server;
            if (!m_serverMap.TryGetValue(serverType, out server))
            {
                server = new ksLocalServer(serverType);
            }
            return server;
        }

        /// <summary>
        /// Checks if a local server instance is running a scene and room type
        /// </summary>
        /// <param name="scene">Scene to check for.</param>
        /// <param name="roomType">Type of room to check for.</param>
        /// <returns>True if a local server instance is running the specified scene and room type.</returns>
        public bool IsLocalServerRunning(string scene, string roomType)
        {
            bool isRunning = false;
            Servers.Iterate((ksLocalServer server) =>
            {
                if (server.IsInstance && server.Scene == scene && server.Room == roomType && server.IsRunning)
                {
                    isRunning = true;
                    Servers.StopIterating();
                }
            });
            return isRunning;
        }

        /// <summary>
        /// Called when a lock file is created in the log directory. Tries to find the local server process associated
        /// with the lock file.
        /// </summary>
        /// <param name="sender">File watcher invoking this method.</param>
        /// <param name="e">Event arguments</param>
        private void OnCreateFile(object sender, FileSystemEventArgs e)
        {
            string path = ksPathUtils.Clean(e.FullPath);
            string logFile = path.Substring(0, path.Length - 5); // Remove ".lock"
            ksLocalServer server = null;
            Servers.Iterate((ksLocalServer s) =>
            {
                if (ksPathUtils.Clean(s.LogFile) == logFile)
                {
                    server = s;
                    Servers.StopIterating();
                }
            });

            if (server == null)
            {
                server = new ksLocalServer();
                server.LoadLockFile(path);
                if (server.FindProcess())
                {
                    AddServer(server);
                }
                else
                {
                    server.DeleteLockFile();
                }
            }
        }
    }
}
