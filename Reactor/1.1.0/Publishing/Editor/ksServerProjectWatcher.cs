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
using UnityEditor;
using UnityEngine;
using KS.Unity.Editor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>
    /// Monitors the server runtime project. If it detects changes in .cs files, it will build the project when Unity
    /// has focus.
    /// </summary>
    public class ksServerProjectWatcher : ksSingleton<ksServerProjectWatcher>
    {
        /// <summary>Contains old path and new path for a file renaming.</summary>
        [Serializable]
        private struct RenameInfo
        {
            /// <summary>Path the file was moved from.</summary>
            public string OldPath;

            /// <summary>Path the file was moved to.</summary>
            public string NewPath;

            /// <summary>Constructor</summary>
            /// <param name="oldPath">Old path</param>
            /// <param name="newPath">New path</param>
            public RenameInfo(string oldPath, string newPath)
            {
                OldPath = oldPath;
                NewPath = newPath;
            }
        }

        [SerializeField]
        private bool m_compileRuntime = true;

        [SerializeField]
        private List<string> m_renameKeys = new List<string>();

        [SerializeField]
        private List<RenameInfo> m_renameValues = new List<RenameInfo>();

        private Dictionary<string, RenameInfo> m_renameData = new Dictionary<string, RenameInfo>();
        private HashSet<string> m_ignoredFiles = new HashSet<string>();
        private ksServerScriptCompiler m_compiler = ksServerScriptCompiler.Instance;

        /// <summary>Was the server project modified?</summary>
        public bool ServerProjectDirty
        {
            get { return m_serverProjectDirty; }
            set { m_serverProjectDirty = value; }
        }
        private bool m_serverProjectDirty = false;

        /// <summary>
        /// Begins monitoring the server runtime project. Builds the server project if Unity just started up.
        /// </summary>
        public void Run()
        {
            // Deserialize rename data
            if (m_renameKeys.Count > 0)
            {
                ksBuildEvents.PreBuildServer += MoveMetaFiles;
                for (int i = 0; i < m_renameKeys.Count; i++)
                {
                    m_renameData[m_renameKeys[i]] = m_renameValues[i];
                }
                m_renameKeys.Clear();
                m_renameValues.Clear();
            }

            if (m_compileRuntime)
            {
                // Prevent Unity from compiling until we generate proxies.
                AssetDatabase.DisallowAutoRefresh();
                EditorApplication.delayCall += Compile;
            }
            ksFileWatcher.Flags flags = ksFileWatcher.Flags.ALL;
            if (ksPathUtils.Create(ksPaths.CommonScripts, true) && ksPathUtils.Create(ksPaths.ServerScripts, true))
            {
                string[] paths = ksPaths.IterateCommonAndServerFolders().ToArray();
                new ksFileWatcher(OnChange, "*.cs|*.dll", flags, paths).Start();
            }
        }

        /// <summary>Serializes rename data so it persists across recompiles/play mode.</summary>
        public void OnDisable()
        {
            m_renameKeys.Clear();
            m_renameValues.Clear();
            foreach (KeyValuePair<string, RenameInfo> pair in m_renameData)
            {
                m_renameKeys.Add(pair.Key);
                m_renameValues.Add(pair.Value);
            }
        }

        /// <summary>Tells the watcher to ignore the next event from a given path.</summary>
        /// <param name="path">Path to ignore next event from.</param>
        public void Ignore(string path)
        {
            m_ignoredFiles.Add(ksPathUtils.ToAbsolutePath(path).Replace('\\', '/'));
        }

        /// <summary>
        /// Called when a .cs file changes in the server runtime project. Adds event listener to compile the project
        /// when Unity has focus, unless the changed file is in the file hash of files to ignore once. If this was a
        /// rename event, instead tracks which file was renamed and which corresponding proxy meta file should be
        /// renamed.
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">Event args</param>
        private void OnChange(object sender, FileSystemEventArgs e)
        {
            string path = e.FullPath.Replace('\\', '/');
            if (m_ignoredFiles.Contains(path))
            {
                m_ignoredFiles.Remove(path);
                return;
            }
            string assetPath = ksPathUtils.ToAssetPath(path);
            if (!ksPaths.IsServerPath(assetPath) && !ksPaths.IsCommonPath(assetPath))
            {
                // This is in a subfolder that has a different asmdef making it not a server or common folder.
                return;
            }
            RenamedEventArgs args = e as RenamedEventArgs;
            if (args != null)
            {
                if (args.OldFullPath.EndsWith(".cs") && path.EndsWith(".cs"))
                {
                    string oldPath = args.OldFullPath.Replace('\\', '/');
                    HandleRename(oldPath, path);
                }
            }
            if (!m_compileRuntime)
            {
                if (!ksReactorConfig.Instance.Server.AutoRebuildServerRuntime)
                {
                    m_serverProjectDirty = true;
                    if (path.EndsWith(".dll"))
                    {
                        ksServerProjectUpdater.Instance.UpdateAsmDefPrecompiledReferences();
                    }
                    return;
                }
                m_compileRuntime = true;
                // This function is called off the main thread, and CompileWhenFocused uses Unity function calls that
                // can only be called on the main thread, so we schedule it to be called on the main thread.
                EditorApplication.update += CompileWhenFocused;
            }
        }

        /// <summary>
        /// Handles rename events for server scripts. Tracks which file was renamed and which corresponding proxy meta
        /// file should be renamed.
        /// </summary>
        /// <param name="oldPath">Path server script was moved from.</param>
        /// <param name="newPath">Path server script was moved to.</param>
        private void HandleRename(string oldPath, string newPath)
        {
            string oldName = ksPathUtils.GetName(oldPath);
            string newName = ksPathUtils.GetName(newPath);
            if (oldName == newName)
            {
                return;
            }
            RenameInfo info;
            if (m_renameData.TryGetValue(oldName, out info))
            {
                m_renameData.Remove(oldName);
                info.NewPath = ksPaths.TempMetafiles + newName;
                m_renameData[newName] = info;
                return;
            }
            string oldProxyPath = FindProxyPath(oldPath);
            if (oldProxyPath != null)
            {
                if (m_renameData.Count == 0)
                {
                    ksBuildEvents.PreBuildServer += MoveMetaFiles;
                }
                // We rename and move the proxy to a temp folder where it is found by the reflection tool.
                string newProxyPath = ksPaths.TempMetafiles + newName;
                m_renameData[newName] = new RenameInfo(oldProxyPath + ".meta", newProxyPath + ".meta");
            }
        }

        /// <summary>Compiles the server runtime project when Unity has focus.</summary>
        private void CompileWhenFocused()
        {
            EditorApplication.update -= CompileWhenFocused;
            if (EditorApplication.isCompiling)
            {
                // Wait until Unity finishes compiling before we compile.
                return;
            }
            // Prevent Unity from compiling until we generate proxies.
            AssetDatabase.DisallowAutoRefresh();
            // Compile when Unity is focused.
            if (ksEditorUtils.IsUnityFocused)
            {
                Compile();
            }
            else
            {
                ksEditorEvents.OnFocusChange += Compile;
            }
        }

        /// <summary>Compiles the server runtime project if Unity is focused.</summary>
        /// <param name="focused">True if Unity is focused.</param>
        private void Compile(bool focused)
        {
            if (focused)
            {
                ksEditorEvents.OnFocusChange -= Compile;
                Compile();
            }
        }

        /// <summary>Compiles the server runtime project.</summary>
        private void Compile()
        {
            EditorApplication.LockReloadAssemblies();
            m_compiler.BuildServerRuntime(false, delegate (ksServerScriptCompiler.CompileResults results)
            {
                m_compileRuntime = false;
                try
                {
                    // Allow Unity to recompile.
                    AssetDatabase.AllowAutoRefresh();
                }
                catch(Exception){} // Do nothing
                EditorApplication.UnlockReloadAssemblies();
            });
        }

        /// <summary>Finds the corresponding proxy file path for a server script path.</summary>
        /// <param name="path">Path to server script to get proxy path for.</param>
        private string FindProxyPath(string path)
        {
            try
            {
                string name = ksPathUtils.GetName(path);
                string[] paths = Directory.GetFiles(ksPaths.Proxies, name, SearchOption.AllDirectories);
                if (paths.Length == 0)
                {
                    return null;
                }
                if (paths.Length > 1)
                {
                    ksLog.Warning(this, "Cannot rename proxy file for '" + path + "' because multiple proxies were " +
                        "found with the same name. This may cause missing script references.");
                    return null;
                }
                return paths[0].Replace('\\', '/');
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Error finding proxy path for " + path, e);
            }
            return null;
        }

        /// <summary>
        /// Renames proxy meta files for renamed server scripts and moves them to a temp folder where they will be found
        /// by the reflection tool.
        /// </summary>
        /// <param name="configuration">Build configuration</param>
        private void MoveMetaFiles(ksServerScriptCompiler.Configurations configuration)
        {
            if (configuration == ksServerScriptCompiler.Configurations.ONLINE_RELEASE)
            {
                return;
            }
            ksBuildEvents.PreBuildServer -= MoveMetaFiles;
            if (m_renameData.Count == 0)
            {
                return;
            }
            ksPathUtils.Create(ksPaths.TempMetafiles, true);
            foreach (RenameInfo info in m_renameData.Values)
            {
                try
                {
                    if (File.Exists(info.OldPath))
                    {
                        File.Move(info.OldPath, info.NewPath);
                    }
                }
                catch (Exception e)
                {
                    ksLog.Error(this, "Error moving '" + info.OldPath + "' to '" + info.NewPath + "'.", e);
                }
            }
            m_renameData.Clear();
        }
    }
}