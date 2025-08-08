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

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Property drawer for <see cref="ksPredictionBehaviour"/>.</summary>
    [CustomPropertyDrawer(typeof(ksPredictionBehaviour))]
    public class ksPredictionBehaviourDrawer : PropertyDrawer
    {
        private const float LINE_HEIGHT = 18f;  // The height of a line.
        private const float PADDING = 2f;       // The padding between lines.
        private const float DASH_WIDTH = 14f;   // The width of the '-' between the Min and Max range boxes.

        /// <summary>The enum index of <see cref="ksPredictionBehaviour.Types.WRAP_FLOAT"/>.</summary>
        private static int WrapFloatIndex
        {
            get
            {
                if (m_wrapFloatIndex < 0)
                {
                    Array enumValues = Enum.GetValues(typeof(ksPredictionBehaviour.Types));
                    for (int i = 0; i < enumValues.Length; i++)
                    {
                        if ((ksPredictionBehaviour.Types)enumValues.GetValue(i) == 
                            ksPredictionBehaviour.Types.WRAP_FLOAT)
                        {
                            m_wrapFloatIndex = i;
                            break;
                        }
                    }
                }
                return m_wrapFloatIndex;
            }
        }
        private static int m_wrapFloatIndex = -1;

        /// <summary>Draws the property.</summary>
        /// <param name="position">Position to draw at.</param>
        /// <param name="property">Property to draw.</param>
        /// <param name="label">Label for the property.</param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            position.height = LINE_HEIGHT;
            EditorGUI.BeginProperty(position, label, property);
            SerializedProperty subProperty = property.FindPropertyRelative("Type");
            GUIContent[] names = ksEnumDrawer.GetEnumDisplayNames(subProperty);
            // This overload that uses GUIContent instead of strings is needed for tooltips to work.
            int index = EditorGUI.Popup(position, label, subProperty.enumValueIndex, names);
            subProperty.enumValueIndex = index;
            // Display Min and Max range if prediction type is WRAP_FLOAT.
            if (index == WrapFloatIndex)
            {
                position.y += LINE_HEIGHT + PADDING;
                position = EditorGUI.PrefixLabel(position, new GUIContent("Range", "The min and max values of the property."));
                int indent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;
                float rangeWidth = (position.width - DASH_WIDTH) / 2f;
                position.width = rangeWidth;
                subProperty = property.FindPropertyRelative("Min");
                subProperty.floatValue = EditorGUI.FloatField(position, subProperty.floatValue);

                position.x += rangeWidth;
                position.width = DASH_WIDTH;
                EditorGUI.PrefixLabel(position, new GUIContent(" -"));
                position.x += DASH_WIDTH;
                position.width = rangeWidth;
                subProperty = property.FindPropertyRelative("Max");
                subProperty.floatValue = EditorGUI.FloatField(position, subProperty.floatValue);
                EditorGUI.indentLevel = indent;
            }
            EditorGUI.EndProperty();
        }

        /// <summary>Gets the height of the property.</summary>
        /// <param name="property">Property to get height for.</param>
        /// <param name="label">Property label.</param>
        /// <returns>The height of the property.</returns>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty subProperty = property.FindPropertyRelative("Type");
            return subProperty.enumValueIndex == WrapFloatIndex ? LINE_HEIGHT * 2f + PADDING : LINE_HEIGHT;
        }
    }
}
