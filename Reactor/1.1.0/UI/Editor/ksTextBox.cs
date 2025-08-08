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
    /// <summary>
    /// A text box element that detects enter key events and checks for text changes when it loses focus.
    /// </summary>
    public class ksTextBox
    {
        /// <summary>Label</summary>
        public string Label
        {
            get { return m_label; }
            set { m_label = value; }
        }
        private string m_label;

        /// <summary>Text in the text box.</summary>
        public string Text
        {
            get { return m_text; }
            set
            {
                m_text = value;
                m_lastText = value;
            }
        }
        private string m_text;

        /// <summary>True if this text box has focus.</summary>
        public bool Focused
        {
            get { return m_focused; }
        }
        private bool m_focused = false;

        private string m_lastText;
        private string m_name;
        private EditorWindow m_window;

        private static uint m_nextId = 0;

        /// <summary>Constructor</summary>
        /// <param name="window">Window this element will be drawn in.</param>
        /// <param name="label">Label for the element.</param>
        /// <param name="text">Initial text in the text box.</param>
        public ksTextBox(EditorWindow window, string label = "", string text = "")
        {
            m_window = window;
            m_label = label;
            m_text = text;
            m_lastText = text;
            m_name = "TextBox" + m_nextId;
            m_nextId++;
        }

        /// <summary>Draws the text box.</summary>
        /// <returns>
        /// True if enter was pressed or if focus was lost and the text changed since focus was received.
        /// </returns>
        public virtual bool Draw()
        {
            GUI.SetNextControlName(m_name);
            m_text = EditorGUILayout.TextField(m_label, m_text, GUILayout.MinHeight(ksStyle.LINE_HEIGHT));

            bool wasFocused = m_focused;
            m_focused = GUI.GetNameOfFocusedControl() == m_name;
            if (m_focused && EditorWindow.focusedWindow != m_window)
            {
                GUI.FocusControl("");// remove focus from this element so text refreshes
                m_focused = false;
            }
            if ((wasFocused && !m_focused && m_text != m_lastText) || 
                (m_focused && Event.current.type == EventType.KeyUp && Event.current.keyCode == KeyCode.Return))
            {
                m_lastText = m_text;
                return true;
            }
            return false;
        }

        /// <summary>Focuses this text box.</summary>
        public void Focus()
        {
            GUI.FocusControl(m_name);
        }
    }
}
