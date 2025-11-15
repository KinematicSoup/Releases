using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using KS.Compression;
using KS.SF.Reactor;
using KS.SceneFusion;
using KS.SceneFusion2.Client;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>
    /// A map of property type names to sets of property names. For root-level properties, the type is the serialized
    /// object type, and for sub properties, it is the type of the container struct/class. Types are stored as string
    /// names instead of Types as some types only exist in C++ and all we can get from the serialized property is the
    /// type name.
    /// </summary>
    public class sfTypePropertyNameMap
    {
        // Maps type names to sets of property names
        private Dictionary<string, HashSet<string>> m_map = new Dictionary<string, HashSet<string>>();

        /// <summary>Adds a property to the set.</summary>
        /// <typeparam name="T">Type containing the property</typeparam>
        /// <param name="name">Property name.</param>
        public void Add<T>(string name)
        {
            Add(typeof(T).FullName, name);
        }

        /// <summary>Adds a property to the set.</summary>
        /// <param name="typeName">Name of the type containing the property</param>
        /// <param name="propertyName">Property name</param>
        public void Add(string typeName, string propertyName)
        {
            HashSet<string> properties;
            if (!m_map.TryGetValue(typeName, out properties))
            {
                properties = new HashSet<string>();
                m_map[typeName] = properties;
            }
            properties.Add(propertyName);
        }

        /// <summary>Removes a property from the set.</summary>
        /// <typeparam name="T">Type containing the property</typeparam>
        /// <param name="name">Property name.</param>
        public void Remove<T>(string name)
        {
            Remove(typeof(T).FullName, name);
        }

        /// <summary>Removes a property from the set.</summary>
        /// <param name="typeName">Name of the type containing the property</param>
        /// <param name="propertyName">Property name</param>
        public void Remove(string typeName, string propertyName)
        {
            HashSet<string> properties;
            if (m_map.TryGetValue(typeName, out properties))
            {
                properties.Remove(typeName);
                if (properties.Count == 0)
                {
                    m_map.Remove(propertyName);
                }
            }
        }

        /// <summary>
        /// Gets the set of all property names contained in this set for a type, including properties from base types.
        /// Returns null if there are no properties for the type.
        /// </summary>
        /// <param name="type">type to get properties for.</param>
        /// <returns>set of property names for the type, or null if none were found.</returns>
        public HashSet<string> GetProperties(Type type)
        {
            HashSet<string> properties = null;
            bool copied = false;
            while (type != null)
            {
                HashSet<string> set;
                if (m_map.TryGetValue(type.FullName, out set))
                {
                    if (properties == null)
                    {
                        properties = set;
                    }
                    else
                    {
                        if (!copied)
                        {
                            copied = true;
                            properties = new HashSet<string>(properties);
                        }
                        foreach (string prop in set)
                        {
                            properties.Add(prop);
                        }
                    }
                }
                type = type.BaseType;
            }
            return properties;
        }

        /// <summary>Gets the set of all property names contained in this set for a type.</summary>
        /// <param name="typeName">typeName to get properties for.</param>
        /// <returns>set of property names for the type, or null if none were found.</returns>
        public HashSet<string> GetProperties(string typeName)
        {
            HashSet<string> properties;
            m_map.TryGetValue(typeName, out properties);
            return properties;
        }
    }
}
