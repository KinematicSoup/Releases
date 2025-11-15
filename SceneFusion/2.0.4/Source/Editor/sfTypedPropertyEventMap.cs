using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>Maps property names to sfTypeEventMaps for delegates of type T.</summary>
    public class sfTypedPropertyEventMap<T> where T : class
    {
        private Dictionary<string, sfTypeEventMap<T>> m_map = new Dictionary<string, sfTypeEventMap<T>>();

        /// <summary>Adds a handler for a type and property name.</summary>
        /// <param name="name">name of property to add handler for.</param>
        /// <param name="handler">handler to add.</param>
        public void Add<Type>(string name, T handler)
        {
            GetOrCreateTypeMap(name).Add<Type>(handler);
        }

        /// <summary>Removes a handler for a type and property name.</summary>
        /// <param name="name">name of property to remove handler for.</param>
        /// <param name="handler">handler to remove.</param>
        public void Remove<Type>(string name, T handler)
        {
            sfTypeEventMap<T> typeMap;
            if (m_map.TryGetValue(name, out typeMap))
            {
                typeMap.Remove<Type>(handler);
            }
        }

        /// <summary>Gets the handlers for the given type and property name.</summary>
        /// <param name="type"></param>
        /// <param name="name">name of property.</param>
        /// <returns>handlers</returns>
        public T GetHandlers(Type type, string name)
        {
            sfTypeEventMap<T> typeMap;
            if (m_map.TryGetValue(name, out typeMap))
            {
                return typeMap.GetHandlers(type);
            }
            return null;
        }

        /// <summary>
        /// Get the handlers for a type and property. If the property is a subproperty, eg. A.B.C, will get the handlers
        /// for C, B, and A.
        /// </summary>
        public T GetHandlers(Type type, sfBaseProperty property)
        {
            T handlers = null;
            while (property.ParentProperty != null)
            {
                if (!string.IsNullOrEmpty(property.Name))
                {
                    sfTypeEventMap<T> typeMap;
                    if (m_map.TryGetValue(property.Name, out typeMap))
                    {
                        T typeHandlers = typeMap.GetHandlers(type);
                        if (typeHandlers != null)
                        {
                            if (handlers == null)
                            {
                                handlers = typeHandlers;
                            }
                            else
                            {
                                handlers = (T)(object)Delegate.Combine(
                                        (Delegate)(object)handlers, (Delegate)(object)typeHandlers);
                            }
                        }
                    }
                }
                property = property.ParentProperty;
            }
            return handlers;
        }

        /// <summary>Gets the type event map for the given name. Creates one if it does not already exist.</summary>
        /// <param name="name">name of property.</param>
        /// <returns></returns>
        private sfTypeEventMap<T> GetOrCreateTypeMap(string name)
        {
            sfTypeEventMap<T> typeMap;
            if (!m_map.TryGetValue(name, out typeMap))
            {
                typeMap = new sfTypeEventMap<T>();
                m_map[name] = typeMap;
            }
            return typeMap;
        }
    }
}
