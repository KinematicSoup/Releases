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
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KS.Reactor.Client.Unity
{
    /// <summary>
    /// A reference to a <see cref="ksScriptAsset"/> that Unity can serialize. References a
    /// <see cref="ksProxyScriptAsset"/> which is used to load a <see cref="ksScriptAsset"/> of type 
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Type of <see cref="ksScriptAsset"/> to reference.</typeparam>
    [Serializable]
    public struct ksScriptAssetReference<T> where T : ksScriptAsset
    {
        private T m_asset;

        [SerializeField]
        private ksProxyScriptAsset m_proxy;

        /// <summary>Gets the referenced asset. Loads it if it hasn't already been loaded.</summary>
        /// <returns>The referenced asset.</returns>
        public T Get()
        {
            if (m_asset == null && m_proxy != null && ksReactor.Assets != null)
            {
                m_asset = ksReactor.Assets.FromProxy<T>(m_proxy);
                m_proxy = null;
            }
            return m_asset;
        }

        /// <summary>
        /// Implicit conversion to <typeparamref name="T"/>. Loads the asset if it hasn't already been loaded.
        /// </summary>
        /// <param name="reference">Reference to convert.</param>
        public static implicit operator T(ksScriptAssetReference<T> reference)
        {
            return reference.Get();
        }
    }
}