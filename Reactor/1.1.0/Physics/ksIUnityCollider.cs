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
using UnityEngine;

#if !UNITY_6000_0_OR_NEWER
using PhysicsMaterial = UnityEngine.PhysicMaterial;
#endif

namespace KS.Reactor.Client.Unity
{
    /// <summary>Interface for getting data from our colliders or Unity's colliders.</summary>
    public interface ksIUnityCollider : ksICollider
    {
        /// <summary>Get the collider shape type.</summary>
        ksShape.ShapeTypes ShapeType { get; }

        /// <summary>The component this interface is for.</summary>
        Component Component { get; }

        /// <summary>The collider this interface is for.</summary>
        Collider Collider { get; }

        /// <summary>
        /// Does this collider exist on the client, server, or both? Note that this only affects whether or not the
        /// collider is written to server configs during publishing or whether the collider is deleted on the client
        /// when the entity is spawned and may not be indicative of the current existance of the collider on the server
        /// at runtime.
        /// </summary>
        ksExistenceModes Existance { get; }

        /// <summary>Physics material.</summary>
        PhysicsMaterial Material { get; }

        /// <summary>Get the collider center.</summary>
        Vector3 Center { get; }

        /// <summary>Gets the volume of the collider.</summary>
        /// <returns>Volume of the collider.</returns>
        float Volume();

        /// <summary>Gets the geometric center of the collider in local space.</summary>
        /// <returns>Geometric center of the collider.</returns>
        Vector3 GeometricCenter();

        /// <summary>Serializes the collider by shape type.</summary>
        /// <returns>Serialized collider data.</returns>
        byte[] Serialize();

        /// <summary>Compare the geometry sources of each collider and return true if they are equal.</summary>
        /// <param name="other">Collider to compare to.</param>
        /// <returns>True if the collider geometry is equal.</returns>
        bool IsGeometryEqual(ksIUnityCollider other);
    }
}
