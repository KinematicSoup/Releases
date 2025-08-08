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

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using KS.Unity.Editor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Editor window for creating scripts from a template.</summary>
    public class ksNewScriptMenu : EditorWindow
    {
        private static ksNewScriptMenu m_instance;

        [SerializeField]
        private string m_className;

        [SerializeField]
        private string m_namespace = "";

        [SerializeField]
        private bool[] m_optionalMethods;

        [SerializeField]
        private string m_path;

        [SerializeField]
        private bool m_attach = false;

        [SerializeField]
        private bool m_focus = false;

        [SerializeField]
        private ScriptableObject m_templateObject;

        // Alphanumeric & underscore not starting with a digit
        private Regex m_validRegex = new Regex("^[a-zA-Z_][a-zA-Z0-9_]*$");
        private ksIScriptTemplate m_template;
        private ksScriptGenerator m_generator;
        
        /// <summary>Template to generate the script from.</summary>
        public ksIScriptTemplate Template
        {
            get { return m_template; }

            set 
            {
                if (m_templateObject != null)
                {
                    DestroyImmediate(m_templateObject);
                }
                m_template = value;
                m_template.Generator = m_generator;
                m_templateObject = m_template as ScriptableObject;
                m_className = m_template.DefaultFileName;
                m_focus = true;
            }
        }

        /// <summary>
        /// Path to generate the script in (for non-attached scripts only).
        /// </summary>
        public string Path
        {
            get { return m_path; }
            set { m_path = value; }
        }

        /// <summary>Will the scripts be attached to one or more game objects once generated?</summary>
        public bool Attach
        {
            get { return m_attach; }
            set { m_attach = value; }
        }

        /// <summary>
        /// Initializes and opens the new script menu window. If a window is already open it will be focused and re-initialized.
        /// </summary>
        /// <param name="template">Template to generate the script from.</param>
        /// <param name="attach">Will the scripts be attached to one or more game objects once they are generated?</param>
        /// <param name="path">Path to generate the script in (for non-attached scripts only).</param>
        public static void Open(ksIScriptTemplate template, bool attach = true, string path = null)
        {
            if (Application.isPlaying)
            {
                return;
            }
            if (m_instance == null)
            {
                m_instance = ScriptableObject.CreateInstance<ksNewScriptMenu>();
            }
            m_instance.Template = template;
            m_instance.Attach = attach;
            m_instance.Path = path;
            m_instance.ShowUtility();
            m_instance.Focus();
        }


        /// <summary>Unity method called after restart.</summary>
        public void OnEnable()
        {
            m_generator = ksScriptGenerator.Get();
            m_template = m_templateObject as ksIScriptTemplate;
            if (m_template != null)
            {
                m_template.Generator = m_generator;
            }
        }

        /// <summary>Unity method called when the window is closed.</summary>
        public void OnDestroy()
        {
            if (m_templateObject != null)
            {
                DestroyImmediate(m_templateObject);
            }
        }

        /// <summary>Unity method called in play mode. Closes this window.</summary>
        void Update()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Close();
            }
        }

        /// <summary>Unity method to create the GUI.</summary>
        void OnGUI()
        {
            // Check the event type before the TextFields change it to Used.
            bool enter = Event.current.keyCode == KeyCode.Return && Event.current.type == EventType.KeyDown;

            m_namespace = EditorGUILayout.TextField("Namespace", m_namespace);

            GUI.SetNextControlName("ClassName");
            m_className = EditorGUILayout.TextField("Name", m_className);

            if (m_template.OptionalMethods != null && m_template.OptionalMethods.Length > 0)
            {
                EditorGUILayout.LabelField("Generate Code");
                EditorGUI.indentLevel++;
                if (m_optionalMethods == null || m_optionalMethods.Length != m_template.OptionalMethods.Length)
                {
                    m_optionalMethods = new bool[m_template.OptionalMethods.Length];
                    for (int i = 0; i < m_optionalMethods.Length; i++)
                    {
                        m_optionalMethods[i] = m_template.OptionalMethods[i].DefaultIncluded;
                    }
                }
                for (int i = 0; i < m_optionalMethods.Length; i++)
                {
                    ksOptionalMethod method = m_template.OptionalMethods[i];
                    m_optionalMethods[i] = EditorGUILayout.Toggle(new GUIContent(method.Name, method.Tooltip),
                        m_optionalMethods[i]);
                }
                EditorGUI.indentLevel--;
            }

            if (m_focus)
            {
                m_focus = false;
                EditorGUI.FocusTextInControl("ClassName");
            }

            EditorGUILayout.Space();

            string label = m_attach ? "Create and Add" : "Create";
            bool create = ksStyle.Button(label) || (enter && Event.current.type == EventType.Used);
            if (ksStyle.Button("Cancel"))
            {
                Close();
            }

            if (create)
            {
                string path = m_attach ? m_template.DefaultPath : m_path;
                path += m_className + ".cs";
                // Create the set of included optional method ids.
                HashSet<uint> optionalMethodSet = new HashSet<uint>();
                if (m_optionalMethods != null)
                {
                    for (int i = 0; i < m_optionalMethods.Length; i++)
                    {
                        if (m_optionalMethods[i])
                        {
                            optionalMethodSet.Add(m_template.OptionalMethods[i].Id);
                        }
                    }
                }
                // Validate may bring up a dialog the interrupts the GUI thread so we can't make any calls
                // that require input after calling Validate or we'll get an error.
                if (Validate(path) && m_generator.Write(path, m_template.Generate(path, m_className, m_namespace,
                    optionalMethodSet)))
                {
                    m_template.HandleCreate(path, m_className, m_namespace);
                    Close();
                }
            }
        }

        /// <summary>
        /// Validates the path, namespace and class name for the new script. Opens an error dialog if
        /// something is invalid.
        /// </summary>
        /// <param name="path">Path the script will be written to.</param>
        /// <returns>True if everything is valid.</returns>
        private bool Validate(string path)
        {
            string errorMsg = "";
            if (!ValidateNamespace(m_namespace))
            {
                errorMsg = "Namespace may only contain letters, digits, and underscores, " +
                    "and cannot start with a digit.";
            }
            if (m_className.Length <= 0)
            {
                if (errorMsg != "")
                {
                    errorMsg += "\n";
                }
                errorMsg += "Please enter a class name.";
            }
            else if (!m_validRegex.IsMatch(m_className))
            {
                if (errorMsg != "")
                {
                    errorMsg += "\n";
                }
                errorMsg += "Class name may only contain letters, digits, and underscores, " +
                    "and cannot start with a digit.";
            }
            if (errorMsg != "")
            {
                EditorUtility.DisplayDialog("Invalid Name", errorMsg, "OK");
                return false;
            }
            if (File.Exists(path))
            {
                return EditorUtility.DisplayDialog("File Already Exists", "The file " + path +
                    " already exists. Do you want to overwrite it?", "Yes", "No");
            }
            return true;
        }


        /// <summary>Validates a namespace.</summary>
        /// <param name="name">Namespace to validate.</param>
        /// <returns>True if the namespace is valid.</returns>
        private bool ValidateNamespace(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return true;
            }
            string[] parts = name.Split('.');
            foreach (string part in parts)
            {
                if (!m_validRegex.IsMatch(part))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
