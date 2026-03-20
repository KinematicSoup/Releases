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
using System.Collections.Generic;
using UnityEngine;

namespace KS.Reactor.Client.Unity.DOTS
{
    /// <summary>An asset that stores a list of prefabs with <see cref="ksEntityComponent"/>.</summary>
    [CreateAssetMenu(menuName = "Reactor/DOTS/Entity Prefab List")]
    public class ksEntityPrefabList : ScriptableObject
    {
        /// <summary>List of entity prefabs.</summary>
        public List<ksEntityComponent> Prefabs
        {
            get { return m_prefabs; }
        }

        [SerializeField]
        private List<ksEntityComponent> m_prefabs = new List<ksEntityComponent>();
    }
}