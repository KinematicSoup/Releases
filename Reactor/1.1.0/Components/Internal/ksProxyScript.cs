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
    /// Proxy scripts are attached to gameobjects to represent server scripts
    /// and provide editing of server script properties.
    /// </summary>
    [ksHideInOwnershipManager]
    public abstract class ksProxyScript : MonoBehaviour
    {
        /// <summary>
        /// A dummy class used for unsupported generic type definitions in proxy scripts. Eg. if script Derived
        /// inherits from script Base<SomeClass> and SomeClass cannot be serialized, the proxy script will derive from
        /// the proxy Base<UnsupportedType> instead.
        /// </summary>
        public class UnsupportedType
        {

        };

        /// <summary>Declaring a start method allows the script to be disabled in the inspector.</summary>
        private void Start()
        {
            
        }
    }
}
