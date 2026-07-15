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
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using KS.SF.Reactor;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>Provides access to Scene Fusion texture assets.</summary>
    [InitializeOnLoad]
    public class sfTextures
    {
        private static Dictionary<string, Texture2D> m_cache = new Dictionary<string, Texture2D>();
        // When the package is imported, we cannot load the textures until the first update, so we don't cache nulls or
        // log errors when textures fail to load until the first update.
        private static bool m_cacheNulls = false;

        /// <summary>Static constructor</summary>
        static sfTextures()
        {
            EditorApplication.update += Initialize;
        }

        /// <summary>Initialization.</summary>
        private static void Initialize()
        {
            EditorApplication.update -= Initialize;
            m_cacheNulls = true;
        }

        /// <summary>Scene Fusion logo</summary>
        public static Texture2D Logo
        {
            get { return Get("SceneFusion", true); }
        }

        /// <summary>Scene Fusion logo with name.</summary>
        public static Texture2D LogoWithName
        {
            get { return Get("SFLogo", true); }
        }

        /// <summary>Activity spinner</summary>
        public static Texture2D Spinner
        {
            get { return Get("Spinner"); }
        }

        /// <summary>Texture for drawing rectangles with round corners.</summary>
        public static Texture2D RoundCorners
        {
            get { return Get("RoundCorners"); }
        }

        /// <summary>Texture for drawing rectangles with round corners and a border.</summary>
        public static Texture2D RoundCornersWithBorder
        {
            get { return Get("RoundCornersWithBorder"); }
        }

        /// <summary>Note tail</summary>
        public static Texture2D Tail
        {
            get { return Get("Tail"); }
        }

        /// <summary>Note thumb</summary>
        public static Texture2D Thumb
        {
            get { return Get("Thumb"); }
        }

        /// <summary>Smiley base</summary>
        public static Texture2D SmileyBase
        {
            get { return Get("SmileyBase"); }
        }

        /// <summary>Smiley face</summary>
        public static Texture2D SmileyFace
        {
            get { return Get("SmileyFace"); }
        }

        /// <summary>Lock icon</summary>
        public static Texture2D Locked
        {
            get { return Get("Locked"); }
        }

        /// <summary>Unlocked icon</summary>
        public static Texture2D Unlocked
        {
            get { return Get("Unlocked"); }
        }

        /// <summary>Partial lock icon</summary>
        public static Texture2D PartialLock
        {
            get { return Get("PartialLock"); }
        }

        /// <summary>Persistent lock icon</summary>
        public static Texture2D PermaLock
        {
            get { return Get("PermaLock"); }
        }

        /// <summary>Online icon</summary>
        public static Texture2D Online
        {
            get { return Get("Online"); }
        }

        /// <summary>Offline icon</summary>
        public static Texture2D Offline
        {
            get { return Get("Offline"); }
        }

        /// <summary>Large question icon</summary>
        public static Texture2D Question
        {
            get { return Get("Question"); }
        }

        /// <summary>Small question icon</summary>
        public static Texture2D QuestionSmall
        {
            get { return Get("QuestionSmall"); }
        }

        /// <summary>Warning icon</summary>
        public static Texture2D Warning
        {
            get { return Get("WarningLarge"); }
        }

        /// <summary>Small warning icon</summary>
        public static Texture2D WarningSmall
        {
            get { return Get("Warning"); }
        }

        /// <summary>Large note icon</summary>
        public static Texture2D Note
        {
            get { return Get("NoteBig"); }
        }

        /// <summary>Small note icon</summary>
        public static Texture2D NoteSmall
        {
            get { return Get("NoteSmall"); }
        }

        /// <summary>Note on icon</summary>
        public static Texture2D NoteOn
        {
            get { return Get("Note"); }
        }

        /// <summary>Note off icon</summary>
        public static Texture2D NoteOff
        {
            get { return Get("NoteOff"); }
        }

        /// <summary>Camera on icon</summary>
        public static Texture2D CameraOn
        {
            get { return Get("Camera"); }
        }

        /// <summary>Camera off icon</summary>
        public static Texture2D CameraOff
        {
            get { return Get("CameraOff"); }
        }

        /// <summary>Terrain brush on icon</summary>
        public static Texture2D TerrainBrushOn
        {
            get { return Get("TerrainBrush"); }
        }

        /// <summary>Terrain brush off icon</summary>
        public static Texture2D TerrainBrushOff
        {
            get { return Get("TerrainBrushOff"); }
        }

        // Scene Fusion 2 Textures

        /// <summary>Lock icon</summary>
        public static Texture2D Lock
        {
            get { return Get("lock16"); }
        }

        /// <summary>Checkmark icon</summary>
        public static Texture2D Check
        {
            get { return Get("check16"); }
        }

        /// <summary>Gets a texture from the cache, or loads and caches it if it was not in the cache.</summary>
        /// <param name="name">name of texture to load.</param>
        /// <param name="themed">
        /// If true, will append either "Dark" or "Light" to the icon name depending on if Unity
        /// is using the light or dark theme.
        /// </param>
        /// <returns>texture, or null if texture failed to load.</returns>
        private static Texture2D Get(string name, bool themed = false)
        {
            if (themed)
            {
                name += EditorGUIUtility.isProSkin ? "Dark" : "Light";
            }
            Texture2D texture;
            if (m_cache.TryGetValue(name, out texture))
            {
                return texture;
            }
            string path = sfPaths.Textures + name + ".png";
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                if (!m_cacheNulls)
                {
                    return null;
                }
                ksLog.Error(typeof(sfTextures).ToString(), "Unable to load texture at " + path);
            }
            m_cache[name] = texture;
            return texture;
        }
    }
}
