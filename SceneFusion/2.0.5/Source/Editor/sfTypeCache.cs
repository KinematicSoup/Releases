/*
KINEMATICSOUP CONFIDENTIAL
 Copyright(c) 2014-2026 KinematicSoup Technologies Incorporated 
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
using System.Reflection;
using UnityEngine;
using KS.SF.Reactor;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>
    /// Maps UnityEngine.Object types strings to types and component type names (without name spaces) to lists of
    /// non-abstract types that may be assignable for that type name, with one entry in the list for each component type
    /// with the same name but a different namespace. We need this because Unity doesn't include the namespace when it
    /// gives us a reference property's type string.
    /// </summary>
    public class sfTypeCache : IEnumerable<Type>
    {
        /// <summary>Assemblies that contain scripts</summary>
        private static readonly string[] SCRIPT_ASSEMBLIES = { "Assembly-CSharp", "Assembly-CSharp-firstpass" };

        /// <summary></summary>
        /// <returns>singleton instance</returns>
        public static sfTypeCache Get()
        {
            return m_instance;
        }
        private static sfTypeCache m_instance = new sfTypeCache();

        // Keys are type names which is all Unity gives us. Values are lists of type-key-value pairs where in each
        // pair the key is a type with the key type name, and pair.value is a non-abstract type that pair.key is
        // assignable from.
        private Dictionary<string, List<KeyValuePair<Type, Type>>> m_componentTypes = new Dictionary<string, List<KeyValuePair<Type, Type>>>();
        private Dictionary<string, Type> m_cache = new Dictionary<string, Type>();
        private HashSet<Type> m_scriptTypes = new HashSet<Type>();

        /// <summary>All Monobehaviour and serializable types in Unity script assemblies.</summary>
        public HashSet<Type> ScriptTypes
        {
            get { return m_scriptTypes; }
        }

        /// <summary>Singleton constructor. </summary>
        private sfTypeCache()
        {
            Load();
        }

        /// <summary>
        /// Gets an array of types that may be assignable to a reference property with the given type name.
        /// </summary>
        /// <param name="name">name of type.</param>
        /// <returns>types that may be assignable for that type name.</returns>
        public Type[] GetComponentTypes(string name)
        {
            List<KeyValuePair<Type, Type>> typeList;
            if (!m_componentTypes.TryGetValue(name, out typeList))
            {
                return new Type[0];
            }
            Type[] types = new Type[typeList.Count];
            for (int i = 0; i < types.Length; i++)
            {
                types[i] = typeList[i].Value;
            }
            return types;
        }

        /// <summary>Gets a UnityEngine.Object type by name (including namespace).</summary>
        /// <param name="name">name of type to get including namespace.</param>
        /// <returns>type with the given name, or null if not found.</returns>
        public Type Load(string name)
        {
            Type type;
            m_cache.TryGetValue(name, out type);
            return type;
        }

        /// <summary></summary>
        /// <returns>enumerator for the cache.</returns>
        public IEnumerator<Type> GetEnumerator()
        {
            return m_cache.Values.GetEnumerator();
        }

        /// <summary></summary>
        /// <returns>enumerator for the cache.</returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>Checks if an assembly is one of the Unity script assemblies.</summary>
        /// <param name="assembly">assembly to check.</param>
        /// <returns>true if the assembly is one of the Unity script assemblies.</returns>
        private bool IsScriptAssembly(Assembly assembly)
        {
            foreach (string name in SCRIPT_ASSEMBLIES)
            {
                if (assembly.FullName.StartsWith(name + ","))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Constructs the map by iterating all types in all assemblies.</summary>
        private void Load()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            Type componentType = typeof(Component);
            Type unityObjectType = typeof(UnityEngine.Object);

            // disable obsolete type warning
#pragma warning disable 0618
            // Some Unity component classes pretend to be abstract when they aren't. We need to treat them like
            // they're abstract.
            HashSet<Type> fauxAbstractTypes = new HashSet<Type>();
            fauxAbstractTypes.Add(typeof(Behaviour));
            fauxAbstractTypes.Add(typeof(Renderer));
            fauxAbstractTypes.Add(typeof(AudioBehaviour));
            fauxAbstractTypes.Add(typeof(Joint));
            fauxAbstractTypes.Add(typeof(Collider));
            fauxAbstractTypes.Add(typeof(Collider2D));
            fauxAbstractTypes.Add(typeof(Joint2D));
            fauxAbstractTypes.Add(typeof(AnchoredJoint2D));
            fauxAbstractTypes.Add(typeof(PhysicsUpdateBehaviour2D));
            fauxAbstractTypes.Add(typeof(Effector2D));
            fauxAbstractTypes.Add(typeof(GridLayout));
#pragma warning restore 0618

            List<KeyValuePair<Type, Type>> types = new List<KeyValuePair<Type, Type>>();
            types.Add(new KeyValuePair<Type,Type>(componentType, typeof(Transform)));
            m_componentTypes[componentType.Name] = types;
            m_cache[unityObjectType.ToString()] = unityObjectType;

            foreach (Assembly assembly in assemblies)
            {
                try
                { 
                    bool isScriptAssembly = IsScriptAssembly(assembly);
                    foreach (Type type in assembly.GetTypes())
                    {
                        if (type.IsAbstract || fauxAbstractTypes.Contains(type) || 
                            type.IsDefined(typeof(ObsoleteAttribute), false))
                        {
                            continue;
                        }
                        if (type.IsSubclassOf(componentType))
                        {
                            // Uncomment this code to test for faux-abstract classes. You'll get a log message telling you
                            // the component couldn't be added because it is abstract for each faux-abstract class.

                            //GameObject gameObject = new GameObject();
                            //gameObject.AddComponent(type);
                            //GameObject.DestroyImmediate(gameObject);

                            if (!m_componentTypes.TryGetValue(type.Name, out types))
                            {
                                types = new List<KeyValuePair<Type, Type>>();
                                m_componentTypes[type.Name] = types;
                            }
                            types.Add(new KeyValuePair<Type, Type>(type, type));
                            Type baseType = type.BaseType;
                            while (baseType != null && baseType.IsSubclassOf(componentType))
                            {
                                if (!m_componentTypes.TryGetValue(baseType.Name, out types))
                                {
                                    types = new List<KeyValuePair<Type, Type>>();
                                    m_componentTypes[baseType.Name] = types;
                                }
                                bool found = false;
                                foreach (KeyValuePair<Type, Type> pair in types)
                                {
                                    if (pair.Key == baseType)
                                    {
                                        found = true;
                                        break;
                                    }
                                }
                                if (found)
                                {
                                    break;
                                }
                                types.Add(new KeyValuePair<Type, Type>(baseType, type));
                                baseType = baseType.BaseType;
                            }
                        }

                        if (type.IsSubclassOf(unityObjectType))
                        {
                            m_cache[type.ToString()] = type;
                        }
                        if (isScriptAssembly && (type.IsSubclassOf(typeof(MonoBehaviour)) ||
                            type.IsDefined(typeof(SerializableAttribute), false)) && !type.ContainsGenericParameters)
                        {
                            Type t = type;
                            while (m_scriptTypes.Add(t))
                            {
                                t = t.BaseType;
                                if (t == null || !IsScriptAssembly(t.Assembly))
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ksLog.Error(this, "Error loading assembly: " + assembly.GetName().Name, ex);
                }
            }
        }
    }
}
