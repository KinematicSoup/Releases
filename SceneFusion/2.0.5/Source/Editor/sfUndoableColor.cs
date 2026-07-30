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
using UnityEngine;
using UnityEditor;
using System.Reflection;
using KS.SF.Unity.Editor;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>Render a color field that is undoable.</summary>
    class sfUndoableColor : ScriptableObject
    {
        /* Color picked from color field */
        public Color Color
        {
            get
            {
                return m_color;
            }

            set
            {
                m_color = value;
            }
        }
        [SerializeField]
        private Color m_color = Color.red;

        private ksReflectionObject m_colorPickIdField;
        private ksReflectionObject m_getLastPickedColorMethod;

        /// <summary>Draws the color field with a label.</summary>
        /// <param name="label"></param>
        public void Draw(GUIContent label)
        {
            Undo.RecordObject(this, "ColorChange");
            m_color = EditorGUILayout.ColorField(label, m_color);
            HandleEyeDropper();
        }

        /// <summary>Make a color field.</summary>
        /// <param name="rect">rect to draw in.</param>
        /// <param name="tooltip"></param>
        public void Draw(Rect rect, string tooltip = null)
        {
            Undo.RecordObject(this, "ColorChange");
            if (!string.IsNullOrEmpty(tooltip))
            {
                // Tooltips don't work with color fields that don't have a label, so we create an empty label with a
                // tooltip.
                GUI.Label(rect, new GUIContent("", tooltip));
            }
            // For some reason Unity doesn't draw the color field in the first 15 pixels of the rect, so we expand
            // the rect on the left size to make it draw in the full original rect.
            rect.x -= 15f;
            rect.width += 15f;
            m_color = EditorGUI.ColorField(rect, m_color);
            HandleEyeDropper();
        }

        /// <summary>Initializes reflection objects if they are not already initialized.</summary>
        private void InitializeReflection()
        {
            if (m_getLastPickedColorMethod == null)
            {
                Assembly unityEditor = typeof(EditorWindow).Assembly;
                m_getLastPickedColorMethod = new ksReflectionObject(unityEditor,
                    "UnityEditor.EyeDropper").GetMethod("GetLastPickedColor");
                m_colorPickIdField = new ksReflectionObject(unityEditor,
                    "UnityEditor.EditorGUI").GetField("s_ColorPickID");
            }
        }

        /// <summary>
        /// Unity's eye dropper is broken and doesn't select a color or stop getting the color under the mouse when you
        /// click. This function fixes it.
        /// </summary>
        private void HandleEyeDropper()
        {
            if (Event.current == null)
            {
                return;
            }
            // Unity's eye dropper is broken and doesn't select a color or stop getting the color under the mouse when
            // you click. This fixes it.
            switch (Event.current.commandName)
            {
                case "EyeDropperClicked":
                {
                    InitializeReflection();
                    float a = m_color.a;
                    m_color = (Color)(m_getLastPickedColorMethod.Invoke());
                    m_color.a = a;
                    m_colorPickIdField.SetValue(0);
                    break;
                }
                case "EyeDropperCancelled":
                {
                    InitializeReflection();
                    m_colorPickIdField.SetValue(0);
                    break;
                }
            }
        }
    }
}
