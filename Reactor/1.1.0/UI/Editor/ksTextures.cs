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
using UnityEditor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Provides access to Reactor texture assets.</summary>
    public class ksTextures
    {
        /// <summary>Reactor logo</summary>
        public static Texture2D Logo
        {
            get { return m_logo; }
        }
        private static Texture2D m_logo = Load("Reactor", true);

        /// <summary>Online icon</summary>
        public static Texture2D Online
        {
            get { return m_online; }
        }
        private static Texture2D m_online = Load("Online");

        /// <summary>Offline icon</summary>
        public static Texture2D Offline
        {
            get { return m_offline; }
        }
        private static Texture2D m_offline = Load("Offline");

        /// <summary>Loads a texture.</summary>
        /// <param name="name">Name of texture to load.</param>
        /// <param name="themed">
        /// If true, will append either "Dark" or "Light" to the icon name depending on if Unity is using the light or
        /// dark theme.
        /// </param>
        /// <returns>Loaded texture, or null if texture failed to load.</returns>
        private static Texture2D Load(string name, bool themed = false)
        {
            if (themed)
            {
                name += EditorGUIUtility.isProSkin ? "Dark" : "Light";
            }
            string path = ksPaths.Textures + name + ".png";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                ksLog.Error(typeof(ksTextures).ToString(), "Unable to load texture at " + path);
            }
            return texture;
        }
    }
}
