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
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using KS.Unity.Editor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>
    /// Property drawer for <see cref="ksMultiType"/>. Shows a message saying to change the field type to 
    /// <see cref="ksSerializableMultiType"/>
    /// </summary>
    [CustomPropertyDrawer(typeof(ksMultiType))]
    public class ksMultiTypeDrawer : PropertyDrawer
    {
        /// <summary>
        /// Shows a message saying to change the field type to <see cref="ksSerializableMultiType"/>.
        /// </summary>
        /// <param name="position">Position to draw at.</param>
        /// <param name="property">Property to draw.</param>
        /// <param name="label">Label for the property.</param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.PrefixLabel(position, label);
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.normal.textColor = Color.red;
            EditorGUI.LabelField(position, "Change the field type to ksSerializableMultiType to edit in the inspector.", style);
            EditorGUI.EndProperty();
        }
    }

    /// <summary>Property drawer for <see cref="ksSerializableMultiType"/>.</summary>
    [CustomPropertyDrawer(typeof(ksSerializableMultiType))]
    public class ksSerializableMultiTypeDrawer : PropertyDrawer
    {
        private delegate T Drawer<T>(Rect position, GUIContent label, T value);

        private const float LINE_HEIGHT = 18f;      // The height of a line.
        private const float PADDING = 2f;           // The padding between lines.
        private const float MIN_TYPE_WIDTH = 50f;   // The minimum width of the type selector.
        private const float MAX_TYPE_WIDTH = 120f;  // The maximum width of the type selector.
        private const float BUTTON_WIDTH = 20f;     // The width of the + and - buttons for arrays.
        private const string INDENT = "    ";       // Indentation for string labels.

        /// <summary>Stores expanded property state.</summary>
        private class ExpandedSet : ksInspectorStateMap<bool, ExpandedSet>
        {

        }

        private int m_focusCtrlId;


        /// <summary>Draws the property.</summary>
        /// <param name="position">Position to draw at.</param>
        /// <param name="property">Property to draw.</param>
        /// <param name="label">Label for the property.</param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // To get focused text boxes to refresh when the value changes, we have to clear the focus and reset it on
            // the next repaint event. We reset the focus here on a repaint event.
            if (m_focusCtrlId != 0 && Event.current.type == EventType.Repaint)
            {
                GUIUtility.keyboardControl = m_focusCtrlId;
                m_focusCtrlId = 0;
            }

            position.height = LINE_HEIGHT;
            EditorGUI.BeginProperty(position, label, property);

            // Get the ksMultiType from the property.
            property.serializedObject.ApplyModifiedProperties();
            ksSerializableMultiType serializableMultiType;
            if (!ksEditorUtils.TryGetPropertyValue(property, out serializableMultiType))
            {
                position = EditorGUI.PrefixLabel(position, label);
                GUIStyle style = new GUIStyle(GUI.skin.label);
                style.normal.textColor = Color.red;
                EditorGUI.LabelField(position, "Error", style);
                EditorGUI.EndProperty();
                return;
            }
            ksMultiType multiType = serializableMultiType != null ? serializableMultiType.Value : ksMultiType.Null;

            Rect typePosition = position;
            typePosition.width = ksMath.Clamp(position.width / 5f, MIN_TYPE_WIDTH, MAX_TYPE_WIDTH);
            position.width -= typePosition.width;
            typePosition.x += position.width;

            // Get the expanded state.
            bool expanded = ExpandedSet.Get().Get(property);

            // Draw the multitype value.
            EditorGUI.BeginChangeCheck();
            DrawMultiType(position, typePosition.width, ref multiType, label, ref expanded);
            bool changed = EditorGUI.EndChangeCheck();

            // Draw the type selector.
            EditorGUI.BeginChangeCheck();
            ksMultiType.Types type = (ksMultiType.Types)EditorGUI.EnumPopup(typePosition, multiType.Type);
            if (EditorGUI.EndChangeCheck())
            {
                // Convert the multitype to the new type.
                multiType = MultiTypeAs(ref multiType, type);
                changed = true;

                // Expand when the type is changed to an array. Collapse when it is changed to a non-array.
                expanded = multiType.IsArray;
            }

            if (changed)
            {
                // Apply the changes.
                ksStreamBuffer buffer = new ksStreamBuffer();
                multiType.Serialize(buffer);
                SetByteArrayProperty(property.FindPropertyRelative("m_data"), buffer.ToArray());
                buffer.Release();
            }
            // Save the expanded state.
            ExpandedSet.Get().Set(property, expanded);
            EditorGUI.EndProperty();
        }

        /// <summary>Gets the height of the property.</summary>
        /// <param name="property">Property to get height for.</param>
        /// <param name="label">Property label.</param>
        /// <returns>The height of the property.</returns>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            int numLines = 1;
            ksSerializableMultiType serializableMultiType;
            if (ExpandedSet.Get().Get(property) && ksEditorUtils.TryGetPropertyValue(property, 
                out serializableMultiType) && serializableMultiType != null)
            {
                ksMultiType multiType = serializableMultiType.Value;
                numLines = multiType.IsArray ? multiType.Count + 2 : 1;
            }
            return LINE_HEIGHT * numLines + PADDING * (numLines - 1);
        }

        /// <summary>Sets the value of a byte array property.</summary>
        /// <param name="property">Byte array property to set.</param>
        /// <param name="array">Array value.</param>
        private void SetByteArrayProperty(SerializedProperty property, byte[] array)
        {
            property.arraySize = array.Length;
            for (int i = 0; i < array.Length; i++)
            {
                property.GetArrayElementAtIndex(i).intValue = array[i];
            }
        }

        /// <summary>Draws an editor for a byte.</summary>
        /// <param name="position">Position to draw at.</param>
        /// <param name="label">Label for the value.</param>
        /// <param name="value">Value</param>
        /// <returns>New value</returns>
        private byte DrawByte(Rect position, GUIContent label, byte value)
        {
            return (byte)ksMath.Clamp(EditorGUI.IntField(position, label, value), 0, 255);
        }

        /// <summary>Draws an editor for a short.</summary>
        /// <param name="position">Position to draw at.</param>
        /// <param name="label">Label for the value.</param>
        /// <param name="value">Value</param>
        /// <returns>New value</returns>
        private short DrawShort(Rect position, GUIContent label, short value)
        {
            return (short)ksMath.Clamp(EditorGUI.IntField(position, label, value), short.MinValue, short.MaxValue);
        }

        /// <summary>Draws an editor for a ushort.</summary>
        /// <param name="position">Position to draw at.</param>
        /// <param name="label">Label for the value.</param>
        /// <param name="value">Value</param>
        /// <returns>New value</returns>
        private ushort DrawUShort(Rect position, GUIContent label, ushort value)
        {
            return (ushort)ksMath.Clamp(EditorGUI.IntField(position, label, value), 0, ushort.MaxValue);
        }

        /// <summary>Draws an editor for a uint.</summary>
        /// <param name="position">Position to draw at.</param>
        /// <param name="label">Label for the value.</param>
        /// <param name="value">Value</param>
        /// <returns>New value</returns>
        private uint DrawUInt(Rect position, GUIContent label, uint value)
        {
            return (uint)ksMath.Clamp(EditorGUI.LongField(position, label, value), 0, uint.MaxValue);
        }

        /// <summary>Draws an editor for a ulong.</summary>
        /// <param name="position">Position to draw at.</param>
        /// <param name="label">Label for the value.</param>
        /// <param name="value">Value</param>
        /// <returns>New value</returns>
        private ulong DrawULong(Rect position, GUIContent label, ulong value)
        {
            // Unity doesn't have a numeric editor that can support all of ulong's values, so we instead use a text
            // editor and try to parse the text as a ulong. This means you cannot drag the label to change the value.
            string text = EditorGUI.TextField(position, label, value.ToString());
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }
            ulong newValue;
            if (ulong.TryParse(text, out newValue))
            {
                return newValue;
            }
            return value;
        }

        /// <summary>Draws an editor for a char.</summary>
        /// <param name="position">Position to draw at.</param>
        /// <param name="label">Label for the value.</param>
        /// <param name="value">Value</param>
        /// <returns>New value</returns>
        private char DrawChar(Rect position, GUIContent label, char value)
        {
            string text = EditorGUI.TextField(position, label, value.ToString());
            if (string.IsNullOrEmpty(text))
            {
                return '\0';
            }
            return text[text.Length - 1];
        }

        /// <summary>Draws an editor for a <see cref="Vector2"/>.</summary>
        /// <param name="position">Position to draw at.</param>
        /// <param name="label">Label for the value.</param>
        /// <param name="value">Value</param>
        /// <returns>New value</returns>
        private Vector2 DrawVector2(Rect position, GUIContent label, Vector2 value)
        {
            // If you pass a non-empty label to Unity's Vector2Field, it sometimes takes up 2 lines instead of 1
            // depending on how much width is available, which messes up our positioning. To force it to use 1 line,
            // we add a separate prefix label, then call Vector2Field with an empty label.
            Rect labelPosition = position;
            position = EditorGUI.PrefixLabel(labelPosition, label);
            float dx = (position.x - labelPosition.x) / 3f;
            position.x -= dx;
            position.width += dx;
            return EditorGUI.Vector2Field(position, "", value);
        }

        /// <summary>Draws an editor for a <see cref="Vector3"/>.</summary>
        /// <param name="position">Position to draw at.</param>
        /// <param name="label">Label for the value.</param>
        /// <param name="value">Value</param>
        /// <returns>New value</returns>
        private Vector3 DrawVector3(Rect position, GUIContent label, Vector3 value)
        {
            // If you pass a non-empty label to Unity's Vector3Field, it sometimes takes up 2 lines instead of 1
            // depending on how much width is available, which messes up our positioning. To force it to use 1 line,
            // we add a separate prefix label, then call Vector3Field with an empty label.
            Rect labelPosition = position;
            position = EditorGUI.PrefixLabel(labelPosition, label);
            float dx = (position.x - labelPosition.x) / 3f;
            position.x -= dx;
            position.width += dx;
            return EditorGUI.Vector3Field(position, "", value);
        }

        /// <summary>Draws an editor for a quaternion as euler angles.</summary>
        /// <param name="position">Position to draw at.</param>
        /// <param name="label">Label for the value.</param>
        /// <param name="value">Value</param>
        /// <returns>New value</returns>
        private Quaternion DrawEuler(Rect position, GUIContent label, Quaternion value)
        {
            Vector3 euler = DrawVector3(position, label, value.eulerAngles);
            return Quaternion.Euler(euler);
        }

        /// <summary>Draws an editor for a templated array.</summary>
        /// <typeparam name="T">Array element type.</typeparam>
        /// <param name="position">Position to draw at.</param>
        /// <param name="typeWidth">The width used by the type selector.</param>
        /// <param name="label">Label to draw.</param>
        /// <param name="array">Array value.</param>
        /// <param name="drawer">Delegate for drawing an element of the array.</param>
        /// <param name="expanded">Is the foldout expanded? If false, does not draw the array elements.</param>
        /// <returns>New array value.</returns>
        private T[] DrawArray<T>(Rect position, float typeWidth, GUIContent label, T[] array, Drawer<T> drawer, ref bool expanded)
        {
            Rect valuePosition = EditorGUI.PrefixLabel(position, new GUIContent(" "));
            Rect labelPosition = position;
            labelPosition.width -= valuePosition.width + PADDING;
            // Draw the foldout.
            expanded = EditorGUI.Foldout(labelPosition, expanded, label, true);

            // Draw the array size.
            int arrayLength = array == null ? -1 : array.Length;
            int length = Math.Max(0, EditorGUI.IntField(valuePosition, "", arrayLength));
            // You can't give a tooltip to something without a label, so create an empty label over the int field to
            // hold a tooltip.
            EditorGUI.LabelField(position, new GUIContent("", "Array Size"));
            if (length != arrayLength)
            {
                T[] newArray = new T[length];
                int count = Math.Min(length, arrayLength);
                for (int i = 0; i < count; i++)
                {
                    newArray[i] = array[i];
                }
                array = newArray;
                expanded = true;
            }
            if (!expanded)
            {
                return array;
            }

            position.width += typeWidth;
            position.y += LINE_HEIGHT + PADDING;
            // Draw each element.
            for (int i = 0; i < array.Length; i++)
            {
                // Set the control name to the index. We use the control name to get the selected index later.
                GUI.SetNextControlName(i.ToString());
                array[i] = drawer(position, new GUIContent(INDENT + i), array[i]);
                position.y += LINE_HEIGHT + PADDING;
            }
            position.x += position.width - BUTTON_WIDTH;
            position.width = BUTTON_WIDTH;
            // Draw the - button to delete an element.
            if (GUI.Button(position, "-") && array.Length > 0)
            {
                int index = GetSelectedIndex(array.Length);
                T[] newArray = new T[array.Length - 1];
                for (int i = 0; i < newArray.Length; i++)
                {
                    newArray[i] = array[i < index ? i : (i + 1)];
                }
                array = newArray;

                // Clear and reset the focus on repaint. This is needed to make the focused text field refresh.
                m_focusCtrlId = GUIUtility.keyboardControl;
                GUIUtility.keyboardControl = 0;
            }
            position.x -= BUTTON_WIDTH + PADDING;
            // Draw the + button to add a new element.
            if (GUI.Button(position, "+"))
            {
                int index = GetSelectedIndex(array.Length);
                T[] newArray = new T[array.Length + 1];
                for (int i = 0; i < array.Length; i++)
                {
                    newArray[i < index ? i : (i + 1)] = array[i];
                }
                array = newArray;

                // Clear and reset the focus on repaint. This is needed to make the focused text field refresh.
                m_focusCtrlId = GUIUtility.keyboardControl;
                GUIUtility.keyboardControl = 0;
            }
            return array;
        }

        /// <summary>Gets the selected array index by converting focused control name to an int.</summary>
        /// <param name="arrayLength">Array length</param>
        /// <returns>The selected index, or <paramref name="arrayLength"/> if no index is selected.</returns>
        private int GetSelectedIndex(int arrayLength)
        {
            int index;
            if (!int.TryParse(GUI.GetNameOfFocusedControl(), out index))
            {
                index = arrayLength;
            }
            return index;
        }

        /// <summary>
        /// Draws an editor for a multitype's value for the given <paramref name="type"/>.
        /// </summary>
        /// <param name="position">Position to draw at.</param>
        /// <param name="typeWidth">The width used by the type selector.</param>
        /// <param name="multiType">Multi type value.</param>
        /// <param name="label">Label to draw.</param>
        /// <param name="expanded">Is the foldout expanded? If false, does not draw the array elements.</param>
        private void DrawMultiType(
            Rect position,
            float typeWidth,
            ref ksMultiType multiType,
            GUIContent label,
            ref bool expanded)
        {
            switch (multiType.Type)
            {
                case ksMultiType.Types.NULL: EditorGUI.LabelField(position, label, new GUIContent("<null>")); break;
                case ksMultiType.Types.BYTE: multiType.Byte = DrawByte(position, label, multiType); break;
                case ksMultiType.Types.SHORT: multiType.Short = DrawShort(position, label, multiType); break;
                case ksMultiType.Types.USHORT: multiType.UShort = DrawUShort(position, label, multiType); break;
                case ksMultiType.Types.INT: multiType.Int = EditorGUI.IntField(position, label, multiType); break;
                case ksMultiType.Types.UINT: multiType.UInt = DrawUInt(position, label, multiType); break;
                case ksMultiType.Types.LONG: multiType.Long = EditorGUI.LongField(position, label, multiType); break;
                case ksMultiType.Types.ULONG: multiType.ULong = DrawULong(position, label, multiType); break;
                case ksMultiType.Types.FLOAT: multiType.Float = EditorGUI.FloatField(position, label, multiType); break;
                case ksMultiType.Types.DOUBLE: multiType.Double = EditorGUI.DoubleField(position, label, multiType); break;
                case ksMultiType.Types.BOOL: multiType.Bool = EditorGUI.Toggle(position, label, multiType); break;
                case ksMultiType.Types.CHAR: multiType.Char = DrawChar(position, label, multiType); break;
                case ksMultiType.Types.VECTOR2: multiType.Vector2 = DrawVector2(position, label, multiType); break;
                case ksMultiType.Types.VECTOR3: multiType.Vector3 = DrawVector3(position, label, multiType); break;
                case ksMultiType.Types.COLOR: multiType.Color = EditorGUI.ColorField(position, label, multiType); break;
                case ksMultiType.Types.QUATERNION: multiType.Quaternion = DrawEuler(position, label, multiType); break;
                case ksMultiType.Types.STRING: multiType.String = EditorGUI.TextField(position, label, multiType); break;
                case ksMultiType.Types.BYTE_ARRAY: multiType.ByteArray = DrawArray<byte>(position, typeWidth, label, multiType, DrawByte, ref expanded); break;
                case ksMultiType.Types.SHORT_ARRAY: multiType.ShortArray = DrawArray<short>(position, typeWidth, label, multiType, DrawShort, ref expanded); break;
                case ksMultiType.Types.USHORT_ARRAY: multiType.UShortArray = DrawArray<ushort>(position, typeWidth, label, multiType, DrawUShort, ref expanded); break;
                case ksMultiType.Types.INT_ARRAY: multiType.IntArray = DrawArray<int>(position, typeWidth, label, multiType, EditorGUI.IntField, ref expanded); break;
                case ksMultiType.Types.UINT_ARRAY: multiType.UIntArray = DrawArray<uint>(position, typeWidth, label, multiType, DrawUInt, ref expanded); break;
                case ksMultiType.Types.LONG_ARRAY: multiType.LongArray = DrawArray<long>(position, typeWidth, label, multiType, EditorGUI.LongField, ref expanded); break;
                case ksMultiType.Types.ULONG_ARRAY: multiType.ULongArray = DrawArray<ulong>(position, typeWidth, label, multiType, DrawULong, ref expanded); break;
                case ksMultiType.Types.FLOAT_ARRAY: multiType.FloatArray = DrawArray<float>(position, typeWidth, label, multiType, EditorGUI.FloatField, ref expanded); break;
                case ksMultiType.Types.DOUBLE_ARRAY: multiType.DoubleArray = DrawArray<double>(position, typeWidth, label, multiType, EditorGUI.DoubleField, ref expanded); break;
                case ksMultiType.Types.BOOL_ARRAY: multiType.BoolArray = DrawArray<bool>(position, typeWidth, label, multiType, EditorGUI.Toggle, ref expanded); break;
                case ksMultiType.Types.CHAR_ARRAY: multiType.CharArray = DrawArray<char>(position, typeWidth, label, multiType, DrawChar, ref expanded); break;
                case ksMultiType.Types.VECTOR2_ARRAY: multiType.UnityVector2Array = DrawArray<Vector2>(position, typeWidth, label, multiType, DrawVector2, ref expanded); break;
                case ksMultiType.Types.VECTOR3_ARRAY: multiType.UnityVector3Array = DrawArray<Vector3>(position, typeWidth, label, multiType, DrawVector3, ref expanded); break;
                case ksMultiType.Types.COLOR_ARRAY: multiType.UnityColorArray = DrawArray<Color>(position, typeWidth, label, multiType, EditorGUI.ColorField, ref expanded); break;
                case ksMultiType.Types.QUATERNION_ARRAY: multiType.UnityQuaternionArray = DrawArray<Quaternion>(position, typeWidth, label, multiType, DrawEuler, ref expanded); break;
                case ksMultiType.Types.STRING_ARRAY: multiType.StringArray = DrawArray<string>(position, typeWidth, label, multiType, EditorGUI.TextField, ref expanded); break;
            }
        }

        private ksMultiType MultiTypeAs(ref ksMultiType multiType, ksMultiType.Types type)
        {
            ksMultiType result = multiType.As(type);
            if (!result.IsNull || type == ksMultiType.Types.NULL)
            {
                return result;
            }
            switch (type)
            {
                case ksMultiType.Types.BYTE_ARRAY: return new byte[0];
                case ksMultiType.Types.SHORT_ARRAY: return new short[0];
                case ksMultiType.Types.USHORT_ARRAY: return new ushort[0];
                case ksMultiType.Types.INT_ARRAY: return new int[0];
                case ksMultiType.Types.UINT_ARRAY: return new uint[0];
                case ksMultiType.Types.LONG_ARRAY: return new long[0];
                case ksMultiType.Types.ULONG_ARRAY: return new ulong[0];
                case ksMultiType.Types.FLOAT_ARRAY: return new float[0];
                case ksMultiType.Types.DOUBLE_ARRAY: return new double[0];
                case ksMultiType.Types.CHAR_ARRAY: return new char[0];
                case ksMultiType.Types.BOOL_ARRAY: return new bool[0];
                case ksMultiType.Types.VECTOR2_ARRAY: return new ksVector2[0];
                case ksMultiType.Types.VECTOR3_ARRAY: return new ksVector3[0];
                case ksMultiType.Types.COLOR_ARRAY: return new ksColor[0];
                case ksMultiType.Types.QUATERNION_ARRAY: return new ksQuaternion[0];
                case ksMultiType.Types.STRING_ARRAY: return new string[0];
                default: return result;
            }
        }
    }
}