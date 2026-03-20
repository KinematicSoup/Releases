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
using Unity.Entities;
using Unity.Collections;

namespace KS.Reactor.Client.Unity.DOTS
{
    /// <summary>
    /// Managed component that stores a <see cref="NativeHashMap{TKey, TValue}"/>. Although
    /// <see cref="NativeHashMap{TKey, TValue}"/> is an unmanaged type, it cannot be stored on an unmanaged component,
    /// so this has to be a managed component. Unity does not support generic components, so to use this you must create
    /// a derived class with the key and value types you want to use.
    /// </summary>
    /// <typeparam name="Key"></typeparam>
    /// <typeparam name="Value"></typeparam>
    public class ksDictionaryComponent<Key, Value> : IComponentData, IDisposable
        where Key : unmanaged, IEquatable<Key> 
        where Value : unmanaged
    {
        /// <summary>
        /// Initial capacity (number of buckets) of the hash map. 37 was chosen because it is prime.
        /// </summary>
        private const int INITIAL_CAPACITY = 37;

        private NativeHashMap<Key, Value> m_map = new NativeHashMap<Key, Value>(INITIAL_CAPACITY, Allocator.Persistent);

        /// <summary>
        /// Number of elements in the map.
        /// </summary>
        public int Count
        {
            get { return m_map.Count; }
        }

        /// <summary>
        /// Get or set a value by key.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public Value this[Key key]
        {
            get { return m_map[key]; }
            set { m_map[key] = value; }
        }

        /// <summary>Disposes the hash map.</summary>
        public void Dispose()
        {
            if (m_map.IsCreated)
            {
                m_map.Dispose();
            }
        }

        /// <summary>Tries to get the value with the given key. Returns false if the key is not found.</summary>
        /// <param name="key">Key to look up.</param>
        /// <param name="value">Set to the value for the key, or the default value if the key is not found.</param>
        /// <returns>True if the key was found.</returns>
        public bool TryGetValue(Key key, out Value value)
        {
            return m_map.TryGetValue(key, out value);
        }
    }
}