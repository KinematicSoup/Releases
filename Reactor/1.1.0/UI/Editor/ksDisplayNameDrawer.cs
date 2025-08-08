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
    /// Property drawer for fields tagged with <see cref="ksDisplayNameAttribute"/> to change the display name of the
    /// field.
    /// </summary>
    [CustomPropertyDrawer(typeof(ksDisplayNameAttribute))]
    public class ksDisplayNameDrawer : PropertyDrawer
    {
        /// <summary>Draws the property.</summary>
        /// <param name="position">Position to draw at.</param>
        /// <param name="property">Property to draw.</param>
        /// <param name="label">Label for the property.</param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // If the label is different from the property's display name, use the custom label instead.
            if (label.text == property.displayName)
            {
                ksDisplayNameAttribute tag = attribute as ksDisplayNameAttribute;
                label.text = tag == null ? property.displayName : tag.DisplayName;
            }
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.PropertyField(position, property, label);
            EditorGUI.EndProperty();
        }
    }
}
