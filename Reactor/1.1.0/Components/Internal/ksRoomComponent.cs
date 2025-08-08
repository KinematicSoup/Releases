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
    /// Room component. This adds no functionality that's not already in the generic base class,
    /// but we need this class because Unity does not allow you to attach generic Monobehaviours.
    /// </summary>
    [AddComponentMenu(ksMenuNames.HIDDEN)]
    public class ksRoomComponent : ksScriptsComponent<ksRoom, ksRoomScript>
    {
        /// <summary>The room this game object is for.</summary>
        public ksRoom Room
        {
            get { return ParentObject; }
        }
    }
}
