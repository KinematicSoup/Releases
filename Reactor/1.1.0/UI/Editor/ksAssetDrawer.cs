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
    /// Property drawer for scriptable objects with the <see cref="ksAssetAttribute"/>.
    /// Provides a <see cref="ScriptableObject"/> browser with an asset tab and no scene tab as scriptable objects are
    /// never in the scene.
    /// </summary>
    [CustomPropertyDrawer(typeof(ksAssetAttribute))]
    public class ksAssetDrawer : PropertyDrawer
    {
        /// <summary>Draws the property.</summary>
        /// <param name="position">Position to draw at.</param>
        /// <param name="property">Property to draw.</param>
        /// <param name="label">Label for the property.</param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            UnityEngine.Object obj = EditorGUI.ObjectField(position, property.objectReferenceValue,
                fieldInfo.FieldType, false);
            if (EditorGUI.EndChangeCheck())
            {
                property.objectReferenceValue = obj;
            }
            EditorGUI.showMixedValue = false;
            EditorGUI.EndProperty();
        }
    }
}
