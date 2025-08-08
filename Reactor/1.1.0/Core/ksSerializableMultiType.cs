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
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;

namespace KS.Reactor.Client.Unity
{
    /// <summary>
    /// Wraps a <see cref="ksMultiType"/> so it can be serialized by Unity and edited in the inspector.
    /// </summary>
    [Serializable]
    public class ksSerializableMultiType : ISerializationCallbackReceiver
    {
        /// <summary>Multi-type value</summary>
        public ksMultiType Value
        {
            get { return m_value; }
            set { m_value = value; }
        }
        private ksMultiType m_value;

        [SerializeField]
        private byte[] m_data;

        [SerializeField]
        [FormerlySerializedAs("m_serialized")]
        private LegacyData m_legacy;

        /// <summary>Constructor</summary>
        public ksSerializableMultiType()
        {

        }

        /// <summary>Constructor that sets the value.</summary>
        /// <param name="value">Value</param>
        public ksSerializableMultiType(ksMultiType value)
        {
            m_value = value;
        }

        /// <summary>Serializes the <see cref="Value"/> to a byte array that Unity can serialize.</summary>
        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            ksStreamBuffer buffer = ksStreamBuffer.Create();
            m_value.Serialize(buffer);
            m_data = buffer.ToArray();
            buffer.Release();
            m_legacy = null;
        }

        /// <summary>Deserializes the Unity data back to the <see cref="Value"/>.</summary>
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            try
            {
                if (m_legacy != null && m_legacy.Data != null && m_legacy.Data.Length > 0)
                {
                    LegacyDeserialize();
                    m_legacy = null;
                    return;
                }
                if (m_data == null)
                {
                    m_value = ksMultiType.Null;
                    return;
                }
                ksStreamBuffer buffer = new ksStreamBuffer(m_data);
                m_value.Deserialize(buffer);
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Error deserializing ksMultiType. Setting to ksMultiType.Null.", e);
                m_value = ksMultiType.Null;
            }
        }

        /* Legacy Deserialization */

        /// <summary>
        /// Legacy types that could be serialized in the inspector. Some pseudo types weren't real and only changed
        /// how the data was shown in the inspector but internally used a different type, such as vectors using
        /// float arrays internally.
        /// </summary>
        public enum LegacyTypes : byte
        {
            // True types
            INT = ksMultiType.LegacyTypes.INT,
            FLOAT = ksMultiType.LegacyTypes.FLOAT,
            STRING = ksMultiType.LegacyTypes.STRING,
            BOOL = ksMultiType.LegacyTypes.BOOL,
            UINT = ksMultiType.LegacyTypes.UINT,
            BYTE = ksMultiType.LegacyTypes.BYTE,
            LONG = ksMultiType.LegacyTypes.LONG,
            INT_ARRAY = ksMultiType.LegacyTypes.INT_ARRAY,
            FLOAT_ARRAY = ksMultiType.LegacyTypes.FLOAT_ARRAY,
            STRING_ARRAY = ksMultiType.LegacyTypes.STRING_ARRAY,
            BOOL_ARRAY = ksMultiType.LegacyTypes.BOOL_ARRAY,
            UINT_ARRAY = ksMultiType.LegacyTypes.UINT_ARRAY,
            BYTE_ARRAY = ksMultiType.LegacyTypes.BYTE_ARRAY,
            LONG_ARRAY = ksMultiType.LegacyTypes.LONG_ARRAY,
            // Pseudo types
            VECTOR2 = ksMultiType.LegacyTypes.FLOAT_ARRAY | (1 << 4),
            VECTOR3 = ksMultiType.LegacyTypes.FLOAT_ARRAY | (2 << 4),
            COLOR = ksMultiType.LegacyTypes.FLOAT_ARRAY | (3 << 4),
            VECTOR2_ARRAY = VECTOR2 | PSEUDO_ARRAY_FLAG,
            VECTOR3_ARRAY = VECTOR3 | PSEUDO_ARRAY_FLAG,
            COLOR_ARRAY = COLOR | PSEUDO_ARRAY_FLAG
        }

        private const byte PSEUDO_ARRAY_FLAG = 1 << 7; // Array flag for legacy pseudo types.

        /// <summary>Holds data that was serialized in legacy format (Reactor 1.0.3 and earlier).</summary>
        [Serializable]
        private class LegacyData
        {
            // Ignore unassigned fields--Unity assigns them when it deserializes.
#pragma warning disable 0649
            public byte PseudoType;
            public byte[] Data;
            public int ArrayLength;
#pragma warning restore
        }

        /// <summary>Deserializes legacy-formatted data.</summary>
        private void LegacyDeserialize()
        {
            LegacyTypes type = (LegacyTypes)m_legacy.PseudoType;
            switch (type)
            {
                case LegacyTypes.BYTE: m_value.Byte = m_legacy.Data[0]; break;
                case LegacyTypes.INT: m_value.Int = BitConverter.ToInt32(m_legacy.Data, 0); break;
                case LegacyTypes.UINT: m_value.UInt = BitConverter.ToUInt32(m_legacy.Data, 0); break;
                case LegacyTypes.LONG: m_value.Long = BitConverter.ToInt64(m_legacy.Data, 0); break;
                case LegacyTypes.FLOAT: m_value.Float = BitConverter.ToSingle(m_legacy.Data, 0); break;
                case LegacyTypes.BOOL: m_value.Bool = m_legacy.Data[0] != 0; break;
                case LegacyTypes.STRING: int offset = 0; m_value.String = ParseString(ref offset); break;
                case LegacyTypes.VECTOR2: m_value.Vector2 = ksFixedDataParser.ParseFromBytes<ksVector2>(m_legacy.Data, 0); break;
                case LegacyTypes.VECTOR3: m_value.Vector3 = ksFixedDataParser.ParseFromBytes<ksVector3>(m_legacy.Data, 0); break;
                case LegacyTypes.COLOR: m_value.Color = ksFixedDataParser.ParseFromBytes<ksColor>(m_legacy.Data, 0); break;
                case LegacyTypes.BYTE_ARRAY: m_value.ByteArray = m_legacy.Data; break;
                case LegacyTypes.INT_ARRAY: m_value.IntArray = ParseArray<int>(); break;
                case LegacyTypes.UINT_ARRAY: m_value.UIntArray = ParseArray<uint>(); break;
                case LegacyTypes.LONG_ARRAY: m_value.LongArray = ParseArray<long>(); break;
                case LegacyTypes.FLOAT_ARRAY: m_value.FloatArray = ParseArray<float>(); break;
                case LegacyTypes.BOOL_ARRAY: m_value.BoolArray = ParseBoolArray(); break;
                case LegacyTypes.STRING_ARRAY: m_value.StringArray = ParseStringArray(); break;
                case LegacyTypes.VECTOR2_ARRAY: m_value.Vector2Array = ParseArray<ksVector2>(); break;
                case LegacyTypes.VECTOR3_ARRAY: m_value.Vector3Array = ParseArray<ksVector3>(); break;
                case LegacyTypes.COLOR_ARRAY: m_value.ColorArray = ParseArray<ksColor>(); break;
                default: m_value.ByteArray = null; break;
            }
        }

        /// <summary>Parses a string from the legacy-formatted data.</summary>
        /// <param name="index">Index to parse at.</param>
        /// <returns>Parsed string</returns>
        private string ParseString(ref int index)
        {
            if (m_legacy.Data.Length < index + Marshal.SizeOf(typeof(int)))
            {
                return "";
            }
            int len = ksFixedDataParser.ParseFromBytes<int>(m_legacy.Data, index);
            index += Marshal.SizeOf(typeof(int));
            if (m_legacy.Data.Length < index + len)
            {
                return "";
            }
            string val = Encoding.UTF8.GetString(m_legacy.Data, index, len);
            index += len;
            return val;
        }

        /// <summary>Parses a string array from the legacy-formatted data.</summary>
        /// <returns>Parsed string array</returns>
        private string[] ParseStringArray()
        {
            if (m_legacy.ArrayLength < 0)
            {
                return null;
            }
            string[] array = new string[m_legacy.ArrayLength];
            int offset = 0;
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = ParseString(ref offset);
            }
            return array;
        }

        /// <summary>Parses a bool array from the legacy-formatted data.</summary>
        /// <returns>Parsed bool array</returns>
        private bool[] ParseBoolArray()
        {
            if (m_legacy.ArrayLength < 0)
            {
                return null;
            }
            bool[] array = new bool[m_legacy.ArrayLength];
            for (int i = 0; i < array.Length; i++)
            {
                int index = i / 8;
                int bit = i % 8;
                array[i] = (m_legacy.Data[index] & (1 << bit)) != 0;
            }
            return array;
        }

        /// <summary>Parses a struct array from the legacy-formatted data.</summary>
        /// <typeparam name="T">Struct element type.</typeparam>
        /// <returns>Parsed stuct array</returns>
        private T[] ParseArray<T>() where T : struct
        {
            if (m_legacy.ArrayLength < 0)
            {
                return null;
            }
            int size = Marshal.SizeOf<T>();
            T[] array = new T[m_legacy.Data.Length / size];
            int offset = 0;
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = ksFixedDataParser.ParseFromBytes<T>(m_legacy.Data, offset);
                offset += size;
            }
            return array;
        }
    }
}
