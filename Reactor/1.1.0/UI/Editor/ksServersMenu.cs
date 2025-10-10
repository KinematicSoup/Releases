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
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Menu for displaying the server list.</summary>
    public class ksServersMenu : ksAuthenticatedMenu
    {
        /// <summary>Flags to control what is included in a servers table.</summary>
        [Flags]
        private enum ServerTableFlags
        {
            NONE = 0,
            /// <summary>Draw an image column</summary>
            IMAGE = 1,
            /// <summary>Draw an options button for stopping/restarting/duplicating servers.</summary>
            OPTIONS = 1 << 1,
            /// <summary>Draw host and port fields when expanded.</summary>
            ADDRESS = 1 << 2
        }

        // GUI Layout
        private static GUILayoutOption m_smallWidth = GUILayout.MinWidth(75);
        private static GUILayoutOption m_mediumWidth = GUILayout.MinWidth(100);
        private static GUILayoutOption m_largeWidth = GUILayout.MinWidth(150);

        private ksPublishService m_service = null;
        private ksWindow m_window = null;
        private ksScrollArea m_scroll;

        // Update UI
        private bool m_updateProjects = true;
        private bool m_updateBoundImage = true;
        private bool m_updateServerFormMessage = true;
        private bool m_updateServers = true;
        private bool m_updateVirtualMachines = true;

        private int m_locationIndex = 0;
        private int m_sceneIndex = 0;
        private int m_roomIndex = 0;
        private uint m_selectedServer = 0;
        private bool m_keepAlive = false;
        private bool m_isPublic = true;
        private string m_name = "";

        private bool m_expandLaunchServer = true;
        private bool m_expandRunningServers = true;
        private bool m_expandOtherServers = false;

        private ksProjectInfo m_project = null;
        private List<ksProjectInfo> m_projects = new List<ksProjectInfo>();
        private ksPublishService.ImageInfo m_guiBoundImage;
        private List<string> m_guiLocationText = new List<string>();
        private List<string> m_guiSceneText = new List<string>();
        private List<string> m_guiRoomText = new List<string>();
        private List<ksJSON> m_guiRunningServers = new List<ksJSON>();
        private List<ksJSON> m_guiOtherServers = new List<ksJSON>();

        private string m_layoutMessage = null;
        private MessageType m_layoutMessageType = MessageType.None;
        private string m_GUILayoutMessage = null;
        private MessageType m_GUILayoutMessageType = MessageType.None;
        private string m_serverFormMessage = null;
        private MessageType m_serverFormMessageType = MessageType.None;
        private string m_GUIServerFormMessage = null;
        private MessageType m_GUIServerFormMessageType = MessageType.None;

        /// <summary>Icon to display.</summary>
        public override Texture Icon
        {
            get { return ksTextures.Logo; }
        }

        /// <summary>Unity on enable</summary>
        private void OnEnable()
        {
            SetLoginMenu(typeof(ksLoginMenu));
            hideFlags = HideFlags.HideAndDontSave;
        }

        /// <summary>Called when the menu is opened.</summary>
        /// <param name="window">GUI window</param>
        public override void OnOpen(ksWindow window)
        {
            m_window = window;
            m_scroll = new ksScrollArea(window);
            m_service = ksPublishService.Get();
            m_service.OnUpdateProjects += OnUpdateProjects;
            m_service.OnSelectProject += OnSelectProject;
            m_service.OnUpdateImages += OnUpdateImages;
            m_service.OnStartServer += OnServerAction;
            m_service.OnStopServer += OnServerAction;
            m_service.OnUpdateServers += OnUpdateSevers;
            m_service.OnUpdateVirtualMachines += OnUpdateVirtualMachines;
            m_service.OnBindImage += OnBindImage;
            m_service.Refresh();
        }

        /// <summary>Called when the menu is closed.</summary>
        /// <param name="window">GUI window</param>
        public override void OnClose(ksWindow window)
        {
            m_service.OnUpdateProjects -= OnUpdateProjects;
            m_service.OnSelectProject -= OnSelectProject;
            m_service.OnUpdateImages -= OnUpdateImages;
            m_service.OnStartServer -= OnServerAction;
            m_service.OnStopServer -= OnServerAction;
            m_service.OnUpdateServers -= OnUpdateSevers;
            m_service.OnUpdateVirtualMachines -= OnUpdateVirtualMachines;
            m_service.OnBindImage -= OnBindImage;
        }

        /// <summary>Draw the GUI.</summary>
        /// <param name="window">GUI window</param>
        public override void OnDraw(ksWindow window)
        {
            // Check compiling
            if (EditorApplication.isCompiling)
            {
                EditorGUILayout.HelpBox("Compiling scripts...", MessageType.Warning);
                ClearStates();
                return;
            }

            // Update GUI
            CheckLayout();
            ksStyle.ProjectSelector(ksEditorWebService.Email, m_project, m_projects, Logout, SelectProject);
            if (!string.IsNullOrEmpty(m_GUILayoutMessage))
            {
                EditorGUILayout.HelpBox(m_GUILayoutMessage, m_GUILayoutMessageType);
                return;
            }

            EditorGUIUtility.labelWidth = ksMath.Min(ksStyle.MAX_LABEL_WIDTH, ksStyle.GetInspectorLabelWidth());
            if (m_guiBoundImage == null)
            {
                if (m_service.Images == null || m_service.Images.Count == 0)
                {
                    EditorGUILayout.HelpBox("You must publish an image before you can launch servers.",
                        MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "You must bind an image from the Publishing Window before you can launch servers.",
                        MessageType.Warning);
                }
                if (ksStyle.Button("Open Publishing Window"))
                {
                    ksReactorMenu.OpenPublishWindow();
                }
                return;
            }

            EditorGUILayout.LabelField("Bound Image", m_guiBoundImage.Name + " (" + m_guiBoundImage.Version + ")");
            DrawLaunchServerForm();
            if (!string.IsNullOrEmpty(m_GUIServerFormMessage))
            {
                EditorGUILayout.HelpBox(m_GUIServerFormMessage, m_GUIServerFormMessageType);
            }
            m_scroll.Begin();
            DrawServerTable(m_guiRunningServers, "Running Servers", ref m_expandRunningServers, 
                ServerTableFlags.OPTIONS | ServerTableFlags.ADDRESS | ServerTableFlags.IMAGE);
            if (m_guiOtherServers.Count > 0)
            {
                DrawServerTable(m_guiOtherServers, "Other Servers", ref m_expandOtherServers, ServerTableFlags.IMAGE);
            }
            m_scroll.End();
        }

        /// <summary>Logout of the service and reset the menu.</summary>
        private void Logout()
        {
            ksEditorWebService.Logout(ksReactorConfig.Instance.Urls.Publishing + "/v3/logout");
            m_window.Repaint();
        }

        /// <summary>Select a project from the project list.</summary>
        /// <param name="project">Selected project</param>
        private void SelectProject(ksProjectInfo project)
        {
            m_project = project;
            m_service.SelectedProjectId = m_project.Id;
        }

        /// <summary>Draws the launch server form.</summary>
        private void DrawLaunchServerForm()
        {
            m_expandLaunchServer = EditorGUILayout.Foldout(m_expandLaunchServer, "Launch Server", true);
            if (!m_expandLaunchServer)
            {
                return;
            }

            EditorGUI.indentLevel++;
            int newLocationIndex = EditorGUILayout.Popup("Location", m_locationIndex, m_guiLocationText.ToArray());
            int newSceneIndex = EditorGUILayout.Popup("Scene", m_sceneIndex, m_guiSceneText.ToArray());
            int newRoomIndex = EditorGUILayout.Popup("Room", m_roomIndex, m_guiRoomText.ToArray());
            m_keepAlive = EditorGUILayout.Toggle("Keep Alive", m_keepAlive);
            m_isPublic = EditorGUILayout.Toggle("Is Public", m_isPublic);
            m_name = EditorGUILayout.TextField("Name", m_name);

            bool refreshLayout = false;
            if (newLocationIndex != m_locationIndex)
            {
                m_locationIndex = newLocationIndex;
                refreshLayout = true;
            }
            if (newSceneIndex != m_sceneIndex)
            {
                m_sceneIndex = newSceneIndex;
                refreshLayout = true;
            }
            if (newRoomIndex != m_roomIndex)
            {
                m_roomIndex = newRoomIndex;
                refreshLayout = true;
            }
            if (refreshLayout)
            {
                UpdateBoundImage();
                m_window.Repaint();
            }

            // Launch Button
            if (ksStyle.Button("Launch Server"))
            {
                string image = m_guiBoundImage.Id.ToString();
                string scene = m_guiSceneText[m_sceneIndex];
                string room = m_guiRoomText[m_roomIndex];
                string location = m_guiLocationText[m_locationIndex];
                if (location.EndsWith(" (overloaded)"))
                {
                    location = location.Substring(0, location.Length - " (overloaded)".Length);
                }
                m_service.StartServer(image, scene, room, m_name, location, m_keepAlive, m_isPublic);
                m_serverFormMessage = "Starting server...";
                m_serverFormMessageType = MessageType.Info;
                m_updateServerFormMessage = true;
            }
            EditorGUILayout.Space();
            EditorGUI.indentLevel--;
        }

        /// <summary>Draws an expandable server table.</summary>
        /// <param name="servers">Server json data to draw.</param>
        /// <param name="title">Tile</param>
        /// <param name="expanded">Is the list expanded?</param>
        /// <param name="flags">Flags to control what is drawn in the table.</param>
        private void DrawServerTable(List<ksJSON> servers, string title, ref bool expanded, ServerTableFlags flags)
        {
            expanded = EditorGUILayout.Foldout(expanded, title + " (" + servers.Count + ")", true);
            if (!expanded)
            {
                return;
            }
            EditorGUI.indentLevel++;

            // Table Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Id", EditorStyles.boldLabel, m_smallWidth);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Cluster Id", EditorStyles.boldLabel, m_mediumWidth);
            GUILayout.FlexibleSpace();
            if ((flags & ServerTableFlags.IMAGE) != 0)
            {
                EditorGUILayout.LabelField("Image", EditorStyles.boldLabel, m_mediumWidth);
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.LabelField("Name", EditorStyles.boldLabel, m_largeWidth);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("State", EditorStyles.boldLabel, m_smallWidth);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Creator", EditorStyles.boldLabel, m_mediumWidth);
            EditorGUILayout.EndHorizontal();

            uint lastClusterId = uint.MaxValue;

            // Server List
            foreach (ksJSON server in servers)
            {
                // Draw a space between different clusters.
                uint clusterId = 0;
                server.GetField("clusterId", ref clusterId);
                if (clusterId != lastClusterId)
                {
                    if (lastClusterId != uint.MaxValue)
                    {
                        ksStyle.Line();
                    }
                    lastClusterId = clusterId;
                }

                Rect row = EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("    " + server["id"], m_smallWidth);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(clusterId == 0 ? "-" : clusterId.ToString(), m_mediumWidth);
                GUILayout.FlexibleSpace();
                if ((flags & ServerTableFlags.IMAGE) != 0)
                {
                    EditorGUILayout.LabelField(server["imageName"] + " (" + server["imageVersion"] + ")", m_mediumWidth);
                    GUILayout.FlexibleSpace();
                }
                EditorGUILayout.LabelField(server["name"], m_largeWidth);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(server.HasField("state") ? server["state"] : "running", m_smallWidth);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(server["userAlias"], m_mediumWidth);
                if ((flags & ServerTableFlags.OPTIONS) != 0 && server["canStop"].Bool)
                {
                    Rect rect = GUILayoutUtility.GetLastRect();
                    rect.x = m_window.position.width - ksStyle.ICON_BUTTON_SIZE + m_scroll.ScrollPosition.x;
                    if (m_scroll.IsVerticalBarVisible)
                    {
                        rect.x -= ksScrollArea.SCROLL_BAR_WIDTH;
                    }
                    rect.width = ksStyle.ICON_BUTTON_SIZE + 1;
                    rect.height = ksStyle.ICON_BUTTON_SIZE;
                    rect.y -= 1;
                    EditorGUI.DrawRect(rect, ksStyle.EditorBackgroundColor);
                    if (ksStyle.OptionsButton(rect.position))
                    {
                        GenericMenu menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Stop"), false, StopServer, server);
                        menu.AddItem(new GUIContent("Restart"), false, RestartServer, server);
                        menu.AddItem(new GUIContent("Duplicate"), false, StartServer, server);
                        menu.ShowAsContext();
                    }
                }
                EditorGUILayout.EndHorizontal();

                // Expand a row when it is clicked.
                bool rowExpanded = m_selectedServer == server["id"];
                if (GUI.Button(row, GUIContent.none, GUI.skin.label))
                {
                    rowExpanded = !rowExpanded;
                    if (rowExpanded)
                    {
                        m_selectedServer = server["id"];
                    }
                    else
                    {
                        m_selectedServer = 0;
                    }
                }
                EditorGUI.Foldout(row, rowExpanded, GUIContent.none);
                if (rowExpanded)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Uptime", server["uptime"]);
                    EditorGUILayout.LabelField("Scene", server["scene"]);
                    EditorGUILayout.LabelField("Room", server["room"]);
                    EditorGUILayout.LabelField("Location", server["location"]);
                    if ((flags & ServerTableFlags.ADDRESS) != 0)
                    {
                        EditorGUILayout.LabelField("Host", server["ip"]);
                        EditorGUILayout.LabelField("Port", server["port"]);
                    }
                    EditorGUILayout.Space();
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUI.indentLevel--;
        }

        /// <summary>Stop a server.</summary>
        /// <param name="value">Server data of type <see cref="ksJSON"/>.</param>
        private void StopServer(object value)
        {
            ksJSON server = value as ksJSON;
            m_service.StopServer(server["id"]);
            m_serverFormMessage = "Stopping server...";
            m_serverFormMessageType = MessageType.Info;
            m_updateServerFormMessage = true;
        }

        /// <summary>Stop and restart a server.</summary>
        /// <param name="value">Server data of type <see cref="ksJSON"/>.</param>
        private void RestartServer(object value)
        {
            ksJSON server = value as ksJSON;
            m_service.StopServer(server["id"]);
            m_service.StartServer(
                m_guiBoundImage.Id.ToString(),
                server["scene"], 
                server["room"], 
                server["name"],
                server["location"], 
                server["keepAlive"],
                server["isPublic"]
            );
            m_serverFormMessage = "Restarting server...";
            m_serverFormMessageType = MessageType.Info;
            m_updateServerFormMessage = true;
        }

        /// <summary>Start a server.</summary>
        /// <param name="value">Server data of type <see cref="ksJSON"/>.</param>
        private void StartServer(object value)
        {
            ksJSON server = value as ksJSON;
            m_service.StartServer(
                m_guiBoundImage.Id.ToString(),
                server["scene"], 
                server["room"], 
                server["name"],
                server["location"], 
                server["keepAlive"],
                server["isPublic"]
            );
            m_serverFormMessage = "Starting server...";
            m_serverFormMessageType = MessageType.Info;
            m_updateServerFormMessage = true;
        }

        /// <summary>Handle project list updates.</summary>
        /// <param name="error">Error message</param>
        private void OnUpdateProjects(string error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                m_layoutMessage = error;
                m_layoutMessageType = MessageType.Error;
            }
            else
            {
                m_layoutMessage = null;
                m_layoutMessageType = MessageType.None;
            }
            m_updateProjects = true;
            m_window.Repaint();
        }

        /// <summary>Handle project selection changes.</summary>
        /// <param name="selectedProjectId">Selected project ID.</param>
        /// <param name="error">Error message</param>
        private void OnSelectProject(int selectedProjectId, string error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                m_layoutMessage = error;
                m_layoutMessageType = MessageType.Error;
            }
            else
            {
                m_layoutMessage = null;
                m_layoutMessageType = MessageType.None;
                m_project = null;
                foreach (ksProjectInfo project in m_projects)
                {
                    if (project.Id == m_service.SelectedProjectId)
                    {
                        m_project = project;
                    }
                }
                if (m_project == null)
                {
                    m_sceneIndex = 0;
                    m_roomIndex = 0;
                }
            }
            m_updateProjects = true;
            m_window.Repaint();
        }

        /// <summary>Handle image list updates.</summary>
        /// <param name="error">Error message</param>
        private void OnUpdateImages(string error)
        {
            m_updateBoundImage = true;
            m_window.Repaint();
        }

        /// <summary>Handle server action requests.</summary>
        /// <param name="error">Error message</param>
        private void OnServerAction(string error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                m_serverFormMessage = error;
                m_serverFormMessageType = MessageType.Error;
            }
            else
            {
                m_serverFormMessage = null;
                m_serverFormMessageType = MessageType.None;
            }
            m_updateServerFormMessage = true;
            m_window.Repaint();
        }

        /// <summary>Handle server list updates.</summary>
        /// <param name="error">Error message</param>
        private void OnUpdateSevers(string error)
        {
            m_updateServers = true;
            m_window.Repaint();
        }

        /// <summary>Handle virtual machine list updates.</summary>
        /// <param name="error">Error message</param>
        private void OnUpdateVirtualMachines(string error)
        {
            m_updateVirtualMachines = true;
            m_window.Repaint();
        }

        /// <summary>Handles a bound image change.</summary>
        /// <param name="imageId">Id of the image that was bound.</param>
        private void OnBindImage(int imageId)
        {
            m_updateBoundImage = true;
            m_updateServers = true;
            m_window.Repaint();
        }

        /// <summary>Check and apply updates that affect the GUI layout.</summary>
        private void CheckLayout()
        {
            if (Event.current.type == EventType.Layout)
            {
                List<ksProjectInfo> projects = m_service.Projects;

                // Update layout message
                if (projects == null)
                {
                    m_GUILayoutMessage = "Fetching projects...";
                    m_GUILayoutMessageType = MessageType.Info;
                }
                else if (m_layoutMessage != null)
                {
                    m_GUILayoutMessage = m_layoutMessage;
                    m_GUILayoutMessageType = m_layoutMessageType;
                }
                else if (projects.Count == 0)
                {
                    m_GUILayoutMessage = "You are not an editor of any project. You must be added to a project " +
                            "before you can publish images.";
                    m_GUILayoutMessageType = MessageType.Warning;
                }
                else if (m_service.Images == null)
                {
                    m_GUILayoutMessage = "Fetching images...";
                    m_GUILayoutMessageType = MessageType.Info;
                }
                else
                {
                    m_GUILayoutMessage = null;
                    m_GUILayoutMessageType = MessageType.None;
                }

                // Update project GUI data
                if (m_updateProjects && projects != null)
                {
                    m_updateProjects = false;
                    m_projects = projects;
                    m_project = null;
                    foreach (ksProjectInfo project in m_projects)
                    {
                        if (project.Id == m_service.SelectedProjectId)
                        {
                            m_project = project;
                        }
                    }
                }

                // Update the image selection GUI data
                if (m_updateBoundImage)
                {
                    m_updateBoundImage = false;
                    UpdateBoundImage();
                }

                // Update server form message GUI data
                if (m_updateServerFormMessage)
                {
                    m_updateServerFormMessage = false;
                    m_GUIServerFormMessage = m_serverFormMessage;
                    m_GUIServerFormMessageType = m_serverFormMessageType;
                }

                // Update server list GUI data
                if (m_updateServers)
                {
                    m_updateServers = false;
                    m_guiRunningServers.Clear();
                    m_guiOtherServers.Clear();
                    if (m_service.Servers != null)
                    {
                        int boundId = m_guiBoundImage == null ? -1 : m_guiBoundImage.Id;
                        foreach (ksJSON server in m_service.Servers.Array)
                        {
                            if (server["imageId"].Int == boundId)
                            {
                                m_guiRunningServers.Add(server);
                            }
                            else
                            {
                                m_guiOtherServers.Add(server);
                            }
                        }

                        // Sort by cluster id so rooms in the same cluster are grouped together.
                        m_guiRunningServers.Sort(CompareClusterId);
                        m_guiOtherServers.Sort(CompareClusterId);
                    }
                }

                // Update virtual machines list GUI data
                if (m_updateVirtualMachines)
                {
                    // Track the old location so that we can update the selected location index
                    string oldLocation = "";
                    if (m_guiLocationText.Count > 0)
                    {
                        oldLocation = m_guiLocationText[m_locationIndex < m_guiLocationText.Count ? m_locationIndex : 0];
                        if (oldLocation.EndsWith(" (overloaded)"))
                        {
                            oldLocation = oldLocation.Substring(0, oldLocation.Length - " (overloaded)".Length);
                        }
                    }
                    m_locationIndex = 0;

                    m_guiLocationText.Clear();
                    m_updateVirtualMachines = false;
                    if (m_service.VirtualMachines != null)
                    {
                        // Add overloaded locations after other locations.  If the location matches the
                        // old selected location, then update the selected location index.
                        List<string> overloadedLocations = new List<string>();
                        foreach (ksJSON vm in m_service.VirtualMachines.Array)
                        {
                            string location = vm["location"];
                            if (vm.GetField("overloaded", 0) == 1)
                            {
                                overloadedLocations.Add(location);
                            }
                            else
                            {
                                m_guiLocationText.Add(location);
                                if (m_locationIndex == 0 && location == oldLocation)
                                {
                                    m_locationIndex = m_guiLocationText.Count - 1;
                                }
                            }
                        }

                        // Add overloaded locations at the bottom of the list
                        foreach (string overloadedLocation in overloadedLocations)
                        {
                            m_guiLocationText.Add(overloadedLocation + " (overloaded)");
                            if (m_locationIndex == 0 && overloadedLocation == oldLocation)
                            {
                                m_locationIndex = m_guiLocationText.Count - 1;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Compares the cluster id from two server jsons. Used for sorting to group servers by cluster.
        /// </summary>
        /// <param name="lhs">Left server json</param>
        /// <param name="rhs">Right server json</param>
        /// <returns>
        /// -1 if <paramref name="lhs"/> has a lower cluster id than <paramref name="rhs"/>, 1 if it has a greater id,
        /// and 0 if they are the same.
        /// </returns>
        private int CompareClusterId(ksJSON lhs, ksJSON rhs)
        {
            uint leftId = 0;
            uint rightId = 0;
            lhs.GetField("clusterId", ref leftId);
            rhs.GetField("clusterId", ref rightId);
            return leftId < rightId ? -1 : (leftId == rightId ? 0 : 1);
        }

        /// <summary>Updates the bound image and the scene and room dropdowns.</summary>
        private void UpdateBoundImage()
        {
            m_guiSceneText.Clear();
            m_guiRoomText.Clear();
            int oldBoundId = m_guiBoundImage == null ? -1 : m_guiBoundImage.Id;
            m_guiBoundImage = m_service.BoundImage;
            if (m_guiBoundImage == null)
            {
                return;
            }
            if (oldBoundId != m_guiBoundImage.Id)
            {
                // Update servers so servers of the bound image are moved from "Other Servers" to "Running Servers".
                m_updateServers = true;
            }

            int index = 0;
            foreach (KeyValuePair<string, List<string>> scene in m_guiBoundImage.Scenes)
            {
                m_guiSceneText.Add(scene.Key);
                if (m_sceneIndex == index++)
                {
                    foreach (string room in scene.Value)
                    {
                        m_guiRoomText.Add(room);
                    }
                }
            }
        }

        /// <summary>Clear message and fetch states.</summary>
        private void ClearStates()
        {
            m_layoutMessage = null;
            m_layoutMessageType = MessageType.None;

            m_serverFormMessage = null;
            m_serverFormMessageType = MessageType.None;
        }
    }
}
