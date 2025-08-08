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
    /// <summary>Base class for scripts attached to entities.</summary>
    [AddComponentMenu(ksMenuNames.HIDDEN)]
    public abstract class ksEntityScript : ksMonoScript<ksEntity, ksEntityScript>
    {
        /// <summary>Entity this script is attached to.</summary>
        public ksEntity Entity
        {
            get { return ParentObject; }
        }

        /// <summary>Room the entity is in.</summary>
        public override sealed ksRoom Room
        {
            get { return Entity == null ? null : Entity.Room; }
        }

        /// <summary>Entity properties</summary>
        public ksPropertyMap Properties
        {
            get { return Entity == null ? null : Entity.Properties; }
        }

        /// <summary>Initialize internal components.</summary>
        protected virtual void OnEnable()
        {
            if (!SetupComponent())
            {
                ksLog.Warning(this, "No ksEntityComponent found on game object " +
                    gameObject.name + " with ksEntityScript");
            }
        }
    }
}
