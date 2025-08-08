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
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using KS.Unity.Editor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>
    /// Intercepts open asset events for proxy scripts so we can instead attempt to load the server script for the
    /// proxy.
    /// </summary>
    public class ksProxyScriptRedirector
    {
        private static readonly string LOG_CHANNEL = typeof(ksProxyScriptRedirector).ToString();

        /// <summary>
        /// Called before Unity opens an asset. If the asset is a proxy script, attempts to locate the server script
        /// for that proxy and opens it instead if found. We look for a file in the server runtime folder or any 
        /// subfolder with the same name as the proxy script.
        /// </summary>
        /// <param name="instanceId">Asset instance ID</param>
        /// <param name="line">Line number</param>
        /// <returns>True if the asset instance was opened.</returns>
        [OnOpenAsset(-1)]
        private static bool OnOpen(int instanceId, int line)
        {
            try
            {
                string path = AssetDatabase.GetAssetPath(instanceId).Replace('\\', '/');
                if (!path.StartsWith(ksPaths.Proxies))
                {
                    return false;
                }
                int index = path.LastIndexOf("/");
                string fileName = path.Substring(index + 1);
                foreach (string folder in ksPaths.IterateCommonAndServerFolders())
                {
                    if (File.Exists(folder + fileName))
                    {
                        MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(folder + fileName);
                        return AssetDatabase.OpenAsset(script, line);
                    }
                    foreach (string filePath in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
                    {
                        if (filePath.EndsWith(fileName) &&
                            (ksPaths.IsServerPath(filePath) || ksPaths.IsCommonPath(filePath)))
                        {
                            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(
                                ksPathUtils.ToAssetPath(filePath));
                            return AssetDatabase.OpenAsset(script, line);
                        }
                    }
                }
                ksLog.Warning(LOG_CHANNEL, "Could not find server script for proxy '" + path +
                    "'. Opening proxy instead.");
            }
            catch (Exception e)
            {
                ksLog.Error(LOG_CHANNEL, "Error finding server script for proxy script.", e);
            }
            return false;
        }
    }
}