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

using KS.Unity.Editor;
using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>
    /// Tracks a local Reactor or Cluster server instance and provides methods for starting and stopping it.
    /// </summary>
    [Serializable]
    public class ksLocalServer
    {
        private object m_lockLock = new object();
        private Process m_process = null;
        private static ksLocalServerManager m_manager = new ksLocalServerManager();

        // Local server types
        /// <summary> Instance type constant </summary>
        public const byte INSTANCE = 0;
        /// <summary> Cluster type constant </summary>
        public const byte CLUSTER = 1;
        /// <summary> Cluster instance type constant </summary>
        public const byte CLUSTER_INSTANCE = 2;

        private const string RUNTIME_DIRTY_MSG =
            "KSServerRuntime is dirty. Build it before starting the local server?";

        [SerializeField]
        private string m_id = "";
        [SerializeField]
        private string m_scene = "";
        [SerializeField]
        private string m_room = "";
        [SerializeField]
        private ushort m_port = 0;
        [SerializeField]
        private string m_protocol = "tcp";
        [SerializeField]
        private string m_roomPorts = "";
        [SerializeField]
        private string m_tags = "";
        [SerializeField]
        private string m_logFile = null;
        [SerializeField]
        private int m_processId = -1;
        [SerializeField]
        private byte m_type = INSTANCE;

        /// <summary>Gets the path of the log file returning a string.</summary>
        public string LogFile
        {
            get { return m_logFile; }
        }

        /// <summary>Gets the local server manager singleton, returning a <see cref="ksLocalServerManager" /> object.</summary>
        public static ksLocalServerManager Manager
        {
            get { return m_manager; }
        }

        /// <summary>ID of the tracked instance.</summary>
        public string Id
        {
            get { return m_id == null ? "" : m_id; }
        }

        /// <summary>Scene name of the instance.</summary>
        public string Scene
        {
            get { return m_scene == null ? "" : m_scene; }
        }

        /// <summary>Room type of the instance.</summary>
        public string Room
        {
            get { return m_room == null ? "" : m_room; }
        }

        /// <summary>Port used by the instance/cluster.</summary>
        public ushort Port
        {
            get { return m_port; }
        }

        /// <summary>Protocol used by the instance.</summary>
        public string Protocol
        {
            get { return m_protocol; }
        }

        /// <summary>Room ports assigned by the cluster to new room instances.</summary>
        public string RoomPorts
        {
            get { return m_roomPorts == null ? "" : m_roomPorts; }
        }

        /// <summary>Tags to apply to the cluster launch rooms returning a string.</summary>
        public string Tags
        {
            get { return m_tags == null ? "" : m_tags; }
        }

        /// <summary>Instance process ID.</summary>
        public int ProcessId
        {
            get { return m_processId; }
        }

        /// <summary>Is this an instance or cluster process (true if either)?</summary>
        public bool IsInstance
        {
            get { return m_type == INSTANCE || m_type == CLUSTER_INSTANCE; }
        }

        /// <summary>Is this an instance started by a cluster?</summary>
        public bool IsClusterInstance
        {
            get { return m_type == CLUSTER_INSTANCE; }
        }

        /// <summary>Is the local server instance running?</summary>
        public bool IsRunning
        {
            get { return m_process != null && !m_process.HasExited; }
        }

        /// <summary>Constructor</summary>
        /// <param name="id">Instance ID</param>
        public ksLocalServer(string id = "")
        {
            m_id = id;
            Init();
        }

        /// <summary>
        /// Initialization method to create non-serialized member variables because unity assigns them a null value after deserialisation.
        /// </summary>
        public void Init()
        {
            m_lockLock = new object();
        }

        /// <summary>
        /// Loads running local server instances through the manager, a <see cref="ksLocalServerManager" /> object.
        /// </summary>
        /// <param name="data">Local server instances.</param>
        public static void Load(ksLocalServerData data)
        {
            Manager.Load(data);
        }

        /// <summary>
        /// Get a local server instance for a room type. If a local server instance of the
        /// room type is running, that instance will be returned. If not, a new instance is created (but not
        /// started). ksLocalServer is not aware of local server processes started externally and will not
        /// return a local server instance for those processes.
        /// </summary>
        /// <param name="id">Unique instance ID.</param>
        /// <returns>
        /// Object matching the room type.
        /// </returns>
        public static ksLocalServer Get(string id = "")
        {
            return Manager.GetServer(id);
        }

        /// <summary>Restart the local server process.</summary>
        public void Restart()
        {
            Stop(false);
            switch (m_type)
            {
                case INSTANCE:
                {
                    StartInstance(m_scene, m_room, m_port, m_protocol);
                    break;
                }
                case CLUSTER:
                {
                    StartCluster(m_scene, m_room, m_port, m_roomPorts, m_tags, m_protocol);
                    break;
                }
                case CLUSTER_INSTANCE:
                {
                    ksLog.Warning(this, "Cannot start server; cluster instances can only be started by the cluster.");
                    break;
                }
            }
        }

        /// <summary>
        /// Checks if the server project is dirty. If so, ask the user if they want to build the server runtime before
        /// starting the local server.
        /// </summary>
        public void CheckServerProjectDirtyFlag()
        {
            if (ksServerProjectWatcher.Get().ServerProjectDirty &&
                        EditorUtility.DisplayDialog("Reactor Local Server", RUNTIME_DIRTY_MSG, "Yes", "No"))
            {
                ksServerProjectWatcher.Get().ServerProjectDirty = !ksReactorMenu.BuildServerRuntime();
            }
        }

        /// <summary>
        /// Checks if the config files and server runtime dll needed to run a local server are present. If not, prompts
        /// the user to build them.
        /// </summary>
        /// <param name="scene">Name of the scene to find config for.</param>
        /// <returns>True if the files were found or built.</returns>
        public bool CheckConfigAndRuntime(string scene)
        {
            bool configExists = false;
            bool runtimeExists = false;
            try
            {
                configExists = File.Exists(ksPaths.SceneConfigFile(scene)) && File.Exists(ksPaths.GameConfig);
                runtimeExists = File.Exists(ksPaths.LocalServerRuntimeDll);
            }
            catch (Exception e)
            {
                ksLog.LogException(this, e);
            }

            if (!configExists || !runtimeExists)
            {
                string message = "Unable to find ";
                if (!configExists && !runtimeExists)
                {
                    message += "config and server runtime.";
                }
                else if (!configExists)
                {
                    message += "config.";
                }
                else
                {
                    message += "server runtime.";
                }
                message += "\nDo you want to build and run the local server?";
                if (!EditorUtility.DisplayDialog("Reactor Local Server", message, "OK", "Cancel"))
                {
                    return false;
                }
                if (!configExists && !runtimeExists)
                {
                    ksReactorMenu.RebuildAll();
                }
                else if (!configExists)
                {
                    ksReactorMenu.BuildConfig();
                }
                else
                {
                    ksReactorMenu.BuildServerRuntime();
                }
            }
            return true;
        }

        /// <summary>Start a Reactor instance.</summary>
        /// <param name="scene">Scene name.</param>
        /// <param name="room">Room name.</param>
        /// <param name="port">Instance port.</param>
        /// <param name="port">Network protocol.</param>
        public void StartInstance(string scene, string room, ushort port, string protocol = "tcp")
        {
            if (IsRunning || ksServerInstaller.Get().Check(false, true) == ksServerInstaller.FileStates.MISSING_SERVER_FILES)
            {
                return;
            }

            m_type = INSTANCE;
            m_scene = scene;
            m_room = room;
            m_port = port;
            m_protocol = protocol;

            ksPathUtils.Create(ksPaths.LogDir, true);
            m_logFile = Path.GetFullPath(ksPathUtils.ToAbsolutePath(ksPaths.LogDir) + 
                scene + "." + room + "." + DateTime.Now.ToString("yyyMMdd.HHmmss.f") + ".log");

            string fileName, arguments;
            if (ksPaths.OperatingSystem == "Unix")
            {
                fileName = "\"" + ksReactorConfig.Instance.Server.MonoPath + "\"";
                arguments = "\"" + ksPaths.ServerDir + "Reactor.exe\"";
            }
            else
            {
                fileName = Path.GetFullPath(Path.Combine(ksPaths.ServerDir, "Reactor.exe"));
                arguments = "";
            }
            string workingDirectory = Path.GetFullPath(ksPaths.ImageDir);

            // Command line arguments
            arguments += " -id=1";
            arguments += " -t=\"" + m_room + "\"";
            arguments += " -s=\"" + m_scene + "\"";
            arguments += " -rt=\"KSServerRuntime.Local.dll\"";
            arguments += " -h=localhost";
            if (protocol == "ws") {
                arguments += " -ws=" + m_port;
            }
            else
            {
                arguments += " -tcp=" + m_port;
                arguments += " -rudp=" + m_port;
            }
            arguments += " -l=\"" + m_logFile + "\"";
            arguments += " -api-url=\"" + ksReactorConfig.Instance.Urls.PlayerAPI + "\"";
            arguments += " -api-key=\"" + ksReactorConfig.Instance.Build.APIKey + "\"";
            arguments += " -api-sec=\"" + ksReactorConfig.Instance.Build.APIServerSecret + "\"";
            //arguments += " -r=Record.bin";
            //arguments += " -m=\"" + Paths.BuildDir + "Model.bin" + "\"";
            //arguments += " -p=\""+ Settings.InstallationPath + "profile.log\"";
            //arguments += " -pr";
            arguments += " \"config.json\"";
            arguments += " \"config." + m_scene + ".json\"";

            StartProcess(fileName, arguments, workingDirectory);
            if (IsRunning)
            {
                WriteLockFile();
            }
        }

        /// <summary>Start a Cluster instance.</summary>
        /// <param name="scene">Scene name.</param>
        /// <param name="room">Room name.</param>
        /// <param name="port">Instance port.</param>
        /// <param name="roomPorts">Cluster room ports.</param>
        /// <param name="tags">Cluster tags.</param>
        /// <param name="protocol">Protocol used by cluster rooms.</param>
        public void StartCluster(
            string scene,
            string room,
            ushort port,
            string roomPorts,
            string tags,
            string protocol = "tcp")
        {
            if (IsRunning || ksServerInstaller.Get().Check(false, true) == ksServerInstaller.FileStates.MISSING_SERVER_FILES)
            {
                return;
            }

            m_type = CLUSTER;
            m_scene = scene;
            m_room = room;
            m_port = port;
            m_roomPorts = roomPorts;
            m_tags = tags;
            m_protocol = protocol;

            ksPathUtils.Create(ksPaths.LogDir, true);
            m_logFile = ksPathUtils.ToAbsolutePath(ksPaths.LogDir) +
                "cluster." + DateTime.Now.ToString("yyyMMdd.HHmmss.f") + ".log";

            // Create config
            string configFile = ksPaths.ClusterDir + "config.json";
            try
            {
                if (File.Exists(configFile))
                {
                    File.Delete(configFile);
                }

                ksJSON clusterConfig = new ksJSON();
                clusterConfig["id"] = 1;
                clusterConfig["name"] = "Local Cluster";
                clusterConfig["cluster port"] = port.ToString();
                clusterConfig["room ports"] = roomPorts;
                clusterConfig["protocol"] = protocol;
                clusterConfig["room limit"] = 10;
                clusterConfig["server path"] = ksPathUtils.ToAbsolutePath(ksPaths.ServerDir);
                clusterConfig["image path"] = ksPathUtils.ToAbsolutePath(ksPaths.ImageDir);
                clusterConfig["log path"] = ksPathUtils.ToAbsolutePath(ksPaths.LogDir);
                clusterConfig["project"] = Application.productName;
                clusterConfig["launch rooms"] = new ksJSON(ksJSON.Types.ARRAY);
                clusterConfig["log"] = m_logFile;

                ksJSON roomConfig = new ksJSON();
                roomConfig["scene"] = scene;
                roomConfig["type"] = room;
                roomConfig["tags"] = new ksJSON(ksJSON.Types.ARRAY);
                roomConfig["isPublic"] = true;

                string[] splitTags = tags.Split(',');
                foreach (string tag in splitTags)
                {
                    roomConfig["tags"].Add(tag.Trim());
                }

                clusterConfig["launch rooms"].Add(roomConfig);
                File.WriteAllText(configFile, clusterConfig.Print(true));
            }
            catch (Exception ex)
            {
                ksLog.Error(this, "Exception caught writing local cluster config", ex);
                return;
            }

            // Create process
            string fileName, arguments;
            if (ksPaths.OperatingSystem == "Unix")
            {
                fileName = "\"" + ksReactorConfig.Instance.Server.MonoPath + "\"";
                arguments = "\"" + ksPaths.ClusterDir + "KSCluster.exe\"";
            }
            else
            {
                fileName = Path.GetFullPath(ksPaths.ClusterDir + "KSCluster.exe");
                arguments = "";
            }
            string workingDirectory = Path.GetFullPath(ksPaths.ClusterDir);
            arguments += "\"" + configFile + "\"";

            StartProcess(fileName, arguments, workingDirectory);
            if (IsRunning)
            {
                WriteLockFile();
            }
        }

        /// <summary>Loads local server data from a lock file.</summary>
        /// <param name="path">Path to the lock file to load from.</param>
        public void LoadLockFile(string path)
        {
            try
            {
                m_logFile = path.Substring(0, path.Length - 5);// Remove .lock from the end
                using (FileStream input = File.Open(path, FileMode.Open))
                {
                    using (BinaryReader reader = new BinaryReader(input))
                    {
                        m_processId = reader.ReadInt32();
                        m_type = reader.ReadByte();
                        if (m_type == CLUSTER_INSTANCE)
                        {
                            reader.Close();
                            return;
                        }
                        m_scene = reader.ReadString();
                        m_room = reader.ReadString();
                        m_port = reader.ReadUInt16();
                        if (m_type == CLUSTER)
                        {
                            m_id = "cluster";
                            m_roomPorts = reader.ReadString();
                            m_tags = reader.ReadString();
                        }
                        else if (m_type == INSTANCE)
                        {
                            m_id = "instance";
                        }
                        else
                        {
                            ksLog.Error(this, "Invalid local server type: " + m_type);
                            m_processId = 0;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Error reading lock file '" + path + "'", e);
                m_processId = 0;
            }
        }

        /// <summary>Delete the local server lock file.</summary>
        public void DeleteLockFile()
        {
            lock (m_lockLock)
            {
                string path = m_logFile + ".lock";
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch (Exception e)
                {
                    ksLog.Error(this, "Error deleting '" + path + "'", e);
                }
            }
        }

        /// <summary>
        /// Writes a lock file containing data about this server. The lock file is the name of the log file 
        /// with ".lock" appended.
        /// </summary>
        private void WriteLockFile()
        {
            try
            {
                using (FileStream output = File.Open(m_logFile + ".lock", FileMode.Create))
                {
                    using (BinaryWriter writer = new BinaryWriter(output))
                    {
                        writer.Write(m_processId);
                        writer.Write(m_type);
                        writer.Write(m_scene);
                        writer.Write(m_room);
                        writer.Write(m_port);
                        if (m_type == CLUSTER)
                        {
                            writer.Write(m_roomPorts);
                            writer.Write(m_tags);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Error writing lock file '" + m_logFile + ".lock'", e);
            }
        }

        /// <summary>Create and start a new process.</summary>
        /// <param name="fileName">Name of the file to execute.</param>
        /// <param name="arguments">Arguments to pass to the process being executed.</param>
        /// <param name="workingDirectory">Directory in which the process should be started.</param>
        private void StartProcess(string fileName, string arguments, string workingDirectory)
        {
            try
            {
                m_process = ksProcessUtils.StartProcess(fileName, arguments, workingDirectory);
                m_processId = m_process.Id;
                m_process.EnableRaisingEvents = true;
                m_process.Exited += OnStop;
                Manager.AddServer(this);

                // Open log console
                ksWindow w = ksWindow.Open(
                    ksWindow.REACTOR_CONSOLE,
                    delegate (ksWindow window)
                    {
                        window.titleContent = new GUIContent(" Logs", ksTextures.Logo);
                        window.minSize = new Vector2(380f, 100f);
                        window.Menu = ScriptableObject.CreateInstance<ksServerLogMenu>();
                    }, ksWindow.WindowStyle.DEFAULT, false
                );
                if (!string.IsNullOrEmpty(m_logFile))
                {
                    (w.Menu as ksServerLogMenu).SelectLog(m_logFile);
                }
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Unable to start local server process.", e);
                m_process = null;
            }
        }

        /// <summary>Stops this local server instance.</summary>
        /// <param name="removeFromManager">Whether the instance should be removed from the <see cref="Manager"/>.</param>
        public void Stop(bool removeFromManager = true)
        {
            if (!IsRunning)
            {
                return;
            }
            try
            {
                m_process.Exited -= OnStop;
                m_process.CloseMainWindow();
                m_process.WaitForExit(1000);

                if (removeFromManager)
                {
                    if (!IsInstance)
                    {
                        Manager.StopClusterInstances();
                    }
                    Manager.RemoveServer(this);
                }
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Error stopping server process", e);
            }
            m_process = null;
            DeleteLockFile();
        }

        /// <summary>Called when the process is stopped unexpectedly.</summary>
        /// <param name="sender">Process invoking this method.</param>
        /// <param name="ev">Event arguments</param>
        private void OnStop(object sender, EventArgs ev)
        {
            DeleteLockFile();
            Manager.RemoveServer(this);
            m_process = null;
        }

        /// <summary>
        /// Finds the local server process started by this local server instance.
        /// The process reference is lost during Unity serialization, so we serialize
        /// the process ID and call this to retrieve the process after serialization.
        /// </summary>
        /// <return>A bool to indicate if the process was found and is running.</return>
        public bool FindProcess()
        {
            try
            {
                m_process = Process.GetProcessById(m_processId);
                if ((IsInstance && m_process.ProcessName != "Reactor") ||
                    (!IsInstance && m_process.ProcessName != "KSCluster"))
                {
                    m_process = null;
                }
                else
                {
                    m_process.EnableRaisingEvents = true;
                    m_process.Exited += OnStop;
                }
            }
            catch (Exception)
            {
                return false;
            }
            return IsRunning;
        }
    }
}
