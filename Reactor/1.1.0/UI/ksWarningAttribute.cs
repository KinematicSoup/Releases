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

namespace KS.Reactor.Client.Unity
{
    /// <summary>Tag proxy script classes to display a warning message in the inspector for the class.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class ksWarningAttribute : Attribute
    {
        /// <summary>Warning message</summary>
        public string Message
        {
            get { return m_message; }
        }
        private string m_message;

        /// <summary>Constructor</summary>
        /// <param name="message">Warning message</param>
        public ksWarningAttribute(string message)
        {
            m_message = message;
        }
    }
}
