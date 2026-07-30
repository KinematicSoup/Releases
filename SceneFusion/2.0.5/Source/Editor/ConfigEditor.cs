using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using KS.SF.Unity.Editor;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>Unity inspector editor for SceneFusionConfig assets</summary>
    [CustomEditor(typeof(sfConfig), false)]
    internal class ConfigEditor : UnityEditor.Editor
    {
        // Width of inspector labels.
        private const float LABEL_WIDTH = 180f;

        private static GUIStyle m_headingStyle;
        private static GUIStyle m_rtfLabelStyle;
        private static string m_expanded = null;

        private static readonly string LOG_CHANNEL = typeof(ConfigEditor).FullName;

        public static string ExpandedGroup
        {
            get { return m_expanded; }
            set { m_expanded = value; }
        }

        public delegate void ChangeHandler();
        public static event ChangeHandler OnChange;


        /// <summary>Draws the GUI.</summary>
        public override void OnInspectorGUI()
        {
            Draw(serializedObject);
        }

        /// <summary>Draw the UI</summary>
        /// <param name="serializedObject">serializedObject for the config.</param>
        public static void Draw(SerializedObject serializedObject)
        {
            serializedObject.Update();

            // Setup gui styles
            if (m_headingStyle == null)
            {
                m_headingStyle = new GUIStyle(EditorStyles.foldout);
                m_headingStyle.fontStyle = FontStyle.Bold;
                m_rtfLabelStyle = new GUIStyle(EditorStyles.label);
                m_rtfLabelStyle.richText = true;
            }

            EditorGUIUtility.labelWidth = LABEL_WIDTH;

            sfConfig config = (sfConfig)serializedObject.targetObject;
            // Version is editable in non-published builds
#if KS_DEVELOPMENT
            SerializedProperty version = serializedObject.FindProperty("m_version");
            EditorGUILayout.PropertyField(version);

            version = serializedObject.FindProperty("m_serverVersion");
            EditorGUILayout.PropertyField(version);

            SerializedProperty debug = serializedObject.FindProperty("Debug");
            if (debug != null)
            {
                debug.boolValue = EditorGUILayout.Toggle("Debug", debug.boolValue);
            }
#else
            EditorGUILayout.LabelField("<b>Scene Fusion Version: </b> " + config.FullVersion, m_rtfLabelStyle);
#endif
#if U2U
            SerializedProperty unrealVersion = serializedObject.FindProperty("UnrealVersion");
            unrealVersion.stringValue = EditorGUILayout.TextField("Unreal Version", unrealVersion.stringValue);
#endif
            SerializedProperty showSplashScreen = serializedObject.FindProperty("ShowGettingStartedScreen");
            showSplashScreen.boolValue = EditorGUILayout.Toggle("Show Getting Started", showSplashScreen.boolValue);

            EditorGUI.BeginDisabledGroup(sfConfig.Get().SessionSettingsLocked);
            SerializedProperty syncMaterials = serializedObject.FindProperty("m_syncMaterials");
            EditorGUILayout.PropertyField(syncMaterials);

            SerializedProperty syncPrefabs = serializedObject.FindProperty("m_syncPrefabs");
            EditorGUILayout.PropertyField(syncPrefabs);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space();

            DrawGroup(serializedObject, "Urls");
            DrawGroup(serializedObject, "UI");
            DrawGroup(serializedObject, "Network");
            DrawGroup(serializedObject, "Performance");
            DrawGroup(serializedObject, "Logging", ()=>
            {
                EditorGUILayout.Space();
                if (ksStyle.Button("Open Log Directory"))
                {
                    Application.OpenURL(Path.Combine(sfPaths.ProjectRoot, sfPaths.ExternalLogs));
                }
            });

            if (serializedObject.ApplyModifiedProperties() && OnChange != null)
            {
                OnChange();
            }
        }

        /// <summary>Gets the display name for a property.</summary>
        /// <param name="property">property to get display name for.</param>
        /// <returns>display name for the property.</returns>
        public static string GetDisplayName(SerializedProperty property)
        {
            switch (property.name)
            {
                case "Urls": return "URLs";
                case "UI": return "UI Settings";
                case "Network": return "Network Settings";
                case "Performance": return "Performance Settings";
                case "Logging": return "Log Settings";
            }
            return property.displayName;
        }

        /// <summary>Handle expansion of the selected group</summary>
        /// <param name="propertyName">propertyName for the group.</param>
        /// <param name="title">title for the group.</param>
        private static bool ExpandGroup(string propertyName, string title)
        {
            if (EditorGUILayout.Foldout(m_expanded == propertyName, title, true, m_headingStyle))
            {
                m_expanded = propertyName;
                return true;
            }
            else if (m_expanded == propertyName)
            {
                m_expanded = null;
            }
            return false;
        }

        /// <summary>Draw the child properties of a parent under an expandable group GUI</summary>
        /// <param name="serializedObject">serializedObject for the config.</param>
        /// <param name="property">property name</param>
        /// <param name="drawCallback">
        /// delegate called after drawing the child properties. Use this to draw a custom
        /// GUI below the properties when the foldout is expanded.
        /// </param>
        private static void DrawGroup(
            SerializedObject serializedObject,
            string propertyName,
            Action drawCallback = null)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            if (ExpandGroup(propertyName, GetDisplayName(property)))
            {
                int baseIndent = EditorGUI.indentLevel;
                bool enterChildren = true;
                while (property.NextVisible(enterChildren) && property.depth > 0)
                {
                    EditorGUI.indentLevel = baseIndent + property.depth;
                    GUIContent content = new GUIContent(GetDisplayName(property), property.tooltip);
                    EditorGUILayout.PropertyField(property, content, false);
                    enterChildren = false;
                }
                if (drawCallback != null)
                {
                    drawCallback();
                }
                EditorGUI.indentLevel = baseIndent;
            }
            EditorGUILayout.Space();
        }
    }
}
