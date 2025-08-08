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
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>A utility window for launching Reactor clusters.</summary>
    public class ksLocalServerMenu : EditorWindow
    {
        // GUI labels and tooltips
        private readonly GUIContent m_clusterPortLabel
            = new GUIContent("Cluster Port", "This is the port used by rooms to connect to the cluster.");
        private readonly GUIContent m_roomPortsLabel
            = new GUIContent("Room Ports", "Rooms launched by the cluster will be restricted to these ports.");
        private readonly GUIContent m_sceneLabel
            = new GUIContent("Launch Scene", "Scene to use in the launch instance.");
        private readonly GUIContent m_roomLabel
            = new GUIContent("Launch Room", "Room to use in the launch instance.");
        private readonly GUIContent m_tagLabel
            = new GUIContent("Launch Tags", "Tags to assign to the launch room.");
        private readonly GUIContent m_protocolLabel
            = new GUIContent("Protocol", "Network protocol used by rooms lauched by the cluster.");

        private readonly string[] m_protocolOptions = new string[2] { "tcp", "ws" };

        // Cluster launch settings
        private string m_scene = null;
        private string m_room = null;
        private ushort m_clusterPort = 1234;
        private string m_roomPorts = "8000-8010";
        private string m_tags = null;
        private int m_protocolIndex = 0;
        private int m_sceneIndex = 0;
        private int m_roomIndex = 0;
        private ksLocalServer m_localServer = null;
        private string[] m_scenes = null;
        private Dictionary<string, string[]> m_sceneRooms = new Dictionary<string, string[]>();

        /// <summary>Open the cluster launch window.</summary>
        /// <param name="scene">scene name</param>
        /// <param name="room">room name</param>
        /// <param name="protocol">network protocol</param>
        public static void Open(string scene = null, string room = null, string protocol = "tcp")
        {
            ksLocalServerMenu window = GetWindow<ksLocalServerMenu>(true, "KS Reactor - Local Cluster");
            window.SetSceneAndRoom(scene, room, protocol);
            window.ShowUtility();
        }

        /// <summary>Initialize the cluster launch setting when the window is focused.</summary>
        private void Awake()
        {
            m_localServer = ksLocalServer.Get("cluster");
            m_clusterPort = ksReactorConfig.Instance.Cluster.ClusterPort;
            m_roomPorts = ksReactorConfig.Instance.Cluster.RoomPorts;
            m_scene = ksReactorConfig.Instance.Cluster.Scene;
            m_room = ksReactorConfig.Instance.Cluster.Room;
            m_tags = ksReactorConfig.Instance.Cluster.Tags;
            m_protocolIndex = GetProtocolIndex(ksReactorConfig.Instance.Cluster.Protocol);

            GetPublishedScenes();
        }

        /// <summary>Update/Render the user interface.</summary>
        void OnGUI()
        {
            if (m_scenes == null || m_scenes.Length == 0)
            {
                EditorGUILayout.HelpBox("You must publish a scene before you can launch a cluster.", MessageType.Warning);
                return;
            }

            if (m_localServer != null && m_localServer.IsRunning)
            {
                EditorGUILayout.HelpBox("Only one local cluster can be run at a time.", MessageType.Info);
                if (ksStyle.Button("Stop Local Cluster"))
                {
                    m_localServer.Stop();
                }
                return;
            }

            EditorGUI.BeginChangeCheck();
            m_clusterPort = (ushort)EditorGUILayout.IntField(m_clusterPortLabel, m_clusterPort);
            m_roomPorts = EditorGUILayout.TextField(m_roomPortsLabel, m_roomPorts);
            m_protocolIndex = EditorGUILayout.Popup(m_protocolLabel, m_protocolIndex, m_protocolOptions);
            m_sceneIndex = EditorGUILayout.Popup(m_sceneLabel, m_sceneIndex, m_scenes);
            if (m_scenes.Length != m_sceneRooms.Count)
            {
                GetPublishedScenes();
            }
            m_roomIndex = EditorGUILayout.Popup(m_roomLabel, m_roomIndex, m_sceneRooms[m_scenes[m_sceneIndex]]);
            m_tags = EditorGUILayout.TextField(m_tagLabel, m_tags);

            if (EditorGUI.EndChangeCheck())
            {
                m_scene = m_scenes[m_sceneIndex];
                m_room = m_sceneRooms[m_scene][m_roomIndex];
                ksReactorConfig.Instance.Cluster.ClusterPort = m_clusterPort;
                ksReactorConfig.Instance.Cluster.RoomPorts = m_roomPorts;
                ksReactorConfig.Instance.Cluster.Scene = m_scene;
                ksReactorConfig.Instance.Cluster.Room = m_room;
                ksReactorConfig.Instance.Cluster.Tags = m_tags;
                ksReactorConfig.Instance.Cluster.Protocol = m_protocolOptions[m_protocolIndex];
            }

            EditorGUILayout.Space();
            if (ksStyle.Button("Launch Local Cluster"))
            {
                m_localServer.CheckServerProjectDirtyFlag();
                if (m_localServer.CheckConfigAndRuntime(m_scene))
                {
                    ksAnalytics.Get().TrackDailyEvent(ksAnalytics.Events.LOCAL_CLUSTER_START);
                    ksLocalServer.Manager.StopClusterInstances();
                    m_localServer.StartCluster(
                        m_scene,
                        m_room,
                        m_clusterPort,
                        m_roomPorts,
                        m_tags,
                        m_protocolOptions[m_protocolIndex]
                    );
                }
                Close();
            }
        }

        /// <summary>
        /// Scan the build directory for published scenes then build the array and dictionaries required to
        /// display scene and room selections in the UI.
        /// </summary>
        private void GetPublishedScenes()
        {
            m_sceneRooms.Clear();
            m_scenes = null;

            string[] files = Directory.GetFiles(ksPaths.ImageDir, "config.*.json", SearchOption.TopDirectoryOnly);
            if (files == null || files.Length == 0)
            {
                return;
            }

            m_scenes = new string[files.Length];
            for (int i = 0; i < files.Length; ++i)
            {
                string scene = Path.GetFileNameWithoutExtension(files[i]).Remove(0, 7);
                m_scenes[i] = scene;
                string[] rooms = GetPublishedSceneRooms(files[i]);
                m_sceneRooms.Add(scene, rooms);

                // Get indices of current scene and room
                if (m_scene == scene)
                {
                    m_sceneIndex = i;
                    for (int j = 0; j < rooms.Length; ++j)
                    {
                        if (rooms[j] == m_room)
                        {
                            m_roomIndex = j;
                            break;
                        }
                    }
                }
            }

            m_scene = m_scenes[m_sceneIndex];
            m_room = m_sceneRooms[m_scene][m_roomIndex];
        }

        /// <summary>Open a scene configuration file and extract a list of published rooms.</summary>
        /// <param name="filepath">Scene config file path.</param>
        /// <returns>Array of room names.</returns>
        private string[] GetPublishedSceneRooms(string filepath)
        {
            if (!File.Exists(filepath))
            {
                return null;
            }

            ksJSON json = ksJSON.Parse(File.ReadAllText(filepath));
            if (!json.HasField("rooms"))
            {
                return null;
            }

            ksConstMap<string, ksJSON> jsonRooms = json["rooms"].Fields;
            if (jsonRooms.Count == 0)
            {
                return null;
            }

            string[] rooms = new string[jsonRooms.Count];
            int i = 0;
            foreach (KeyValuePair<string, ksJSON> pair in jsonRooms)
            {
                rooms[i++] = pair.Key;
            }
            return rooms;
        }

        /// <summary> Set the selected scene and room.</summary>
        /// <param name="scene">Scene name</param>
        /// <param name="room">Room name</param>
        /// <param name="protocol">network protocol</param>
        private void SetSceneAndRoom(string scene, string room, string protocol)
        {
            if (string.IsNullOrEmpty(scene) || m_scenes == null)
            {
                return;
            }

            for (int i = 0; i < m_scenes.Length; ++i)
            {
                if (m_scenes[i] == scene)
                {
                    m_sceneIndex = i;
                    m_scene = scene;
                    m_protocolIndex = GetProtocolIndex(protocol);
                    break;
                }
            }

            string[] rooms = null;
            if (m_sceneRooms.TryGetValue(scene, out rooms))
            {
                if (string.IsNullOrEmpty(room))
                {
                    if (rooms.Length > 0)
                    {
                        m_roomIndex = 0;
                        m_room = rooms[0];
                    }
                }
                else
                {
                    for (int i = 0; i < rooms.Length; ++i)
                    {
                        if (rooms[i] == room)
                        {
                            m_roomIndex = i;
                            m_room = room;
                            break;
                        }
                    }
                }
            }

            Repaint();
        }

        /// <summary>Return the index of the protocol striung in the protocol options array.</summary>
        /// <param name="protocol"></param>
        /// <returns>Index of the protocol in the protocol options array.</returns>
        private int GetProtocolIndex(string protocol)
        {
            for (int i = 0; i < m_protocolOptions.Length; ++i)
            {
                if (m_protocolOptions[i] == protocol)
                {
                    return m_protocolIndex = i;
                }
            }
            return 0;
        }
    }
}
