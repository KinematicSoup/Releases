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
    /// Attribute to indicate a flag mask enum is using our naming standard (ENUM_VALUE) so we know in the editor to
    /// convert it to a more readable format (Enum Value). Composite flag values are not shown.
    /// </summary>
    public class ksFlagsAttribute : PropertyAttribute
    {
    }
}
