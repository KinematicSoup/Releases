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

namespace KS.Reactor.Client.Unity
{
    /// <summary>
    /// Class tag that hides components from the <see cref="ksOwnershipScriptManager"/> inspector. If a base class has
    /// this tag, derived classes will also be hidden. A derived class can pass false to the constructor to make itself
    /// visible to the <see cref="ksOwnershipScriptManager"/> inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ksHideInOwnershipManagerAttribute : Attribute
    {
        /// <summary>Is the tagged class hidden from the <see cref="ksOwnershipScriptManager"/> inspector?</summary>
        public bool Hide
        {
            get { return m_hide; }
        }
        private bool m_hide = true;

        /// <summary>Constructor</summary>
        /// <param name="hide">
        /// Is the component hidden from the <see cref="ksOwnershipScriptManager"/> inspector? Defaults to true.
        /// If you want to hide a base class but show a derived class, tag both the base and the derived class, and
        /// pass false to the derived class.
        /// </param>
        public ksHideInOwnershipManagerAttribute(bool hide = true)
        {
            m_hide = hide;
        }
    }
}
