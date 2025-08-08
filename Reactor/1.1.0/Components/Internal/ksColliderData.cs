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
using UnityEngine.Serialization;

namespace KS.Reactor.Client.Unity
{
    /// <summary>
    /// A hidden component that contains a mapping from a collider to its PhysX simulation flag value and collision
    /// filter override.
    /// </summary>
    [ksHideInOwnershipManager]
    [AddComponentMenu(ksMenuNames.HIDDEN)]
    [ExecuteInEditMode]
    public class ksColliderData : MonoBehaviour
    {
        /// <summary>Unity collider</summary>
        [HideInInspector]
        public Component Collider;

        /// <summary>Shape ID</summary>
        [ksReadOnly]
        [Tooltip("ID used by Reactor to identify this collider geometry.")]
        public int ShapeId;

        /// <summary>Does this collider exist on the client, server, or both?</summary>
        [ksEnum]
        [Tooltip("Does this collider exist on the client, server, or both?")]
        [FormerlySerializedAs("Existance")]
        public ksExistenceModes Existence;

        /// <summary>
        /// Is this collider used in physics simulations? If false, this collider wll not participate in collision and
        /// overlap checks.
        /// </summary>
        [Tooltip("Is this collider used in physics simulations? " +
            "If false this collider will not participate in collision and overlap checks.")]
        public bool IsSimulation = true;

        /// <summary>
        /// Is this collider used in physics queries? If false, physics queries will never return this collider in
        /// results.
        /// </summary>
        [Tooltip("Is this collider used in physics queries? " + 
            "If false, physics queries will never return this collider in results.")]
        public bool IsQuery = true;

        /// <summary>
        /// Collision filter on a specific collder. This overrides the collision filter
        /// set in the scene or whole entity.
        /// </summary>
        [Tooltip("Set the collision filter on a specific collder. " +
            "This overrides the collision filter set in the scene or whole entity.")]
        public ksCollisionFilterAsset Filter;

        /// <summary>
        /// Colliders whose distance is less than the sum of their contact offset values will generate contacts.
        /// </summary>
        [Tooltip(
            "Colliders whose distance is less than the sum of their contact offset values will generate contacts. " +
            "(Unchecked = Use default room settings)")]
        public float ContactOffset = -1f;

        /// <summary>Hides the component in the inspector.</summary>
        public void OnEnable()
        {
            hideFlags = HideFlags.HideInInspector;
        }

        /// <summary>Destroys the component if the <see cref="Collider"/> is missing.</summary>
        public void Start()
        {
            if (Collider == null)
            {
                if (Application.isPlaying)
                {
                    Destroy(this);
                }
                else
                {
                    DestroyImmediate(this, true);
                }
            }
        }
    }
}
