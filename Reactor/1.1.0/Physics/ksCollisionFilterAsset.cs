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
using UnityEngine.Serialization;

namespace KS.Reactor.Client.Unity
{
    /// <summary>
    /// The collsion filter uses 3 mask properties (Group, Notify, Collide) to determine interaction 
    /// and notifications during the physics simulation. This is used as a proxy asset to save data for a
    /// <see cref="ksCollisionFilter"/> in Unity.
    /// </summary>
    [CreateAssetMenu(menuName = ksMenuNames.REACTOR + "Collision Filter", order = ksMenuGroups.BUILTIN_ASSETS)]
    public class ksCollisionFilterAsset : ksProxyScriptAsset
    {
        /// <summary>Groups that this filter belongs to.</summary>
        public uint Group
        {
            get { return m_group; }
            set { m_group = value; }
        }
        [SerializeField]
        [FormerlySerializedAs("Group")]
        private uint m_group;

        /// <summary>Groups that this filter reports collision and overlap events with.</summary>
        public uint Notify
        {
            get { return m_notify; }
            set { m_notify = value; }
        }
        [SerializeField]
        [FormerlySerializedAs("Notify")]
        private uint m_notify;

        /// <summary>Groups that this filter will simulate collisions with.</summary>
        public uint Collide
        {
            get { return m_collide; }
            set { m_collide = value; }
        }
        [SerializeField]
        [FormerlySerializedAs("React")]
        [FormerlySerializedAs("Collide")]
        private uint m_collide;
    }
}
