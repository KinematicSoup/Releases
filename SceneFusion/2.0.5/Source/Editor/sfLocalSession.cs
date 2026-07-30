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
using KS.SF.Reactor;
using System;
using System.Diagnostics;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityEditor;
using KS.SF.Unity.Editor;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>Launch a local Scene Fusion session</summary
    public partial class sfLocalSession : sfILocalSession
    {
        /// <summary>
        /// Maximum size of local server launch errors to report in the session window. Larger error messages will be
        /// reported in the logs.
        /// </summary>
        private const int MAX_ERROR_MESSAGE_SIZE = 512;
        /// <summary>Search for this string in the log to find authentication errors.</summary>
        private const string LOG_ERROR_SEARCH_STR = "[ERROR; KS.SceneFusion.Server.srAuthenticationManager]";

        private const string SF_PROCESS_KEY = "com.kinematicsoup.scenefusion.session_id";
        private const string PROCESS_NAME = "SceneFusion";

        private Process m_process = null;
        private int m_processId = -1;

        /// <summary>Constructor. Find any running sessions</summary>
        internal sfLocalSession()
        {
            m_processId = EditorPrefs.GetInt(SF_PROCESS_KEY, -1);
            if (m_processId >= 0 && !GetProcess())
            {
                m_processId = -1;
                m_process = null;
                EditorPrefs.DeleteKey(SF_PROCESS_KEY);
            }
        }

        /// <summary>Check if a session process is already running.</summary>
        public bool IsRunning
        {
            get
            {
                return m_process != null && !m_process.HasExited;
            }
        }

        /// <summary>Version check results</summary>
        public enum ServerState
        {
            OK,
            MISSING,
            INCOMPATIBLE_VERSION,
            INCOMPATIBLE_OS
        }

        /// <summary>Check that a local SF server exists and that it is compatible with the current SF version</summary>
        /// <returns></returns>
        public static ServerState ServerCheck()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                return ServerState.INCOMPATIBLE_OS;
            }

            string serverPath = sfPaths.ExternalServer + "SceneFusion.exe";
            // Check that the server and runtime are available
            if (!File.Exists(serverPath))
            {
                return ServerState.MISSING;
            }

            if (!VersionCheck(serverPath))
            {
                return ServerState.INCOMPATIBLE_VERSION;
            }
            return ServerState.OK;
        }

        /// <summary>
        /// Reads and closes the standard output stream from the local server process. Does not read if the process is
        /// still running.
        /// </summary>
        /// <returns>standard output, or null if the standard output stream could not be read.</returns>
        public string ReadStdOut()
        {
            if (m_process == null || !m_process.HasExited)
            {
                return null;
            }
            try
            {
                string str = m_process.StandardOutput.ReadToEnd();
                m_process.StandardOutput.Close();
                return str;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Reads and closes the standard error stream from the local server process. Does not read if the process is
        /// still running.
        /// </summary>
        /// <returns>standard error, or null if the standard error stream could not be read.</returns>
        public string ReadStdErr()
        {
            if (m_process == null || !m_process.HasExited)
            {
                return null;
            }
            try
            {
                string str = m_process.StandardError.ReadToEnd();
                m_process.StandardError.Close();
                return str;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Starts the local server instance. If a local server is already running, stops it and starts a new one.
        /// </summary>
        /// <param name="port">Port</param>
        /// <param name="sessionId">Session id</param>
        /// <param name="callback">Callback to call on success/fail.</param>
        public void Start(int port, string sessionId, sfILocalSession.StartCallback callback)
        {
            SceneFusion.Get().Service.Coroutines.Start(StartCoroutine(port, sessionId, callback, false));
        }

        /// <summary>Coroutine to start the local server instance.</summary>
        /// <param name="port">Port</param>
        /// <param name="sessionId">Session id</param>
        /// <param name="callback">Callback to call on success/fail.</param>
        /// <param name="useStdOutAndErr">
        /// True to capture the standard output and error streams. This requires us to use
        /// the fallback method of starting a process that opens a window and steals focus on Windows.
        /// </param>
        private IEnumerator StartCoroutine(int port, string sessionId, sfILocalSession.StartCallback callback, bool useStdOutAndErr)
        {
            string error;
            if (!StartProcess(port, sessionId, out error, useStdOutAndErr))
            {
                if (callback != null)
                {
                    if (string.IsNullOrEmpty(error))
                    {
                        error = "An unknown error occurred starting the local server process.";
                    }
                    callback(error);
                }
                yield break;
            }

            yield return SceneFusion.Get().Service.Coroutines.Wait(2f, () => !IsRunning, true);

            sfLogFile.RemoveOldLogFiles(sfPaths.ExternalLogs + "server/");

            // Parse log for the last session error
            if (!IsRunning)
            {
                string logpath = sfPaths.ExternalLogs + "server/" + sessionId + ".log";
                if (File.Exists(logpath))
                {
                    string[] logdata = File.ReadAllLines(logpath);

                    for (int i = logdata.Length - 1; i >= 0; i--)
                    {
                        if (logdata[i].Contains(LOG_ERROR_SEARCH_STR))
                        {
                            error = logdata[i].Substring(LOG_ERROR_SEARCH_STR.Length).Trim();
                            if (string.IsNullOrEmpty(error))
                            {
                                break;
                            }
                            if (callback != null)
                            {
                                callback(error);
                            }
                            yield break;
                        }
                    }
                    if (callback != null)
                    {
                        callback("Unable to start local session. " +
                            "Please contact support and provide the following file: '" + logpath + "'");
                    }
                }
                else if (!useStdOutAndErr)
                {
                    // We didn't find a log file. Try to start launch the local server again and this time capture
                    // stdout and stderr. We don't capture stdout/err the first time because it requires us to use the
                    // fallback method of starting the process that creates a window that steals focus on Windows.
                    yield return StartCoroutine(port, sessionId, callback, true);
                }
                else
                {
                    string message = ReadStdOut();
                    if (!string.IsNullOrEmpty(message))
                    {
                        ksLog.Info(this, message);
                    }

                    // Try to get an error message from stderr.
                    message = ReadStdErr();
                    if (!string.IsNullOrEmpty(message))
                    {
                        if (message.Length > MAX_ERROR_MESSAGE_SIZE)
                        {
                            ksLog.Error(this, "Error starting local session: " + message);
                            if (callback != null)
                            {
                                callback("Unable to start local session. See logs for details.");
                            }
                        }
                        else if (callback != null)
                        {
                            callback(message);
                        }
                    }
                    else if (callback != null)
                    {
                        callback("Unable to start local session. Unknown error.");
                    }
                }
                yield break;
            }
            if (callback != null)
            {
                // Null means the local server started successfully.
                callback(null);
            }
        }

        /// <summary>Starts the local server instance.</summary>
        /// <param name="port">Port</param>
        /// <param name="sessionId">Session id</param>
        /// <param name="errorMessage">Error message</param>
        /// <param name="useStdOutAndErr">
        /// True to capture the standard output and error streams. This requires us to use
        /// the fallback method of starting a process that opens a window and steals focus on Windows.
        /// </param>
        /// <returns>true if the local server was started successfully.</returns>
        private bool StartProcess(int port, string sessionId, out string errorMessage, bool useStdOutAndErr = false)
        {
            // Stop any existing sessions before launching a new one.
            Stop();

            try
            {
                string fileName = Path.GetFullPath(Path.Combine(sfPaths.ProjectRoot, sfPaths.ExternalServer, "SceneFusion.exe"));
                string workingDir = Path.GetFullPath(Path.Combine(sfPaths.ProjectRoot, sfPaths.ExternalServer));
                string arguments = "";
                if (!string.IsNullOrEmpty(sessionId))
                {
                    string logPath = Path.GetFullPath(Path.Combine(sfPaths.ProjectRoot, sfPaths.ExternalLogs, "server", sessionId + ".log"));
                    ksPathUtils.Create(logPath);
                    arguments += " -l=\"" + logPath + "\"";
                }
                arguments += " -tcp=" + port;
                arguments += " \"" + sfPaths.External + "config.json\"";
                m_process = ksProcessUtils.StartProcess(fileName, arguments, workingDir, useStdOutAndErr);
                m_process.Exited += OnExit;
                m_processId = m_process.Id;
                EditorPrefs.SetInt(SF_PROCESS_KEY, m_processId);
                errorMessage = null;
                return true;
            }
            catch (Exception e)
            {
                errorMessage = "Unable to start local session. See log file for more information.";
                ksLog.Error("Unable to start local session. " + e.Message, e);
                if (m_process != null)
                {
                    m_process.Dispose();
                    m_process = null;
                }
                ClearProcess();
                return false;
            }
        }

        /// <summary>Handle an exit event from a process</summary>
        /// <param name="-">process that sent the on exit event</param>
        /// <param name="-">event arguments</param>
        private void OnExit(object sender, EventArgs e)
        {
            ksLog.Warning("Process " + m_process.Id + " has exited");
            ClearProcess();
        }

        /// <summary>Stop the current session</summary>
        /// <returns>true if the local session was running.</returns>
        public bool Stop()
        {
            if (GetProcess())
            {
                ClearProcess();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Finds the local server process started by this local server instance.
        /// The process reference is lost during Unity serialization, so we serialize
        /// the process id and call this to retrieve the process after serialization.
        /// </summary>
        /// <returns>true if a bouillon process was found</returns>
        private bool GetProcess()
        {
            try
            {
                m_process = Process.GetProcessById(m_processId);
                if (m_process.ProcessName != PROCESS_NAME)
                {
                    ClearProcess();
                    return false;
                }
                return true;
            }
            catch (Exception)
            {
                ClearProcess();
            }
            return false;
        }

        /// <summary>Close and cleanup a process</summary>
        private void ClearProcess()
        {
            // Only kill Scene Fusion processes
            if (m_process != null && !m_process.HasExited && m_process.ProcessName == PROCESS_NAME)
            {
                m_process.Kill();
                m_process.Close();
                m_process.Dispose();
            }
            m_process = null;
            m_processId = -1;
            EditorPrefs.DeleteKey(SF_PROCESS_KEY);
        }
    }
}
