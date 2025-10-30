using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using KS.SceneFusion2.Client;
using KS.SF.Unity.Editor;
using KS.SceneFusion;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>SF2 UI for the online menu. Shows notification and object counts.</summary>
    [Serializable]
    public class sfOnlineMenuUI
    {
        // sfConfig UI settings to hide from the settings section.
        private static readonly string[] HIDDEN_SETTINGS = new string[] { "m_hierarchyIconOffset", 
            "m_projectBrowserIconOffset" };

        private bool m_infoExpanded = true;
        private bool m_settingsExpanded = true;
        [NonSerialized]
        private int m_notificationCount = 0;
        [NonSerialized]
        private uint m_gameObjectCount = 0;
        [NonSerialized]
        private uint m_gameObjectLimit = 0;

        /// <summary>Draws notification and object counts, and settings.</summary>
        public void Draw()
        {
            sfSession session = SceneFusion.Get().Service.Session;
            if (Event.current.type == EventType.Layout)
            {
                m_notificationCount = sfNotificationManager.Get().Count;
                m_gameObjectCount = session == null ? 0 : session.GetObjectCount(sfType.GameObject);
                m_gameObjectLimit = session == null ? uint.MaxValue : session.GetObjectLimit(sfType.GameObject);
            }

            if (m_notificationCount > 0)
            {
                string message = m_notificationCount == 1 ?
                    "1 notification" : (m_notificationCount + " notifications");
                if (ksStyle.HelpBox(MessageType.Warning, message, "") != -1)
                {
                    sfNotificationWindow.Open();
                }
            }

            if (m_gameObjectCount >= m_gameObjectLimit)
            {
                ksStyle.HelpBox(MessageType.Warning, "You cannot create more game objects because you reached the " +
                    m_gameObjectLimit + " game object limit. ", null, "Click here to upgrade.",
                    sfConfig.Get().Urls.Upgrade);
            }

            m_infoExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(m_infoExpanded, "Info");
            if (m_infoExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Synced Game Objects", m_gameObjectCount + 
                    (m_gameObjectLimit != uint.MaxValue ? " / " + m_gameObjectLimit : ""));
                GUIContent label = new GUIContent("Synced Objects", "The number of synced sfObjects");
                GUIContent content = new GUIContent(session == null ? "0" : session.NumObjects.ToString());
                EditorGUILayout.LabelField(label, content);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            m_settingsExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(m_settingsExpanded, "Settings");
            if (m_settingsExpanded)
            {
                EditorGUI.indentLevel++;
                DrawSettings();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        /// <summary>Draws UI settings.</summary>
        private void DrawSettings()
        {
            SerializedObject so = new SerializedObject(sfConfig.Get());
            SerializedProperty sprop = so.FindProperty("UI");
            if (sprop == null)
            {
                return;
            }
            int depth = sprop.depth;
            bool enterChildren = true;
            while (sprop.NextVisible(enterChildren) && sprop.depth > depth)
            {
                enterChildren = false;
                if (!HIDDEN_SETTINGS.Contains(sprop.name))
                {
                    EditorGUILayout.PropertyField(sprop);
                }
            }
            so.ApplyModifiedProperties();
        }
    }
}
