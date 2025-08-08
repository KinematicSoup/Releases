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
    /// <summary>
    /// Property drawer for enums with the <see cref="ksEnumAttribute"/>. Converts our naming standard (ENUM_VALUE)
    /// to a more readable display name (Enum Value).
    /// </summary>
    [CustomPropertyDrawer(typeof(ksEnumAttribute))]
    public class ksEnumDrawer : PropertyDrawer
    {
        /// <summary>Draws the property.</summary>
        /// <param name="position">Position to draw at.</param>
        /// <param name="property">Property to draw.</param>
        /// <param name="label">Label for the property.</param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GUIContent[] names = GetEnumDisplayNames(property);
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int index = EditorGUI.Popup(position, label, property.enumValueIndex, names);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                property.enumValueIndex = index;
            }
            EditorGUI.EndProperty();
        }

        /// <summary>
        /// Converts a property's enum names from our naming standard (ENUM_VALUE) to a more readable format
        /// (Enum Value).
        /// </summary>
        /// <param name="property">Enum property to get display names for.</param>
        /// <returns>Display names for the enum values.</returns>
        public static GUIContent[] GetEnumDisplayNames(SerializedProperty property)
        {
            return GetEnumDisplayNames(property.enumDisplayNames);
        }

        /// <summary>
        /// Converts enum names from our naming standard (ENUM_VALUE) to a more readable format (Enum Value).
        /// </summary>
        /// <param name="enumNames">The enum names to convert to display names.</param>
        /// <returns>Display names for the enum values.</returns>
        public static GUIContent[] GetEnumDisplayNames(string[] enumNames)
        {
            GUIContent[] names = new GUIContent[enumNames.Length];
            for (int i = 0; i < enumNames.Length; i++)
            {
                names[i] = new GUIContent(GetEnumDisplayName(enumNames[i]));
            }
            return names;
        }

        /// <summary>
        /// Converts an enum name from our naming standard (ENUM_VALUE) to a more readable format (Enum Value).
        /// </summary>
        /// <param name="enumName">The enum name to convert to a display name.</param>
        /// <returns>Display name for the enum value.</returns>
        public static string GetEnumDisplayName(string enumName)
        {
            string[] words = enumName.Split('_');
            string name = "";
            foreach (string word in words)
            {
                if (word.Length <= 1)
                {
                    if (name.Length > 0)
                    {
                        name += " ";
                    }
                    name += word;
                    continue;
                }
                string firstLetter = word.Substring(0, 1);
                string remainingLetters = word.Substring(1);
                if (name.Length > 0)
                {
                    name += " ";
                }
                name += firstLetter + remainingLetters.ToLowerInvariant();
            }
            return name;
        }
    }
}
