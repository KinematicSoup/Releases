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
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;

namespace KS.Reactor.Client.Unity
{
    /// <summary>Cloning and reflection utility functions.</summary>
    public class ksUnityReflectionUtils
    {
        /// <summary>Delegate for filtering which fields get copied.</summary>
        /// <param name="field">Field</param>
        /// <returns>True to copy the field. False to skip it.</returns>
        public delegate bool Filter(FieldInfo field);

        private static HashSet<Type> m_builtInSerializedTypes = new HashSet<Type>();

        private static readonly string LOG_CHANNEL = typeof(ksUnityReflectionUtils).ToString();

        /// <summary>Static initialization.</summary>
        static ksUnityReflectionUtils()
        {
            // Unity doesn't provide a way of checking if a type can be serialized and their types don't follow their
            // own serialization rules, so there's no easy way to check if a Unity type can be serialized. This is a
            // list of Unity serializable types that do not derive from UnityEngine.Object. It may be incomplete; Unity
            // doesn't maintain a list of serializable types in their documentation.
            m_builtInSerializedTypes.Add(typeof(Vector2));
            m_builtInSerializedTypes.Add(typeof(Vector3));
            m_builtInSerializedTypes.Add(typeof(Vector4));
            m_builtInSerializedTypes.Add(typeof(Vector2Int));
            m_builtInSerializedTypes.Add(typeof(Vector3Int));
            m_builtInSerializedTypes.Add(typeof(Quaternion));
            m_builtInSerializedTypes.Add(typeof(Color));
            m_builtInSerializedTypes.Add(typeof(Color32));
            m_builtInSerializedTypes.Add(typeof(LayerMask));
            m_builtInSerializedTypes.Add(typeof(Rect));
            m_builtInSerializedTypes.Add(typeof(RectInt));
            m_builtInSerializedTypes.Add(typeof(Matrix4x4));
            m_builtInSerializedTypes.Add(typeof(AnimationCurve));
            m_builtInSerializedTypes.Add(typeof(Keyframe));
        }

        /// <summary>
        /// Creates a copy of a component on another game object using reflection. If the script implements
        /// <see cref="ksICloneableScript"/>, copies it by calling <see cref="ksICloneableScript.CopyTo(Component)"/>
        /// instead of using reflection. When using reflection, we try to follow the same rules Unity uses when copying
        /// objects with <see cref="UnityEngine.Object.Instantiate(UnityEngine.Object)"/>, but Unity does not expose
        /// this functionality for copying scripts so we had to try recreate their rules and there may be differences.
        /// </summary>
        /// <typeparam name="T">Type of component to clone.</typeparam>
        /// <param name="component">Component to copy.</param>
        /// <param name="gameObject">Game object to add the copy to.</param>
        /// <returns>The copied component.</returns>
        public static T CloneScript<T>(T component, GameObject gameObject) where T : Component
        {
            Type type = component.GetType();
            T clone = (T)gameObject.AddComponent(type);
            ksICloneableScript cloneable = component as ksICloneableScript;
            if (cloneable != null)
            {
                cloneable.CopyTo(clone);
            }
            else
            {
                Copy(type, component, clone, true, IsUnitySerialized);
            }
            return clone;
        }

        /// <summary>Clones an object using reflection.</summary>
        /// <typeparam name="T">Type of the object being cloned.</typeparam>
        /// <param name="src">Object to clone.</param>
        /// <param name="deepCopy">
        /// If true, referenced objects are also copied. References to <see cref="UnityEngine.Object"/>s never create
        /// copies and always reference the original object.
        /// </param>
        /// <param name="filter">
        /// Optional filter for excluding fields from being copied. If null, all fields are copied.
        /// </param>
        /// <returns>Cloned object.</returns>
        public static T Clone<T>(T src, bool deepCopy = false, Filter filter = null) where T : class
        {
            return Clone((object)src, deepCopy, filter) as T;
        }

        /// <summary>Clones an object using reflection.</summary>
        /// <param name="src">Object to clone.</param>
        /// <param name="deepCopy">
        /// If true, referenced objects are also copied. References to <see cref="UnityEngine.Object"/>s never create
        /// copies and always reference the original object.
        /// </param>
        /// <param name="filter">
        /// Optional filter for excluding fields from being copied. If null, all fields are copied.
        /// </param>
        /// <returns>Cloned object.</returns>
        public static object Clone(object src, bool deepCopy = false, Filter filter = null)
        {
            try
            {
                if (src == null)
                {
                    return null;
                }
                Type type = src.GetType();
                if (type.IsValueType || src is string)
                {
                    return src;
                }
                if (type.IsArray)
                {
                    Array array = (Array)src;
                    Type elementType = array.GetType().GetElementType();
                    Array copy = Array.CreateInstance(elementType, array.Length);
                    if (deepCopy && !elementType.IsValueType)
                    {
                        for (int i = 0; i < array.Length; i++)
                        {
                            copy.SetValue(Clone(array.GetValue(i), true, filter), i);
                        }
                    }
                    else
                    {
                        Array.Copy(array, copy, array.Length);
                    }
                    return copy;
                }
                if (typeof(IList).IsAssignableFrom(type))
                {
                    foreach (Type itype in type.GetInterfaces())
                    {
                        if (itype.IsGenericType && itype.GetGenericTypeDefinition() == typeof(IList<>))
                        {
                            IList list = (IList)src;
                            IList copy = (IList)Activator.CreateInstance(type);
                            Type elementType = itype.GetGenericArguments()[0];
                            for (int i = 0; i < list.Count; i++)
                            {
                                copy.Add(deepCopy ? Clone(list[i], true, filter) : list[i]);
                            }
                            return copy;
                        }
                    }
                }
                if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                {
                    // Don't clone unity objects.
                    return src;
                }
                object dest = Activator.CreateInstance(type, true);
                Copy(type, src, dest, deepCopy, filter);
                return dest;
            }
            catch (Exception e)
            {
                if (e is MissingMethodException)
                {
                    ksLog.Warning(LOG_CHANNEL, src.GetType().Name + 
                        " has no default constructor and cannot be cloned.");
                }
                else
                {
                    ksLog.Error(LOG_CHANNEL, "Error cloning object.", e);
                }
                return src;
            }
        }

        /// <summary>Copies field values from one object onto another using reflection.</summary>
        /// <typeparam name="T">Type of the object to copy values from.</typeparam>
        /// <param name="src">Object to copy values from.</param>
        /// <param name="dest">Object to copy values to.</param>
        /// <param name="deepCopy">
        /// If true, referenced objects are also copied. References to <see cref="UnityEngine.Object"/>s never create
        /// copies and always reference the original object.
        /// </param>
        /// <param name="filter">
        /// Optional filter for excluding fields from being copied. If null, all fields are copied.
        /// </param>
        public static void Copy<T>(T src, T dest, bool deepCopy = false, Filter filter = null)
        {
            Copy(typeof(T), src, dest, deepCopy, filter);
        }

        /// <summary>Copies field values from one object onto another using reflection.</summary>
        /// <param name="type">
        /// Type of the object being copied. <paramref name="src"/> and <paramref name="dest"/> must be instances of
        /// this type or a derived type. Derived type fields will not be copied.
        /// </param>
        /// <param name="src">Object to copy values from.</param>
        /// <param name="dest">Object to copy values to.</param>
        /// <param name="deepCopy">
        /// If true, referenced objects are also copied. References to <see cref="UnityEngine.Object"/>s never create
        /// copies and always reference the original object.
        /// </param>
        /// <param name="filter">
        /// Optional filter for excluding fields from being copied. If null, all fields are copied.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="type"/>, <paramref name="src"/>, or <paramref name="dest"/> are null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="src"/> or <paramref name="dest"/> are not instances of <paramref name="type"/> or
        /// a derived type.
        /// </exception>
        public static void Copy(Type type, object src, object dest, bool deepCopy = false, Filter filter = null)
        {
            if (type == null)
            {
                throw new ArgumentNullException("type");
            }
            if (src == null)
            {
                throw new ArgumentNullException("src");
            }
            if (dest == null)
            {
                throw new ArgumentNullException("dest");
            }
            if (!type.IsAssignableFrom(src.GetType()))
            {
                throw new ArgumentException("src  '" + type.Name + "' must be assignable from '" + src.GetType().Name + "'.");
            }
            if (!type.IsAssignableFrom(dest.GetType()))
            {
                throw new ArgumentException("dest '" + type.Name + "' must be assignable from '" + dest.GetType().Name + "'.");
            }
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            while (type != null)
            {
                foreach (FieldInfo field in type.GetFields(flags))
                {
                    if (filter != null && !filter(field))
                    {
                        continue;
                    }
                    object value = field.GetValue(src);
                    if (deepCopy && value != null && !value.GetType().IsValueType)
                    {
                        value = Clone(value, true, filter);
                    }
                    field.SetValue(dest, value);
                }
                type = type.BaseType;
            }
        }

        /// <summary>
        /// Checks if a field will be serialized by Unity. Unity does not expose a function for this, so we've tried to 
        /// recreate Unity's functionality. It may not be 100% accurate.
        /// </summary>
        /// <param name="field">Field to check.</param>
        /// <returns>True if Unity will serialize the field.</returns>
        public static bool IsUnitySerialized(FieldInfo field)
        {
            return (field.IsPublic || field.GetCustomAttribute<SerializeField>() != null) && 
                field.GetCustomAttribute<NonSerializedAttribute>() == null && IsUnitySerialized(field.FieldType);
        }

        /// <summary>
        /// Checks if a type will be serialized by Unity. Unity does not expose a function for this, so we've tried to 
        /// recreate Unity's functionality. It may not be 100% accurate.
        /// </summary>
        /// <param name="type">Type to check.</param>
        /// <returns>True if Unity will serialize the type.</returns>
        public static bool IsUnitySerialized(Type type)
        {
            if (type.IsGenericType)
            {
                if (type.GetGenericTypeDefinition() == typeof(List<>))
                {
                    Type elementType = type.GetGenericArguments()[0];
                    return !elementType.IsGenericType && !elementType.IsArray && IsUnitySerialized(elementType);
                }
                return false;
            }
            if (type.IsArray)
            {
                Type elementType = type.GetElementType();
                return !elementType.IsGenericType && !elementType.IsArray && type.GetArrayRank() == 1 &&
                    IsUnitySerialized(elementType);
            }
            return type.IsPrimitive || m_builtInSerializedTypes.Contains(type) || type.IsEnum ||
                typeof(UnityEngine.Object).IsAssignableFrom(type) || 
                type.GetCustomAttribute<SerializableAttribute>() != null;
        }
    }
}
