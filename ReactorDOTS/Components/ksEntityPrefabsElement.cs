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

namespace KS.Reactor.Client.Unity.DOTS
{
    /// <summary>ECS bufferable element data containing an asset id and an entity prefab that can be spawned.</summary>
    public struct ksEntityPrefabsElement : IBufferElementData
    {
        /// <summary>Asset id of the prefab.</summary>
        public uint AssetId
        {
            get { return m_assetId; }
            internal set { m_assetId = value; }
        }
        private uint m_assetId;

        /// <summary>Entity prefab</summary>
        public Entity Prefab
        {
            get { return m_prefab; }
            internal set { m_prefab = value; }
        }
        private Entity m_prefab;
    }
}