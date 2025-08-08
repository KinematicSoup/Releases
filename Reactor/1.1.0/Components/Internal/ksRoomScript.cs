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
    /// Base class for scripts attached to rooms. These can be attached to game objects with <see cref="ksRoomType"/> in the
    /// editor, but they won't run on those game objects. They are copied to room game objects for rooms of the
    /// given room type.
    /// </summary>
    [AddComponentMenu(ksMenuNames.HIDDEN)]
    public class ksRoomScript : ksMonoScript<ksRoom, ksRoomScript>
    {
        /// <summary>Room the script is attached to.</summary>
        public override sealed ksRoom Room
        {
            get { return ParentObject; }
        }

        /// <summary>Room properties</summary>
        public ksPropertyMap Properties
        {
            get { return Room == null ? null : Room.Properties; }
        }

        /// <summary>Initialize internal components.</summary>
        protected virtual void OnEnable()
        {
            if (!SetupComponent() && GetComponent<ksRoomType>() == null)
            {
                ksLog.Warning(this, "No ksRoomType or Room found on game object " +
                    gameObject.name + " with ksRoomScript");
            }
        }
    }
}
