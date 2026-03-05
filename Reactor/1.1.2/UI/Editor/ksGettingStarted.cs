using KS.Unity.Editor;
using UnityEditor;
using UnityEngine;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>
    /// Popup screen with Reactor Multiplayer getting started instructions.  This screen will appear
    /// when opening unity and the last loaded version and the current version are different
    /// or when the show getting started config value is true.
    /// </summary>
    public class ksGettingStarted: ksSingleton<ksGettingStarted>, ksIMenu
    {
        public const string SESSION_STATE = "KS.Reactor.ShowGettingStarted";

        private Vector2 m_scrollPosition;
        private SerializedObject m_config = null;
        private SerializedProperty m_showGettingStarted = null;

        /// <summary>Icon</summary>
        public Texture Icon
        {
            get { return ksTextures.Logo; }
        }

        /// <summary>Destroy this menu on close.</summary>
        public bool DestroyOnClose
        {
            get { return true; }
        }

        /// <summary>Opens the getting started window.</summary>
        public void Open()
        {
            ksWindow.Open(
                ksWindow.REACTOR_GETTING_STARTED,
                delegate (ksWindow window)
                {
                    window.titleContent = new GUIContent("Reactor Mulitplayer - Getting Started", ksTextures.Logo);
                    window.minSize = new Vector2(400f, 500f);
                    window.Menu = this;
                },
                ksWindow.WindowStyle.UTILITY
            );
        }

        /// <summary>Called when the menu is opened.</summary>
        /// <param name="window">window that opened the menu.</param>
        public void OnOpen(ksWindow window)
        {
        }

        /// <summary>Called when the menu is closed. Does nothing.</summary>
        /// <param name="window">window that closed the menu.</param>
        public void OnClose(ksWindow window)
        {

        }

        /// <summary>Draw the window content</summary>
        /// <param name="window">window the GUI is for.</param>
        public void Draw(ksWindow window)
        {
            if (m_config == null)
            {
                m_config = new SerializedObject(ksReactorConfig.Instance);
                m_showGettingStarted = m_config.FindProperty("ShowGettingStartedScreen");
            }

            GUIStyle title = new GUIStyle(EditorStyles.label);
            title.fontSize = 20;
            title.alignment = TextAnchor.MiddleCenter;

            GUIStyle heading = new GUIStyle(EditorStyles.label);
            heading.fontSize = 14;
            heading.padding = new RectOffset(10, 0, 0, -10);

            GUIStyle paragraph = new GUIStyle(EditorStyles.label);
            paragraph.wordWrap = true;
            paragraph.stretchWidth = true;
            paragraph.richText = true;
            paragraph.padding = new RectOffset(20, 0, 0, 0);

            // Reactor Multiplayer Logo
            Rect rect = EditorGUILayout.GetControlRect(false, 75.0f);
            GUI.DrawTexture(rect, ksTextures.FullLogo, ScaleMode.ScaleToFit, true, 0);

            m_scrollPosition = EditorGUILayout.BeginScrollView(m_scrollPosition);

            // Signup
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Reactor Accounts", heading);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("• An account with a Reactor subscription is needed to launch cloud servers.", paragraph);
            EditorGUILayout.LabelField("• Only one member of a team needs to have a subscription.", paragraph);
            EditorGUILayout.LabelField("• The subscription account can invite other users to their Reactor projects.", paragraph);
            EditorGUILayout.Space();
            EditorGUI.indentLevel++;
            ksStyle.Link("Create a Reactor Account", ksReactorConfig.Instance.Urls.WebConsole + "/ksauthentication/login?register=1");
            EditorGUI.indentLevel--;

            // Links
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Links", heading);
            EditorGUILayout.Space();
            EditorGUI.indentLevel++;
            ksStyle.Link("Tutorials and Documentation", ksReactorConfig.Instance.Urls.Documentation);
            ksStyle.Link("Web Console", ksReactorConfig.Instance.Urls.WebConsole);
            ksStyle.Link("Discord Support", ksReactorConfig.Instance.Urls.Discord);
            ksStyle.Link("Youtube Tutorials", ksReactorConfig.Instance.Urls.Youtube);
            EditorGUI.indentLevel--;

            // Notes
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tips", heading);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("• The Reactor menu has actions for building, publishing, and managing different aspects of your Reactor project. ", paragraph);
            EditorGUILayout.LabelField("• Most context menus have a Reactor category which contains Reactor actions for creating scripts and assets.", paragraph);
            EditorGUILayout.LabelField("• Reactor scripts created by context menus and other build processes are placed in <b>/Assets/ReactorScripts</b>.", paragraph);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(" <b>/Assets/ReactorScripts/Common</b> contains scripts common to the Unity client and the Reactor server runtime.", paragraph);
            EditorGUILayout.LabelField(" <b>/Assets/ReactorScripts/Client</b> contains client scripts generated by Reactor menu actions.", paragraph);
            EditorGUILayout.LabelField(" <b>/Assets/ReactorScripts/Server</b> contains server scripts generated by Reactor menu actions.", paragraph);
            EditorGUILayout.LabelField(" <b>/Assets/ReactorScripts/Proxies</b> contains auto-generated proxy scripts of server scripts used for publishing.", paragraph);
            EditorGUI.indentLevel--;

            EditorGUILayout.EndScrollView();

            bool show = !EditorGUILayout.ToggleLeft("Do not show this screen on startup", !m_showGettingStarted.boolValue);
            if (show != m_showGettingStarted.boolValue)
            {
                m_showGettingStarted.boolValue = show;
                m_config.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}