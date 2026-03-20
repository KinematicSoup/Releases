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
using Unity.Entities;
using Unity.Collections;

namespace KS.Reactor.Client.Unity.DOTS
{
    /// <summary>
    /// System that loads entity prefab data from a singleton dynamic buffer of <see cref="ksEntityPrefabsElement"/>
    /// and builds a map of asset ids to entity prefabs. If there are multiple instances of this system in different
    /// Worlds, the first one to update will load the prefabs and make itself the static instances that can be retrieved
    /// using <see cref="Get"/> and all other instances will do nothing until the first instance is destroyed.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ksPrefabCacheSystem : SystemBase
    {
        private NativeHashMap<uint, Entity> m_prefabMap;

        /// <summary>Gets the instance of the prefab system with loaded prefab data, or null if none exists.</summary>
        /// <returns>Prefab system with prefab data, or null.</returns>
        public static ksPrefabCacheSystem Get()
        {
            return m_instance;
        }

        private static ksPrefabCacheSystem m_instance;

        /// <summary>Sets the update requirement that a <see cref="ksEntityPrefabsElement"/> exists.</summary>
        protected override void OnCreate()
        {
            RequireForUpdate<ksEntityPrefabsElement>();
        }

        /// <summary>
        /// If the static instance is null, sets the static instance to this system and builds the prefab map from the
        /// prefab data.
        /// </summary>
        protected override void OnUpdate()
        {
            if (m_instance != null)
            {
                return;
            }
            m_instance = this;
            DynamicBuffer<ksEntityPrefabsElement> buffer = SystemAPI.GetSingletonBuffer<ksEntityPrefabsElement>();
            m_prefabMap = new NativeHashMap<uint, Entity>(buffer.Capacity, Allocator.Persistent);
            for (int i = 0; i < buffer.Length; i++)
            {
                ksEntityPrefabsElement element = buffer[i];
                m_prefabMap[element.AssetId] = element.Prefab;
            }
            Enabled = false;
        }

        /// <summary>
        /// Disposes the prefab map and sets the static instance to null if this system was the static instance.
        /// </summary>
        protected override void OnDestroy()
        {
            if (m_instance == this)
            {
                m_instance = null;
            }
            if (m_prefabMap.IsCreated)
            {
                m_prefabMap.Dispose();
            }
        }

        /// <summary>Tries to get the entity prefab for an asset id.</summary>
        /// <param name="assetId">Asset id of the prefab to get.</param>
        /// <param name="prefab">Set to the prefab with the given asset id, or null if none was found.</param>
        /// <returns>True if a prefab with that asset id was found.</returns>
        public bool TryGet(uint assetId, out Entity prefab)
        {
            return m_prefabMap.TryGetValue(assetId, out prefab);
        }
    }
}