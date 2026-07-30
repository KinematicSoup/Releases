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
using System;
using UnityEngine;
using UnityEditor;
using KS.SF.Reactor;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>Draw utility functions</summary>
    internal class sfDrawUtils
    {
        /// <summary>Singleton instance</summary>
        public static sfDrawUtils Instance
        {
            get { return m_instance; }
        }
        private static sfDrawUtils m_instance = new sfDrawUtils();

        /// <summary>Singleton constructor</summary>
        private sfDrawUtils()
        {

        }

        /// <summary>Loads a material.</summary>
        /// <param name="name">name of material to load.</param>
        /// <returns>material, or null if material failed to load.</returns>
        public Material LoadMaterial(string name)
        {
            string path = sfPaths.Materials + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                ksLog.Error(this, "Unable to load material at " + path);
            }
            return material;
        }

        /// <summary>Draws a user icon.</summary>
        /// <param name="position">position to draw to.</param>
        /// <param name="colour">colour of icon.</param>
        /// <param name="tooltip">tooltip for the icon.</param>
        public void DrawUserIcon(Rect position, ksColor colour, string tooltip = null)
        {
            if (sfTextures.SmileyBase == null || sfTextures.SmileyFace == null)
            {
                return;
            }
            Color oldColour = GUI.color;
            GUI.color = colour;
            GUI.Label(position, sfTextures.SmileyBase);
            GUI.color = colour.Luma() >= .5f ? Color.black : Color.white;
            if (string.IsNullOrEmpty(tooltip))
            {
                GUI.Label(position, sfTextures.SmileyFace);
            }
            else
            {
                GUI.Label(position, new GUIContent(sfTextures.SmileyFace, tooltip));
            }
            GUI.color = oldColour;
        }

        /// <summary>Draws a user icon within a rectangle with the given texture coordinates.</summary>
        /// <param name="position">position to draw to.</param>
        /// <param name="colour">colour of icon.</param>
        /// <param name="texCoords."></param>
        public void DrawUserIconWithTexCoords(Rect position, ksColor colour, Rect texCoords)
        {
            if (sfTextures.SmileyBase == null || sfTextures.SmileyFace == null)
            {
                return;
            }
            Color oldColour = GUI.color;
            GUI.color = colour;
            GUI.DrawTextureWithTexCoords(position, sfTextures.SmileyBase, texCoords);
            GUI.color = colour.Luma() >= .5f ? Color.black : Color.white;
            GUI.DrawTextureWithTexCoords(position, sfTextures.SmileyFace, texCoords);
            GUI.color = oldColour;
        }

        /// <summary>Draws a rectangle with rounded corners.</summary>
        /// <param name="rectangle">rectangle dimensions to draw.</param>
        /// <param name="colour">colour to draw.</param>
        /// <param name="cornderRadius">cornderRadius in pixels.</param>
        public void DrawRoundedBox(Rect rectangle, Color colour, float cornerRadius)
        {
            DrawBorderedTexture(rectangle, sfTextures.RoundCorners, colour, cornerRadius, 10);
        }

        /// <summary>
        /// Draws a stretched texture with a border formed by the edges of the texture. The border corners will
        /// not be stretched, the top and bottom will be stretched horizontally, and the left and right sides will
        /// be stretched vertically.
        /// </summary>
        /// <param name="rectangle">rectangle dimensions to draw.</param>
        /// <param name="texture">texture to draw.</param>
        /// <param name="colour">colour to tint the texture.</param>
        /// <param name="border">border thickness in pixels.</param>
        /// <param name="srcBorder">source-texture-border thickness in pixels.</param>
        public void DrawBorderedTexture(Rect rectangle, Texture2D texture, Color colour, float border, float srcBorder)
        {
            if (texture == null)
            {
                return;
            }
            border = Math.Min(border, rectangle.width / 2);
            border = Math.Min(border, rectangle.height / 2);
            srcBorder = Math.Min(srcBorder / texture.width, .5f);
            float[] posXs = { rectangle.x, rectangle.x + border, rectangle.x + rectangle.width - border };
            float[] srcXs = { 0, srcBorder, 1 - srcBorder };
            float[] posYs = { rectangle.y, rectangle.y + border, rectangle.y + rectangle.height - border };
            float[] srcYs = { 1 - srcBorder, srcBorder, 0 };
            float[] widths = { border, rectangle.width - border * 2, border };
            float[] srcWidths = { srcBorder, 1 - srcBorder * 2, srcBorder };
            float[] heights = { border, rectangle.height - border * 2, border };
            float[] srcHeights = { srcBorder, 1 - srcBorder * 2, srcBorder };
            Color oldColour = GUI.color;
            GUI.color = colour;
            for (int x = 0; x < 3; x++)
            {
                if (widths[x] <= 0 || srcWidths[x] <= 0)
                {
                    continue;
                }
                for (int y = 0; y < 3; y++)
                {
                    if (heights[y] <= 0 || srcHeights[y] <= 0)
                    {
                        continue;
                    }
                    Rect destRect = new Rect(posXs[x], posYs[y], widths[x], heights[y]);
                    Rect srcRect = new Rect(srcXs[x], srcYs[y], srcWidths[x], srcHeights[y]);
                    GUI.DrawTextureWithTexCoords(destRect, texture, srcRect);
                }
            }
            GUI.color = oldColour;
        }
    }
}
