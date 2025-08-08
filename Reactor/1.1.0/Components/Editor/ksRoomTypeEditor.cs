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
using UnityEngine;
using UnityEditor;
using KS.Unity.Editor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Inspector editor for <see cref="ksRoomType"\>.</summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ksRoomType))]
    public class ksRoomTypeEditor : UnityEditor.Editor
    {
        private const ushort MIN_PORT = 1024;
        private const ushort MAX_PORT = 49151;

        /// <summary>
        /// Support a fixed number of update rates which are all divisors of 60
        /// </summary>
        private readonly int[] CLIENT_UPDATE_VALUES = { 1, 2, 3, 4, 5, 6, 10, 12, 15, 20, 30, 60 };
        private readonly GUIContent[] CLIENT_UPDATE_STRINGS = { 
            new GUIContent("1 update \u2044 second"), new GUIContent("2 updates \u2044 second"), 
            new GUIContent("3 updates \u2044 second"), new GUIContent("4 updates \u2044 second"), 
            new GUIContent("5 updates \u2044 second"), new GUIContent("6 updates \u2044 second"), 
            new GUIContent("10 updates \u2044 second"), new GUIContent("12 updates \u2044 second"), 
            new GUIContent("15 updates \u2044 second"), new GUIContent("20 updates \u2044 second"), 
            new GUIContent("30 updates \u2044 second"), new GUIContent("60 updates \u2044 second") };

        private static ksLocalServer m_localServer;
        private static string m_scene;
        private static string m_room;
        private static ushort m_port = 8000;
        private static string m_protocol = "tcp";

        private string focusedName = "";
        private bool m_showPredictorSettings = false;
        private bool m_showClusterSettings = false;
        private bool m_showAdvancedSettings = false;
        private bool m_localServerWasRunning = false;

        /// <summary>Called when inspector becomes visible.</summary>
        public void OnEnable()
        {
            m_localServer = null;
        }

        /// <summary>Create the GUI.</summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            if (Event.current.type == EventType.Layout)
            {
                // We change the layout based on what's focused, and it's only safe
                // to change layout on Layout events.
                focusedName = GUI.GetNameOfFocusedControl();
            }
            if (m_localServer == null)
            {
                // We wait until here to get the local server because we need to ensure
                // LocalServerData.OnEnable() was called first
                m_localServer = ksLocalServer.Get("instance");
            }

            SerializedProperty iterator = serializedObject.GetIterator();
            bool shutdownWithoutCluster = false;
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                switch (iterator.name)
                {
                    case "m_Script":
                    {
                        enterChildren = false;
                        continue;
                    }
                    case "m_playerLimit":
                    {
                        EditorGUILayout.PropertyField(iterator, new GUIContent("Player Limit"));
                        EditorGUI.showMixedValue = false;
                        break;
                    }
                    case "m_serverFrameRate":
                    {
                        EditorGUILayout.PropertyField(iterator);
                        EditorGUILayout.LabelField(" ", iterator.intValue + " updates / second");
                        if (iterator.intValue > 60)
                        {
                            EditorGUILayout.HelpBox(
                                "FPS is limited to 60 for online servers. Contact us if you need a higher rate.",
                                MessageType.Info);
                        }
                        break;
                    }
                    case "m_clientSendRate":
                    {
                        EditorGUILayout.PropertyField(iterator);
                        if (iterator.intValue == 1)
                        {
                            EditorGUILayout.LabelField(" ", iterator.intValue + " sync / second");
                        }
                        else
                        {
                            EditorGUILayout.LabelField(" ", iterator.intValue + " syncs / second");
                        }
                        break;
                    }
                    case "m_serverFramesPerSync":
                    {
                        ksRoomType roomType = target as ksRoomType;
                        int fps = roomType == null ? 60 : roomType.ServerFrameRate;
                        GUIContent content = new GUIContent("Network Sync Rate",
                            "The number of server frames between each network update sent to clients.");
                        iterator.intValue = EditorGUILayout.IntSlider(content, Math.Min(fps, iterator.intValue), fps, 1);
                        float syncRate = (float)fps / iterator.intValue;
                        EditorGUILayout.LabelField(" ", syncRate == 1 ? 
                            " 1 sync / second" : (syncRate.ToString("G4") + " syncs / second"));
                        break;
                    }
                    case "m_recoverUpdateTime":
                    {
                        EditorGUILayout.PropertyField(iterator, new GUIContent("Recover Update Time"));
                        break;
                    }
                    case "m_defaultEntityPredictor":
                    {
                        m_showPredictorSettings = EditorGUILayout.Foldout(m_showPredictorSettings,
                            "Default Predictors");
                        if (m_showPredictorSettings)
                        {
                            EditorGUI.indentLevel = 1;
                            EditorGUILayout.PropertyField(iterator, new GUIContent("Entity", iterator.tooltip));
                            ShowWarningIfPredictorNeedsController(iterator);
                            EditorGUI.indentLevel = 0;
                        }
                        break;
                    }
                    case "m_defaultEntityControllerPredictor":
                    {
                        if (m_showPredictorSettings)
                        {
                            EditorGUI.indentLevel = 1;
                            EditorGUILayout.PropertyField(iterator, new GUIContent("Controlled Entity",
                                iterator.tooltip));
                            EditorGUI.indentLevel = 0;
                        }
                        break;
                    }
                    case "m_defaultRoomPredictor":
                    {
                        if (m_showPredictorSettings)
                        {
                            EditorGUI.indentLevel = 1;
                            EditorGUILayout.PropertyField(iterator, new GUIContent("Room", iterator.tooltip));
                            ShowWarningIfPredictorNeedsController(iterator);
                            EditorGUI.indentLevel = 0;
                        }
                        break;
                    }
                    case "m_defaultPlayerPredictor":
                    {
                        if (m_showPredictorSettings)
                        {
                            EditorGUI.indentLevel = 1;
                            EditorGUILayout.PropertyField(iterator, new GUIContent("Player", iterator.tooltip));
                            ShowWarningIfPredictorNeedsController(iterator);
                            EditorGUI.indentLevel = 0;
                        }
                        break;
                    }
                    case "m_clusterReconnectDelay":
                    {
                        m_showClusterSettings = EditorGUILayout.Foldout(m_showClusterSettings, "Cluster");
                        if (m_showClusterSettings)
                        {
                            EditorGUI.indentLevel = 1;
                            EditorGUILayout.PropertyField(iterator, new GUIContent("Reconnect Delay"));
                            iterator.intValue = Math.Max(iterator.intValue, 1);
                            EditorGUI.showMixedValue = false;
                            EditorGUI.indentLevel = 0;
                        }
                        break;
                    }
                    case "m_shutdownWithoutCluster":
                    {
                        if (m_showClusterSettings)
                        {
                            EditorGUI.indentLevel = 1;
                            shutdownWithoutCluster = iterator.boolValue;
                            EditorGUILayout.PropertyField(iterator, new GUIContent("Shutdown Without Cluster"));
                            EditorGUI.showMixedValue = false;
                            EditorGUI.indentLevel = 0;
                        }
                        break;
                    }
                    case "m_clusterReconnectAttempts":
                    {
                        if (m_showClusterSettings && shutdownWithoutCluster)
                        {
                            EditorGUI.indentLevel = 1;
                            EditorGUILayout.PropertyField(iterator, new GUIContent("Reconnect Attempts"));
                            EditorGUI.showMixedValue = false;
                            EditorGUI.indentLevel = 0;
                        }
                        break;
                    }
                    case "m_requiredCoreCount":
                    {
                        m_showAdvancedSettings = EditorGUILayout.Foldout(m_showAdvancedSettings, "Advanced");
                        if (m_showAdvancedSettings)
                        {
                            EditorGUI.indentLevel = 1;
                            EditorGUILayout.PropertyField(iterator);
                            EditorGUI.indentLevel = 0;
                        }
                        break;
                    }
                    case "m_requiredMemory":
                    case "m_physicsThreads":
                    case "m_networkThreads":
                    case "m_encodingThreads":
                    case "m_maxConcurrentAuthentications":
                    case "m_parallelControllerUpdates":
                    case "m_parallelOwnedEntityUpdates":
                    {
                        if (m_showAdvancedSettings)
                        {
                            EditorGUI.indentLevel = 1;
                            EditorGUILayout.PropertyField(iterator);
                            EditorGUI.indentLevel = 0;
                        }
                        break;
                    }
                    case "m_port":
                        {
                            EditorGUILayout.PropertyField(iterator);
                            m_port = (ushort)iterator.intValue;
                            if ((m_port < MIN_PORT || m_port > MAX_PORT) && focusedName != iterator.name)
                            {
                                EditorGUILayout.HelpBox("Port must be in the range " + MIN_PORT + " - " + MAX_PORT,
                                    MessageType.Warning, true);
                            }
                            break;
                        }
                    default:
                    {
                        EditorGUILayout.PropertyField(iterator);
                        enterChildren = false;
                        continue;
                    }
                }
                enterChildren = iterator.hasVisibleChildren && iterator.isExpanded;
            }
            
            if (targets.Length == 1 && !PrefabUtility.IsPartOfPrefabAsset(target))
            {
                LocalServerGUI();
            }

            if (GUI.changed)
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        /// <summary>
        /// Displays a warning if the property is assigned a player controller that requires a controller.
        /// </summary>
        /// <param name="property">Property to check.</param>
        private void ShowWarningIfPredictorNeedsController(SerializedProperty property)
        {
            ksPredictor predictor = property.objectReferenceValue as ksPredictor;
            if (predictor != null && predictor.RequiresController)
            {
                EditorGUILayout.HelpBox(predictor.GetType().Name + 
                    " can only be used for entities with player controllers.", MessageType.Warning);
            }
        }

        /// <summary>Create the GUI component for controlling a local server.</summary>
        private void LocalServerGUI()
        {
            // Refresh GUI if running state has changed
            if (m_localServerWasRunning != m_localServer.IsRunning)
            {
                m_localServerWasRunning = m_localServer.IsRunning;
                Repaint();
            }

            if (m_localServer.IsRunning && ksStyle.Button("Stop Local Server"))
            {
                m_localServer.Stop();
            }
            else if (!m_localServer.IsRunning)
            {
                if (ksPaths.OperatingSystem != "Microsoft")
                {
                    EditorGUILayout.HelpBox("Local servers for macOS and linux operating systems are currently in development.",
                        MessageType.Info);
                }
                else if (ksServerScriptCompiler.Instance.IsCompiling)
                {
                    EditorGUILayout.HelpBox("Cannot start local server while server scripts are compiling.",
                        MessageType.Warning);
                }
                EditorGUI.BeginDisabledGroup(ksServerScriptCompiler.Instance.IsCompiling || ksPaths.OperatingSystem != "Microsoft");
                if (ksStyle.Button("Start Local Server"))
                {
                    if (Event.current.button == 0)
                    {
                        StartTCPServer();
                    }
                    else
                    {
                        GenericMenu menu = new GenericMenu();
                        menu.AddItem(new GUIContent("TCP Server"), false, StartTCPServer);
                        menu.AddItem(new GUIContent("Websocket Server"), false, StartWSServer);
                        menu.AddItem(new GUIContent("TCP Cluster"), false, StartTCPCluster);
                        menu.AddItem(new GUIContent("Websocket Cluster"), false, StartWSCluster);
                        menu.ShowAsContext();
                    }
                }
                EditorGUI.EndDisabledGroup();
            }
        }

        /// <summary>Launches the local server.</summary>
        private static void RunLocalServer()
        {
            EditorApplication.update -= RunLocalServer;
            m_localServer.CheckServerProjectDirtyFlag();
            if (m_localServer.CheckConfigAndRuntime(m_scene))
            {
                ksAnalytics.Get().TrackDailyEvent(ksAnalytics.Events.LOCAL_SERVER_START);
                m_localServer.StartInstance(m_scene, m_room, m_port, m_protocol);
            }
        }

        /// <summary>Start a local server that listens for TCP connections.</summary>
        private void StartTCPServer()
        {
            ksRoomType type = target as ksRoomType;
            m_scene = type.gameObject.scene.name;
            m_room = type.gameObject.name;
            m_protocol = "tcp";
            EditorApplication.update += RunLocalServer;
        }

        /// <summary>Start a local server that listens for websocket connections.</summary>
        private void StartWSServer()
        {
            ksRoomType type = target as ksRoomType;
            m_scene = type.gameObject.scene.name;
            m_room = type.gameObject.name;
            m_protocol = "ws";
            EditorApplication.update += RunLocalServer;
        }

        /// <summary>Show the menu for starting tcp clusters.</summary>
        private void StartTCPCluster()
        {
            ksRoomType type = target as ksRoomType;
            ksLocalServerMenu.Open(type.gameObject.scene.name, type.gameObject.name);
        }

        /// <summary>Show the menu for starting ws clusters.</summary>
        private void StartWSCluster()
        {
            ksRoomType type = target as ksRoomType;
            ksLocalServerMenu.Open(type.gameObject.scene.name, type.gameObject.name, "ws");
        }
    }
}
