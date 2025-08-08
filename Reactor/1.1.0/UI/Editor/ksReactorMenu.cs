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

using UnityEngine;
using System;
using System.IO;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEditor.Experimental;
using KS.Unity.Editor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>This class is responsible for the file menu and context menu GUIs.</summary>
    public class ksReactorMenu
    {
        private const string LOG_CHANNEL = "KS.Unity.ReactorMenu";

        /// <summary>Opens Reactor settings window.</summary>
        [MenuItem(ksMenuNames.REACTOR + "Settings", false, ksStyle.WINDOW_GROUP + 1)]
        public static void Init()
        {
            Selection.objects = new UnityEngine.Object[] { ksReactorConfig.Instance };
            ksEditorUtils.FocusInspectorWindow();
        }

        /// <summary>Publishes the current scene online.</summary>
        [MenuItem(ksMenuNames.REACTOR + "Publishing", priority = ksStyle.WINDOW_GROUP + 2)]
        public static void OpenPublishWindow()
        {
            ksWindow.Open(ksWindow.REACTOR_PUBLISH, delegate (ksWindow window)
            {
                window.titleContent = new GUIContent(" Publish", ksTextures.Logo);
                window.minSize = new Vector2(380f, 100f);
                window.Menu = ScriptableObject.CreateInstance<ksPublishMenu>();
            });
        }

        /// <summary>Launches instances of a server.</summary>
        [MenuItem(ksMenuNames.REACTOR + "Servers", priority = ksStyle.WINDOW_GROUP + 3)]
        static void ServerManagers()
        {
            ksWindow.Open(ksWindow.REACTOR_SERVERS, delegate (ksWindow window)
            {
                window.titleContent = new GUIContent(" Servers", ksTextures.Logo);
                window.minSize = new Vector2(380f, 100f);
                window.Menu = ScriptableObject.CreateInstance<ksServersMenu>();
            });
        }


        /// <summary>Builds the current scene locally.</summary>
        [MenuItem(ksMenuNames.REACTOR + "Build Scene Configs %F2", priority = ksStyle.BUILD_GROUP + 1)]
        public static void BuildConfig()
        {
            ksServerInstaller.Get().ShowDownloadRequest = true;
            if (ksServerInstaller.Get().Check(true, false, true) == ksServerInstaller.FileStates.MISSING_BUILD_FILES)
            {
                return;
            }

            ksConfigWriter configWriter = new ksConfigWriter();
            configWriter.PrettyConfigs = true;
            if (configWriter.Build(true, false, false))
            {
                ksLog.Info("Build complete." + configWriter.Summary);
                ksLocalServer.Manager.StopServers();
                ksLocalServer.Manager.RestartOrRemoveStoppedServers();
            }
        }

        /// <summary>Builds the server runtime.</summary>
        /// <returns>True if the server runtime built successfully.</returns>
        [MenuItem(ksMenuNames.REACTOR + "Build KSServerRuntime %F3", priority = ksStyle.BUILD_GROUP + 2)]
        public static bool BuildServerRuntime()
        {
            ksServerInstaller.Get().ShowDownloadRequest = true;
            if (ksServerInstaller.Get().Check(true, false, true) == ksServerInstaller.FileStates.MISSING_BUILD_FILES)
            {
                return false;
            }

            EditorUtility.DisplayProgressBar("KS Reactor Build", "Building runtime.", 0.1f);
            bool buildSuccess = false;
            try
            {
                ksConfigWriter configWriter = new ksConfigWriter();
                if (configWriter.Build(false, true))
                {
                    buildSuccess = true;
                    ksLog.Info("Config build complete." + configWriter.Summary);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            return buildSuccess;
        }

        /// <summary>Rebuilds the server runtime.</summary>
        [MenuItem(ksMenuNames.REACTOR + "Rebuild All %F4", priority = ksStyle.BUILD_GROUP + 3)]
        public static void RebuildAll()
        {
            ksServerInstaller.Get().ShowDownloadRequest = true;
            if (ksServerInstaller.Get().Check(true, false, true) == ksServerInstaller.FileStates.MISSING_BUILD_FILES)
            {
                return;
            }

            ksConfigWriter configWriter = new ksConfigWriter();
            configWriter.PrettyConfigs = true;
            if (configWriter.Build(true, true, true, ksServerScriptCompiler.Configurations.LOCAL_RELEASE, true))
            {
                ksLog.Info("Config and server build complete." + configWriter.Summary);
                ksLocalServer.Manager.RestartOrRemoveStoppedServers();
            }
        }

        /// <summary>Launches local cluster.</summary>
        [MenuItem(ksMenuNames.REACTOR + "Launch Local Cluster", priority = ksStyle.TESTING_GROUP + 1)]
        private static void LaunchLocalCluster()
        {
            ksLocalServerMenu.Open(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        /// <summary>Disable the LaunchLocalCluster menu item for non-Microsoft operating systems.</summary>
        [MenuItem(ksMenuNames.REACTOR + "Launch Local Cluster", priority = ksStyle.TESTING_GROUP + 1, validate = true)]
        private static bool CanLaunchLocalClusters()
        {
            return ksPaths.OperatingSystem == "Microsoft";
        }

        /// <summary>Shows local server log files.</summary>
        [MenuItem(ksMenuNames.REACTOR + "Local Server Logs", priority = ksStyle.TESTING_GROUP + 2)]
        static void ShowConsole()
        {
            ksWindow.Open(ksWindow.REACTOR_CONSOLE, delegate (ksWindow window)
            {
                window.titleContent = new GUIContent(" Logs", ksTextures.Logo);
                window.minSize = new Vector2(380f, 100f);
                window.Menu = ScriptableObject.CreateInstance<ksServerLogMenu>();
            });
        }

        /// <summary>Publishes the current scene online.</summary>
        [MenuItem(ksMenuNames.REACTOR + "Download Reactor Server", priority = ksStyle.MISC_GROUP + 1)]
        static void DownloadReactorServer()
        {
            if (ksServerInstaller.Get().Download("Do you want to download and install the Reactor development server?"))
            {
                ksLog.Info("Reactor development server installed.");
            }
        }

        /// <summary>Publishes the current scene online.</summary>
        [MenuItem(ksMenuNames.REACTOR + "Check for Updates", priority = ksStyle.MISC_GROUP + 1)]
        static void CheckForUpdates()
        {
            ksPackageUpdater.Get().Check(null, true);
        }

        /// <summary>Context menu for creating a common scripts folder.</summary>
        [MenuItem(ksMenuNames.CREATE + "Common Scripts Folder", priority = ksMenuGroups.FOLDER)]
        private static void CreateCommonFolder()
        {
            ScriptsFolderCreateAction action = ScriptableObject.CreateInstance<ScriptsFolderCreateAction>();
            action.FolderType = ScriptsFolderCreateAction.FolderTypes.COMMON;
            Texture2D icon = EditorGUIUtility.IconContent(EditorResources.emptyFolderIconName).image as Texture2D;
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, action, "Common", icon, null);
        }

        /// <summary>Context menu for creating a server scripts folder.</summary>
        [MenuItem(ksMenuNames.CREATE + "Server Scripts Folder", priority = ksMenuGroups.FOLDER)]
        private static void CreateServerFolder()
        {
            ScriptsFolderCreateAction action = ScriptableObject.CreateInstance<ScriptsFolderCreateAction>();
            action.FolderType = ScriptsFolderCreateAction.FolderTypes.SERVER;
            Texture2D icon = EditorGUIUtility.IconContent(EditorResources.emptyFolderIconName).image as Texture2D;
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, action, "Server", icon, null);
        }

        /// <summary>Checks if the selected path is a valid location for a common scripts folder.</summary>
        /// <returns>True if a common scripts folder can be created at the selected path.</returns>
        [MenuItem(ksMenuNames.CREATE + "Common Scripts Folder", true)]
        private static bool ValidateCommonFolder()
        {
            return !ValidateCommonScriptsPath() && !Application.isPlaying;
        }

        /// <summary>Checks if the selected path is a valid location for a server scripts folder.</summary>
        /// <returns>True if a server scripts folder can be created at the selected path.</returns>
        [MenuItem(ksMenuNames.CREATE + "Server Scripts Folder", true)]
        private static bool ValidateServerFolder()
        {
            return !ValidateServerScriptPath() && !Application.isPlaying;
        }

        /// <summary>Context menu for creating a <see cref="ksRoomScript"/> in the project views right click menu.</summary>
        [MenuItem(ksMenuNames.CREATE + "Client Room Script", priority = ksMenuGroups.CLIENT)]
        static void CreateClientRoomScript()
        {
            ksNewScriptMenu.Open(ScriptableObject.CreateInstance<ksClientScriptTemplate>().Initialize(
                ksClientScriptTemplate.ScriptType.ROOM), false, GetSelectedFolder());
        }

        /// <summary>Context menu for creating a <see cref="ksPlayerScript"/> in the project views right click menu.</summary>
        [MenuItem(ksMenuNames.CREATE + "Client Player Script", priority = ksMenuGroups.CLIENT)]
        static void CreateClientPlayerScript()
        {
            ksNewScriptMenu.Open(ScriptableObject.CreateInstance<ksClientScriptTemplate>().Initialize(
                ksClientScriptTemplate.ScriptType.PLAYER), false, GetSelectedFolder());
        }

        /// <summary>
        /// Context menu for creating a <see cref="ksEntityScript"/> in the project views right click menu.
        /// This will not attach it to a game object for you.
        /// </summary>
        [MenuItem(ksMenuNames.CREATE + "Client Entity Script", priority = ksMenuGroups.CLIENT)]
        static void CreateClientEntityScript()
        {
            ksNewScriptMenu.Open(ScriptableObject.CreateInstance<ksClientScriptTemplate>().Initialize(
                ksClientScriptTemplate.ScriptType.ENTITY), false, GetSelectedFolder());
        }

        /// <summary>
        /// Context menu for creating a <see cref="ksPredictor"/> in the project views right click menu.
        /// This will not attach it to a game object for you.
        /// </summary>
        [MenuItem(ksMenuNames.CREATE + "Predictor", priority = ksMenuGroups.CLIENT)]
        static void CreatePredictor()
        {
            ksNewScriptMenu.Open(
                ScriptableObject.CreateInstance<ksPredictorTemplate>().Initialize(),
                false,
                GetSelectedFolder());
        }

        /// <summary>Checks if the selected path is a valid location for a client script.</summary>
        /// <returns>True if a client script can be created at the selected path.</returns>
        [MenuItem(ksMenuNames.CREATE + "Client Room Script", true)]
        [MenuItem(ksMenuNames.CREATE + "Client Player Script", true)]
        [MenuItem(ksMenuNames.CREATE + "Client Entity Script", true)]
        [MenuItem(ksMenuNames.CREATE + "Predictor", true)]
        static bool ValidateClientScriptPath()
        {
            string path = GetSelectedFolder();
            return !string.IsNullOrEmpty(path) &&
                !ksPaths.IsCommonPath(path) &&
                !path.StartsWith(ksPaths.Proxies) &&
                !ksPaths.IsServerPath(path) &&
                !Application.isPlaying;
        }


        /// <summary>
        /// Context menu for creating a <see cref="ksPlayerController"/> in the project views right click menu.
        /// This will not attach it to a game object for you.
        /// </summary>
        [MenuItem(ksMenuNames.CREATE + "Player Controller", priority = ksMenuGroups.COMMON)]
        static void CreatePlayerController()
        {
            ksNewScriptMenu.Open(ScriptableObject.CreateInstance<ksPlayerControllerTemplate>(), false,
                GetSelectedFolder());
        }

        /// <summary>Checks if the selected path is a valid location for common scripts.</summary>
        /// <returns>True if a common script can be created at the selected path.</returns>
        [MenuItem(ksMenuNames.CREATE + "Player Controller", true)]
        static bool ValidateCommonScriptsPath()
        {
            string path = GetSelectedFolder();
            return !string.IsNullOrEmpty(path) &&
                ksPaths.IsCommonPath(path) &&
                !Application.isPlaying;
        }

        /// <summary>
        /// Context menu for creating a <see cref="ksServerEntityScript"/> in the project views right click menu.
        /// This will not attach it to a game object for you.
        /// </summary>
        [MenuItem(ksMenuNames.CREATE + "Server Entity Script", priority = ksMenuGroups.SERVER)]
        static void CreateServerEntityScript()
        {
            ksNewScriptMenu.Open(ScriptableObject.CreateInstance<ksServerScriptTemplate>().Initialize(
                ksServerScriptTemplate.ScriptType.ENTITY), false, GetSelectedFolder());
        }

        /// <summary>
        /// Context menu for creating a <see cref="ksServerRoomScript"/> in the project view's right click menu.
        /// This will not attach it to a game object for you.
        /// </summary>
        [MenuItem(ksMenuNames.CREATE + "Server Room Script", priority = ksMenuGroups.SERVER)]
        static void CreateServerRoomScript()
        {
            ksNewScriptMenu.Open(ScriptableObject.CreateInstance<ksServerScriptTemplate>().Initialize(
                ksServerScriptTemplate.ScriptType.ROOM), false, GetSelectedFolder());
        }

        /// <summary>
        /// Context menu for creating a <see cref="ksScriptAsset"/> in the project view's right click menu.
        /// </summary>
        [MenuItem(ksMenuNames.CREATE + "Script Asset", priority = ksMenuGroups.COMMON)]
        static void CreateScriptAsset()
        {
            ksNewScriptMenu.Open(ScriptableObject.CreateInstance<ksScriptAssetTemplate>(), false, GetSelectedFolder());
        }

        /// <summary>
        /// Context menu for creating a <see cref="ksServerPlayerScript"/> in the project views right click menu.
        /// This will not attach it to a game object for you.
        /// </summary>
        [MenuItem(ksMenuNames.CREATE + "Server Player Script", priority = ksMenuGroups.SERVER)]
        static void CreateServerPlayerScript()
        {
            ksNewScriptMenu.Open(ScriptableObject.CreateInstance<ksServerScriptTemplate>().Initialize(
                ksServerScriptTemplate.ScriptType.PLAYER), false, GetSelectedFolder());
        }

        /// <summary>Checks if the selected path is a valid location for a server script.</summary>
        /// <returns>True if a server script can be created at the selected path.</returns>
        [MenuItem(ksMenuNames.CREATE + "Server Entity Script", true)]
        [MenuItem(ksMenuNames.CREATE + "Server Room Script", true)]
        [MenuItem(ksMenuNames.CREATE + "Server Player Script", true)]
        static bool ValidateServerScriptPath()
        {
            string path = GetSelectedFolder();
            return !string.IsNullOrEmpty(path) &&
                ksPaths.IsServerPath(path) &&
                !Application.isPlaying;
        }

        /// <summary>Checks if the selected path is a valid location for a script asset.</summary>
        /// <returns>True if a script asset can be created at the selected path.</returns>
        [MenuItem(ksMenuNames.CREATE + "Script Asset", true)]
        static bool ValidateScriptAssetPath()
        {
            string path = GetSelectedFolder();
            return !string.IsNullOrEmpty(path) &&
                (ksPaths.IsServerPath(path) ||
                ksPaths.IsCommonPath(path)) &&
                !Application.isPlaying;
        }

        /// <summary>
        /// Hierarchy context menu for creating a game object with a <see cref="ksRoomType"/> and a
        /// <see cref="ksPhysicsSettings"/>.
        /// </summary>
        /// <param name="command">Contains menu context data.</param>
        [MenuItem(ksMenuNames.HIERARCHY + "Physics Room", false, 10)]
        static void CreatePhysicsRoom(MenuCommand command)
        {
            GameObject gameObject = new GameObject();
            gameObject.name = "Room";
            gameObject.AddComponent<ksRoomType>();
            gameObject.AddComponent<ksPhysicsSettings>();
            gameObject.AddComponent<ksConnect>();
            GameObjectUtility.SetParentAndAlign(gameObject, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create " + gameObject.name);
            Selection.activeGameObject = gameObject;
        }

        /// <summary>Hierarchy context menu for creating a game object with a <see cref="ksRoomType"/>.</summary>
        /// <param name="command">Contains menu context data.</param>
        [MenuItem(ksMenuNames.HIERARCHY + "Room", false, 10)]
        static void CreateRoom(MenuCommand command)
        {
            GameObject gameObject = new GameObject();
            gameObject.name = "Room";
            gameObject.AddComponent<ksRoomType>();
            gameObject.AddComponent<ksConnect>();
            GameObjectUtility.SetParentAndAlign(gameObject, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create " + gameObject.name);
            Selection.activeGameObject = gameObject;
        }

        /// <summary>Returns path to the selected folder relative to the project.</summary>
        /// <returns>Selected folder path.</returns>
        private static string GetSelectedFolder()
        {
            if (Selection.assetGUIDs == null || Selection.assetGUIDs.Length != 1)
            {
                return null;
            }
            string path = AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]);
            if (!Directory.Exists(path))
            {
                int index = path.LastIndexOf("/");
                if (index != -1)
                {
                    path = path.Substring(0, index);
                }
            }
            return path.Replace('\\', '/') + "/";
        }

        /// <summary>
        /// Implementation of <see cref="EndNameEditAction"/> that creates a folder with an asmref referencing either
        /// KSScripts-Common or KSScripts-Server.
        /// </summary>
        private class ScriptsFolderCreateAction : EndNameEditAction
        {
            /// <summary>Will this create a folder for common scripts or server scripts?</summary>
            public enum FolderTypes
            {
                COMMON = 0,
                SERVER = 1
            }

            /// <summary>Will this create a folder for common scripts or server scripts?</summary>
            public FolderTypes FolderType
            {
                get { return m_type; }
                set { m_type = value; }
            }
            private FolderTypes m_type;

            private static readonly string TEMPLATE =
@"{
    ""reference"": ""KSScripts-%TYPE%""
}";

            /// <summary>Creates the common or server scripts folder.</summary>
            /// <param name="instanceId">Unused</param>
            /// <param name="pathName">Path to the folder to create.</param>
            /// <param name="resourceFile">Unused</param>
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                // Create the folder.
                AssetDatabase.CreateFolder(Path.GetDirectoryName(pathName), Path.GetFileName(pathName));
                ProjectWindowUtil.ShowCreatedAsset(
                    AssetDatabase.LoadAssetAtPath(pathName, typeof(UnityEngine.Object)));

                // Create the asmref.
                string type = m_type == FolderTypes.COMMON ? "Common" : "Server";
                string template = TEMPLATE.Replace("%TYPE%", type);
                Write(pathName + "/KSScripts-" + type + ".asmref", template);
                AssetDatabase.Refresh();
            }

            /// <summary>Writes to a file.</summary>
            /// <param name="path">Path to write to.</param>
            /// <param name="data">Data to write.</param>
            private void Write(string path, string data)
            {
                try
                {
                    using (StreamWriter writer = new StreamWriter(path))
                    {
                        writer.Write(data);
                        writer.Close();
                    }
                }
                catch (Exception e)
                {
                    ksLog.Error(this, "Error writing to " + path, e);
                }
            }
        }
    }
}