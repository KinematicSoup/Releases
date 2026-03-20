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
using UnityEditor;

namespace KS.Reactor.Client.Unity.DOTS.Editor
{
    /// <summary>
    /// Baker that bakes DOTS entity prefabs from the <see cref="ksEntityPrefabList"/> asset at
    /// <see cref="ksEntityPrefabListBuilder.PrefabListPath"/>. The baker is triggered to bake <see cref="ksRoomType"/>
    /// but it does nothing with the <see cref="ksRoomType"/> data and just bakes the <see cref="ksEntityPrefabList"/>
    /// asset.
    /// </summary>
    public class ksEntityPrefabListBaker : Baker<ksRoomType>
    {
        /// <summary>
        /// Bakes a dynamic buffer of DOTS entity prefabs from a the <see cref="ksEntityPrefabList"/> asset at
        /// <see cref="ksEntityPrefabListBuilder.PrefabListPath"/>.
        /// </summary>
        /// <param name="authoring"></param>
        public override void Bake(ksRoomType authoring)
        {
            ksEntityPrefabList prefabList = AssetDatabase.LoadAssetAtPath<ksEntityPrefabList>(
                ksEntityPrefabListBuilder.PrefabListPath);
            if (prefabList == null)
            {
                return;
            }
            DependsOn(prefabList);
            Entity ent = GetEntity(TransformUsageFlags.None);
            DynamicBuffer<ksEntityPrefabsElement> buffer = AddBuffer<ksEntityPrefabsElement>(ent);
            foreach (ksEntityComponent entityPrefab in prefabList.Prefabs)
            {
                if (entityPrefab != null)
                {
                    DependsOn(entityPrefab);
                    buffer.Add(new ksEntityPrefabsElement()
                    {
                        AssetId = entityPrefab.AssetId,
                        Prefab = GetEntity(entityPrefab.gameObject, TransformUsageFlags.Dynamic)
                    });
                }
            }
        }
    }
}