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

using UnityEditor;
using UnityEngine;
using KS.Unity.Editor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>A text box element with a button to open a file browser.</summary>
    public class ksPathBox : ksTextBox
    {
        /// <summary>Title of the file browser window.</summary>
        public string Title
        {
            get { return m_title; }
            set { m_title = value; }
        }
        private string m_title;

        /// <summary>
        /// Extension filter to apply to files. If the extension is a "/" then a folder selection dialog will be used.
        /// All other extensions patterns will be used to filter file selection dialogs.
        /// </summary>
        public string Extension
        {
            get { return m_extension; }
            set { m_extension = value; }
        }
        private string m_extension;

        private uint m_browseTimer = 0;

        /// <summary>Constructor</summary>
        /// <param name="window">Window this element will be drawn in.</param>
        /// <param name="label">Label for the element.</param>
        /// <param name="title">Title for the file browser window.</param>
        /// <param name="path">Initial text in the text box.</param>
        /// <param name="extension">Extension allowed in the file browser. "/" for folders. "" for all files.</param>
        public ksPathBox(EditorWindow window, string label = "", string title = "", string path = "", string extension = "")
            : base(window, label, path)
        {
            m_title = title;
            m_extension = extension;
        }

        /// <summary>Draws the text box.</summary>
        /// <returns>
        /// True if enter was pressed, if focus was lost and the text changed since focus was received, or if a path
        /// was selected in the file browser window.
        /// </returns>
        public override bool Draw()
        {
            GUILayout.BeginHorizontal();
            bool changed = base.Draw();
            if (m_browseTimer > 0)
            {
                m_browseTimer--;
                if (m_browseTimer == 0 && Browse())
                {
                    changed = true;
                }
            }
            if (ksStyle.BrowseButton())
            {
                Focus();
                // We have to wait this many frames before the focus change effects rendering
                m_browseTimer = 3;
            }
            GUILayout.EndHorizontal();
            if (changed)
            {
                Text = Text.Replace("\\", "/");
            }
            return changed;
        }

        /// <summary>Opens a file browser window, and blocks until the window is closed.</summary>
        /// <returns>True if a path was selected in the file browser window.</returns>
        private bool Browse()
        {
            string path;
            if (m_extension == "/")
            {
                path = EditorUtility.OpenFolderPanel(m_title, Text, "");
            }
            else
            {
                string folder = Text.Replace("\\", "/");
                int index = folder.LastIndexOf("/");
                if (index >= 0)
                {
                    folder = folder.Substring(0, index);
                }
                path = EditorUtility.OpenFilePanel(m_title, folder, m_extension);
            }
            if (path != "")
            {
                Text = path;
                GUI.FocusControl("");// remove focus from this element so text refreshes
                return true;
            }
            return false;
        }
    }
}
