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
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>
    /// Performs initialization logic when the Unity editor loads.
    /// - Loads Reactor configs.
    /// - Checks Reactor package versions.
    /// - Prepares caches.
    /// - Attaches Reactor scripts to game objects.
    /// - Initializes UI windows.
    /// </summary>
    [InitializeOnLoad]
    public class ksInitializer
    {
        private static readonly string LOG_CHANNEL = typeof(ksInitializer).ToString();

        /// <summary>Static constructor</summary>
        static ksInitializer()
        {
            ksRoomType.LocalServerRunningChecker = ksLocalServer.Manager.IsLocalServerRunning;
            if (!ksEditorUtils.IsMPPMVirtualPlayer)
            {
                EditorApplication.update += Init;
            }
        }

        /// <summary>Performs initialization logic that must wait until after Unity deserialization finishes.</summary>
        private static void Init()
        {
            EditorApplication.update -= Init;
            bool newConfig = CreateReactorConfig();
            bool changedDefines = ksEditorUtils.SetDefineSymbol("REACTOR");
            EditorApplication.update += TrackUserId;

            // Detect if we are in a development environment
            if (File.Exists(Application.dataPath + "/KSSource/Reactor.asmdef"))
            {
                if (ksEditorUtils.SetDefineSymbol("KS_DEVELOPMENT"))
                {
                    changedDefines = true;
                }

                // If the config is new and we are in development mode, then set a Reactor server override path.
                if (newConfig)
                {
                    ksReactorConfig.Instance.Server.OverrideServerPath = "Reactor" + Path.DirectorySeparatorChar;
                    EditorUtility.SetDirty(ksReactorConfig.Instance);
                    AssetDatabase.SaveAssets();
                }
            }
            else if (ksEditorUtils.ClearDefineSymbol("KS_DEVELOPMENT"))
            {
                changedDefines = true;
            }
            if (changedDefines)
            {
                return;
            }

            ksRoomType.InitialPredictor = AssetDatabase.LoadAssetAtPath<ksPredictor>(ksPaths.DefaultLinearPredictor);
            ksRoomType.InitialControllerPredictor =
                AssetDatabase.LoadAssetAtPath<ksPredictor>(ksPaths.DefaultConvergingInputPredictor);

            // Prevent editing of default predictor assets outside a development environment.
#if KS_DEVELOPMENT
            if (ksRoomType.InitialPredictor != null)
            {
                ksRoomType.InitialPredictor.hideFlags &= ~HideFlags.NotEditable;
            }
            if (ksRoomType.InitialControllerPredictor != null)
            {
                ksRoomType.InitialControllerPredictor.hideFlags &= ~HideFlags.NotEditable;
            }
#else
            if (ksRoomType.InitialPredictor != null)
            {
                ksRoomType.InitialPredictor.hideFlags |= HideFlags.NotEditable;
            }
            if (ksRoomType.InitialControllerPredictor != null)
            {
                ksRoomType.InitialControllerPredictor.hideFlags |= HideFlags.NotEditable;
            }
#endif

            ksPublishService.Get().Start();
            PrioritizeUpdate();
            bool isNewOrUpdate = UpdateVersion();
            new ksIconManager().SetScriptIcons();
            ksServerProjectUpdater.Instance.GenerateMissingAsmDefs();
            ksPaths.FindCommonAndServerFolders();
            ksServerProjectWatcher.Get().Run();
            ksScriptGenerator.Get().LoadAttachments();
            
            // Initialize Publish Window
            ksWindow.SetMenuType(ksWindow.REACTOR_PUBLISH, typeof(ksPublishMenu));
            ksWindow window = ksWindow.Find(ksWindow.REACTOR_PUBLISH);
            if (window != null)
            {
                window.Repaint();
            }

            // Initialize Servers Window
            ksWindow.SetMenuType(ksWindow.REACTOR_SERVERS, typeof(ksServersMenu));
            window = ksWindow.Find(ksWindow.REACTOR_SERVERS);
            if (window != null)
            {
                window.Repaint();
            }

            // Initialize Console Window
            ksWindow.SetMenuType(ksWindow.REACTOR_CONSOLE, typeof(ksServerLogMenu));
            window = ksWindow.Find(ksWindow.REACTOR_CONSOLE);
            if (window != null)
            {
                window.Repaint();
            }

            if (!Application.isPlaying)
            {
                ksPackageUpdater.Get().Check((bool updated) => {
                    if (!updated)
                    {
                        ksServerInstaller.Get().Check(true, true, true, !isNewOrUpdate);
                    }
                });
            }
            ksServerProjectUpdater.Instance.UpdateOutputPath();
        }

        /// <summary>
        /// Sets priority for <see cref="ksUpdateHook"/> to ensure our update loop runs before other scripts, and for
        /// <see cref="ksLateUpdateHook"/> to ensure it runs after other scripts.
        /// </summary>
        private static void PrioritizeUpdate()
        {
            string updateName = typeof(ksUpdateHook).Name;
            string lateUpdateName = typeof(ksLateUpdateHook).Name;
            int count = 0;
            foreach (MonoScript monoScript in MonoImporter.GetAllRuntimeMonoScripts())
            {
                if (monoScript.name == updateName)
                {
                    // without this we will get stuck in an infinite loop
                    if (MonoImporter.GetExecutionOrder(monoScript) != -1000)
                    {
                        MonoImporter.SetExecutionOrder(monoScript, -1000);
                    }
                    count++;
                    if (count == 2)
                    {
                        return;
                    }
                }
                else if (monoScript.name == lateUpdateName)
                {
                    // without this we will get stuck in an infinite loop
                    if (MonoImporter.GetExecutionOrder(monoScript) != 1000)
                    {
                        MonoImporter.SetExecutionOrder(monoScript, 1000);
                    }
                    count++;
                    if (count == 2)
                    {
                        return;
                    }
                }
            }
        }

        /// <summary>Updates the project API version number.</summary>
        /// <returns>True if the Reactor package is new or updated</returns>
        private static bool UpdateVersion()
        {
            try
            {
                ksVersion.Current = ksVersion.FromString(ksReactorConfig.Instance.FullVersion);
            }
            catch (ArgumentException)
            {
                ksLog.Warning(LOG_CHANNEL, "Invalid Reactor version string " +
                    ksReactorConfig.Instance.Version);
                return false;
            }

            bool isNewOrUpdate = false;
            string versionString = ksReactorConfig.Instance.Build.LastBuildVersion;
            if (versionString != "")
            {
                ksVersion version = new ksVersion();
                bool isValid = true;
                try
                {
                    version = ksVersion.FromString(versionString);
                }
                catch (ArgumentException)
                {
                    ksLog.Warning(LOG_CHANNEL, "Project has invalid version string (" + versionString +
                        ") and may be incompatible with the current version (" + ksVersion.Current + ")");
                    isValid = false;
                }
                if (isValid && version != ksVersion.Current)
                {
                    isNewOrUpdate = true;
                    if (version > ksVersion.Current)
                    {
                        if (!EditorUtility.DisplayDialog(
                            "Reactor Project Version Mismatch",
                            "This project is using a newer Reactor version (" + version +
                            ") than the current version (" + ksVersion.Current +
                            ") and may be incompatible. Continue anyway?",
                            "OK",
                            "Cancel"))
                        {
                            EditorApplication.Exit(0);
                        }
                    }
                    else if (version < new ksVersion(1, 0, 0, 0))
                    {
                        DisplayUpgradeMessage("Cannot automatically upgrade projects older than " +
                            "1.0.0. You should upgrade to 1.0.0 before upgrading further.", true);
                    }
                    else
                    {
                        ksAnalytics.Get().TrackEvent(ksAnalytics.Events.UPGRADE);

                        ksLog.Info(LOG_CHANNEL, "This project was using an old Reactor version " + version +
                            ". Your project has been successfully updated to " + ksVersion.Current);
                    }

                    if (!version.Equals(ksVersion.Current, ksVersion.Precision.REVISION))
                    {
                        // When updating client versions, we clear the download check so that users
                        // are prompted to download the updated server.
                        ksReactorConfig.Instance.Server.CheckServerFiles = true;
                    }
                }
            }
            else
            {
                isNewOrUpdate = true;
                ksAnalytics.Get().TrackEvent(ksAnalytics.Events.INSTALL);
            }
            ksReactorConfig.Instance.Build.LastBuildVersion = ksReactorConfig.Instance.FullVersion;
            EditorUtility.SetDirty(ksReactorConfig.Instance);
            AssetDatabase.SaveAssets();
            return isNewOrUpdate;
        }

        /// <summary>
        /// Create a <see cref="ksReactorConfig"/> asset if one does not already exist.
        /// </summary>
        /// <returns>Returns true if a new Reactor config was created.</returns>
        public static bool CreateReactorConfig()
        {
            if (ksReactorConfig.Instance == null)
            {
                ksLog.Info(LOG_CHANNEL, "Creating Reactor Config");
                ksPathUtils.Create(ksPaths.Config);
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<ksReactorConfig>(), ksPaths.Config);
                return true;
            }
            return false;
        }

        /// <summary>Displays a popup project update message.</summary>
        /// <param name="details">Added to the displayed message.</param>
        /// <param name="error">Was the project updated successfully?</param>
        private static void DisplayUpgradeMessage(string details, bool error = false)
        {
            string title = "Reactor Project Update";
            if (error)
            {
                title += " Error";
                details = "Could not update your project to version " + ksVersion.Current + ".\n" + details +
                    "\nContinue anyway?";
                if (!EditorUtility.DisplayDialog(title, details, "OK", "Quit"))
                {
                    EditorApplication.Exit(0);
                }
            }
            else
            {
                details = "Your project was automatically updated to version " + ksVersion.Current + ".\n" + details;
                EditorUtility.DisplayDialog(title, details, "OK");
            }
        }

        /// <summary>
        /// Track the unity user who is using the plugin
        /// </summary>
        private static void TrackUserId()
        {
            string uid = ksEditorUtils.GetUnityUserId();
            string rid = ksEditorUtils.GetReleaseId();
            if (!string.IsNullOrEmpty(uid) && uid != "anonymous" && !string.IsNullOrEmpty(rid))
            {
                EditorApplication.update -= TrackUserId;
                string url = $"{ksReactorConfig.Instance.Urls.WebConsole}/unauth/api/v1/trackUserId?uid=unity-{uid}&rid={rid}";
                UnityWebRequest.Get(url).SendWebRequest();
            }
        }
    }
}
