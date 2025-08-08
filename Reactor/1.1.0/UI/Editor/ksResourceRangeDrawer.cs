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
    /// <summary>
    /// Property drawer for floats with the <see cref="ksResourceRangeAttribute"/>.
    /// </summary>
    [CustomPropertyDrawer(typeof(ksResourceRangeAttribute))]
    public class ksResourceRangeDrawer : PropertyDrawer
    {
        private float m_value;

        /// <summary>Draws the property.</summary>
        /// <param name="position">Position to draw at.</param>
        /// <param name="property">Property to draw.</param>
        /// <param name="label">Label for the property.</param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Float)
            {
                EditorGUI.LabelField(position, label.text, "Use ksRange with floats.");
                return;
            }

            ksResourceRangeAttribute attr = (ksResourceRangeAttribute)attribute;
            m_value = EditorGUI.Slider(position, label, property.floatValue, attr.Min, attr.Max);

            float step = attr.MinStepSize;
            while (step < m_value / attr.DoubleSteps)
            {
                step *= 2;
            }

            m_value = Mathf.Floor(m_value / step) * step;
            property.floatValue = m_value;
        }
    }
}
