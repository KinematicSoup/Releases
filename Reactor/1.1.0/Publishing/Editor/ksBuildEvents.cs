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

using System.IO;
using UnityEngine.SceneManagement;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>
    /// Reactor config and runtime build event manager.  
    /// This class can be used by developers to invoke code during different stages of building. 
    /// Examples: 
    ///     A pre-build-server event can be registered by a developer to write scripts which convert a list of strings 
    ///     in the UI to a script containing enums for those strings prior to compiling the KSServerRuntime assembly.
    ///     
    ///     A pre-build-config event can be registered by a developer to extract and write scene navigation data to 
    ///     the project directory to be loaded by a server script.
    /// </summary>
    public static class ksBuildEvents
    {
        /// <summary>Callback to check if a scene should be included in Reactor builds.</summary>
        /// <param name="scenePath">Path to a scene file</param>
        /// <returns>True if the scene should be included in the Reactor build.</returns>
        public delegate bool IncludeSceneCallback(string scenePath);

        /// <summary>Callback for pre-build events called before any build process starts.</summary>
        public delegate void PreBuildCallback();

        /// <summary>Callback for post-build events called after all build process complete.</summary>
        /// <param name="success">Was the build successful?</param>
        public delegate void PostBuildCallback(bool success);

        /// <summary>Callback for pre-build-config events called before common and scene configs are written.</summary>
        /// <param name="scene">Scene to be built. <see cref="Scene.IsValid()"/> = false for common configs.</param>
        public delegate void PreBuildConfigCallback(Scene scene);

        /// <summary>Callback for post-build-config events called after common and scene configs are written.</summary> 
        /// <param name="scene">Scene to be built. <see cref="Scene.IsValid()"/> = false for common configs.</param>
        /// <param name="success">Was the config created successfully?</param>
        public delegate void PostBuildConfigCallback(bool success, Scene scene);

        /// <summary>Callback for pre-build events called before the KSServerRuntime is compiled.</summary>
        /// <param name="configuration">Build configuration</param>
        public delegate void PreBuildServerCallback(ksServerScriptCompiler.Configurations configuration);

        /// <summary>Callback for post-build events called after the KSServerRuntime compile completes.</summary>
        /// <param name="configuration">Build configuration</param>
        /// <param name="success">Was the KSServerRuntime compiled successfully?</param>
        public delegate void PostBuildServerCallback(bool success, ksServerScriptCompiler.Configurations configuration);

        /// <summary>Invoked before any build process.</summary>
        public static IncludeSceneCallback IncludeSceneCheck;

        /// <summary>Invoked before any build process.</summary>
        public static event PreBuildCallback PreBuild;

        /// <summary>Invoked after any build process.</summary>
        public static event PostBuildCallback PostBuild;

        /// <summary>Invoked before common and scene configs are written.</summary>
        public static event PreBuildConfigCallback PreBuildConfig;

        /// <summary>Invoked after common and scene configs are written.</summary>
        public static event PostBuildConfigCallback PostBuildConfig;

        /// <summary>Invoked before the KSServerRuntime is compiled.</summary>
        public static event PreBuildServerCallback PreBuildServer;

        /// <summary>Invoked after the KSServerRuntime is compiled.</summary>
        public static event PostBuildServerCallback PostBuildServer;


        /// <summary>Validate that a scene at a specific path exists and should be included in Reactor builds.</summary>
        /// <param name="scenePath">Path to a scene file</param>
        /// <returns>True if the scene should be included in the Reactor build.</returns>
        public static bool ValidateScenePath(string scenePath)
        {
            if (!File.Exists(scenePath))
            {
                ksLog.Warning("Could not find the scene file '" + scenePath + "'");
                return false;
            }
            if (IncludeSceneCheck != null)
            {
                return IncludeSceneCheck(scenePath);
            }
            return true;
        }

        /// <summary>Invoke registered <see cref="PreBuildConfig"/> events.</summary>
        /// <param name="scene">Scene to be built. <see cref="Scene.IsValid()"/> = false for common configs.</param>
        public static void InvokePreBuildConfig(Scene scene)
        {
            if (PreBuildConfig != null)
            {
                PreBuildConfig(scene);
            }
        }

        /// <summary>Invoke registered <see cref="PostBuildServer"/> events.</summary>
        /// <param name="scene">Scene to be built. <see cref="Scene.IsValid()"/> = false for common configs.</param>
        /// <param name="success">Was the config created successfully?</param>
        public static void InvokePostBuildConfig(Scene scene, bool success)
        {
            if (PostBuildConfig != null)
            {
                PostBuildConfig(success, scene);
            }
        }

        /// <summary>Invoke registered <see cref="PreBuildServer"/> events.</summary>
        /// <param name="configuration">Build configuration</param>
        public static void InvokePreBuildServer(ksServerScriptCompiler.Configurations configuration)
        {
            if (PreBuildServer != null)
            {
                PreBuildServer(configuration);
            }
        }

        /// <summary>Invoke registered <see cref="PostBuildServer"/> events.</summary>
        /// <param name="configuration">Build configuration</param>
        /// <param name="success">Was the KSServerRuntime compiled successfully?</param>
        public static void InvokePostBuildServer(ksServerScriptCompiler.Configurations configuration, bool success)
        {
            if (PostBuildServer != null)
            {
                PostBuildServer(success, configuration);
            }
        }

        /// <summary>Invoke registered <see cref="PreBuild"/> events.</summary>
        public static void InvokePreBuild()
        {
            if (PreBuild != null)
            {
                PreBuild();
            }
        }

        /// <summary>Invoke registered <see cref="PostBuild"/> events.</summary>
        /// <param name="success">Was the build successful?</param>
        public static void InvokePostBuild(bool success)
        {
            if (PostBuild != null)
            {
                PostBuild(success);
            }
        }
    }
}
