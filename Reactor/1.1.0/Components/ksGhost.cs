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
    /// Attach this script to a Reactor entity game object to have it spawn an associated ghost object. 
    /// The ghost object will update its trasform to match the entity server transform. This allows the
    /// entity server transform to be compared to its client transform, include prediction and smoothing
    /// adjustements.
    /// </summary>
    public class ksGhost : ksEntityScript
    {
        [Tooltip("Prefab to use for the ghost object. If the value is null then a duplicate of the game object with only mesh renderer components will be created.")]
        public GameObject GhostPrefab = null;
        [Tooltip("Material to use on renderer when a ghost prefab is not defined.")]
        public Material GhostMaterial = null;
        public GameObject GhostInstance = null;
        public bool ShowGhost = true;

        /// <summary>
        /// Spawns/Destroys/Update a ghost gameobject.
        /// </summary>
        public void Update()
        {
            if (Entity == null || Entity.IsDestroyed)
            {
                return;
            }

            if (GhostInstance == null)
            {
                SpawnGhost();
            }

            if (GhostInstance != null) {
                if (GhostInstance.activeSelf != ShowGhost)
                {
                    GhostInstance.SetActive(ShowGhost);
                }

                if (GhostInstance.activeSelf)
                {
                    GhostInstance.transform.position = Entity.ServerTransform.Position;
                    GhostInstance.transform.rotation = Entity.ServerTransform.Rotation;
                    GhostInstance.transform.localScale = Entity.ServerTransform.Scale;
                }
            }
        }

        /// <summary>
        /// Destroy the ghost gameobject instance.
        /// </summary>
        public override void Detached()
        {
            if (GhostInstance != null)
            {
                Destroy(GhostInstance);
                GhostInstance = null;
            }
        }

        /// <summary>
        /// Spawn a ghost gameobject instance.
        /// </summary>
        private void SpawnGhost()
        {
            if (GhostPrefab == null)
            {
                GhostInstance = CreateRenderDuplicate(gameObject);
            }
            else
            {
                GhostInstance = Instantiate(GhostPrefab);
                GhostInstance.name = name + "(ksGhost)";
            }
        }

        private GameObject CreateRenderDuplicate(GameObject origGameObject)
        {
            GameObject go = new GameObject(origGameObject.name + " (ksGhost)");

            MeshFilter origFilter = origGameObject.GetComponent<MeshFilter>();
            if (origFilter != null)
            {
                MeshFilter filter = go.AddComponent<MeshFilter>();
                filter.mesh = origFilter.mesh;
            }

            MeshRenderer origRenderer = origGameObject.GetComponent<MeshRenderer>();
            if (origRenderer != null)
            {
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.material = GhostMaterial != null ? GhostMaterial : origRenderer.material;
            }

            foreach(Transform child in origGameObject.transform)
            {
                GameObject childGo = CreateRenderDuplicate(child.gameObject);
                childGo.transform.parent = go.transform;
            }

            go.transform.localPosition = origGameObject.transform.localPosition;
            go.transform.localRotation = origGameObject.transform.localRotation;
            go.transform.localScale = origGameObject.transform.localScale;
            return go;
        }
    }
}
