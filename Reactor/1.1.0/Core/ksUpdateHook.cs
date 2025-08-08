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
    /// Provides access to Unity's Update and OnApplicationQuit MonoBehaviour events. 
    /// Used by <see cref="ksReactor"/> to call events on non-Unity objects.
    /// </summary>
    [AddComponentMenu(ksMenuNames.HIDDEN)]
    public class ksUpdateHook : MonoBehaviour
    {
        /// <summary>Void delegate used for OnQuit events.</summary>
        public delegate void Callback();

        /// <summary>Update delegate use for Update events.</summary>
        public delegate void UpdateCallback(float realDeltaTime);

        /// <summary>Update handler</summary>
        public UpdateCallback OnUpdate;

        /// <summary>Application quit handler.</summary>
        public Callback OnQuit;

        /// <summary>Call the update handler. This method is invoked by Unity every frame.</summary>
        private void Update()
        {
            if (OnUpdate != null)
            {
                OnUpdate(Time.unscaledDeltaTime);
            }
        }

        /// <summary>Calls the on quit handler. This method is invoked by Unity when the application quits.</summary>
        private void OnApplicationQuit()
        {
            if (OnQuit != null)
            {
                OnQuit();
            }
        }
    }
}
