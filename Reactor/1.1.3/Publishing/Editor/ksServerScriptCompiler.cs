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

using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using KS.Unity.Editor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>This class compiles the KSServerRuntime project.</summary>
    public class ksServerScriptCompiler
    {

        /// <summary>Singleton instance</summary>
        public static ksServerScriptCompiler Instance
        {
            get { return m_instance; }
        }
        private static ksServerScriptCompiler m_instance = new ksServerScriptCompiler();

        /// <summary>Compile complete handler.</summary>
        /// <param name="result">Compile result</param>
        public delegate void CompileCompleteHandler(CompileResults result);

        /// <summary>
        /// Callback for custom compiling code.
        /// </summary>
        /// <param name="workingDirectory"></param>
        /// <param name="buildToolPath"></param>
        /// <param name="runtimeProjectPath"></param>
        /// <returns></returns>
        public delegate CompileResults CustomCompileCallback(string workingDirectory, string buildToolPath, string runtimeProjectPath);

        /// <summary>
        /// Sets a custom compile callback to use in place of the default callbacks. The callback regigstered
        /// will be invoked in a standalone thread.
        /// </summary>
        public static CustomCompileCallback CompileCallback = null;

        /// <summary>Compile results</summary>
        public enum CompileResults
        {
            /// <summary>Build succeeded</summary>
            SUCCESS = 0,
            /// <summary>Build failed</summary>
            FAILURE = 1,
            /// <summary>Nothing built because everything was up-to-date.</summary>
            UP_TO_DATE = 2,
            /// <summary>An error occurred with the build process.</summary>
            ERROR = 3,
            /// <summary>Could not build because we could not detect the operating system.</summary>
            UNKNOWN_OS = 4,
            /// <summary>Did not build because another build is currently in progress.</summary>
            BUILD_IN_PROGRESS = 5
        }

        /// <summary>Build configurations</summary>
        public enum Configurations
        {
            /// <summary>Local release configuration.</summary>
            LOCAL_RELEASE = 0,
            /// <summary>Local debug configuration.</summary>
            LOCAL_DEBUG = 1,
            /// <summary>Online release configuration.</summary>
            ONLINE_RELEASE = 2,
            /// <summary>Online debug configuration.</summary>
            ONLINE_DEBUG = 3
        }

        private ksServerProjectUpdater m_projectUpdater = ksServerProjectUpdater.Instance;
        private CompileResults m_result;
        private Configurations m_configuration;
        private CompileCompleteHandler m_callback;
        private Thread m_thread;
        private bool m_playWhenCompiled = false;
        private bool m_restartServers = false;

        /// <summary>Is the server runtime compiling?</summary>
        public bool IsCompiling
        {
            get { return m_thread != null && m_thread.IsAlive; }
        }

        /// <summary>Singleton constructor</summary>
        private ksServerScriptCompiler()
        {
        }

        /// <summary>Builds the serverruntime syncronously.</summary>
        /// <param name="rebuild">If true, will force a rebuild.</param>
        /// <param name="configuration">Build configuration.</param>
        /// <param name="restartServers">Restart servers after building.</param>
        /// <returns>Compile result</returns>
        public CompileResults BuildServerRuntimeSync(
            bool rebuild = false,
            Configurations configuration = Configurations.LOCAL_RELEASE,
            bool restartServers = true)
        {
            if (m_thread != null && m_thread.IsAlive)
            {
                return CompileResults.BUILD_IN_PROGRESS;
            }
            m_result = CompileResults.ERROR;
            BuildServerRuntime(rebuild, null, configuration, restartServers);
            if (m_thread != null && m_thread.IsAlive)
            {
                m_thread.Join();
            }
            CheckCompileStatus();
            return m_result;
        }

        /// <summary>Builds the serverruntime asyncronously.</summary>
        /// <param name="rebuild">If true, will force a rebuild.</param>
        /// <param name="callback">Called when compile finishes.</param>
        /// <param name="configuration">Build configuration.</param>
        /// <param name="restartServers">Restart servers after building.</param>
        public void BuildServerRuntime(
            bool rebuild = false,
            CompileCompleteHandler callback = null,
            Configurations configuration = Configurations.LOCAL_RELEASE,
            bool restartServers = true)
        {
            if (ksEditorUtils.IsMPPMVirtualPlayer)
            {
                // Do not build from an MPPM virtual player instance.
                return;
            }
            if (!Directory.Exists(ksPaths.ServerDir))
            {
                ksLog.Error("Unable to locate server build tools.");
                if (callback != null)
                {
                    callback(CompileResults.ERROR);
                }
                return;
            }

            if (m_thread != null && m_thread.IsAlive)
            {
                if (callback != null)
                {
                    callback(CompileResults.BUILD_IN_PROGRESS);
                }
                return;
            }

            m_result = CompileResults.ERROR;
            m_restartServers = restartServers;
            m_callback = callback;
            m_projectUpdater.UpdateIncludes();

            if (!CheckBuildToolPath())
            {
                return;
            }

            ksBuildEvents.InvokePreBuildServer(configuration);
            m_configuration = configuration;
            Build(rebuild);
            EditorApplication.update += CheckCompileStatus;
            EditorApplication.playModeStateChanged += DisablePlayMode;
        }

        /// <summary>
        /// Checks if compiling is finished. Calls the callback passed to
        /// <see cref="BuildServerRuntime(bool, CompileCompleteHandler)"/> when compiling finishes, and reloads 
        /// proxy scripts after a successful compile.
        /// </summary>
        private void CheckCompileStatus()
        {
            if (m_thread == null || !m_thread.IsAlive)
            {
                try
                {
                    ksServerProjectWatcher.Get().RequiresServerBuild = false;
                    ksBuildEvents.InvokePostBuildServer(m_configuration, m_result == CompileResults.SUCCESS);
                    if (m_result == CompileResults.SUCCESS)
                    {
                        // Reload proxy scripts
                        AssetDatabase.Refresh();
                        MoveAssembliesAndRestartServers();
                    }
                    if (m_callback != null)
                    {
                        m_callback(m_result);
                        m_callback = null;
                    }
                }
                finally
                {
                    EditorApplication.update -= CheckCompileStatus;
                    EditorApplication.playModeStateChanged -= DisablePlayMode;
                    if (m_playWhenCompiled)
                    {
                        m_playWhenCompiled = false;
                        EditorApplication.isPlaying = true;
                    }
                }
            }
        }

        /// <summary>
        /// Moves the built assemblies to the <see cref="ksPaths.ImageDir"/>. Restarts local servers.
        /// </summary>
        private void MoveAssembliesAndRestartServers()
        {
            try
            {
                // Stop local servers so we can overwrite the server runtime dll.
                ksLocalServer.Manager.StopServers();
                // Copy dlls from the output to the build directory.
                ksPathUtils.CopyFiles(
                    ksPaths.ServerDir + "KSServerRuntime",
                    ksPaths.ImageDir,
                    new string[] { ".dll", ".pdb" },
                    new string[] { "KSReactor", "KSCommon", "KSLZMA" });

                // Restart local servers.
                if (m_restartServers)
                {
                    ksLocalServer.Manager.RestartOrRemoveStoppedServers();
                }
            }
            catch (Exception e)
            {
                ksLog.LogException(this, e);
            }
        }

        /// <summary>Prevents Unity from entering play mode until server compilation is finished.</summary>
        /// <param name="state">Play mode state</param>
        private void DisablePlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                EditorApplication.isPlaying = false;
                m_playWhenCompiled = true;
            }
        }

        /// <summary>Creates a <see cref="ProcessStartInfo"/> for the build process.</summary>
        /// <param name="args">Build arguments</param>
        /// <param name="toolPath">Path to build tool.</param>
        /// <param name="workingDirectory">Path to build tool.</param>
        /// <returns>Start info for the build process.</returns>
        private ProcessStartInfo BuildProcessStartInfo(string args, string toolPath, string workingDirectory)
        {
            ProcessStartInfo buildIStartInfo = new ProcessStartInfo();
            buildIStartInfo.CreateNoWindow = true;
            buildIStartInfo.FileName = toolPath;
            buildIStartInfo.Arguments = args;
            buildIStartInfo.RedirectStandardOutput = true;
            buildIStartInfo.UseShellExecute = false;
            buildIStartInfo.WorkingDirectory = workingDirectory;
            return buildIStartInfo;
        }

        /// <summary>Builds the KSServerRuntime Solution using dotnet.</summary>
        /// <param name="rebuild">If true, will force a rebuild.</param>
        private void Build(bool rebuild)
        {
            m_thread = new Thread(delegate()
            {
                try
                {
                    if (CompileCallback != null)
                    {
                        m_result = CompileCallback(
                            ksPaths.ProjectRoot + Path.DirectorySeparatorChar,
                            ksReactorConfig.Instance.Server.DotNetPath,
                            ksPathUtils.ToAbsolutePath(ksPaths.ServerRuntimeProject)
                        );
                    }
                    else
                    {
                        using (Process process = new Process())
                        {
                            string args = "build" +
                              " \"" + ksPaths.ServerRuntimeProject + "\"" +
                              " --configuration " + GetConfigurationName() +
                              " --runtime any" +
                              " --no-self-contained" +
                              " --no-incremental" +
                              " --nologo";
                            process.StartInfo = BuildProcessStartInfo(args, ksReactorConfig.Instance.Server.DotNetPath, ksPaths.ProjectRoot + Path.DirectorySeparatorChar);
                            ksLog.Info(this, "KSServerRuntime build command: " + ksReactorConfig.Instance.Server.DotNetPath + " " + args);
                            process.Start();
                            string output = process.StandardOutput.ReadToEnd();
                            if (process.ExitCode == 0)
                            {
                                ksLog.Info(this, "KSServerRuntime build succeeded: " + output);
                                m_result = CompileResults.SUCCESS;
                            }
                            else
                            {
                                ksLog.Error(this, "KSServerRuntime build failed: " + output);
                                m_result = CompileResults.FAILURE;
                            }
                            ParseOutput(output);
                        }
                    }
                }
                catch (Exception e)
                {
                    ksLog.Error(this, "KSServerRuntime build failed", e);
                    m_result = CompileResults.ERROR;
                }
            });
            m_thread.Start();
        }

        /// <summary>Gets the configuration name.</summary>
        /// <returns>Configuration name.</returns>
        private string GetConfigurationName()
        {
            switch (m_configuration)
            {
                default:
                case Configurations.LOCAL_RELEASE: return "LocalRelease";
                case Configurations.LOCAL_DEBUG: return "LocalDebug";
                case Configurations.ONLINE_RELEASE: return "OnlineRelease";
                case Configurations.ONLINE_DEBUG: return "OnlineDebug";
            }
        }

        /// <summary>Parses Visual Studio build output to find the number of projects with the given status.</summary>
        /// <param name="output">Visual Studio output</param>
        /// <param name="status">Status to parse for.</param>
        /// <returns>Number of projects with the given status. -1 if a parse error occurs.</returns>
        private int GetStatusCount(string output, string status)
        {
            int endIndex = output.LastIndexOf(status);
            if (endIndex <= 2)
                return -1;
            int startIndex = output.LastIndexOf(' ', endIndex - 2);
            if (startIndex == -1)
                return -1;
            string str = output.Substring(startIndex, endIndex - startIndex);
            int val;
            if (int.TryParse(str, out val))
            {
                return val;
            }
            return -1;
        }

        /// <summary>
        /// Parses output for errors and warnings to log. Looks for "[ERROR]" or "[WARNING]" and logs everything after
        /// that to the end of the line.
        /// </summary>
        /// <param name="output">Output to parse</param>
        private void ParseOutput(string output)
        {
            int index = output.IndexOf("[ERROR]");
            while (index >= 0)
            {
                int startIndex = index + 7;
                int endIndex = output.IndexOf("\n", startIndex);
                if (endIndex < 0)
                {
                    ksLog.Error(this, output.Substring(startIndex));
                    break;
                }
                ksLog.Error(this, output.Substring(startIndex, endIndex - startIndex - 1));
                index = output.IndexOf("[ERROR]", endIndex);
            }

            index = output.IndexOf("[WARNING]");
            while (index >= 0)
            {
                int startIndex = index + 9;
                int endIndex = output.IndexOf("\n", startIndex);
                if (endIndex < 0)
                {
                    ksLog.Warning(this, output.Substring(startIndex));
                    break;
                }
                ksLog.Warning(this, output.Substring(startIndex, endIndex - startIndex - 1));
                index = output.IndexOf("[WARNING]", endIndex);
            }
        }

        /// <summary>Prompts the user to enter a build tool path if one is not set.</summary>
        /// <returns>True if the build tool path is set.</returns>
        private bool CheckBuildToolPath()
        {
            string dotnetPath = ksReactorConfig.Instance.Server.DotNetPath;

            // Clear the dotnet path if the dotnet executable is not found
            if (!File.Exists(dotnetPath) || !(dotnetPath.EndsWith("dotnet.exe") || dotnetPath.EndsWith("dotnet")))
            {
                dotnetPath = "";
                ksReactorConfig.Instance.Server.DotNetPath = "";
            }

            // If the build tool path is empty, try to use the external script editor
            if (string.IsNullOrEmpty(dotnetPath))
            {
                dotnetPath = GetDotNetPath();
                if (File.Exists(dotnetPath))
                {
                    ksReactorConfig.Instance.Server.DotNetPath = dotnetPath;
                    return true;
                }
            }

            if (!File.Exists(ksReactorConfig.Instance.Server.DotNetPath))
            {
                EditorUtility.DisplayDialog(
                    "Unable to locate dotnet executable.",
                    "Please locate the dotnet executable file.",
                    "OK"
                );

                string path = EditorUtility.OpenFilePanel(
                    "Build Tool",
                    ksReactorConfig.Instance.Server.DotNetPath,
                    ""
                );
                if (!File.Exists(path) || !(path.EndsWith("dotnet.exe") || path.EndsWith("dotnet")))
                {
                    ksLog.Error(this, "Server Runtime Build Failed: " + path + " is an invalid dotnet build tool.");
                    return false;
                }
                ksReactorConfig.Instance.Server.DotNetPath = path;
                ksServerProjectUpdater.Instance.UpdateOutputPath();
            }
            return true;
        }

        /// <summary>Attempt to find the dotnet excutable path.</summary>
        /// <param name="os">Operating system</param>
        /// <returns>Dotnet executable path or null</returns>
        private string GetDotNetPath()
        {
            List<string> paths = new List<string>();
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                paths.Add($"{programFiles}\\dotnet\\dotnet.exe");
                // TODO: Use output of "where dotnet" command
            }
            else
            {
                paths.Add($"/usr/local/share/dotnet/dotnet");
                // TODO: Use output of "which dotnet" command
            }

            foreach (string path in paths)
            {
                if (File.Exists(path))
                {
                    ksReactorConfig.Instance.Server.DotNetPath = path;
                    return path;
                }
            }
            return null;
        }
    }
}