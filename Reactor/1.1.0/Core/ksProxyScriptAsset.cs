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

using UnityEngine;

namespace KS.Reactor.Client.Unity
{
    /// <summary>
    /// Proxy scripts assets store data used in server script assets.
    /// </summary>
    public class ksProxyScriptAsset : ScriptableObject
    {
        /// <summary>Asset id</summary>
        public uint AssetId
        {
            get { return __ksAssetId; }
            set { __ksAssetId = value; }
        }
        [SerializeField]
        [HideInInspector]
        // Because Unity serialization breaks if a derived class has a serialized field with the same name as another
        // serialized field in a base class, we prefix the field with "__ks" to reduce the likelyhood of a developer
        // creating an editable field with the same name.
        private uint __ksAssetId;
    }
}
