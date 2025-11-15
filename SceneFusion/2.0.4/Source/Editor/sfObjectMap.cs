using System;
using System.Collections.Generic;
using UnityEngine;
using KS.SceneFusion2.Client;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>Maps sfObjects to uobjects and vice versa.</summary>
    public class sfObjectMap : IEnumerable<KeyValuePair<sfObject, UObject>>
    {
        /// <summary></summary>
        /// <returns>singleton instance.</returns>
        public static sfObjectMap Get()
        {
            return m_instance;
        }
        private static sfObjectMap m_instance = new sfObjectMap();

        private Dictionary<UObject, sfObject> m_uToSFObjectMap = new Dictionary<UObject, sfObject>();
        private Dictionary<sfObject, UObject> m_sfToUObjectMap = new Dictionary<sfObject, UObject>();

        /// <summary>Checks if a uobject is in the map.</summary>
        /// <param name="uobj"></param>
        /// <returns>true if the uobject is in the map.</returns>
        public bool Contains(UObject uobj)
        {
            return (object)uobj != null && m_uToSFObjectMap.ContainsKey(uobj);
        }

        /// <summary>Checks if an sfObject is in the map.</summary>
        /// <param name="obj"></param>
        /// <returns>true if the object is in the map.</returns>
        public bool Contains(sfObject obj)
        {
            return obj != null && m_sfToUObjectMap.ContainsKey(obj);
        }

        /// <summary>Gets the sfObject for a uobject, or null if the uobject has no sfObject.</summary>
        /// <param name="uobj"></param>
        /// <returns>sfObject for the uobject, or null if none was found.</returns>
        public sfObject GetSFObject(UObject uobj)
        {
            sfObject obj;
            if ((object)uobj == null || !m_uToSFObjectMap.TryGetValue(uobj, out obj))
            {
                return null;
            }
            return obj;
        }

        /// <summary>
        /// Gets the sfObject for a uobject, or creates one with an empty dictionary property and adds it to the map if none
        /// was found.
        /// </summary>
        /// <param name="uobj"></param>
        /// <param name="type">type of object to create.</param>
        /// <param name="flags">flags to create object with.</param>
        /// <returns>sfObject for the uobject.</returns>
        public sfObject GetOrCreateSFObject(
            UObject uobj,
            string type, 
            sfObject.ObjectFlags flags = sfBaseObject<sfObject>.ObjectFlags.NONE)
        {
            if (uobj == null)
            {
                return null;
            }
            sfObject obj;
            if (m_uToSFObjectMap.TryGetValue(uobj, out obj))
            {
                // When uobjects are destroyed and recreated via undo, the recreated script is actually a new object
                // that pretends to be the old object (has the same hashcode and Equals(object) returns true with the
                // old object). We detect if the current uobject in the map is a different object and replace it with
                // the new one.
                UObject current;
                m_sfToUObjectMap.TryGetValue(obj, out current);
                if (!object.ReferenceEquals(uobj, current))
                {
                    m_sfToUObjectMap[obj] = uobj;
                }

                return obj;
            }
            obj = new sfObject(type, new sfDictionaryProperty(), flags);
            Add(obj, uobj);
            return obj;
        }

        /// <summary>Gets the uobject for an sfObject, or null if the sfObject has no uobject.</summary>
        /// <param name="obj">obj to get uobject for.</param>
        /// <returns>uobject for the sfObject.</returns>
        public UObject GetUObject(sfObject obj)
        {
            UObject uobj;
            if (obj == null || !m_sfToUObjectMap.TryGetValue(obj, out uobj))
            {
                return null;
            }
            return uobj;
        }

        /// <summary>Gets the uobject for an sfObject cast to T.</summary>
        /// <error>sfObject obj to get uobject for.</error>
        /// <returns>uobject for the sfObject, or null if not found or not of type T.</returns>
        public T Get<T>(sfObject obj) where T : UObject
        {
            return GetUObject(obj) as T;
        }

        /// <summary>Adds a mapping between a uobject and an sfObject.</summary>
        /// <param name="obj"></param>
        /// <param name="uobj"></param>
        public void Add(sfObject obj, UObject uobj)
        {
            if (obj == null || uobj == null)
            {
                return;
            }
            m_sfToUObjectMap[obj] = uobj;
            m_uToSFObjectMap[uobj] = obj;
        }

        /// <summary>Removes a uobject and its sfObject from the map.</summary>
        /// <param name="uobj">uobj to remove.</param>
        /// <returns>that was removed, or null if the uobject was not in the map.</returns>
        public sfObject Remove(UObject uobj)
        {
            if ((object)uobj == null)
            {
                return null;
            }
            sfObject obj;
            if (m_uToSFObjectMap.TryGetValue(uobj, out obj))
            {
                m_uToSFObjectMap.Remove(uobj);
                // If the sfObject is mapped to a different uobject, do not remove it.
                UObject currentUObj;
                if (!m_sfToUObjectMap.TryGetValue(obj, out currentUObj) || uobj != currentUObj)
                {
                    return null;
                }
                m_sfToUObjectMap.Remove(obj);
            }
            return obj;
        }

        /// <summary>Removes an sfObject and its uobject from the map.</summary>
        /// <param name="obj">obj to remove.</param>
        /// <returns>that was removed, or null if the sfObject was not in the map.</returns>
        public UObject Remove(sfObject obj)
        {
            if (obj == null)
            {
                return null;
            }
            UObject uobj;
            if (m_sfToUObjectMap.TryGetValue(obj, out uobj))
            {
                m_sfToUObjectMap.Remove(obj);
                m_uToSFObjectMap.Remove(uobj);
            }
            return uobj;
        }

        /// <summary>Clears the map.</summary>
        public void Clear()
        {
            m_uToSFObjectMap.Clear();
            m_sfToUObjectMap.Clear();
        }

        /// <summary></summary>
        /// <returns>enumerator for the map.</returns>
        public IEnumerator<KeyValuePair<sfObject, UObject>> GetEnumerator()
        {
            return m_sfToUObjectMap.GetEnumerator();
        }

        /// <summary></summary>
        /// <returns>for the map.</returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
