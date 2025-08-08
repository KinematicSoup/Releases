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
using UnityEngine;

namespace KS.Reactor.Client.Unity
{
    /// <summary>
    /// Provides access to Unity's LateUpdate MonoBehaviour event. This script is separate from 
    /// <see cref="ksUpdateHook"/> so they can use different script execution times.
    /// Used by <see cref="ksReactor"/> to call events on non-Unity objects.
    /// </summary>
    [AddComponentMenu(ksMenuNames.HIDDEN)]
    public class ksLateUpdateHook : MonoBehaviour
    {
        /// <summary>Invoked from Unity's LateUpdate.</summary>
        public Action OnLateUpdate;

        /// <summary>
        /// Calls the late update handler. This method is invoked be Unity every frame after all scripts update.
        /// </summary>
        private void LateUpdate()
        {
            if (OnLateUpdate != null)
            {
                OnLateUpdate();
            }
        }
    }
}
