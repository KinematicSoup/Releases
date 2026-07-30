/*
KINEMATICSOUP CONFIDENTIAL
 Copyright(c) 2014-2026 KinematicSoup Technologies Incorporated 
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
using UnityEditor;
using UnityEngine;
using KS.SF.Unity.Editor;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>
    /// Popup screen with Scene Fusion getting started instructions.  This screen will appear
    /// when opening unity and the last loaded version and the current version are different
    /// or when the show getting started config value is true.
    /// </summary>
    public partial class sfGettingStartedWindow : ksSingleton<sfGettingStartedWindow>, ksIMenu
    {
        /// <summary>Delegate to open the session window.</summary>
        public delegate void SessionWindowOpener();

        /// <summary>Callback to open the session window.</summary>
        public static SessionWindowOpener OpenSessionWindow;

        private Vector2 m_scrollPosition;
        private SerializedObject m_config = null;
        private SerializedProperty m_showGettingStarted = null;
        private GUIStyle m_title;
        private GUIStyle m_heading;
        private GUIStyle m_paragraph;

        private const float BUTTON_WIDTH = 200f;
        // Indent amount to line up open session window button and register link with text.
        private const float INDENT = 20f;

        /// <summary>Icon</summary>
        public Texture Icon
        {
            get { return sfTextures.Logo; }
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
                ksWindow.SCENE_FUSION_SPLASHSCREEN,
                delegate (ksWindow window)
                {
                    window.titleContent = new GUIContent(Product.NAME + " - Getting Started", sfTextures.Logo);
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

        /// <summary>Draw the feedback window content</summary>
        /// <param name="window">window the GUI is for.</param>
        public void Draw(ksWindow window)
        {
            if (m_config == null)
            {
                m_config = new SerializedObject(sfConfig.Get());
                m_showGettingStarted = m_config.FindProperty("ShowGettingStartedScreen");
            }

            if (m_title == null)
            {
                m_title = new GUIStyle(EditorStyles.label);
                m_title.fontSize = 20;
                m_title.alignment = TextAnchor.MiddleCenter;

                m_heading = new GUIStyle(EditorStyles.label);
                m_heading.fontSize = 14;
                m_heading.padding = new RectOffset(10, 0, 0, -10);

                m_paragraph = new GUIStyle(EditorStyles.label);
                m_paragraph.wordWrap = true;
                m_paragraph.stretchWidth = true;
                m_paragraph.richText = true;
                m_paragraph.padding = new RectOffset(20, 0, 0, 0);
            }

            // Scene Fusion Logo
            Rect rect = EditorGUILayout.GetControlRect(false, 75.0f);
            GUI.DrawTexture(rect, sfTextures.LogoWithName, ScaleMode.ScaleToFit, true, 0);

            m_scrollPosition = EditorGUILayout.BeginScrollView(m_scrollPosition);

            DrawSignupInstructions();

            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Editing Sessions", m_heading);
            EditorGUILayout.Space();

            DrawEditingInstructions();
            
            EditorGUILayout.Space();
            rect = EditorGUILayout.GetControlRect(GUILayout.MaxWidth(BUTTON_WIDTH));
            rect.x += INDENT;
            if (OpenSessionWindow != null && GUI.Button(rect, "Open Session Window"))
            {
                OpenSessionWindow();
            }

            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Notes", m_heading);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("- Selecting a game object will lock it and prevent other users from editing it.", m_paragraph);
            EditorGUILayout.LabelField("- Locked game objects will appear with a colored overlay in the Scene View.", m_paragraph);
            EditorGUILayout.LabelField("- Synced game objects will have an icon next to them in the Hierarchy window.", m_paragraph);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("- Synced objects that are locked by other users will have a lock icon.", m_paragraph);
            EditorGUILayout.LabelField("- Synced objects with notifications will have a yellow warning icon. Select the object to see the notifications in the inspector, or open the notification window by going to <b>Window > Scene Fusion > Notifications</b> to view all notifications.", m_paragraph);
            EditorGUILayout.LabelField("- Synced objects that are unlocked and have no notifications will have a green checkmark.", m_paragraph);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            ksStyle.Link("- Documentation and Troubleshooting", sfConfig.Get().Urls.Documentation);
            DrawWebConsoleLink();
            ksStyle.Link("- Youtube", sfConfig.Get().Urls.Youtube);
            ksStyle.Link("- Discord", sfConfig.Get().Urls.Discord);
            ksStyle.Link("- Email Support", "mailto:" + sfConfig.Get().Urls.SupportEmail);
            EditorGUILayout.EndScrollView();

            bool show = !EditorGUILayout.ToggleLeft("Do not show this screen on startup", !m_showGettingStarted.boolValue);
            if (show != m_showGettingStarted.boolValue)
            {
                m_showGettingStarted.boolValue = show;
                m_config.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        partial void DrawSignupInstructions();

        partial void DrawEditingInstructions();

        partial void DrawWebConsoleLink();
    }
}
