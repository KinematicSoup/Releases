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
using System.Collections.Generic;
using KS.SF.Reactor;
using KS.SF.Unity.Editor;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>This class is used to write ksLog messages to a log file.</summary>
    public class sfLogFile
    {
        /// <summary>Type of current log file </summary>
        private enum LogType
        {
            DEFAULT = 0,
            SESSION_LOG = 1
        }

        /// <summary>Max size in bytes of the session log file to send </summary>
        private const int MAX_FILE_SIZE = 100000;

        private string m_filePath = "";
        private StreamWriter m_writer = null;
        private FileStream m_outputStream = null;
        private LogType m_logType = LogType.DEFAULT;
        private string m_sessionLogPath = "";

        /// <summary>Are we logging to a session log file?</summary>
        public bool IsLoggingToSessionLog
        {
            get { return m_logType == LogType.SESSION_LOG; }
        }

        /// <summary>Constructor</summary>
        public sfLogFile()
        {
        }

        /// <summary>Destructor</summary>
        ~sfLogFile()
        {
            Close();
        }

        /// <summary>
        /// Open a file to receive log messages.  If the file is already open, then return.  If a file is open and
        /// it does not match the requested file, then close the current file and open a new one.
        /// </summary>
        /// <param name="pathname"></param>
        public void Open(string filePath)
        {
            filePath = filePath.Trim();

            if (filePath == null || filePath.Length <= 0 || filePath == m_filePath)
            {
                return;
            }

            m_filePath = filePath;

            // Close any open stream
            if (m_outputStream != null || m_writer != null)
            {
                Close();
            }

            // Create the log file directory if necessary
            if (!ksPathUtils.Create(m_filePath))
            {
                return;
            }

            // Open the file for writing
            try
            {
                m_outputStream = File.Open(m_filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                m_writer = new StreamWriter(m_outputStream);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error opening " + Product.NAME + " log file '"
                    + m_filePath + "' for writing.(" + ex.Message + ")");
            }

            RemoveOldLogFiles(sfPaths.ExternalLogs);
        }


        /// <summary>Flush and close the current log file </summary>
        public void Close()
        {
            try
            {
                if (m_writer != null)
                {
                    m_writer.Flush();
                    m_writer = null;
                }

                if (m_outputStream != null)
                {
                    m_outputStream.Flush();
                    m_outputStream.Close();
                    m_outputStream = null;
                }
                m_filePath = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error closing " + Product.NAME + " log file '"
                    + m_filePath + "' (" + ex.Message + ")");
            }
        }


        /// <summary>Write a string to the open log file</summary>
        /// <param name="message"></param>
        private void WriteToFile(string message)
        {
            try
            {
                switch (m_logType)
                {
                    case LogType.DEFAULT:
                        Open(sfPaths.ExternalLogs + "sessions/sf_" + DateTime.Now.ToString("yyyyMMdd") + ".log");
                        break;

                    case LogType.SESSION_LOG:
                        Open(m_sessionLogPath);
                        break;
                }
                    
                if (m_writer != null)
                {
                    m_writer.WriteLine(message);
                    m_writer.Flush();
                    m_outputStream.Flush();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error writing to " + Product.NAME + " log file '"
                    + m_filePath + "' (" + ex.Message + ")");
                Close();
            }
        }


        /// <summary>Write data on to the output stream</summary>
        /// <param name="log">log level</param>
        /// <param name="channel"></param>
        /// <param name="message"></param>
        public void Write(ksLog.Level level, string channel, string message)
        {
            string msg = "";
            msg += DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffff") + " ";
            msg += GetPrefix(level, channel) + " ";
            msg += message;
            WriteToFile(msg);
        }

        /// <summary>Write data on to the output stream and inlcude exceptions</summary>
        /// <param name="log">log level</param>
        /// <param name="channel"></param>
        /// <param name="message"></param>
        /// <param name=""></param>
        /// <param name="context">object related to this message.</param>
        public void Write(ksLog.Level level, string channel, string message, Exception e, object context)
        {
            if (message == null && e == null)
            {
                return;
            }

            if (e != null)
            {
                if (message == null)
                {
                    message = "An error occured";
                }
                message += ": " + e.Message + "\n" + e.StackTrace;
            }

            Write(level, channel, message);
        }

        /// <summary>Unity log handler that writes exceptions to the log file.</summary>
        /// <param name="message"></param>
        /// <param name="stackTrace">stackTrace of log statement or exception.</param>
        /// <param name="type">type of log. The log will be ignored if this is not Exception.</param>
        public void LogUnityException(string message, string stackTrace, UnityEngine.LogType type)
        {
            if (type == UnityEngine.LogType.Exception)
            {
                Write(ksLog.Level.ERROR, "UnityException", message + "\n" + stackTrace);
            }   
        }

        /// <summary>Starts session loggging.</summary>
        /// <param name="sessionLogId">identifies which log file to log to.</param>
        public void StartSessionLog(string sessionLogId)
        {
            m_logType = LogType.SESSION_LOG;
            m_sessionLogPath = sfPaths.ExternalLogs + "session." + sessionLogId + ".log";
        }

        /// <summary>Deletes the oldest log files until there are no more than the log history size.</summary>
        /// <param name="path">path to folder to delete logs in.</param>
        public static void RemoveOldLogFiles(string path)
        {
            if (sfConfig.Get().Logging.LogHistorySize <= 0)
            {
                return;
            }
            string[] files = Directory.GetFiles(path, "*.log");
            if (files.Length <= sfConfig.Get().Logging.LogHistorySize)
            {
                return;
            }
            List<Tuple<string, DateTime>> logs = new List<Tuple<string, DateTime>>();
            foreach (string file in files)
            {
                logs.Add(new Tuple<string, DateTime>(file, File.GetLastWriteTime(file)));
            }
            // Sort newest to oldest.
            logs.Sort((Tuple<string, DateTime> lhs, Tuple<string, DateTime> rhs) =>
            {
                return lhs.Item2.Ticks < rhs.Item2.Ticks ? 1 : lhs.Item2.Ticks == rhs.Item2.Ticks ? 0 : -1;
            });
            for (int i = sfConfig.Get().Logging.LogHistorySize; i < logs.Count; i++)
            {
                ksPathUtils.Delete(logs[i].Item1, deleteMetaFile: false);
            }
        }

        /// <summary>
        /// Renames the current session log file. Does nothing if the log type is not LogType.SESSION_LOG.
        /// </summary>
        /// <param name="sessionLogId">
        /// determines the new name of the log file. If a log already exists with
        /// this id, a '.' followed by a number will be appended to make a unique log file path.
        /// </param>
        public void RenameSessionLog(ref string sessionLogId)
        {
            if (m_logType != LogType.SESSION_LOG)
            {
                return;
            }
            Close();
            string oldPath = m_sessionLogPath;
            m_sessionLogPath = sfPaths.ExternalLogs + "session." + sessionLogId + ".log";
            // If a log file already exists at this path, append a number to get a unique path.
            int num = 0;
            while (File.Exists(m_sessionLogPath))
            {
                num++;
                m_sessionLogPath = sfPaths.ExternalLogs + "session." + sessionLogId + "." + num +  ".log";
            }
            if (num > 0)
            {
                sessionLogId += "-" + num;
            }
            ksPathUtils.Move(oldPath, m_sessionLogPath, false, ksPathUtils.LoggingFlags.EXCEPTIONS, false);
        }

        /// <summary>Ends session logging.</summary>
        public void EndSessionLog()
        {
            Close();
            m_logType = LogType.DEFAULT;
        }

        /// <summary>Builds the level/channel message prefix</summary>
        /// <param name="log">log level</param>
        /// <param name="channel"></param>
        /// <returns></returns>
        private string GetPrefix(ksLog.Level level, string channel)
        {
            string prefix = "[";

            switch (level)
            {
                case ksLog.Level.DEBUG: prefix += "DEBUG"; break;
                case ksLog.Level.INFO: prefix += "INFO"; break;
                case ksLog.Level.WARNING: prefix += "WARNING"; break;
                case ksLog.Level.ERROR: prefix += "ERROR"; break;
                case ksLog.Level.FATAL: prefix += "FATAL"; break;
                default: prefix += "UNKNOWN"; break;
            }

            prefix += "]";

            return prefix;
        }

        /// <summary>Sends log file to server</summary>
        /// <param name="webService"></param>
        /// <param name="sessionInfo">sessionInfo the logs are for. Null if the logs are not for a session.</param>
        public void SendLog(sfIWebService webService, sfSessionInfo sessionInfo)
        {
            if (string.IsNullOrEmpty(m_sessionLogPath))
            {
                return;
            }
            string log = "";
            try
            {
                FileInfo file = new FileInfo(m_sessionLogPath);
                if (file.Length <= MAX_FILE_SIZE)
                {
                    log = File.ReadAllText(m_sessionLogPath);
                }
                else
                {
                    log = "Log Length exceded, Length : " + file.Length + " file location : " + m_sessionLogPath;
                }
            }
            catch (Exception e)
            {
                ksLog.Warning(this, "Error reading log file: " + e.Message);
                return;
            }

            SendLog(webService, sessionInfo, log);
        }

        /// <summary>Sends log file to server with call back </summary>
        /// <param name="webService"></param>
        /// <param name="sessionInfo">sessionInfo the logs are for. Null if the logs are not for a session.</param>
        /// <param name="log"></param>
        public void SendLog(sfIWebService webService, sfSessionInfo sessionInfo, string log)
        {
            if (webService.SendLog(log, sessionInfo))
            {
                ksLog.Debug(this, "Sent " + m_sessionLogPath);
            }
            else
            {
                ksLog.Warning(this, "Failed to send session logs.");
            }
            m_sessionLogPath = "";
        }
    }
}
