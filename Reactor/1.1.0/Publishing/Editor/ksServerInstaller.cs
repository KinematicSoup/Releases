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
using System.Diagnostics;
using KS.Unity.Editor;
using UnityEditor;
using UnityEngine.Networking;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>
    /// Provides methods for validating, downloading, and installing local Reactor test servers.
    /// </summary>
    public class ksServerInstaller : ksSingleton<ksServerInstaller>
    {
        /// <summary>Server installation states.</summary>
        public enum FileStates
        {
            OK = 0,
            MISSING_BUILD_FILES = 1,
            MISSING_SERVER_FILES = 2,
            INVALID_VERSION = 3
        };

        // Required server files
        private static readonly string[] m_serverFiles = new string[] {
            "Reactor.exe",
            "KSCommon.dll",
            "KSReactor.dll",
            "KSLZMA.dll",
            "KSPhysX.dll",
            "ServerModule.dll"
        };

        private static readonly string[] m_buildFiles = new string[] {
            "KSReflectionTool.exe",
            "KSCommon.dll",
            "KSReactor.dll"
        };

        // This overrides the file checks and is used when a user select no to the download request
        // For the remainder of this unity session, the files will not be checked.
        public bool ShowDownloadRequest = true;

        /// <summary>
        /// Check local server file existance and versions and optionally prompt the user to download a
        /// new server if the check fails.
        /// </summary>
        /// <param name="checkBuildFiles">Check if the files required for building images are available.</param>
        /// <param name="checkServerFiles">Check if the files required for running local servers and clusters are available.</param>
        /// <param name="attemptDownload">Attempt to download a new server if the check fails.</param>
        /// <param name="logWarnings">Should warning messages for missing of invalid versioning written to the logs.</param>
        /// <returns>Result of the server check.</returns>
        public FileStates Check(bool checkBuildFiles = true, bool checkServerFiles = true, bool attemptDownload = false, bool logWarnings = true)
        {
            // If the override server path is set, we do not validate files or versions.
            if (!ShowDownloadRequest ||
                !ksReactorConfig.Instance.Server.CheckServerFiles ||
                !string.IsNullOrEmpty(ksReactorConfig.Instance.Server.OverrideServerPath))
            {
                return FileStates.OK;
            }

            string buildFileWarnings = "";
            string serverFileWarnings = "";
            string versionWarnings = "";
            bool missingBuildFiles = false; 
            bool missingServerFiles = false;
            bool versionMismatch = false;

            if (checkBuildFiles)
            {
                foreach (string file in m_buildFiles)
                {
                    string filepath = ksPaths.BuildToolsDir + file;
                    if (!File.Exists(filepath))
                    {
                        buildFileWarnings += $"Missing build tool file {filepath}\n";
                        missingBuildFiles = true;
                    }
                    else
                    {
                        FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(filepath);
                        if (versionInfo.FileVersion != ksReactorConfig.Instance.Version)
                        {
                            versionWarnings += $"{filepath} version mismatch. Expected={ksReactorConfig.Instance.Version}, found={versionInfo.FileVersion}";
                            versionMismatch = true;
                        }
                    }
                }
            }

            if (checkServerFiles)
            {
                foreach (string file in m_serverFiles)
                {
                    string filepath = ksPaths.ServerDir + file;
                    if (!File.Exists(filepath))
                    {
                        serverFileWarnings += $"Missing server file {filepath}\n";
                        missingServerFiles = true;
                    }
                    else
                    {
                        FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(filepath);
                        if (versionInfo.FileVersion != ksReactorConfig.Instance.Version)
                        {
                            versionWarnings += $"{filepath} version mismatch. Expected={ksReactorConfig.Instance.Version}, found={versionInfo.FileVersion}";
                            versionMismatch = true;
                        }
                    }
                }
            }

            if (logWarnings)
            {
                if (missingBuildFiles && !String.IsNullOrEmpty(buildFileWarnings))
                {
                    ksLog.Warning(buildFileWarnings);
                }

                if (missingServerFiles && !String.IsNullOrEmpty(serverFileWarnings))
                {
                    ksLog.Warning(serverFileWarnings);
                }

                if (versionMismatch && !String.IsNullOrEmpty(versionWarnings))
                {
                    ksLog.Warning(versionWarnings);
                }
            }


            if (attemptDownload) {
                string downloadMessage = "";
                if (missingBuildFiles)
                {
                    downloadMessage = "Files required for building server images are missing.";
                }
                else if(missingServerFiles)
                {
                    downloadMessage = "Files required for launching local instances are missing.";
                }
                else if(versionMismatch)
                {
                    downloadMessage = "The installed Reactor server version does not match the Reactor client version.";
                }

                if (ShowDownloadRequest && !String.IsNullOrEmpty(downloadMessage) && Download(downloadMessage + "\n\nDo you want to download and install the Reactor server tools?"))
                {
                    return FileStates.OK;
                }
            }

            if (missingBuildFiles)
            {
                return FileStates.MISSING_BUILD_FILES;
            }
            if (missingBuildFiles)
            {
                return FileStates.MISSING_SERVER_FILES;
            }
            if (versionMismatch)
            {
                return FileStates.INVALID_VERSION;
            }
            return FileStates.OK;
        }

        /// <summary>
        /// Prompt the user to download a new server instance.
        /// </summary>
        /// <param name="dialogMessage">Message to display when asking to download a new server.</param>
        /// <returns>Returns false if the download was not accepted or encountered an error.</returns>
        public bool Download(string dialogMessage)
        {
            int result = EditorUtility.DisplayDialogComplex("Download Reactor Server", dialogMessage, "Yes", "Never", "No");
            if (result == 1)
            {
                ksReactorConfig.Instance.Server.CheckServerFiles = false;
                EditorUtility.SetDirty(ksReactorConfig.Instance);
                AssetDatabase.SaveAssets();
            }
            if (result != 0)
            {
                ShowDownloadRequest = false;
                return false;
            }

            try
            {
                string serverFile = $"ReactorServer-{ksReactorConfig.Instance.Version}{(ksPaths.OperatingSystem == "Microsoft" ? "-win" : "")}.zip";
                string url = $"{ksReactorConfig.Instance.Urls.Downloads}/localservers/{serverFile}";
                ksLog.Info($"Downloading Reactor server from {url}");
                UnityWebRequest www = new UnityWebRequest(url);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SendWebRequest();

                while (!www.isDone && !www.downloadHandler.isDone)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Installing Reactor Test Server", "Downloading server...", 0f))
                    {
                        ksLog.Warning($"Reactor server install canceled.");
                        EditorUtility.ClearProgressBar();
                        return false;
                    }
                }

                if (string.IsNullOrEmpty(www.error))
                {
                    return Install(www.downloadHandler.data);
                }
                else
                {
                    ksLog.Error($"Download failed: {www.error}");
                    EditorUtility.DisplayDialog("Reactor Server Download Failed.", www.error, "Ok");
                    return false;
                }
            }
            catch (Exception ex)
            {
                ksLog.Error("Exceptaion caught downloading the Reactor server.", ex);
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>Install a downloaded server.</summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private static bool Install(byte[] downloadedData)
        {
            EditorUtility.DisplayProgressBar("Installing Reactor Test Server", "Installing server...", 0.5f);
            try
            {
                // Error handling
                if (downloadedData == null || downloadedData.Length == 0)
                {
                    ksLog.Error("Missing data.");
                    EditorUtility.ClearProgressBar();
                    return false;
                }

                ksPathUtils.Create(ksPaths.External, true);
                // Do not use ksPaths.ServerDir as that can return an override directory we do not want to touch
                string serverFolder = ksPaths.External + "server/";
                string tempFile = ksPaths.External + "ReactorServer.zip";

                // Delete the old server file
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }

                // Remove the old server directory
                if (Directory.Exists(serverFolder))
                {
                    Directory.Delete(serverFolder, true);
                }

                // Save and then extract the server zip file.
                File.WriteAllBytes(tempFile, downloadedData);
                ksPathUtils.Unzip(tempFile, serverFolder);
                return true;
            }
            catch (Exception ex)
            {
                ksLog.Error("Exceptaion caught installing the Reactor server.", ex);
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
