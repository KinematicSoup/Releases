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

using UnityEngine;
using UnityEditor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Property drawer for int vectors tagged with the <see cref="ksBoolVectorAttribute"/>.</summary>
    [CustomPropertyDrawer(typeof(ksBoolVectorAttribute))]
    public class ksBoolVectorDrawer : PropertyDrawer
    {
        // width of X/Y/Z labels including padding
        private const float LABEL_WIDTH = 15;
        // width of value box + spacing
        private const float SPACING = 30;

        /// <summary>Draws the property.</summary>
        /// <param name="position">Position to draw at.</param>
        /// <param name="property">Property to draw.</param>
        /// <param name="label">Label for the property.</param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {   
            EditorGUI.BeginProperty(position, label, property);

            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
            position.width = LABEL_WIDTH;

            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            SerializedProperty subProperty = property.FindPropertyRelative("x");
            if (subProperty != null)
            {
                EditorGUI.PrefixLabel(position, new GUIContent("X"));
                position.x += LABEL_WIDTH;
                subProperty.intValue = EditorGUI.Toggle(position, subProperty.intValue != 0) ? 1 : 0;
                position.x += SPACING;
            }

            subProperty = property.FindPropertyRelative("y");
            if (subProperty != null)
            {
                EditorGUI.PrefixLabel(position, new GUIContent("Y"));
                position.x += LABEL_WIDTH;
                subProperty.intValue = EditorGUI.Toggle(position, subProperty.intValue != 0) ? 1 : 0;
                position.x += SPACING;
            }

            subProperty = property.FindPropertyRelative("z");
            if (subProperty != null)
            {
                EditorGUI.PrefixLabel(position, new GUIContent("Z"));
                position.x += LABEL_WIDTH;
                subProperty.intValue = EditorGUI.Toggle(position, subProperty.intValue != 0) ? 1 : 0;
            }

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }
}
