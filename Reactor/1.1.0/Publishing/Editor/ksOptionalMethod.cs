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
using System.Threading.Tasks;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Data for the UI about an optional method a <see cref="ksIScriptTemplate"/> can generate.</summary>
    public class ksOptionalMethod
    {
        /// <summary>
        /// ID for the template to identify the method. Different templates can use the same ID for different methods.
        /// </summary>
        public uint Id;
        /// <summary>Method name to show in the UI.</summary>
        public string Name;
        /// <summary>Is the method included by default?</summary>
        public bool DefaultIncluded;
        /// <summary>Tooltip</summary>
        public string Tooltip;

        /// <summary>Constructor</summary>
        /// <param name="id">
        /// ID for the template to identify the method. Different templates can use the same ID for different methods.
        /// </param>
        /// <param name="name">Method name to show in the UI.</param>
        /// <param name="defaultIncluded">Is the method included by default?</param>
        /// <param name="tooltip">Tooltip to display.</param>
        public ksOptionalMethod(uint id, string name, bool defaultIncluded, string tooltip = "")
        {
            Id = id;
            Name = name;
            DefaultIncluded = defaultIncluded;
            Tooltip = tooltip;
        }
    }
}
