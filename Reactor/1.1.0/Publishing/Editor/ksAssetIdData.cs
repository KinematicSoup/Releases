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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using KS.Unity.Editor;
using KS.Unity;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>
    /// An editor asset that stores a persistent mapping of assets to ids. Some assets use Unity classes as proxies
    /// instead of deriving from <see cref="ksProxyScriptAsset"/>. Since these assets cannot store there own asset id
    /// like our proxy assets, they must save their asset ids in this map.
    /// </summary>
    public class ksAssetIdData : ScriptableObject
    {
        private static ksAssetIdData m_instance;

        [HideInInspector]
        [SerializeField]
        private ksSerializableDictionary<UnityEngine.Object, uint> m_assetToIdMap = 
            new ksSerializableDictionary<UnityEngine.Object, uint>();

        /// <summary>
        /// Gets the <see cref="ksAssetIdData"/> singleton asset. Loads it if it's not already loaded.
        /// </summary>
        /// <returns>Singleton asset.</returns>
        public static ksAssetIdData Get()
        {
            if (m_instance == null)
            {
                m_instance = AssetDatabase.LoadAssetAtPath<ksAssetIdData>(ksPaths.AssetIdData);
                if (m_instance == null)
                {
                    ksPathUtils.Create(ksPaths.AssetIdData);
                    m_instance = CreateInstance<ksAssetIdData>();
                    AssetDatabase.CreateAsset(m_instance, ksPaths.AssetIdData);
                }
            }
            return m_instance;
        }

        /// <summary>Sets the id of an asset in the map.</summary>
        /// <param name="asset">Asset to set id for.</param>
        /// <param name="id">Id of the asset.</param>
        public void SetId(UnityEngine.Object asset, uint id)
        {
            if (id == 0)
            {
                m_assetToIdMap.Remove(asset);
            }
            else
            {
                m_assetToIdMap[asset] = id;
            }
            EditorUtility.SetDirty(this);
        }

        /// <summary>Gets the id of an asset from the map.</summary>
        /// <param name="asset">Asset to get id for.</param>
        /// <returns>Id of the asset, or 0 if the asset has no id.</returns>
        public uint GetId(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return 0;
            }
            uint id;
            m_assetToIdMap.TryGetValue(asset, out id);
            return id;
        }

        /// <summary>Removes missing assets from the map.</summary>
        public void RemoveMissingAssets()
        {
            List<UnityEngine.Object> toRemove = new List<UnityEngine.Object>();
            foreach (KeyValuePair<UnityEngine.Object, uint> asset in m_assetToIdMap)
            {
                if (asset.Key == null)
                {
                    toRemove.Add(asset.Key);
                }
            }
            if (toRemove.Count > 0)
            {
                foreach (UnityEngine.Object missingAsset in toRemove)
                {
                    m_assetToIdMap.Remove(missingAsset);
                }
                EditorUtility.SetDirty(this);
            }
        }
    }
}
