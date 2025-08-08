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
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using KS.Unity.Editor;
using UnityEngine;
using UnityEditor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>
    /// Utility for searching and replacing regex patterns in files.
    /// </summary>
    public class ksScriptUpdater
    {
        private List<KeyValuePair<string, string>> m_replacements = new List<KeyValuePair<string, string>>();
        private List<string> m_excludePaths = new List<string>();

        /// <summary>
        /// Adds a mapping between a search pattern and a replacement string. Once you've added all the replacements
        /// you want, call <see cref="ReplaceAll(string, bool, string[])"/> to replace matched patterns in all files in
        /// a directory, or call <see cref="ReplaceInFile(string)"/> to replace matched patterns in a single file.
        /// </summary>
        /// <param name="pattern">Regex pattern to search for and replace.</param>
        /// <param name="replacement">Replacement string</param>
        public void AddReplacement(string pattern, string replacement)
        {
            m_replacements.Add(new KeyValuePair<string, string>(pattern, replacement));
        }

        /// <summary>
        /// When calling <see cref="ReplaceAll(string, bool, string[])"/>, will ignore all files that begin with
        /// <paramref name="path"/>.
        /// </summary>
        /// <param name="path">Ignore files that begin with this path.</param>
        public void ExcludePath(string path)
        {
            path.Replace('\\', '/');
            m_excludePaths.Add(path);
        }

        /// <summary>
        /// Replaces all patterns matches with replacements strings that were passed to
        /// <see cref="AddReplacement(string, string)"/> in all files in the <paramref name="directory"/> that match a file
        /// pattern in <paramref name="filePatterns"/>, or all .cs scripts by default.
        /// </summary>
        /// <param name="directory">Directory to process files in. </param>
        /// <param name="recurse">Should we recursively process files in subdirectories?</param>
        /// <param name="filePatterns">
        /// Files that match any of these patterns will be processed. If left empty, processes all cs files. Example:
        /// "*.txt" matches all txt files.
        /// </param>
        public void ReplaceAll(string directory = "Assets", bool recurse = true, params string[] filePatterns)
        {
            if (m_replacements.Count == 0)
            {
                return;
            }
            HashSet<string> paths = null;
            try
            {
                if (filePatterns == null || filePatterns.Length == 0)
                {
                    paths = new HashSet<string>(Directory.GetFiles(directory, "*.cs", 
                        recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                        .Where((string file) => !IsExcluded(file)));
                }
                else
                {
                    foreach (string pattern in filePatterns)
                    {
                        IEnumerable<string> files = Directory.GetFiles(directory, pattern,
                            recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                            .Where((string file) => !IsExcluded(file));
                        if (paths == null)
                        {
                            paths = new HashSet<string>(files);
                        }
                        else
                        {
                            paths.UnionWith(files);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Error getting paths.", e);
                return;
            }
            if (paths == null)
            {
                return;
            }
            float num = 0f;
            int count = paths.Count();
            try
            {
                foreach (string file in paths)
                {
                    EditorUtility.DisplayProgressBar("Reactor - Updating Scripts", "Processing " + file,
                        num / count);
                    ReplaceInFile(file);
                    num++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>Replaces all matched string patterns in a file with replacements strings.</summary>
        /// <param name="path">Path to file</param>
        public void ReplaceInFile(string path)
        {
            string tmpPath = path + ".tmp";
            try
            {
                // Pass UTF8 no-bom encoding, and it will change to bom encoding if it detects a bom when it reads the
                // first line. This allows us to detect and preserve bom encoding.
                using (StreamReader reader = new StreamReader(path, new UTF8Encoding(false)))
                {
                    string text = reader.ReadToEnd();
                    using (StreamWriter writer = new StreamWriter(tmpPath, false, reader.CurrentEncoding))
                    {
                        // replace matching patterns with replacement strings
                        foreach (KeyValuePair<string, string> replacement in m_replacements)
                        {
                            text = Regex.Replace(text, replacement.Key, replacement.Value);
                        }
                        writer.Write(text);
                    }
                }
                // Delete the old file and replace it with the temp file
                File.Delete(path);
                File.Move(tmpPath, path);
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Error updating " + path + ".", e);
                ksPathUtils.Delete(tmpPath);
            }
        }

        /// <summary>Checks if a path is excluded. Exclude paths using <see cref="ExcludePath(string)"/></summary>
        /// <param name="path">Path to check.</param>
        /// <returns>Is the path excluded?</returns>
        private bool IsExcluded(string path)
        {
            path.Replace('\\', '/');
            foreach (string excludePath in m_excludePaths)
            {
                if (path.StartsWith(excludePath))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
