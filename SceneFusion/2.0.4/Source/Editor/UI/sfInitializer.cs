using System.IO;
using UnityEngine;
using UnityEditor;
using KS.SceneFusion;
using KS.SF.Unity.Editor;
using KS.SceneFusionCommon;
using KS.SF.Reactor.Client;
using UnityEngine.Networking;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>Performs initialization logic when the editor loads.</summary>
    [InitializeOnLoad]
    class sfInitializer
    {
        /// <summary>Menu group name for Scene Fusion windows.</summary>
        public const string WINDOWS = "Window/" + Product.NAME + "/";

        /// <summary>Menu group name for other Scene Fusion menu options.</summary>
        public const string TOOLS = "Tools/" + Product.NAME + "/";

        /// <summary>Menu priority for Scene Fusion windows.</summary>
        public const int WINDOWS_PRIORITY = 4000;

        /// <summary>Menu priority for Scene Fusion windows.</summary>
        public const int TOOLS_PRIORITY = 4000;

        /// <summary>Static constructor</summary>
        static sfInitializer()
        {
            EditorApplication.update += Init;
        }

        /// <summary>Performs initialization logic that must wait until after Unity derserialization finishes.</summary>
        private static void Init()
        {
            EditorApplication.update -= Init;
            ksEditorUtils.SetDefineSymbol("SCENE_FUSION_2");
            EditorApplication.update += TrackUserId;
            SceneFusionService.Set(SceneFusion.Get().Service);
            sfGettingStartedWindow.OpenSessionWindow = OpenSessionWindow;

            if (sfConfig.Get().Version.ToString() != sfConfig.Get().LastVersion)
            {
                if (sfConfig.Get().LastVersion == "0.0.0")
                {
                    sfAnalytics.Get().TrackEvent(sfAnalytics.Events.INSTALL);
                }
                else
                {
                    sfAnalytics.Get().TrackEvent(sfAnalytics.Events.UPGRADE);
                }
                SerializedObject config = new SerializedObject(sfConfig.Get());
                SerializedProperty lastVersion = config.FindProperty("LastVersion");
                lastVersion.stringValue = sfConfig.Get().Version.ToString();
                sfPropertyUtils.ApplyProperties(config);
            }


            ksWindow window = ksWindow.Find(ksWindow.SCENE_FUSION_MAIN);
            if (window != null && window.Menu == null)
            {
                window.Menu = ScriptableObject.CreateInstance<sfSessionsMenu>();
            }

            sfPackageUpdater.Get().CheckForUpdates();
        }

        /// <summary>Opens the sessions menu.</summary>
        [MenuItem(WINDOWS + "Session", priority = WINDOWS_PRIORITY)]
        private static void OpenSessionWindow()
        {
            ksWindow.Open(ksWindow.SCENE_FUSION_MAIN, delegate (ksWindow window)
            {
                window.titleContent = new GUIContent(" Session", KS.SceneFusion.sfTextures.Logo);
                window.minSize = new Vector2(380f, 100f);
                window.Menu = ScriptableObject.CreateInstance<sfSessionsMenu>();
            });
        }

        /// <summary>Opens the notifications window.</summary>
        [MenuItem(WINDOWS + "Notifications", priority = WINDOWS_PRIORITY + 1)]
        private static void OpenNotifications()
        {
            sfNotificationWindow.Open();
        }

        /// <summary>Opens the getting started window.</summary>
        [MenuItem(WINDOWS + "Getting Started", priority = WINDOWS_PRIORITY + 2)]
        private static void OpenGettingStarted()
        {
            sfGettingStartedWindow.Get().Open();
        }

        /// <summary>Opens the Scene Fusion project settings.</summary>
        [MenuItem(WINDOWS + "Settings", priority = WINDOWS_PRIORITY + 3)]
        public static void OpenSettings()
        {
            SettingsService.OpenProjectSettings("Project/" + Product.NAME);
        }

        /// <summary>Checks for updates to the Scene Fusion package and prompts the uesr to apply the update.</summary>
        [MenuItem(TOOLS + "Check for Updates", priority = TOOLS_PRIORITY)]
        public static void CheckForUpdates()
        {
            sfPackageUpdater.Get().CheckForUpdates(null, true);
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
                string url = $"{sfConfig.Get().Urls.WebConsole}/unauth/api/v1/trackUserId?uid=unity-{uid}&rid={rid}";
                UnityWebRequest.Get(url).SendWebRequest();
            }
        }
    }
}