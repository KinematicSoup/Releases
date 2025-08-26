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
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using KS.Unity.Editor;
using System.Collections.Generic;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Inspector editor for <see cref="ksReactorConfig"/>.</summary>
    [CustomEditor(typeof(ksReactorConfig), false)]
    public class ksReactorConfigEditor : UnityEditor.Editor, IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private HashSet<string> m_hideFields = new HashSet<string>();

        private GUIStyle m_headingStyle;
        private GUIStyle m_rtfLabelStyle;
        private string m_expanded = null;
        private string m_changedPropertyName = null;

        public ksReactorConfigEditor()
        {
            // The Mono/Dotnet path are only required on non Microsoft OS where explicit calls to mono or dotnet
            // are required to run c# executables.
            if (ksPaths.OperatingSystem == "Microsoft")
            {
                m_hideFields.Add("Server.MonoPath");
            }
        }

        /// <summary>Called by Unity to draw/update the inspector UI.</summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Setup gui styles
            m_headingStyle = new GUIStyle(EditorStyles.foldout);
            m_headingStyle.fontStyle = FontStyle.Bold;
            m_rtfLabelStyle = new GUIStyle(EditorStyles.label);
            m_rtfLabelStyle.richText = true;

            // Version is editable in development builds
#if KS_DEVELOPMENT
            SerializedProperty version = serializedObject.FindProperty("Version");
            version.stringValue = EditorGUILayout.TextField("Reactor Version", version.stringValue);

            SerializedProperty clientVersion = serializedObject.FindProperty("ClientVersion");
            clientVersion.stringValue = EditorGUILayout.TextField("Client Version", clientVersion.stringValue);
#else

            EditorGUILayout.LabelField("<b>Reactor Version: </b> " + 
                (target as ksReactorConfig).Version + "-" + (target as ksReactorConfig).ClientVersion, m_rtfLabelStyle);
#endif
            SerializedProperty runInBackgroundProp = serializedObject.FindProperty(
                nameof(ksReactorConfig.AutoRunInBackground));
            EditorGUILayout.PropertyField(runInBackgroundProp);
            EditorGUILayout.Space();

            DrawGroup("Urls", "URLs");
            DrawGroup("Server", "Server", true);
            DrawGroup("Build", "Build");
            DrawGroup("Debug", "Debug", true);
            DrawCollisionGroups();

            serializedObject.ApplyModifiedProperties();

            // Call the property change event if a property changed and it no longer has focus.
            if (!string.IsNullOrEmpty(m_changedPropertyName) && GUI.GetNameOfFocusedControl() != m_changedPropertyName)
            {
                OnPropertyChange();
                m_changedPropertyName = null;
            }
        }

        /// <summary>
        /// Called by Unity when the script is disabled. Check for changed properties.
        /// </summary>
        private void OnDisable()
        {
            if (!string.IsNullOrEmpty(m_changedPropertyName))
            {
                OnPropertyChange();
            }
        }

        /// <summary>
        /// Called when a property changes. Checks to see if the Reactor server needs to be rebuilt or
        /// if debug settings need to be applied to rooms, players, and entities.
        /// </summary>
        private void OnPropertyChange()
        {
            switch (m_changedPropertyName)
            {
                case "DefineSymbols":
                {
                    ksServerScriptCompiler.Instance.BuildServerRuntime();
                    break;
                }
                case "ServerPath":
                {
                    ksServerProjectUpdater.Instance.UpdateOutputPath();
                    ksServerScriptCompiler.Instance.BuildServerRuntime();
                    break;
                }
                case "MonoPath":
                {
                    ksServerProjectUpdater.Instance.UpdateOutputPath();
                    ksServerScriptCompiler.Instance.BuildServerRuntime();
                    break;
                }
            }
        }

        /// <summary>Handle expansion of configuration groups.</summary>
        /// <param name="title">Group title</param>
        /// <returns>True if the group is expanded.</returns>
        private bool ExpandGroup(string title)
        {
            if (EditorGUILayout.Foldout(m_expanded == title, title, true, m_headingStyle))
            {
                m_expanded = title;
                return true;
            }
            else if (m_expanded == title)
            {
                m_expanded = null;
            }
            return false;
        }

        /// <summary>
        /// Draw the child properties of a parent property under an expandable group UI.
        /// </summary>
        /// <param name="propertyName">Property name</param>
        /// <param name="groupTitle">Group title</param>
        /// <param name="checkChanges">Check for property changes.</param>
        private void DrawGroup(string propertyName, string groupTitle, bool checkChanges = false)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            if (ExpandGroup(groupTitle))
            {
                int baseIndent = EditorGUI.indentLevel;
                while (property.NextVisible(true) && property.depth > 0)
                {
                    if (m_hideFields.Contains(propertyName + "." + property.name))
                    {
                        continue;
                    }
                    EditorGUI.indentLevel = baseIndent + property.depth;
                    checkChanges &= !serializedObject.hasModifiedProperties;
                    if (checkChanges)
                    {
                        GUI.SetNextControlName(property.name);
                    }
                    EditorGUILayout.PropertyField(property, true);
                    if (checkChanges && serializedObject.hasModifiedProperties)
                    {
                        m_changedPropertyName = property.name;
                    }
                }
                EditorGUI.indentLevel = baseIndent;
            }
            EditorGUILayout.Space();
        }

        /// <summary>Draw a GUI for the collision groups property.</summary>
        private void DrawCollisionGroups()
        {
            SerializedProperty property = serializedObject.FindProperty("CollisionGroups");
            if (property == null)
            {
                return;
            }

            int baseIndent = EditorGUI.indentLevel;
            if (ExpandGroup("Collision Groups"))
            {
                EditorGUI.indentLevel = baseIndent + 1;
                for (int i = 0; i < property.arraySize; i++)
                {
                    SerializedProperty element = property.GetArrayElementAtIndex(i);
                    EditorGUILayout.PropertyField(element, new GUIContent(i.ToString()));
                }
                EditorGUI.indentLevel = baseIndent;
            }
            EditorGUILayout.Space();
        }


        /// <summary>Post/Pre build callback order.</summary>
        public int callbackOrder {
            get { return 0; }
        }

        private string m_tempAPIServerSecret = null;

        /// <summary>Do not save the APIServerSecret when building the project.</summary>
        /// <param name="report">Unity build report.</param>
        public void OnPreprocessBuild(BuildReport report)
        {
            m_tempAPIServerSecret = ksReactorConfig.Instance.Build.APIServerSecret;
            ksReactorConfig.Instance.Build.APIServerSecret = null;
            EditorUtility.SetDirty(ksReactorConfig.Instance);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Restore the APIServerSecret after building completes.</summary>
        /// <param name="report">Unity build report.</param>
        public void OnPostprocessBuild(BuildReport report)
        {
            ksReactorConfig.Instance.Build.APIServerSecret = m_tempAPIServerSecret;
            m_tempAPIServerSecret = null;
            EditorUtility.SetDirty(ksReactorConfig.Instance);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Sets a server define symbol and optionally builds the server if the defines changed.
        /// </summary>
        /// <param name="symbol">Symbol to define</param>
        /// <param name="buildServer">If true, builds the server if the defines change.</param>
        /// <returns>True if the symbol was added. False if it was already defined.</returns>
        public static bool SetServerDefineSymbol(string symbol, bool buildServer = true)
        {
            if (ksReactorConfig.Instance == null)
            {
                ksInitializer.CreateReactorConfig();
            }
            string defineSymbols = ksReactorConfig.Instance.Server.DefineSymbols;
            // replace commas, and spaces with semi-colons
            defineSymbols = defineSymbols.Replace(',', ';').Replace(' ', ';');
            string[] symbols = defineSymbols.Split(';');
            if (Array.IndexOf(symbols, symbol) == -1)
            {
                ksLog.Info(typeof(ksReactorConfigEditor).ToString(), "Setting server define symbol " + symbol);
                ksReactorConfig.Instance.Server.DefineSymbols = 
                    string.IsNullOrEmpty(defineSymbols) ? symbol : defineSymbols + ";" + symbol;
                EditorUtility.SetDirty(ksReactorConfig.Instance);
                if (buildServer)
                {
                    ksServerScriptCompiler.Instance.BuildServerRuntime();
                }
                return true;
            }
            return false;
        }

        /// <summary>Removes a server define symbol and optionally builds the server if the defines changed.</summary>
        /// <param name="symbol">Symbol to clear.</param>
        /// <param name="buildServer">If true, builds the server if the defines change.</param>
        /// <returns>True if the symbol was removed. False if it was not defined.</returns>
        public static bool ClearServerDefineSymbol(string symbol, bool buildServer = true)
        {
            if (ksReactorConfig.Instance == null)
            {
                ksInitializer.CreateReactorConfig();
            }
            string defineSymbols = ksReactorConfig.Instance.Server.DefineSymbols;
            // replace commas, and spaces with semi-colons
            defineSymbols = defineSymbols.Replace(',', ';').Replace(' ', ';');
            string[] symbols = defineSymbols.Split(';');
            int index = Array.IndexOf(symbols, symbol);
            if (index == -1)
            {
                return false;
            }
            string defines = "";
            for (int i = 0; i < symbols.Length; i++)
            {
                if (i != index)
                {
                    if (defines != "")
                    {
                        defines += ";";
                    }
                    defines += symbols[i];
                }
            }
            ksLog.Info(typeof(ksReactorConfigEditor).ToString(), "Clearing server define symbol " + symbol);
            ksReactorConfig.Instance.Server.DefineSymbols = defines;
            EditorUtility.SetDirty(ksReactorConfig.Instance);
            if (buildServer)
            {
                ksServerScriptCompiler.Instance.BuildServerRuntime();
            }
            return true;
        }
    }
}
