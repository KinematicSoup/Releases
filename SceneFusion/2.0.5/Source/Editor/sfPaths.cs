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
using System.IO;
using UnityEditor;
using UnityEngine;
using KS.SF.Reactor;
using UnityEngine.SceneManagement;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>Provides static access to paths used throughout the project.</summary>
    public partial class sfPaths
    {
        private static string LOG_CHANNEL = typeof(sfPaths).ToString();
        private static string m_packageRoot = null;
        private static string m_assetsPath = Application.dataPath;

        /// <summary>
        /// Detects the Fusion folder by querying Unity's asset database for the location of the FusionRoot script.
        /// </summary>
        private static void FindPackageRoot()
        {
            string scriptName = "FusionRoot";
            ScriptableObject script = ScriptableObject.CreateInstance(scriptName);
            if (script == null)
            {
                UseDefaultPackageFolder("Unable to load script " + scriptName);
                return;
            }
            string path = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(script));
            ScriptableObject.DestroyImmediate(script);
            if (path == null)
            {
                UseDefaultPackageFolder("Unable to get asset path for " + scriptName);
                return;
            }
            path = path.Replace('\\', '/');
            if (!path.StartsWith("Assets/") && !path.StartsWith(Package))
            {
                UseDefaultPackageFolder(scriptName + " asset path does not start with Assets/  or '"+ Package + "'");
                return;
            }
            m_packageRoot = path.Substring(0, path.LastIndexOf('/') + 1);
        }

        private static string Package
        {
            get { return "Packages/" + sfPackageInfo.PACKAGE_ID; }
        }

        /// <summary>Root of the Unity project</summary>
        public static string ProjectRoot
        {
            get
            {
                return m_assetsPath.Substring(0, m_assetsPath.Length - 6);// Remove Assets from end
            }
        }

        /// <summary>Root location of the Scene Fusion package</summary>
        public static string PackageRoot
        {
            get
            {
                if (m_packageRoot == null)
                {
                    FindPackageRoot();
                }
                return m_packageRoot;
            }
        }

        /// <summary>Root location of the Scene Fusion folder under Assets</summary>
        public static string AssetRoot
        {
            get { return "Assets/KinematicSoup/SceneFusion/"; }
        }

        /// <summary>Location of textures.</summary>
        public static string Textures
        {
            get { return PackageRoot + "Textures/"; }
        }

        /// <summary>Location of materials.</summary>
        public static string Materials
        {
            get { return PackageRoot + "Materials/"; }
        }

        /// <summary>External location for files that should not be included under the project assets folder</summary>
        public static string External
        {
            get { return ProjectRoot + "KinematicSoup/SceneFusion/"; }
        }

        /// <summary>Expected location of SF client logs</summary>
        public static string ExternalLogs
        {
            get { return External + "logs/"; }
        }

        /// <summary>Location of temporary scene assets.</summary>
        public static string Temp
        {
            get { return AssetRoot + "Temp/"; }
        }

        /// <summary>Location of stand-in template assets.</summary>
        public static string StandIns
        {
            get { return PackageRoot + "Stand-Ins/"; }
        }

        /// <summary>Location of prefab assets.</summary>
        public static string Prefabs
        {
            get { return PackageRoot + "Prefabs/"; }
        }

        /// <summary>Name of scene</summary>
        public static string SceneName
        {
            get
            {
                string name = SceneManager.GetActiveScene().name;
                if (string.IsNullOrEmpty(name))
                {
                    name = "Untitled";
                }
                return name;
            }
        }

        /// <summary>Singleton constructor</summary>
        private sfPaths()
        {

        }

        /// <summary>
        /// Creates a folder relative to the project root if it does not exist and refreshes Unity's asset
        /// database so Unity notices the folder.
        /// </summary>
        /// <param name="path">path to create relative to the project root.</param>
        /// <returns>false if the path could not be created and did not already exist.</returns>
        public static bool CreateAssetPath(string path)
        {
            try
            {
                path = sfPaths.ProjectRoot + path;
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    AssetDatabase.Refresh();
                }
                return true;
            }
            catch (Exception e)
            {
                ksLog.Error(LOG_CHANNEL, "Error creating " + path, e);
                return false;
            }
        }

        /// <summary>
        /// Deletes a folder and its meta file relative to the project root and refreshes Unity's asset database so
        /// Unity notices the folder is gone.
        /// </summary>
        /// <param name="path">path to delete relative to the project root.</param>
        public static void DeleteAssetPath(string path)
        {
            try
            {
                path = sfPaths.ProjectRoot + path;
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);

                    // Delete meta file
                    string metaPath = path;
                    if (metaPath.EndsWith("/") || metaPath.EndsWith("\\"))
                    {
                        metaPath = metaPath.Substring(0, metaPath.Length - 1);
                    }
                    metaPath += ".meta";
                    if (File.Exists(metaPath))
                    {
                        File.Delete(metaPath);
                    }

                    AssetDatabase.Refresh();
                }
            }
            catch (Exception e)
            {
                ksLog.Error(LOG_CHANNEL, "Error deleting " + path, e);
            }
        }

        /// <summary>Sets the package root folder to the default and logs an error message.</summary>
        /// <param name="errorMessage"></param>
        private static void UseDefaultPackageFolder(string errorMessage)
        {
            m_packageRoot = Directory.Exists(Package)
                ? Package + '/'
                : "Assets/SceneFusionAssets";
            ksLog.Warning(LOG_CHANNEL, "Error detecting Scene Fusion folder: " + errorMessage +
                ". Setting Reactor folder to " + m_packageRoot);
        }
    }
}
