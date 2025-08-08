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

using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine;

namespace KS.Reactor.Client.Unity
{
    /// <summary>Manages caching and loading and entity prefabs.</summary>
    public class ksPrefabCache
    {
        private Dictionary<uint, string> m_assetPaths = new Dictionary<uint, string>();
        private Dictionary<uint, GameObject> m_prefabCache = new Dictionary<uint, GameObject>();
        private Dictionary<string, AssetBundle> m_assetBundles;
        private GameObject m_container;

        /// <summary>Constructor</summary>
        /// <param name="assetBundles">Map of asset bundle names to asset bundles.</param>
        internal ksPrefabCache(Dictionary<string, AssetBundle> assetBundles)
        {
            m_assetBundles = assetBundles;
        }

        /// <summary>Gets the entity type for an asset ID.</summary>
        /// <param name="assetId">Asset ID</param>
        /// <returns>Entity type</returns>
        public string GetEntityType(uint assetId)
        {
            if (assetId == 0)
            {
                return "";
            }
            if (m_assetPaths.ContainsKey(assetId))
            {
                return m_assetPaths[assetId];
            }
            else
            {
                ksLog.Error(this, "Unable to match a file path with asset " + assetId + ". If your entity prefabs " +
                    "are in asset bundles, make sure to call ksReactor.RegisterAssetBundle after loading the " +
                    "asset bundle.");
                return "";
            }
        }

        /// <summary>Loads an entity prefab from resources, an asset bundle, or the cache.</summary>
        /// <param name="assetId">Asset ID</param>
        /// <param name="entityType">Entity type</param>
        /// <returns>Prefab assigned to the asset ID and/or entity type. Null if no prefab was found.</returns>
        public GameObject GetPrefab(uint assetId, string entityType)
        {
            if (string.IsNullOrEmpty(entityType))
            {
                return null;
            }
            // Return the loaded prefab if it exists in the cache
            GameObject prefab;
            if (m_prefabCache.TryGetValue(assetId, out prefab) && prefab != null)
            {
                return prefab;
            }

            // Load the prefab and store it in the cache before returning it
            int index = entityType.IndexOf(":");
            if (index >= 0)
            {
                string assetBundleName = entityType.Substring(0, index);
                AssetBundle assetBundle;
                if (m_assetBundles.TryGetValue(assetBundleName, out assetBundle))
                {
                    string assetName = entityType.Substring(index + 1);
                    prefab = assetBundle.LoadAsset<GameObject>(assetName);
                    prefab = FindEntity(prefab, assetId);
                }
            }
            else
            {
                prefab = Resources.Load<GameObject>(entityType);
                prefab = FindEntity(prefab, assetId);
            }
            if (prefab == null)
            {
                ksLog.Error(this, "Could not find entity with asset ID " + assetId + " in prefab " + entityType);
                return null;
            }
            if (HasChildEntity(prefab.transform))
            {
                // Setup a disabled pseudo prefab container if it does not already exist.
                if (m_container == null)
                {
                    m_container = new GameObject("ksEntityPrefabCache");
                    m_container.SetActive(false);
                    GameObject.DontDestroyOnLoad(m_container);
                }

                // Instantiate a prefab instance to use as a pseudo prefab and remove its child entities.
                // Instantiating it directly into the disabled container is required to prevent scripts from running awake(),
                // enabled() and other behaviours which would normaly be triggered during instantiation.
                string name = prefab.name;
                prefab = GameObject.Instantiate<GameObject>(prefab, m_container.transform);
                prefab.name = name;
                DestroyChildEntities(prefab.transform);
            }
            m_prefabCache[assetId] = prefab;
            return prefab;
        }

        /// <summary>Maps an asset id to an entity type.</summary>
        /// <param name="assetId">Asset id.</param>
        /// <param name="entityType">Entity type.</param>
        internal void RegisterEntityType(uint assetId, string entityType)
        {
            string oldPath;
            if (m_assetPaths.TryGetValue(assetId, out oldPath) && oldPath != entityType)
            {
                ksLog.Warning(this, "Multiple entity prefabs found with asset ID " + assetId + ". '" +
                    oldPath + "' will be replaced with '" + entityType + "'.");
            }
            m_assetPaths[assetId] = entityType;
        }

        /// <summary>
        /// Checks if a game object is a pseudo prefab. Pseudo-prefabs are disabled instances of prefabs which have
        /// had instances of child objects containing ksEntityComponents removed. Reactor uses these objects as a
        /// workaround to a Unity prefab limitation that does not allow you to delete prefab child objects or create 
        /// prefabs at runtime.
        /// </summary>
        /// <param name="gameObject">Game object to check.</param>
        /// <returns>True if the game object is a pseudo prefab.</returns>
        public bool IsPseudoPrefab(GameObject gameObject)
        {
            return m_container != null && gameObject.transform.parent == m_container.transform;
        }

        /// <summary>
        /// Find the first game object in a target game object or its descendants that has a 
        /// <see cref="ksEntityComponent"/> component and has a specific <see cref="ksEntityComponent.AssetId"/>.
        /// </summary>
        /// <param name="prefab">Prefab root game object.</param>
        /// <param name="assetId">Asset ID</param>
        /// <returns>Game object with a <see cref="ksEntityComponent"/> that has the required asset ID.</returns>
        private GameObject FindEntity(GameObject prefab, uint assetId)
        {
            if (prefab == null)
            {
                return null;
            }
            ksEntityComponent entity = prefab.GetComponent<ksEntityComponent>();
            if (entity != null && entity.AssetId == assetId)
            {
                return prefab;
            }
            foreach (Transform child in prefab.transform)
            {
                GameObject asset = FindEntity(child.gameObject, assetId);
                if (asset != null)
                {
                    return asset;
                }
            }
            return null;
        }

        /// <summary>
        /// Checks if any descendants of a transform have <see cref="ksEntityComponent"/> components.
        /// </summary>
        /// <param name="transform">Root transform</param>
        /// <returns>True if any descendants have a <see cref="ksEntityComponent"/> component.</returns>
        private bool HasChildEntity(Transform transform)
        {
            foreach (Transform child in transform)
            {
                if (child.GetComponent<ksEntityComponent>() != null || HasChildEntity(child))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Destroys all descendants of an object with <see cref="ksEntityComponent"/> components.</summary>
        /// <param name="transform">Root transform</param>
        private void DestroyChildEntities(Transform transform)
        {
            foreach (Transform child in transform)
            {
                if (child.GetComponent<ksEntityComponent>() != null)
                {
                    GameObject.DestroyImmediate(child.gameObject);
                }
                else
                {
                    DestroyChildEntities(child);
                }
            }
        }
    }
}