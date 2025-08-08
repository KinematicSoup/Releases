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
using KS.Unity.Editor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>
    /// Property drawer for enum flag masks using the <see cref="ksFlagsAttribute"/>. Converts names using our naming
    /// standard (ENUM_VALUE) to a more readable format (Enum Value). Composite flag values are not shown.
    /// </summary>
    [CustomPropertyDrawer(typeof(ksFlagsAttribute))]
    public class ksFlagsDrawer : PropertyDrawer
    {
        /// <summary>Draws the property.</summary>
        /// <param name="position">Position to draw at.</param>
        /// <param name="property">Property to draw.</param>
        /// <param name="label">Label for the property.</param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Type enumType = ksEditorUtils.GetPropertyType(property);
            if (enumType == null)
            {
                base.OnGUI(position, property, label);
                return;
            }
            List<string> names;
            List<int> values;
            GetFlagsAndDisplayNames(enumType, out names, out values);
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int mask = EditorGUI.MaskField(position, label, EnumFlagsToIntMask(property.intValue, values),
                names.ToArray());
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                property.intValue = IntMaskToEnumFlags(mask, values);
            }
            EditorGUI.EndProperty();
        }

        /// <summary>
        /// Gets the display names and flag values for an enum flag type.
        /// </summary>
        /// <param name="enumType">Enum type to get display names for.</param>
        /// <param name="displayNames">Set to enum flag display names</param>
        /// <param name="values">Set to enum flag values</param>
        private void GetFlagsAndDisplayNames(Type enumType, out List<string> displayNames, out List<int> values)
        {
            displayNames = new List<string>();
            values = new List<int>();
            string[] names = Enum.GetNames(enumType);
            Array enumValues = Enum.GetValues(enumType);
            for (int i = 0; i < names.Length; i++)
            {
                int value = (int)enumValues.GetValue(i);
                // Do not include zero or composite flag values (values that aren't powers of 2).
                if (value != 0 && Mathf.IsPowerOfTwo(value))
                {
                    values.Add(value);
                    displayNames.Add(ksEnumDrawer.GetEnumDisplayName(names[i]));
                }
            }
        }

        /// <summary>
        /// Converts an enum flags value to an int mask that Unity's MaskField can use. The bit at index i is set if
        /// <paramref name="flags"/> & <paramref name="values"/>[i] != 0.
        /// </summary>
        /// <param name="flags">Enum flags</param>
        /// <param name="values">Enum flag values</param>
        /// <returns>Int mask that can be used with Unity's MaskField.</returns>
        private int EnumFlagsToIntMask(int flags, List<int> values)
        {
            int mask = 0;
            for (int i = 0; i < values.Count; i++)
            {
                if ((flags & values[i]) != 0)
                {
                    mask |= 1 << i;
                }
            }
            return mask;
        }

        /// <summary>
        /// Converts an int mask from Unity's MaskField to an enum flags value. Each set bit in the
        /// <paramref name="mask"/> will set the flag from <paramref name="values"/> as the corresponding index.
        /// </summary>
        /// <param name="mask">Int mask for Unity's MaskField.</param>
        /// <param name="values">Enum flag values</param>
        /// <returns>Enum flag value</returns>
        private int IntMaskToEnumFlags(int mask, List<int> values)
        {
            int flags = 0;
            for (int i = 0; i < values.Count; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    flags |= values[i];
                }
            }
            return flags;
        }
    }
}
