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
    /// Adds a <see cref="Teleport(Transform, Vector3)"/> extension method to the Unity <see cref="Transform"/>.
    /// </summary>
    public static class ksUnityTransformExtension
    {
        /// <summary>
        /// Sets the position. If the entity is owned by the local player with the transform permission, also sets
        /// <see cref="ksBaseEntity.LocalOwnerTeleported"/> to teleport the entity on other clients.
        /// </summary>
        /// <param name="transform">Transform</param>
        /// <param name="position">Position to set.</param>
        public static void Teleport(this Transform transform, Vector3 position)
        {
            transform.position = position;
            ksEntityComponent component = transform.GetComponent<ksEntityComponent>();
            if (component != null && component.Entity != null)
            {
                // The setter does nothing if the entity is not owned locally with the transform permission.
                component.Entity.LocalOwnerTeleported = true;
            }
        }
    }
}
