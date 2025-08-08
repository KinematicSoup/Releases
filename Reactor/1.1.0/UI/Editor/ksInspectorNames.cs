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
using System.Reflection;
using System.Collections.Generic;
using KS.Unity.Editor;
using UnityEditor;
using UnityEngine;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>
    /// Utility class for registering delegates to override the name of Unity objects in the inspector title bar.
    /// Delegates are registered with a type. When drawing an object's name in the inspector, it will first check the
    /// object's type for a delegate, and if none is found, it will check the base types recursively.
    /// </summary>
    public class ksInspectorNames
    {
        /// <summary>Gets the singleton instance.</summary>
        /// <returns>Singleton instance</returns>
        public static ksInspectorNames Get()
        {
            return m_instance;
        }
        private static ksInspectorNames m_instance = new ksInspectorNames();

        /// <summary>Delegate to get the name of an object to display in the inspector.</summary>
        /// <param name="obj">Object to get name for.</param>
        /// <returns>Name of the object. If null or empty, the default name is used.</returns>
        public delegate string NameGetter(UnityEngine.Object obj);

        /// <summary>Delegate to get the name of an object to display in the inspector.</summary>
        /// <typeparam name="T">Object type</typeparam>
        /// <param name="obj">Object to get name for.</param>
        /// <returns>Name of the object. If null or empty, the default name is used.</returns>
        public delegate string NameGetter<T>(T obj) where T : UnityEngine.Object;

        private Dictionary<Type, NameGetter> m_nameHandlers = new Dictionary<Type, NameGetter>();
        private Dictionary<Type, string> m_inspectorTitles;
        private Dictionary<Type, string> m_defaultNames;
        private ksReflectionObject m_roHeaderMethods;

        /// <summary>Constructor</summary>
        private ksInspectorNames()
        {
            // Unity stores a mapping of object types to custom inspector names in a private dictionary. We get a
            // reference to this dictionary using reflection so we can write to it.
            m_inspectorTitles = new ksReflectionObject(typeof(ObjectNames))
                .GetNestedType("InspectorTitles")
                .GetField("s_InspectorTitles")
                .GetValue() as Dictionary<Type, string>;

            // Create a copy of the dictionary with the default names before we start modifying them.
            m_defaultNames = new Dictionary<Type, string>(m_inspectorTitles);

            // Unity has a private list of delegates to call to draw header items before drawing the object name in
            // component inspector headers. We add our own delegate to this list with reflection so we can get a
            // callback right before the object name is drawn and use it to change the type name in the type names map.
            m_roHeaderMethods = new ksReflectionObject(typeof(EditorGUIUtility)).GetField("s_EditorHeaderItemsMethods");
            if (!RegisterHeaderMethod())
            {
                // The list of delegates isn't created yet. We must wait until it is created to add our delegate.
                EditorApplication.update += Update;
            }
        }

        /// <summary>
        /// Adds a <paramref name="handler"/> to override the inspector name for objects of the given
        /// <paramref name="type"/>.
        /// </summary>
        /// <param name="type">Type to register <paramref name="handler"/> for.</param>
        /// <param name="handler">Handler for getting the object name.</param>
        public void Add(Type type, NameGetter handler)
        {
            if (handler == null)
            {
                m_nameHandlers.Remove(type);
            }
            else
            {
                m_nameHandlers[type] = handler;
            }
        }

        /// <summary>
        /// Adds a <paramref name="handler"/> to override the inspector name for objects of type 
        /// <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Type to register <paramref name="handler"/> for.</typeparam>
        /// <param name="handler">Handler for getting the object name.</param>
        public void Add<T>(NameGetter<T> handler) where T : UnityEngine.Object
        {
            if (handler == null)
            {
                m_nameHandlers.Remove(typeof(T));
            }
            else
            {
                m_nameHandlers[typeof(T)] = (UnityEngine.Object obj) => handler((T)obj);
            }
        }

        /// <summary>
        /// Removes the handler for the given <paramref name="type"/>. Objects of this <paramref name="type"/> will use
        /// their default name in the inspector.
        /// </summary>
        /// <param name="type">Type to remove handler for.</param>
        public void Remove(Type type)
        {
            m_nameHandlers.Remove(type);
        }

        /// <summary>
        /// Removes the handler for type <typeparamref name="T"/>. Objects of this type will use their default name in
        /// the inspector.
        /// </summary>
        /// <typeparam name="T">Type to remove handler for.</typeparam>
        public void Remove<T>() where T : UnityEngine.Object
        {
            m_nameHandlers.Remove(typeof(T));
        }

        /// <summary>Gets the default name for a <paramref name="type"/>.</summary>
        /// <param name="type">Type to get default name for.</param>
        /// <returns>Default name</returns>
        public string GetDefaultName(Type type)
        {
            string name;
            if (!m_defaultNames.TryGetValue(type, out name))
            {
                name = ObjectNames.NicifyVariableName(type.Name);
            }
            return name;
        }

        /// <summary>Gets the default name for type <typeparamref name="T"/>.</summary>
        /// <typeparam name="T">Type to get default name for.</typeparam>
        /// <returns>Default name</returns>
        public string GetDefaultName<T>() where T : UnityEngine.Object
        {
            return GetDefaultName(typeof(T));
        }

        /// <summary>Adds our callback to Unity's list of header callbacks once the list is created.</summary>
        private void Update()
        {
            if (RegisterHeaderMethod())
            {
                EditorApplication.update -= Update;
            }
        }

        /// <summary>
        /// Tries to insert our callback to change the object name into Unity's list of header callbacks.
        /// </summary>
        /// <returns>
        /// False if the callback could not be added because the list of callbacks wasn't created yet, so we need to
        /// try again later. True if the callback was added or could not be added because reflection failed and we
        /// should not try again.
        /// </returns>
        private bool RegisterHeaderMethod()
        {
            if (m_roHeaderMethods.IsVoid)
            {
                return true;
            }
            if (m_roHeaderMethods.GetValue() == null)
            {
                return false;
            }
            Type delegateType = new ksReflectionObject(typeof(EditorGUIUtility))
                .GetNestedType("HeaderItemDelegate").Type;
            if (delegateType == null)
            {
                return true;
            }
            m_roHeaderMethods.GetMethod("Add", paramTypes: new Type[] { delegateType })
                .Invoke(Delegate.CreateDelegate(delegateType, this, "HandleName"));
            return true;
        }

        /// <summary>
        /// Calls the <see cref="NameGetter"/> for the first target in <paramref name="targets"/> if there is one to
        /// get the name of the target object, and modifies Unity's map of type names with the name to display. We
        /// insert this function into Unity's private list of callbacks to call when drawing component headers using
        /// reflection.
        /// </summary>
        /// <param name="rectangle">Unused because we don't draw anything.</param>
        /// <param name="targets">Targets to get name for.</param>
        /// <returns>False because we don't draw anything.</returns>
        private bool HandleName(Rect rectangle, UnityEngine.Object[] targets)
        {
            if (targets == null || targets.Length == 0)
            {
                return false;
            }

            // Find and call the name handler for this type to get the object name.
            string name = null;
            UnityEngine.Object target = targets[0];
            Type type = target.GetType();
            while (type != null)
            {
                NameGetter handler;
                if (m_nameHandlers.TryGetValue(type, out handler))
                {
                    name = handler(target);
                    // Show the default name if some of the targets have different names.
                    for (int i = 1; i < targets.Length; i++)
                    {
                        if (handler(targets[i]) != name)
                        {
                            name = null;
                            break;
                        }
                    }
                    break;
                }
                // If the type doesn't have a handler, check the base types.
                type = type.BaseType;
            }

            // Modify Unity's map of type names with the new name.
            if (!string.IsNullOrEmpty(name) || m_defaultNames.TryGetValue(target.GetType(), out name))
            {
                m_inspectorTitles[target.GetType()] = name;
            }
            else
            {
                m_inspectorTitles.Remove(target.GetType());
            }
            return false;
        }
    }
}
