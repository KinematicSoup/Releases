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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Draw serialized properties tagged with [ksReadOnly] in the inspector as a label.</summary>
    [CustomPropertyDrawer(typeof(ksReadOnlyAttribute))]
    class ksReadOnlyDrawer : PropertyDrawer
    {
        /// <summary>Convert property types to strings and render them as a label.</summary>
        /// <param name="position">Render position.</param>
        /// <param name="property">Property to render.</param>
        /// <param name="label">Label assigned to the property.</param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            string value = null;
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer: value = property.intValue.ToString(); break;
                case SerializedPropertyType.Boolean: value = property.boolValue.ToString(); break;
                case SerializedPropertyType.Float: value = property.floatValue.ToString(); break;
                case SerializedPropertyType.String: value = property.stringValue; break;
                case SerializedPropertyType.Color: value = property.colorValue.ToString(); break;
                case SerializedPropertyType.Enum: value = property.enumDisplayNames[property.enumValueIndex]; break;
                case SerializedPropertyType.Vector2: value = property.vector2Value.ToString(); break;
                case SerializedPropertyType.Vector3: value = property.vector3Value.ToString(); break;
                case SerializedPropertyType.Vector4: value = property.vector4Value.ToString(); break;
                case SerializedPropertyType.Rect: value = property.rectValue.ToString(); break;
                case SerializedPropertyType.Character: value = property.stringValue.ToString(); break;
                case SerializedPropertyType.Bounds: value = property.boundsValue.ToString(); break;
                case SerializedPropertyType.Quaternion: value = property.quaternionValue.ToString(); break;
                case SerializedPropertyType.Vector2Int: value = property.vector2IntValue.ToString(); break;
                case SerializedPropertyType.Vector3Int: value = property.vector3IntValue.ToString(); break;
                case SerializedPropertyType.RectInt: value = property.rectIntValue.ToString(); break;
                case SerializedPropertyType.BoundsInt: value = property.boundsIntValue.ToString(); break;
                //case SerializedPropertyType.ObjectReference:
                //case SerializedPropertyType.LayerMask:
                //case SerializedPropertyType.ArraySize: 
                //case SerializedPropertyType.AnimationCurve:
                //case SerializedPropertyType.Gradient:
                //case SerializedPropertyType.ExposedReference:
                //case SerializedPropertyType.FixedBufferSize:
                //case SerializedPropertyType.ManagedReference:
                default: return;
            }
            EditorGUI.LabelField(position, label, new GUIContent(value));
        }
    }
}
