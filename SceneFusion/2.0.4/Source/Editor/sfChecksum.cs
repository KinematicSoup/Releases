using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEditor;
using KS.SF.Reactor;
using UObject = UnityEngine.Object;
using NUnit.Framework;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>
    /// Utility class for computing a checksum from a Unity object's serialized properties or an
    /// <see cref="sfBaseProperty"/>. Only syncable serialized properties are used to compute the checksum. References
    /// to non-asset Unity objects will have the same checksum as a null reference.
    /// </summary>
    public class sfChecksum
    {
        /// <summary>Gets the singleton instance.</summary>
        /// <returns>Singleton instance</returns>
        public static sfChecksum Get()
        {
            return m_instance;
        }
        private static sfChecksum m_instance = new sfChecksum();

        /// <summary>Filter for excluding dictionary fields from the checksum.</summary>
        /// <param name="name">Name of the dictionary field.</param>
        /// <returns>False to exclude the field from the checksum.</returns>
        public delegate bool Filter(string name);

        /// <summary>
        /// Delegate for computing updated checksum values.
        /// </summary>
        /// <param name="checksum1">First checksum</param>
        /// <param name="checksum2">Second checksum</param>
        private delegate void ChecksumDelegate(ref ulong checksum1, ref ulong checksum2);

        private sfLoader m_loader;

        /// <summary>Singleton constructor</summary>
        private sfChecksum()
        {
            m_loader = sfLoader.Get();
        }

        /// <summary>
        /// Constructor that uses a different loader for testing. Use this to set a loader that returns a mock
        /// <see cref="sfAssetInfo"/> for non-asset objects.
        /// </summary>
        /// <param name="loader">Loader for loading the asset info of referenced objects.</param>
        internal sfChecksum(sfLoader loader)
        {
            m_loader = loader;
        }

        /// <summary>
        /// Computes a fletcher 64 checksum for a uobject from its syncable serialized property values. References to
        /// non-asset Unity objects will have the same checksum as a null reference.
        /// </summary>
        /// <param name="uobject">Object to compute checksum for.</param>
        /// <returns>Checksum</returns>
        public ulong Fletcher64(UObject uobject)
        {
            if (uobject == null)
            {
                return 0;
            }
            SerializedObject so = new SerializedObject(uobject);
            return Fletcher64((ref ulong checksum1, ref ulong checksum2) =>
            {
                ChecksumSubProperties(so.GetIterator(), ref checksum1, ref checksum2);
            });
        }

        /// <summary>Computes a fletcher 64 checksum for a property.</summary>
        /// <param name="property">Property to compute checksum for.</param>
        /// <param name="filter">Filter for excluding dictionary fields from the checksum.</param>
        /// <returns>Checksum</returns>
        public ulong Fletcher64(sfBaseProperty property, Filter filter = null)
        {
            if (property == null)
            {
                return 0;
            }
            return Fletcher64((ref ulong checksum1, ref ulong checksum2) =>
            {
                Checksum(property, ref checksum1, ref checksum2, filter);
            });
        }

        /// <summary>
        /// Computes a fletcher 64 checksum by calling a delegate to compute two checksum values and combining them
        /// into one checksum.
        /// </summary>
        /// <param name="checksum">Checksum delegate</param>
        /// <returns>Checksum</returns>
        private ulong Fletcher64(ChecksumDelegate checksum)
        {
            // Fletcher-64 computes two 32-bit checksums and combines them to form a 64-bit checksum. The first is the modular
            // sum of each value, and the second is computed from the first by adding the first to the the second every time a
            // value is added to the first.

            // When both sums are 0, the algorithm cannot distinguish between varying lengths of zeros, so we start
            // the first sum at 1.
            ulong checksum1 = 1;
            ulong checksum2 = 0;
            unchecked
            {
                checksum(ref checksum1, ref checksum2);
            }
            return checksum1 + (checksum2 << 32);
        }

        /// <summary>
        /// Updates two checksum values by adding a uint to the first checksum, then adding the first checksum to the second.
        /// </summary>
        /// <param name="value">Value to add</param>
        /// <param name="checksum1">First checksum</param>
        /// <param name="checksum2">Second checksum</param>
        private void Checksum(uint value, ref ulong checksum1, ref ulong checksum2)
        {
            checksum1 += value;
            checksum1 %= (long)uint.MaxValue + 1;
            checksum2 += checksum1;
            checksum2 %= (long)uint.MaxValue + 1;
        }

        /// <summary>
        /// Computes updated checksum values from a serialized property's sub properties and the existing checksum
        /// values.
        /// </summary>
        /// <param name="sprop">Serialized property with sub properties to compute checksum from.</param>
        /// <param name="checksum1">First checksum</param>
        /// <param name="checksum2">Second checksum</param>
        private void ChecksumSubProperties(SerializedProperty sprop, ref ulong checksum1, ref ulong checksum2)
        {
            if (sprop.isArray)
            {
                Checksum((uint)sprop.arraySize, ref checksum1, ref checksum2);
            }
            foreach (SerializedProperty subProp in sfPropertyManager.Get().Iterate(sprop))
            {
                Checksum(subProp, ref checksum1, ref checksum2);
            }
        }

        /// <summary>
        /// Computes updated checksum values from a serialized property and the existing checksum values.
        /// </summary>
        /// <param name="sprop">Serialized property to compute checksum from.</param>
        /// <param name="checksum1">First checksum</param>
        /// <param name="checksum2">Second checksum</param>
        private void Checksum(SerializedProperty sprop, ref ulong checksum1, ref ulong checksum2)
        {
            Checksum((uint)sprop.propertyType, ref checksum1, ref checksum2);
            switch (sprop.propertyType)
            {
                case SerializedPropertyType.AnimationCurve: Checksum(sprop.animationCurveValue, ref checksum1, ref checksum2); break;
                case SerializedPropertyType.Boolean: Checksum(sprop.boolValue ? 1u : 0u, ref checksum1, ref checksum2); break;
                case SerializedPropertyType.Bounds: Checksum(sprop.boundsValue, ref checksum1, ref checksum2); break;
                case SerializedPropertyType.BoundsInt: Checksum(sprop.boundsIntValue, ref checksum1, ref checksum2); break;
                case SerializedPropertyType.Character:
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.LayerMask: Checksum((uint)sprop.intValue, ref checksum1, ref checksum2); break;
                case SerializedPropertyType.Color: Checksum(sprop.colorValue, ref checksum1, ref checksum2); break;
                case SerializedPropertyType.ExposedReference: Checksum(sprop.exposedReferenceValue, sprop, ref checksum1, ref checksum2); break;
                case SerializedPropertyType.Float: Checksum(sprop.doubleValue, ref checksum1, ref checksum2); break;
                case SerializedPropertyType.Generic: ChecksumSubProperties(sprop, ref checksum1, ref checksum2); break;
                case SerializedPropertyType.Gradient: Checksum(sfPropertyUtils.GetGradient(sprop), ref checksum1, ref checksum2); break;
                case SerializedPropertyType.Integer: Checksum(sprop.longValue, ref checksum1, ref checksum2); break;
                case SerializedPropertyType.ObjectReference: Checksum(sprop.objectReferenceValue, sprop, ref checksum1, ref checksum2); break;
                case SerializedPropertyType.Quaternion: Checksum(sprop.quaternionValue, ref checksum1, ref checksum2); break;
                case SerializedPropertyType.Rect: Checksum(sprop.rectValue, ref checksum1, ref checksum2); break;
                case SerializedPropertyType.RectInt: Checksum(sprop.rectIntValue, ref checksum1, ref checksum2); break;
                case SerializedPropertyType.String: Checksum(sprop.stringValue, ref checksum1, ref checksum2); break;
                case SerializedPropertyType.Vector2: Checksum(sprop.vector2Value, ref checksum1, ref checksum2); break;
                case SerializedPropertyType.Vector2Int: Checksum(sprop.vector2IntValue, ref checksum1, ref checksum2); break;
                case SerializedPropertyType.Vector3: Checksum(sprop.vector3Value, ref checksum1, ref checksum2); break;
                case SerializedPropertyType.Vector3Int: Checksum(sprop.vector3IntValue, ref checksum1, ref checksum2); break;
                case SerializedPropertyType.Vector4: Checksum(sprop.vector4Value, ref checksum1, ref checksum2); break;
            }
        }

        /// <summary>
        /// Computes updated checksum values from a <typeparamref name="T"/> struct and the existing checksum values.
        /// </summary>
        /// <param name="value">Value to compute checksum from.</param>
        /// <param name="checksum1">First checksum</param>
        /// <param name="checksum2">Second checksum</param>
        /// <typeparam name="T">Struct type</typeparam>
        private void Checksum<T>(T value, ref ulong checksum1, ref ulong checksum2) where T : struct
        {
            byte[] data = new byte[Marshal.SizeOf(typeof(T))];
            ksFixedDataWriter.WriteData(data, data.Length, 0, value);
            Checksum(data, ref checksum1, ref checksum2);
        }

        /// <summary>
        /// Computes updated checksum values from a <typeparamref name="T"/> struct array and the existing checksum 
        /// values.
        /// </summary>
        /// <param name="array">Array to compute checksum from.</param>
        /// <param name="checksum1">First checksum</param>
        /// <param name="checksum2">Second checksum</param>
        /// <typeparam name="T">Struct element type</typeparam>
        private void Checksum<T>(T[] array, ref ulong checksum1, ref ulong checksum2) where T : struct
        {
            if (array != null)
            {
                Checksum((uint)array.Length, ref checksum1, ref checksum2);
                for (int i = 0; i < array.Length; i++)
                {
                    Checksum(array[i], ref checksum1, ref checksum2);
                }
            }
        }

        /// <summary>
        /// Computes updated checksum values from a <see cref="ksMultiType"/> and the existing checksum values.
        /// </summary>
        /// <param name="value">Value to compute checksum from.</param>
        /// <param name="checksum1">First checksum</param>
        /// <param name="checksum2">Second checksum</param>
        private void Checksum(ksMultiType value, ref ulong checksum1, ref ulong checksum2)
        {
            Checksum((uint)value.Type, ref checksum1, ref checksum2);
            switch (value.Type)
            {
                case ksMultiType.Types.BYTE:
                case ksMultiType.Types.BOOL:
                case ksMultiType.Types.SHORT:
                case ksMultiType.Types.USHORT:
                case ksMultiType.Types.CHAR:
                case ksMultiType.Types.INT:
                case ksMultiType.Types.UINT: Checksum(value.AsUInt(), ref checksum1, ref checksum2); break;
                case ksMultiType.Types.LONG: Checksum(value.Long, ref checksum1, ref checksum2); break;
                case ksMultiType.Types.ULONG: Checksum(value.ULong, ref checksum1, ref checksum2); break;
                case ksMultiType.Types.FLOAT: Checksum(value.Float, ref checksum1, ref checksum2); break;
                case ksMultiType.Types.DOUBLE: Checksum(value.Double, ref checksum1, ref checksum2); break;
                case ksMultiType.Types.VECTOR2: Checksum(value.Vector2, ref checksum1, ref checksum2); break;
                case ksMultiType.Types.VECTOR3: Checksum(value.Vector3, ref checksum1, ref checksum2); break;
                case ksMultiType.Types.COLOR: Checksum(value.Color, ref checksum1, ref checksum2); break;
                case ksMultiType.Types.QUATERNION: Checksum(value.Quaternion, ref checksum1, ref checksum2); break;
                case ksMultiType.Types.STRING: Checksum(value.String, ref checksum1, ref checksum2); break;
                case ksMultiType.Types.BYTE_ARRAY: Checksum(value.ByteArray, ref checksum1, ref checksum2); break;
                case ksMultiType.Types.SHORT_ARRAY: Checksum(value.ShortArray, ref checksum1, ref checksum2); break;
                case ksMultiType.Types.USHORT_ARRAY: Checksum(value.UShortArray, ref checksum1, ref checksum2); break;
                case ksMultiType.Types.INT_ARRAY: Checksum(value.IntArray, ref checksum1, ref checksum2); break;
                case ksMultiType.Types.UINT_ARRAY: Checksum(value.UIntArray, ref checksum1, ref checksum2); break;
                case ksMultiType.Types.LONG_ARRAY: Checksum(value.LongArray, ref checksum1, ref checksum2); break;
                case ksMultiType.Types.ULONG_ARRAY: Checksum(value.ULongArray, ref checksum1, ref checksum2); break;
                case ksMultiType.Types.CHAR_ARRAY: Checksum(value.CharArray, ref checksum1, ref checksum2); break;
                case ksMultiType.Types.BOOL_ARRAY: Checksum(value.BoolArray, ref checksum1, ref checksum2); break;
                case ksMultiType.Types.VECTOR2_ARRAY: Checksum(value.Vector2Array, ref checksum1, ref checksum2); break;
                case ksMultiType.Types.VECTOR3_ARRAY: Checksum(value.Vector3Array, ref checksum1, ref checksum2); break;
                case ksMultiType.Types.COLOR_ARRAY: Checksum(value.ColorArray, ref checksum1, ref checksum2); break;
                case ksMultiType.Types.QUATERNION_ARRAY: Checksum(value.QuaternionArray, ref checksum1, ref checksum2); break;
                case ksMultiType.Types.STRING_ARRAY: Checksum(value.StringArray, ref checksum1, ref checksum2); break;
            }
        }

        /// <summary>
        /// Computes updated checksum values from an <see cref="AnimationCurve"/> and the existing checksum values.
        /// </summary>
        /// <param name="animationCurve">Animation curve to compute checksum from.</param>
        /// <param name="checksum1">First checksum</param>
        /// <param name="checksum2">Second checksum</param>
        private void Checksum(AnimationCurve animationCurve, ref ulong checksum1, ref ulong checksum2)
        {
            sfValueProperty property = sfPropertyUtils.FromAnimationCurve(animationCurve);
            Checksum(property.Value.ByteArray, ref checksum1, ref checksum2);
        }

        /// <summary>
        /// Computes updated checksum values from a <see cref="Gradient"/> and the existing checksum values.
        /// </summary>
        /// <param name="gradient">Gradient to compute checksum from.</param>
        /// <param name="checksum1">First checksum</param>
        /// <param name="checksum2">Second checksum</param>
        private void Checksum(Gradient gradient, ref ulong checksum1, ref ulong checksum2)
        {
            sfValueProperty property = sfPropertyUtils.FromGradient(gradient);
            Checksum(property.Value.ByteArray, ref checksum1, ref checksum2);
        }

        /// <summary>
        /// Computes updated checksum values from a uobject reference and the existing checksum values. If the object
        /// is not an asset, it will have the same checksum as a null reference.
        /// </summary>
        /// <param name="reference">Object reference to compute checksum from.</param>
        /// <param name="sprop">Serialized property containing the reference.</param>
        /// <param name="checksum1">First checksum</param>
        /// <param name="checksum2">Second checksum</param>
        private void Checksum(UObject reference, SerializedProperty sprop, ref ulong checksum1, ref ulong checksum2)
        {
            sfAssetInfo assetInfo = m_loader.GetAssetInfo(reference);
            if (assetInfo.IsValid)
            {
                Checksum(assetInfo.ClassName, ref checksum1, ref checksum2);
                Checksum(assetInfo.Path, ref checksum1, ref checksum2);
                if (assetInfo.IsSubasset)
                {
                    Checksum((uint)assetInfo.Index, ref checksum1, ref checksum2);
                }
            }
            else if (reference != null)
            {
                ksLog.Info(this, reference.GetType() + " " + sprop.propertyPath + " on " +
                    sprop.serializedObject.targetObject + " is not an asset and will be ignored in the checksum.");
            }
        }

        /// <summary>Computes updated checksum values from a string and the existing checksum values.</summary>
        /// <param name="str">String to compute checksum from.</param>
        /// <param name="checksum1">First checksum</param>
        /// <param name="checksum2">Second checksum</param>
        private void Checksum(string str, ref ulong checksum1, ref ulong checksum2)
        {
            if (str != null)
            {
                Checksum(System.Text.Encoding.UTF8.GetBytes(str), ref checksum1, ref checksum2);
            }
        }

        /// <summary>
        /// Computes updated checksum values from a string array and the existing checksum values.
        /// </summary>
        /// <param name="array">Array to compute checksum from.</param>
        /// <param name="checksum1">First checksum</param>
        /// <param name="checksum2">Second checksum</param>
        private void Checksum(string[] array, ref ulong checksum1, ref ulong checksum2)
        {
            if (array != null)
            {
                Checksum((uint)array.Length, ref checksum1, ref checksum2);
                for (int i = 0; i < array.Length; i++)
                {
                    Checksum(array[i], ref checksum1, ref checksum2);
                }
            }
        }

        /// <summary>Computes updated checksum values from a byte array and the existing checksum values.</summary>
        /// <param name="data">Byte array to compute checksum from.</param>
        /// <param name="checksum1">First checksum</param>
        /// <param name="checksum2">Second checksum</param>
        private void Checksum(byte[] data, ref ulong checksum1, ref ulong checksum2)
        {
            Checksum((uint)data.Length, ref checksum1, ref checksum2);
            uint val = 0;
            for (int i = 0; i < data.Length; i++)
            {
                int n = i % 4;
                if (n == 0)
                {
                    val = data[i];
                }
                else
                {
                    val += (uint)data[i] << (8 * n);
                }
                if (n == 3 || i == data.Length - 1)
                {
                    Checksum(val, ref checksum1, ref checksum2);
                }
            }
        }

        /* sfBaseProperty checksum functions */

        /// <summary>Computes updated checksum values from a property and the existing checksum values.</summary>
        /// <param name="property">Property to compute checksum for.</param>
        /// <param name="checksum1">First checksum</param>
        /// <param name="checksum2">Second checksum</param>
        /// <param name="filter">Filter for excluding dictionary fields.</param>
        private void Checksum(sfBaseProperty property, ref ulong checksum1, ref ulong checksum2, Filter filter)
        {
            Checksum((uint)property.Type, ref checksum1, ref checksum2);
            switch (property.Type)
            {
                case sfBaseProperty.Types.DICTIONARY:
                {
                    Checksum((sfDictionaryProperty)property, ref checksum1, ref checksum2, filter);
                    break;
                }
                case sfBaseProperty.Types.LIST:
                {
                    Checksum((sfListProperty)property, ref checksum1, ref checksum2, filter);
                    break;
                }
                case sfBaseProperty.Types.VALUE:
                {
                    Checksum(((sfValueProperty)property).Value, ref checksum1, ref checksum2);
                    break;
                }
                case sfBaseProperty.Types.REFERENCE:
                {
                    Checksum((sfReferenceProperty)property, ref checksum1, ref checksum2);
                    break;
                }
                case sfBaseProperty.Types.STRING:
                {
                    Checksum(((sfStringProperty)property).String, ref checksum1, ref checksum2);
                    break;
                }
            }
        }

        /// <summary>
        /// Computes updated checksum values from a dictionary property and the existing checksum values.
        /// </summary>
        /// <param name="dict">Dictionary to compute checksum for.</param>
        /// <param name="checksum1">First checksum</param>
        /// <param name="checksum2">Second checksum</param>
        /// <param name="filter">Filter for excluding dictionary fields.</param>
        private void Checksum(sfDictionaryProperty dict, ref ulong checksum1, ref ulong checksum2, Filter filter)
        {
            // Dictionary key order is not defined, so get the keys and sort them
            List<string> names = new List<string>();
            foreach (string key in dict.Keys)
            {
                // Use the filter to exclude keys we don't want
                if (filter == null || filter(key))
                {
                    names.Add(key);
                }
            }
            names.Sort();

            foreach (string name in names)
            {
                Checksum(name, ref checksum1, ref checksum2);
                Checksum(dict[name], ref checksum1, ref checksum2, filter);
            }
        }

        /// <summary>Computes updated checksum values from a list property and the existing checksum values.</summary>
        /// <param name="list">List to compute checksum for</param>
        /// <param name="checksum1">First checksum</param>
        /// <param name="checksum2">Second checksum</param>
        /// <param name="filter">Filter for excluding dictionary fields.</param>
        private void Checksum(sfListProperty list, ref ulong checksum1, ref ulong checksum2, Filter filter)
        {
            foreach (sfBaseProperty prop in list)
            {
                Checksum(prop, ref checksum1, ref checksum2, filter);
            }
        }

        /// <summary>
        /// Computes updated checksum values from a reference property and the existing checksum values.
        /// </summary>
        /// <param name="reference">Reference to compute checksum for.</param>
        /// <param name="checksum1">First checksum</param>
        /// <param name="checksum2">Second checksum</param>
        private void Checksum(sfReferenceProperty reference, ref ulong checksum1, ref ulong checksum2)
        {
            Checksum(reference.ObjectId, ref checksum1, ref checksum2);
        }
    }
}
