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
using UnityEngine.Rendering;
using UnityEditor;
using KS.SF.Reactor;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>Manages user materials.</summary>
    public class sfUserMaterials
    {
        private static Material m_dragObjectMaterial = null;
        private static Material m_dragObject2dMaterial = null;

        /// <summary>Lock material.</summary>
        public static Material LockMaterial
        {
            get { return m_lockMaterial; }
        }
        private static Material m_lockMaterial = null;

        /// <summary>Lock icon material.</summary>
        public static Material LockIconMaterial
        {
            get { return m_lockIconMaterial; }
        }
        private static Material m_lockIconMaterial = null;

        /// <summary>Camera material.</summary>
        public static Material CameraMaterial
        {
            get { return m_cameraMaterial; }
        }
        private static Material m_cameraMaterial = null;

        /// <summary>Brush preview material.</summary>
        public static Material BrushPreviewMaterial
        {
            get { return m_brushPreviewMaterial; }
        }
        private static Material m_brushPreviewMaterial = null;

        /// <summary>Static initialization</summary>
        static sfUserMaterials()
        {
            m_lockMaterial = LoadCopy("Locked");
            m_lockIconMaterial = sfDrawUtils.Instance.LoadMaterial("LockCutout");
            if (GraphicsSettings.defaultRenderPipeline != null)
            {
                m_cameraMaterial = GraphicsSettings.defaultRenderPipeline.defaultMaterial;
            }
            else
            {
                m_cameraMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Diffuse.mat");
            }
            m_brushPreviewMaterial = sfDrawUtils.Instance.LoadMaterial("NewBrushPreview");
            m_dragObjectMaterial = sfDrawUtils.Instance.LoadMaterial("Drag");
            m_dragObject2dMaterial = sfDrawUtils.Instance.LoadMaterial("Drag2D");
        }

        /// <summary>Creates lock materials with the given color.</summary>
        /// <param name="color"></param>
        /// <param name="lockMaterial">lockMaterial for meshes.</param>
        /// <param name="lockIconMaterial">lockIconMaterial for icons.</param>
        public static void CreateLockMaterialsForPlayer(Color color,
            out Material lockMaterial, out Material lockIconMaterial)
        {
            lockMaterial = TintCopy(LockMaterial, color);
            lockIconMaterial = TintCopy(LockIconMaterial, color);
        }

        /// <summary>Creates lock materials with the given color.</summary>
        /// <param name="color"></param>
        /// <param name="dragObjectMaterial">dragObjectMaterial for meshes.</param>
        /// <param name="for">for sprites.</param>
        public static void CreateDragObjectMaterialsForPlayer(Color color,
            out Material dragObjectMaterial, out Material dragObject2dMaterial)
        {
            dragObjectMaterial = TintCopy(m_dragObjectMaterial, color);
            dragObject2dMaterial = TintCopy(m_dragObject2dMaterial, color);
        }

        /// <summary>Loads a copy of the material with the given name in the materials folder.</summary>
        /// <param name="name">name of material to load.</param>
        /// <returns>copied material.</returns>
        private static Material LoadCopy(string name)
        {
            return Copy(sfDrawUtils.Instance.LoadMaterial(name));
        }

        /// <summary>Creates a tinted copy of a material.</summary>
        /// <param name="source">source to copy.</param>
        /// <param name="colour">colour to tint.</param>
        /// <returns>tinted copy.</returns>
        private static Material TintCopy(Material source, Color colour)
        {
            if (source == null)
            {
                return null;
            }
            Material material = Copy(source);
            material.SetColor("m_colour", colour);
            return material;
        }

        /// <summary>Copies a material.</summary>
        /// <param name="source">source to copy.</param>
        /// <returns>copied material.</returns>
        private static Material Copy(Material source)
        {
            if (source == null)
            {
                return null;
            }
            Material material = Material.Instantiate(source);
            material.hideFlags = HideFlags.HideAndDontSave;
            material.name = source.name;// Keep the same name so we can do name comparisons
            return material;
        }
    }
}
