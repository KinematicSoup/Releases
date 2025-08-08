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
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using KS.Unity.Editor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Provides static access to paths used throughout the project.</summary>
    [InitializeOnLoad]
    public static class ksPaths
    {
        private static string LOG_CHANNEL = typeof(ksPaths).ToString();

        private const string REACTOR_SERVER = "KinematicSoup/Reactor/";
        private const string REACTOR_PACKAGE = "Packages/com.kinematicsoup.reactor/";
        private const string REACTOR_SCRIPTS = "Assets/ReactorScripts/";
        private const string SERVER_SCRIPTS = "Server/";
        private const string CLIENT_SCRIPTS = "Client/";
        private const string COMMON_SCRIPTS = "Common/";
        private const string PROXIE_SCRIPTS = "Proxies/";

        private static string m_projectRoot;
        private static string m_reactorRoot = null;

        private static List<string> m_commonFolders = new List<string>();
        private static List<string> m_serverFolders = new List<string>();

        /// <summary>Static intialization</summary>
        static ksPaths()
        {
            // Remove "Assets" from the end.
            m_projectRoot = Application.dataPath.Substring(0, Application.dataPath.Length - 6);
        }

        /// <summary>
        /// Detects the Reactor folder by querying Unity's asset database for the location of the ReactorRoot script.
        /// </summary>
        private static void FindReactorRoot()
        {
            string scriptName = "ReactorRoot";
            ScriptableObject script = ScriptableObject.CreateInstance(scriptName);
            if (script == null)
            {
                UseDefaultReactorFolder("Unable to load script " + scriptName);
                return;
            }
            string path = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(script));
            ScriptableObject.DestroyImmediate(script);
            if (path == null)
            {
                UseDefaultReactorFolder("Unable to get asset path for " + scriptName);
                return;
            }
            path = path.Replace('\\', '/');
            if (!path.StartsWith("Assets/") && !path.StartsWith(REACTOR_PACKAGE))
            {
                UseDefaultReactorFolder(scriptName + " asset path does not start with 'Assets/' or '"+REACTOR_PACKAGE+"'");
                return;
            }
            m_reactorRoot = path.Substring(0, path.LastIndexOf('/') + 1);
        }

        /// <summary>Root location of Reactor assets.</summary>
        public static string ReactorRoot
        {
            get
            {
                if (m_reactorRoot == null)
                {
                    FindReactorRoot();
                }
                return m_reactorRoot;
            }
        }

        /// <summary>Root of the Unity project.</summary>
        public static string ProjectRoot
        {
            get { return m_projectRoot; }
        }

        /// <summary>List of folders containing common scripts.</summary>
        public static List<string> CommonFolders
        {
            get { return m_commonFolders; }
        }

        /// <summary>List of folders containing server scripts.</summary>
        public static List<string> ServerFolders
        {
            get { return m_serverFolders; }
        }

        /// <summary>Iterates the <see cref="CommonFolders"/> and <see cref="ServerFolders"/>.</summary>
        /// <returns>Iterator</returns>
        public static IEnumerable<string> IterateCommonAndServerFolders()
        {
            return CommonFolders.Concat(ServerFolders);
        }

        /// <summary>
        /// Builds the lists of <see cref="CommonFolders"/> and <see cref="ServerFolders"/> by finding all asmref files
        /// that reference the <see cref="CommonScriptsAsmDef"/> and <see cref="ServerScriptsAsmDef"/> asmdefs. Folders
        /// containing an asmref referencing <see cref="CommonScriptsAsmDef"/> are common folders and folders
        /// containing an asmref referencing <see cref="ServerScriptsAsmDef"/> are server folders.
        /// </summary>
        public static void FindCommonAndServerFolders()
        {
            m_commonFolders.Clear();
            m_serverFolders.Clear();
            m_commonFolders.Add(CommonScripts);
            m_serverFolders.Add(ServerScripts);
            // Get the name and guid of the common and server script asmdefs.
            string commonGuid = AssetDatabase.AssetPathToGUID(CommonScriptsAsmDef);
            string commonGuidStr = string.IsNullOrEmpty(commonGuid) ? null : "GUID:" + commonGuid;
            string commonName = ksPathUtils.GetName(CommonScriptsAsmDef, false);
            string serverGuid = AssetDatabase.AssetPathToGUID(ServerScriptsAsmDef);
            string serverGuidStr = string.IsNullOrEmpty(serverGuid) ? null : "GUID:" + serverGuid;
            string serverName = ksPathUtils.GetName(ServerScriptsAsmDef, false);
            // Find all asmrefs
            string[] guids = AssetDatabase.FindAssets("t:AsmRef");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // Get the referenced asmdef string.
                string reference = ParseAsmReference(path);
                if (reference == null)
                {
                    continue;
                }
                // Add the path to the common or server folders if it is referencing the common or server asmdef.
                if (reference == commonName || reference == commonGuidStr)
                {
                    m_commonFolders.Add(ksPathUtils.GetDirectory(path).Replace('\\', '/') + "/");
                }
                else if (reference == serverName || reference == serverGuidStr)
                {
                    m_serverFolders.Add(ksPathUtils.GetDirectory(path).Replace('\\', '/') + "/");
                }
            }
        }

        /// <summary>Checks if a path is in one of the <see cref="CommonFolders"/> containing common scripts.</summary>
        /// <param name="path">Path to check.</param>
        /// <returns>True if the path is in one of the <see cref="CommonFolders"/>.</returns>
        public static bool IsCommonPath(string path)
        {
            return BelongsToAsmDef(path, m_commonFolders);
        }

        /// <summary>Checks if a path is in one of the <see cref="ServerFolders"/> containing server scripts.</summary>
        /// <param name="path">Path to check.</param>
        /// <returns>True if the path is in one of the <see cref="ServerFolders"/>.</returns>
        public static bool IsServerPath(string path)
        {
            return BelongsToAsmDef(path, m_serverFolders);
        }

        /// <summary>Location of textures.</summary>
        public static string Textures
        {
            get { return ReactorRoot + "UI/Editor/Icons/"; }
        }

        /// <summary>Folder containing developer Reactor scripts.</summary>
        public static string ReactorScripts
        {
            get { return REACTOR_SCRIPTS; }
        }

        /// <summary>Asset storing asset ids for Unity assets used as proxy assets.</summary>
        public static string AssetIdData
        {
            get { return REACTOR_SCRIPTS + "ksAssetIds.asset"; }
        }

        public static string DefaultLinearPredictor
        {
            get { return ReactorRoot + "Core/DefaultLinearPredictor.asset"; }
        }

        public static string DefaultConvergingInputPredictor
        {
            get { return ReactorRoot + "Core/DefaultConvergingInputPredictor.asset"; }
        }

        /// <summary>Reactor config file.</summary>
        public static string Config
        {
            get { return REACTOR_SCRIPTS + "Resources/ksReactorConfig.asset"; }
        }

        /// <summary>Gets the path to the file mapping IDs to assets for an asset bundle.</summary>
        /// <param name="assetBundle">Name to get file for, or null or empty string for resource assets.</param>
        /// <returns>Path to asset data file.</returns>
        public static string GetAssetDataPath(string assetBundle)
        {
            if (string.IsNullOrEmpty(assetBundle))
            {
                return REACTOR_SCRIPTS + "Resources/ksAssets.txt";
            }
            // File name must match ksPrefabCache.cs
            return AssetBundles + assetBundle + "/ksAssets.txt";
        }

        /// <summary>Path to folder containing entity data files for asset bundles.</summary>
        public static string AssetBundles
        {
            get { return REACTOR_SCRIPTS + "AssetBundles/"; }
        }

        /// <summary>Server scripts folder.</summary>
        public static string ServerScripts
        {
            get { return REACTOR_SCRIPTS + SERVER_SCRIPTS; }
        }

        /// <summary>Asmdef file for server scripts.</summary>
        public static string ServerScriptsAsmDef
        {
            get { return ServerScripts + "KSScripts-Server.asmdef"; }
        }

        /// <summary>Server runtime project.</summary>
        public static string ServerRuntimeProject
        {
            get { return ServerScripts + "KSServerRuntime.csproj"; }
        }

        /// <summary>Server runtime solution.</summary>
        public static string ServerRuntimeSolution
        {
            get { return ServerScripts + "KSServerRuntime.sln"; }
        }

        /// <summary>Local server runtime dll.</summary>
        public static string LocalServerRuntimeDll
        {
            get { return ImageDir + "KSServerRuntime.Local.dll"; }
        }

        /// <summary>Server runtime dll.</summary>
        public static string ServerRuntimeDll
        {
            get { return ImageDir + "KSServerRuntime.dll"; }
        }

        /// <summary>Default location for client scripts.</summary>
        public static string ClientScripts
        {
            get { return REACTOR_SCRIPTS + CLIENT_SCRIPTS; }
        }

        /// <summary>Default location for server proxy scripts.</summary>
        public static string Proxies
        {
            get { return REACTOR_SCRIPTS + PROXIE_SCRIPTS; }
        }

        /// <summary>Location of temporary proxy meta files.</summary>
        public static string TempMetafiles
        {
            get { return Proxies + ".Temp/"; }
        }

        /// <summary>Default location for common scripts.</summary>
        public static string CommonScripts
        {
            get { return REACTOR_SCRIPTS + COMMON_SCRIPTS; }
        }

        /// <summary>Asmdef file for common scripts.</summary>
        public static string CommonScriptsAsmDef
        {
            get { return CommonScripts + "KSScripts-Common.asmdef"; }
        }

        /// External location for Reactor files
        public static string External
        {
            get { return ProjectRoot + "KinematicSoup/Reactor/"; }
        }

        /// <summary>Reactor server directory.</summary>
        public static string ServerDir
        {
            get { 
                // Check for server path override
                if (!string.IsNullOrEmpty(ksReactorConfig.Instance.Server.OverrideServerPath))
                {
                    return ksReactorConfig.Instance.Server.OverrideServerPath; 
                }
                return External + "server/";
            }
        }

        /// <summary>Reactor build tools directory</summary>
        public static string BuildToolsDir
        {
            get
            {
                return ServerDir + "KSServerRuntime/";
            }
        }

        /// <summary>Reactor build directory.</summary>
        public static string ImageDir
        {
            get { return External + "image/"; }
        }

        /// <summary>Reactor cluster directory.</summary>
        public static string ClusterDir
        {
            get { return ServerDir + "cluster/"; }
        }

        /// <summary>Reactor log directory.</summary>
        public static string LogDir
        {
            get { return External + "logs/"; }
        }

        /// <summary>Game config file.</summary>
        public static string GameConfig
        {
            get { return ImageDir + "config.json"; }
        }

        /// <summary>Reactor scene config file for the active scene.</summary>
        /// <param name="scene">Scene name</param>
        /// <returns>Scene config file path.</returns>
        public static string SceneConfigFile(string scene)
        {
            return ImageDir + "config." + scene + ".json";
        }

        /// <summary>Reactor scene geometry file for the active scene.</summary>
        /// <param name="scene">Scene name</param>
        /// <returns>Scene geometry file path.</returns>
        public static string SceneGeometryFile(string scene)
        {
            return ImageDir + "geometry." + scene + ".dat";
        }

        /// <summary>Reactor geometry data file.</summary>
        public static string GeometryFile
        {
            get { return ImageDir + "geometry.dat"; }
        }

        /// <summary>Current operating system.</summary>
        public static string OperatingSystem
        {
            get
            {
                if (m_operatingSystem == null)
                {
                    string osstring = System.Environment.OSVersion.ToString();
                    string[] split = osstring.Split(' ');
                    m_operatingSystem = split[0];
                    return split[0];
                }
                else
                {
                    return m_operatingSystem;
                }
            }
        }
        private static string m_operatingSystem = null;

        /// <summary>Converts a prefab path to an entity type.</summary>
        /// <param name="assetPath">Asset path to prefab.</param>
        /// <param name="assetBundle">
        /// AssetBundle the prefab belongs to, or null or empty string if it is not part of an AssetBundle.
        /// </param>
        /// <returns>Entity type</returns>
        public static string GetEntityType(string assetPath, string assetBundle = null)
        {
            assetPath = assetPath.Replace("\\", "/");
            if (string.IsNullOrEmpty(assetBundle))
            {
                string resourcesStr = "/Resources/";
                int index = assetPath.IndexOf(resourcesStr);
                if (index < 0)
                {
                    return "";
                }
                assetPath = assetPath.Substring(index + resourcesStr.Length);
            }
            else
            {
                int index = assetPath.LastIndexOf("/");
                assetPath = assetBundle + ":" + assetPath.Substring(index + 1);
            }
            string ext = ".prefab";
            if (assetPath.EndsWith(ext))
            {
                assetPath = assetPath.Substring(0, assetPath.Length - ext.Length);
            }
            return assetPath;
        }

        /// <summary>Sets the Reactor folder to the default and logs an error message.</summary>
        /// <param name="errorMessage"></param>
        private static void UseDefaultReactorFolder(string errorMessage)
        {
            m_reactorRoot = Directory.Exists(REACTOR_PACKAGE)
                ? REACTOR_PACKAGE
                : "Assets/KinematicSoup/Reactor/";
            ksLog.Error(LOG_CHANNEL, "Error detecting Reactor folder: " + errorMessage +
                ". Setting Reactor folder to " + m_reactorRoot);
        }

        /// <summary>
        /// Checks if a <paramref name="path"/> is a subpath of one of the <paramref name="asmDefFolders"/> and is part
        /// of the same asmdef or asmref defined in that folder.
        /// </summary>
        /// <param name="path">Path to check.</param>
        /// <param name="asmDefFolders">
        /// List of folders containing asmdef/refs to check if the <paramref name="path"/> is in.
        /// </param>
        /// <returns>
        /// True if the <paramref name="path"/> is a subpath of one of the <paramref name="asmDefFolders"/> and is part
        /// of the same asmdef or asmref.
        /// </returns>
        private static bool BelongsToAsmDef(string path, List<string> asmDefFolders)
        {
            path = ksPathUtils.Clean(path);
            if (!Directory.Exists(path))
            {
                path = ksPathUtils.GetDirectory(path);
            }
            bool foundAsm = false;
            foreach (string p in asmDefFolders)
            {
                string folder = ksPathUtils.Clean(p);
                // If the path is in this folder...
                if (path.StartsWith(folder))
                {
                    // Check if any of the subfolders between the path and the folder contain an asmdef or asmref.
                    while (true)
                    {
                        if (path.Length == folder.Length)
                        {
                            return true;
                        }
                        if (foundAsm || Directory.GetFiles(path, "*.asmdef").Length != 0 ||
                            Directory.GetFiles(path, "*.asmref").Length != 0)
                        {
                            foundAsm = true;
                            break;
                        }
                        // There is no asmdef/asmref in this folder, so check the parent folder.
                        path = ksPathUtils.GetDirectory(path);
                    }
                }
            }
            return false;
        }

        /// <summary>Parses an asmref to get the referenced asmdef string.</summary>
        /// <param name="path">Path to asmref.</param>
        /// <returns>
        /// Referenced asmdef string. This is either the name of the asmdef, or `GUID:X` where X is the asmdef guid.
        /// Returns null if the asmdef could not be parsed.
        /// </returns>
        private static string ParseAsmReference(string path)
        {
            try
            {
                // The asmref file should have a single field called "reference". Find the "reference" field.
                string fileText = File.ReadAllText(path);
                string searchStr = "\"reference\":";
                int startIndex = fileText.IndexOf(searchStr);
                if (startIndex < 0)
                {
                    ksLog.Warning(LOG_CHANNEL, "Could not find reference in '" + path + "'.");
                    return null;
                }
                startIndex = fileText.IndexOf('"', startIndex + searchStr.Length);
                startIndex++;
                int endIndex = fileText.IndexOf('"', startIndex);
                if (startIndex >= endIndex)
                {
                    return null;
                }
                // Return the value of the "reference" field.
                return fileText.Substring(startIndex, endIndex - startIndex);
            }
            catch (Exception e)
            {
                ksLog.Error(LOG_CHANNEL, "Error parsing '" + path + "'.", e);
                return null;
            }
        }
    }
}