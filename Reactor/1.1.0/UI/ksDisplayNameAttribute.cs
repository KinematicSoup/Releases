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
    /// <summary>Attribute to change a field's display name in the inspector.</summary>
    public class ksDisplayNameAttribute : PropertyAttribute
    {
        /// <summary>Name to display in the inspector.</summary>
        public string DisplayName
        {
            get { return m_displayName; }
        }
        private string m_displayName;

        /// <summary>Constructor</summary>
        /// <param name="displayName">Name to display in the inspector.</param>
        public ksDisplayNameAttribute(string displayName)
        {
            m_displayName = displayName;
        }
    }
}
