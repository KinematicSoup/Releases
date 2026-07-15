/*
KINEMATICSOUP CONFIDENTIAL
 Copyright(c) 2014-2026 KinematicSoup Technologies Incorporated 
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
using UnityEditor;
using KS.SF.Reactor;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>Material utility functions.</summary>
    public class sfMaterialUtils
    {
        /// <summary></summary>
        /// <returns>singleton instance.</returns>
        public static sfMaterialUtils Get()
        {
            return m_instance;
        }
        private static sfMaterialUtils m_instance = new sfMaterialUtils();

        /// <summary>Singleton constructor</summary>
        private sfMaterialUtils()
        {

        }

        /// <summary>Updates material on a game object.</summary>
        /// <param name="gameObject"></param>
        /// <param name="material">material for mesh renderers.</param>
        /// <param name="material2d">material2d for sprite renderers.</param>
        /// <param name="altMaterial">will use for mesh renderers if the current material has the same name.</param>
        public void UpdateMaterialOnObject(
            GameObject gameObject,
            Material material,
            Material material2d,
            Material altMaterial)
        {
            if (gameObject == null)
            {
                return;
            }

            SkinnedMeshRenderer childSkin = gameObject.GetComponent<SkinnedMeshRenderer>();
            if (childSkin != null)
            {
                int materialCount = childSkin.sharedMaterials.Length;
                UpdateMaterialsOnRenderer(childSkin, material, materialCount);
            }

            MeshRenderer childRenderer = gameObject.GetComponent<MeshRenderer>();
            if (childRenderer != null)
            {
                if (altMaterial != null && childRenderer.sharedMaterial != null
                    && childRenderer.sharedMaterial.name == altMaterial.name)
                {
                    childRenderer.sharedMaterial = altMaterial;
                }
                else
                {
                    Material mat = gameObject.GetComponent<TextMesh>() == null ? material : material2d;
                    int materialCount = childRenderer.sharedMaterials.Length;
                    UpdateMaterialsOnRenderer(childRenderer, mat, materialCount);
                }
            }
            else
            {
                SpriteRenderer childSprite = gameObject.GetComponent<SpriteRenderer>();
                if (childSprite != null)
                {
                    childSprite.sharedMaterial = material2d;
                }
            }

            LineRenderer line = gameObject.GetComponent<LineRenderer>();
            if (line != null)
            {
                line.sharedMaterial = material2d;
            }
        }

        /// <summary>Updates materials on renderer.</summary>
        /// <param name="renderer"></param>
        /// <param name="material">material to set on renderer.</param>
        /// <param name="materialCount">number of materials to set.</param>
        public void UpdateMaterialsOnRenderer<T>(T renderer, Material material, int materialCount)
            where T : Renderer
        {
            Material[] lockMaterials = new Material[materialCount];
            for (int i = 0; i < materialCount; i++)
            {
                lockMaterials[i] = material;
            }
            renderer.sharedMaterials = lockMaterials;
        }

        /// <summary>Updates material on a game object and each of its descendants.</summary>
        /// <param name="gameObject"></param>
        /// <param name="material">material for mesh renderers.</param>
        /// <param name="material2d">material2d for sprite renderers.</param>
        public void UpdateMaterialOnObjectAndDescendants(GameObject gameObject, Material material, Material material2d)
        {
            if (gameObject != null)
            {
                UpdateMaterialOnObject(gameObject, material, material2d, null);
                foreach (Transform child in gameObject.transform)
                {
                    UpdateMaterialOnObjectAndDescendants(child.gameObject, material, material2d);
                }
            }
        }
    }
}
