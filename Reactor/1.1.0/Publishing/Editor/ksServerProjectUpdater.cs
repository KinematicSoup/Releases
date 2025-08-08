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
using System.Xml;
using System.Collections.Generic;
using System.Linq;
using KS.Unity.Editor;
using UnityEngine;
using UnityEditor;
using System.Text.RegularExpressions;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Provides methods for updating and syncing the KSServerRuntime project with the file system.</summary>
    public class ksServerProjectUpdater 
    {
        /// <summary>Singleton instance</summary>
        public static ksServerProjectUpdater Instance
        {
            get
            {
                if (m_instance == null)
                {
                    m_instance = new ksServerProjectUpdater();
                }
                return m_instance;
            }
        }
        private static ksServerProjectUpdater m_instance;

        /// <summary>Singleton constructor</summary>
        private ksServerProjectUpdater()
        {
        }

        /// <summary>
        /// Updates the contents of the KSServerRuntime project with any unreferenced .cs and dll files and
        /// removes references to missing files. Updates the common and server scripts asmdef files with references to
        /// dll files.
        /// </summary>
        public void UpdateIncludes()
        {
            if (!GenerateMissingFiles(true))
            {
                return;
            }
            UpdateAsmDefPrecompiledReferences();
            try
            {
                XmlDocument projectXml = LoadProject();
                if (projectXml == null)
                {
                    return;
                }

                bool changed = UpdateCompileIncludes(projectXml);
                changed = UpdateReferenceIncludes(projectXml) || changed;
                changed = UpdateDefineSymbols(projectXml) || changed;
                if (changed)
                {
                    SaveProject(projectXml);
                }
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Error updating KSServerRuntime project includes", e);
            }
        }

        /// <summary>
        /// Updates the common and server scripts asmdef files with references to dll files located in the common or
        /// server folders.
        /// </summary>
        public void UpdateAsmDefPrecompiledReferences()
        {
            try
            {
                if (!File.Exists(ksPaths.CommonScriptsAsmDef) || !File.Exists(ksPaths.ServerScriptsAsmDef))
                {
                    return;
                }
                List<string> references = new List<string>();
                int index = 0;
#if !KS_DEVELOPMENT
                references.Add("\"KSCommon.dll\"");
                index++;
#endif
                // Get dlls from common folders.
                foreach (string path in ksPaths.CommonFolders)
                {
                    GetFilesInAsmDefFolder(path, "dll", references);
                }
                // Convert paths to file names surrounded by '"'.
                for (; index < references.Count; index++)
                {
                    references[index] = "\"" + ksPathUtils.GetName(references[index]) + "\"";
                }
                UpdateAsmDefPrecompiledReferences(ksPaths.CommonScriptsAsmDef, references);

#if !KS_DEVELOPMENT
                references.Add("\"KSReactor.dll\"");
                index++;
#endif
                // Get dlls from server folders.
                foreach (string path in ksPaths.ServerFolders)
                {
                    GetFilesInAsmDefFolder(path, "dll", references);
                }
                // Convert paths to file names surrounded by '"'.
                for (; index < references.Count; index++)
                {
                    references[index] = "\"" + ksPathUtils.GetName(references[index]) + "\"";
                }
                UpdateAsmDefPrecompiledReferences(ksPaths.ServerScriptsAsmDef, references);
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Error updating asmdef precompiled references.", e);
            }
        }

        /// <summary>Edits the KSServerRuntime project so that output path builds to the installation path.</summary>
        public void UpdateOutputPath()
        {
            if (!GenerateMissingFiles(false))
            {
                return;
            }
            try
            {
                XmlDocument projectXml = LoadProject();
                if (projectXml == null)
                {
                    return;
                }
                XmlNode root = projectXml.DocumentElement;
                XmlElement rootElement = root as XmlElement;
                XmlNodeList nodeList = rootElement.SelectNodes("//*");
                string outputPath;
                string proxyPath;
                string intermediatePath;
                GetRelativePaths(out outputPath, out proxyPath, out intermediatePath);
                foreach (XmlNode node in nodeList)
                {
                    switch (node.Name)
                    {
                        case "OutputPath":
                            node.InnerText = outputPath + "KSServerRuntime/";
                            break;
                        case "HintPath":
                            if (node.InnerText.EndsWith("KSReactor.dll"))
                            {
                                node.InnerText = outputPath + "KSReactor.dll";
                            }
                            else if (node.InnerText.EndsWith("KSCommon.dll"))
                            {
                                node.InnerText = outputPath + "KSCommon.dll";
                            }
                            else if (node.InnerText.EndsWith("KSLZMA.dll"))
                            {
                                node.InnerText = outputPath + "KSLZMA.dll";
                            }
                            break;
                        case "PostBuildEvent":
                            node.InnerText = GetProxyCommand(proxyPath);
                            break;
                    }
                }
                SaveProject(projectXml);
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Error updating KSServerRuntime project output path", e);
            }
        }

        /// <summary>Adds a file to the server runtime project's includes.</summary>
        /// <param name="path">Path to file to add.</param>
        public void AddFileToProject(string path)
        {
            if (!GenerateMissingFiles(true))
            {
                return;
            }
            path = ksPathUtils.MakeRelative(path, ksPaths.ServerScripts);
            
            try
            {
                XmlDocument projectXml = LoadProject();
                if (!IsFileIncluded(projectXml, path))
                {
                    HashSet<string> fileHash = new HashSet<string>();
                    fileHash.Add(path);
                    AddFilesToProject(projectXml, fileHash);
                    SaveProject(projectXml);
                }
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Error adding " + path + " to KSServerRuntime project", e);
            }
        }

        /// <summary>Generates missing asmdef files for common and server scripts.</summary>
        public void GenerateMissingAsmDefs()
        {
            if (!ksPathUtils.Create(ksPaths.ServerScripts, true) ||
                !ksPathUtils.Create(ksPaths.CommonScripts, true))
            {
                return;
            }
            if (!File.Exists(ksPaths.CommonScriptsAsmDef))
            {
                GenerateCommonAsmDef();
            }
            if (!File.Exists(ksPaths.ServerScriptsAsmDef))
            {
                GenerateServerAsmDef();
            }
        }

        /// <summary>
        /// Generates the server runtime solution and/or project if the they are missing and a valid
        /// installation path is set in the project settings.
        /// </summary>
        /// <param name="projectGeneratedReturnValue">Value to return if the project is generated.</param>
        /// <returns>
        /// true if the server runtime project already existed. False if the project does not exist and we were unable
        /// to generate it. Returns projectGeneratedReturnValue if the project was generated.
        /// </returns>
        private bool GenerateMissingFiles(bool projectGeneratedReturnValue)
        {
            if (!Directory.Exists(ksPaths.ServerDir))
            {
                return false;
            }

            if (!ksPathUtils.Create(ksPaths.ServerScripts, true) ||
                !ksPathUtils.Create(ksPaths.CommonScripts, true) ||
                !ksPathUtils.Create(ksPaths.Proxies, true))
            {
                return false;
            }

            if (!File.Exists(ksPaths.CommonScriptsAsmDef))
            {
                GenerateCommonAsmDef();
            }

            if (!File.Exists(ksPaths.ServerScriptsAsmDef))
            {
                GenerateServerAsmDef();
            }

            bool solutionExists = File.Exists(ksPaths.ServerRuntimeSolution);
            bool projectExists = File.Exists(ksPaths.ServerRuntimeProject);
            if (!solutionExists || !projectExists)
            {
                Guid projectGUID;
                if (!TryGetGuidFromFile(ksPaths.ServerRuntimeSolution, "^Project.*\"KSServerRuntime.csproj\", \"{(.*)}\"", out projectGUID) && 
                    !TryGetGuidFromFile(ksPaths.ServerRuntimeProject, "^\\s*<ProjectGuid>(.*)</ProjectGuid>", out projectGUID))
                {
                    projectGUID = Guid.NewGuid();
                }

                if (!solutionExists)
                {
                    GenerateSolution(projectGUID);
                }

                if (!projectExists)
                {
                    GenerateProject(projectGUID);
                    return projectGeneratedReturnValue;
                }
            }


            return true;
        }

        /// <summary>Loads the server runtime project.</summary>
        /// <returns>Server runtime XML project configuration</returns>
        private XmlDocument LoadProject()
        {
            XmlDocument projectXml = new XmlDocument();
            try
            {
                projectXml.Load(ksPaths.ServerRuntimeProject);
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Error loading " + ksPaths.ServerRuntimeProject, e);
                return null;
            }
            return projectXml;
        }

        /// <summary>Saves the server runtime project.</summary>
        /// <param name="projectXml">Server runtime XML project configuration data to save.</param>
        private void SaveProject(XmlDocument projectXml)
        {
            try
            {
                projectXml.Save(ksPaths.ServerRuntimeProject);
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Unable to write to " + ksPaths.ServerRuntimeProject, e);
            }
        }

        /// <summary>Adds files to a .csproj's includes.</summary>
        /// <param name="projectXml">Server runtime XML project configuration data to update.</param>
        /// <param name="fileNames">Files to add.</param>
        private void AddFilesToProject(XmlDocument projectXml, HashSet<string> fileNames)
        {
            XmlNode root = projectXml.DocumentElement;
            XmlNode node = FindNode(root, "Compile");
            if (node == null)
            {
                node = projectXml.CreateElement("ItemGroup", root.NamespaceURI);
                root.AppendChild(node);
            }
            else
            {
                node = node.ParentNode;
            }
            foreach (string fileName in fileNames)
            {
                XmlElement element = projectXml.CreateElement("Compile", root.NamespaceURI);
                element.SetAttribute("Include", fileName);
                node.AppendChild(element);
            }
        }

        /// <summary>
        /// Returns a set of files with the given extension in common and server folders, relative to the server
        /// runtime folder.
        /// </summary>
        /// <param name="ext">File extension</param>
        /// <returns>File names found.</returns>
        private HashSet<string> GetCommonAndServerFiles(string ext)
        {
            HashSet<string> files = new HashSet<string>();
            foreach (string path in ksPaths.CommonFolders)
            {
                GetFilesInAsmDefFolder(path, ext, files, ksPaths.ServerScripts);
            }
            foreach (string path in ksPaths.ServerFolders)
            {
                GetFilesInAsmDefFolder(path, ext, files, ksPaths.ServerScripts);
            }
            return files;
        }

        /// <summary>
        /// Adds references to unreferenced .cs files in the server runtime folder, and removes references to missing
        /// files.
        /// </summary>
        /// <param name="projectXml">Server runtime XML project configuration data to update.</param>
        /// <returns>True if a reference was added or removed.</returns>
        private bool UpdateCompileIncludes(XmlDocument projectXml)
        {
            HashSet<string> files = GetCommonAndServerFiles("cs");
            List<XmlNode> removeList = new List<XmlNode>();
            XmlNodeList compileNodes = FindNodes(projectXml.DocumentElement, "Compile");
            bool changed = false;
            foreach (XmlNode node in compileNodes)
            {
                XmlNode includeAttribute = node.Attributes.GetNamedItem("Include");
                if (includeAttribute == null)
                {
                    continue;
                }
                string path = ksPathUtils.Clean(includeAttribute.InnerText);
                if (!files.Remove(path))
                {
                    removeList.Add(node);
                    changed = true;
                }
            }
            foreach (XmlNode node in removeList)
            {
                RemoveNode(node);
            }
            if (files.Count > 0)
            {
                AddFilesToProject(projectXml, files);
                changed = true;
            }
            return changed;
        }

        /// <summary>Update the project file with server define symbols.</summary>
        /// 
        /// <param name="projectXml">Server runtime XML project configuration data to update.</param>
        /// <returns>True if the project was updated.</returns>
        private bool UpdateDefineSymbols(XmlDocument projectXml)
        {
            bool updated = false;
            string symbols = ksReactorConfig.Instance.Server.DefineSymbols.Trim();

            //replace commas, and spaces with semi-colons
            symbols = symbols.Replace(',', ';');
            symbols = symbols.Replace(' ', ';');
            if (symbols.Length > 0)
            {
                symbols += ";";
            }

            XmlNode root = projectXml.DocumentElement;
            XmlNodeList defineNodes = FindNodes(root, "DefineConstants");

            foreach (XmlNode node in defineNodes)
            {
                if (node != null)
                {
                    string newValue = symbols;

                    // REACTOR_SERVER is always the first built-in define symbol.
                    int index = node.InnerText.LastIndexOf("REACTOR_SERVER");
                    // Append build-in define symbols to the custom define symbols.
                    if (index >= 0)
                    {
                        newValue = symbols + node.InnerText.Substring(index);
                    }
                    else
                    {
                        newValue = symbols;
                    }

                    if (node.InnerText != newValue)
                    {
                        node.InnerText = newValue;
                        updated = true;
                    }
                }
            }

            return updated;
        }

        /// <summary>Checks if a .cs file is included in a project.</summary>
        /// <param name="projectXml">Server runtime XML project configuration data to check.</param>
        /// <param name="fileName">File name to check for.</param>
        /// <returns>True if the file is included in the project.</returns>
        private bool IsFileIncluded(XmlDocument projectXml, string fileName)
        {
            XmlNode root = projectXml.DocumentElement;
            XmlNodeList compileNodes = FindNodes(root, "Compile");
            foreach (XmlNode node in compileNodes)
            {
                XmlNode includeAttribute = node.Attributes.GetNamedItem("Include", root.NamespaceURI);
                if (includeAttribute != null && ksPathUtils.Clean(includeAttribute.InnerText).Equals(fileName))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Adds libraries to a project's references.</summary>
        /// <param name="projectXml">Server runtime XML project configuration data to update.</param>
        /// <param name="libraries">Libraries to add.</param>
        private void AddLibrariesToProject(XmlDocument projectXml, HashSet<string> libraries)
        {
            XmlNode root = projectXml.DocumentElement;
            XmlNode node = FindNode(root, "Reference");
            if (node == null)
            {
                node = projectXml.CreateElement("ItemGroup", root.NamespaceURI);
                root.AppendChild(node);
            }
            else
            {
                node = node.ParentNode;
            }
            foreach (string library in libraries)
            {
                string name = library;
                int index = name.LastIndexOf('.');
                if (index >= 0)
                {
                    name = name.Substring(0, index);
                }
                index = name.LastIndexOf('/');
                if (index >= 0)
                {
                    name = name.Substring(index + 1);
                }
                XmlElement element = projectXml.CreateElement("Reference", root.NamespaceURI);
                element.SetAttribute("Include", name);
                XmlElement hintPath = projectXml.CreateElement("HintPath", root.NamespaceURI);
                hintPath.InnerText = library;
                element.AppendChild(hintPath);
                node.AppendChild(element);
            }
        }

        /// <summary>
        /// Adds references to unreferenced dlls in the server runtime folder,
        /// and removes references to missing dlls.
        /// </summary>
        /// <param name="projectXml">Server runtime XML project configuration data to update.</param>
        /// <returns>True if a reference was added or removed.</returns>
        private bool UpdateReferenceIncludes(XmlDocument projectXml)
        {
            HashSet<string> libraries = GetCommonAndServerFiles("dll");
            List<XmlNode> removeList = new List<XmlNode>();
            XmlNodeList compileNodes = FindNodes(projectXml.DocumentElement, "HintPath");
            bool changed = false;
            foreach (XmlNode node in compileNodes)
            {
                if (node.InnerText.EndsWith("KSReactor.dll") || node.InnerText.EndsWith("KSCommon.dll") || node.InnerText.EndsWith("KSLZMA.dll"))
                {
                    continue;
                }
                string path = ksPathUtils.Clean(node.InnerText);
                if (!libraries.Remove(path))
                {
                    removeList.Add(node.ParentNode);
                    changed = true;
                }
            }
            foreach (XmlNode node in removeList)
            {
                RemoveNode(node);
            }
            if (libraries.Count > 0)
            {
                AddLibrariesToProject(projectXml, libraries);
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// Updates the precompiled references in an asmdef file. Parses the file to see if the existing references
        /// match the new <paramref name="references"/>, and updates the file if they do not.
        /// </summary>
        /// <param name="path">Path to the asmdef file.</param>
        /// <param name="references">
        /// Precompiled references. Each reference should be the file name of a dll including the extension, surrounded
        /// by '"'.
        /// </param>
        private void UpdateAsmDefPrecompiledReferences(string path, List<string> references)
        {
            string fileText = File.ReadAllText(path);
            int startIndex = fileText.IndexOf("\"precompiledReferences\":");
            if (startIndex < 0)
            {
                ksLog.Warning(this, "Could not find precompiledReferences in '" + path + "'.");
                return;
            }
            startIndex = fileText.IndexOf('[', startIndex) + 1;
            int endIndex = fileText.IndexOf(']', startIndex);
            string referencesStr = fileText.Substring(startIndex, endIndex - startIndex);
            string[] parts = referencesStr.Split(',');
            // Build a set of the current precompiled references.
            HashSet<string> currentReferences = new HashSet<string>();
            foreach (string str in parts)
            {
                // Remove parts of string outside quotes.
                int s = str.IndexOf('"');
                int e = str.LastIndexOf('"');
                if (s >= e)
                {
                    continue;
                }
                currentReferences.Add(str.Substring(s, e - s + 1));
            }
            if (ContentsEqual(references, currentReferences))
            {
                // The references do not need updating.
                return;
            }
            // Write the file with the updated references.
            referencesStr = string.Join(", ", references);
            fileText = fileText.Substring(0, startIndex) + referencesStr + fileText.Substring(endIndex);
            Write(path, fileText);
        }

        /// <summary>Checks if a list and a set of strings contain the same contents.</summary>
        /// <param name="list">List</param>
        /// <param name="set">Set</param>
        /// <returns>
        /// True if the <paramref name="list"/> and the <paramref name="set"/> have the same contents.
        /// </returns>
        private bool ContentsEqual(List<string> list, HashSet<string> set)
        {
            if (list.Count != set.Count)
            {
                return false;
            }
            foreach (string str in list)
            {
                if (!set.Contains(str))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Fills a collection with a list of files in a folder and its subfolders. Does not check subfolders that
        /// contain an asmdef or asmref.
        /// </summary>
        /// <param name="path">Path to search for files.</param>
        /// <param name="extension">Extension of files to find.</param>
        /// <param name="outFiles">Collection to put file names in.</param>
        /// <param name="relativeTo">
        /// If provided, file names will be relative to this folder. If null, they are relative to <paramref name="path"/>.
        /// </param>
        private void GetFilesInAsmDefFolder(string path, string extension, ICollection<string> outFiles, string relativeTo = null)
        {
            string relativePath = "";
            if (relativeTo != null)
            {
                relativePath = ksPathUtils.MakeRelative(path, relativeTo);
            }
            try
            {
                if (!Directory.Exists(path))
                {
                    return;
                }
                foreach (string fileName in Directory.GetFiles(path, "*." + extension))
                {
                    string name = ksPathUtils.Clean(fileName.Substring(path.Length));
                    if (name.StartsWith("/") || name.StartsWith("\\"))
                    {
                        name = name.Substring(1);
                    }
                    outFiles.Add(Path.Combine(relativePath, name));
                }

                // Search sub directories that don't have asmdefs or asmrefs.
                foreach (string directory in Directory.GetDirectories(path))
                {
                    if (!HasAsmDefOrRef(directory))
                    {
                        GetFilesInAsmDefFolder(directory, extension, outFiles, relativeTo == null ? path : relativeTo);
                    }
                }
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Error loading " + extension + " files in " + path, e);
            }
        }

        /// <summary>Checks if a directory has an asmdef or asmref file.</summary>
        /// <param name="directory">Directory to check.</param>
        /// <returns>True if the directory has an amsdef or asmref file.</returns>
        private bool HasAsmDefOrRef(string directory)
        {
            return Directory.GetFiles(directory, "*.asmdef").Length != 0 || 
                Directory.GetFiles(directory, "*.asmref").Length != 0;
        }

        /// <summary>Removes an XML node. Remove's the node's parent if the parent has no other children.</summary>
        /// <param name="node">Node to remove.</param>
        private void RemoveNode(XmlNode node)
        {
            if (node.ParentNode.ChildNodes.Count == 1)
            {
                node.ParentNode.ParentNode.RemoveChild(node.ParentNode);
            }
            else
            {
                node.ParentNode.RemoveChild(node);
            }
        }

        /// <summary>Finds all nodes of a given type in an XML document.</summary>
        /// <param name="node">Node with the namespace to search in.</param>
        /// <param name="type">Type of node to look for.</param>
        /// <returns>Nodes of the given type.</returns>
        private XmlNodeList FindNodes(XmlNode node, string type)
        {
            XmlNamespaceManager namespaceManager = new XmlNamespaceManager(node.OwnerDocument.NameTable);
            namespaceManager.AddNamespace("ns", node.NamespaceURI);
            return node.SelectNodes("//ns:" + type, namespaceManager);
        }

        /// <summary>Finds the first nodes of a given type in an XML document.</summary>
        /// <param name="node">Node with the namespace to search in.</param>
        /// <param name="type">Type of node to look for.</param>
        /// <returns>First node of the given type, or null if no nodes of that type are found.</returns>
        private XmlNode FindNode(XmlNode node, string type)
        {
            XmlNamespaceManager namespaceManager = new XmlNamespaceManager(node.OwnerDocument.NameTable);
            namespaceManager.AddNamespace("ns", node.NamespaceURI);
            return node.SelectSingleNode("//ns:" + type, namespaceManager);
        }

        /// <summary>Gets output and proxy paths.</summary>
        /// <param name="outputPath">Path to build to relative to the server runtime project directory.</param>
        /// <param name="proxyPath">Path to put proxy scripts in relative to the reflection tool.</param>
        /// <param name="intermediatePath">Path to the intermediate directory used when compiling.</param>
        private void GetRelativePaths(out string outputPath, out string proxyPath, out string intermediatePath)
        {
            Uri root = new Uri(ksPaths.ProjectRoot);
            Uri installationPathUri = null;
            try
            {
                installationPathUri = new Uri(ksPaths.ServerDir);
            }
            catch (UriFormatException)
            {
                installationPathUri = new Uri(root, ksPaths.ServerDir);
            }

            Uri serverRuntimeUri = new Uri(root, ksPaths.ServerScripts);
            Uri reflectionToolPathUri = new Uri(installationPathUri, "KSServerRuntime/");
            outputPath = serverRuntimeUri.MakeRelativeUri(installationPathUri).ToString();
            intermediatePath = serverRuntimeUri.MakeRelativeUri(new Uri(root, ksPaths.External + "intermediate/")).ToString();
            proxyPath = reflectionToolPathUri.MakeRelativeUri(new Uri(root, ksPaths.Proxies)).ToString();
#if !UNITY_2019_3_OR_NEWER
            if (EditorApplication.scriptingRuntimeVersion != ScriptingRuntimeVersion.Latest)
            {
                outputPath = "../" + outputPath;
                proxyPath = "../" + proxyPath;
            }
#endif
        }

        /// <summary>Gets the command to generate proxy scripts.</summary>
        /// <param name="proxyPath">Path to put proxy scripts in.</param>
        /// <returns>Command to generate proxy scripts.</returns>
        private string GetProxyCommand(string proxyPath)
        {
            switch (ksPaths.OperatingSystem)
            {
                case "Unix":
                    if (String.IsNullOrEmpty(ksReactorConfig.Instance.Server.MonoPath))
                    {
                        return "mono KSReflectionTool.exe \"" + proxyPath + "\"";
                    }
                    else
                    {
                        return "\"" + ksReactorConfig.Instance.Server.MonoPath + "\" KSReflectionTool.exe \"" + proxyPath + "\"";
                    }
                default:
                    return "KSReflectionTool.exe \"" + proxyPath + "\"";
            }
        }

        /// <summary>Generates the common scripts asmdef.</summary>
        private void GenerateCommonAsmDef()
        {
            string template =
@"{
    ""name"": ""KSScripts-Common"",
    ""rootNamespace"": """",
    ""references"": %REFERENCES%,
    ""includePlatforms"": [],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": true,
    ""precompiledReferences"": %PRECOMPILED%,
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": [],
    ""noEngineReferences"": true
}";
#if KS_DEVELOPMENT
            template = template.Replace("%REFERENCES%", "[\"Reactor\"]")
                .Replace("%PRECOMPILED%", "[]");
#else
            template = template.Replace("%REFERENCES%", "[]")
                .Replace("%PRECOMPILED%", "[\"KSCommon.dll\"]");
#endif
            Write(ksPaths.CommonScriptsAsmDef, template);
        }

        /// <summary>Generates the server scripts asmdef.</summary>
        private void GenerateServerAsmDef()
        {
            string template =
@"{
    ""name"": ""KSScripts-Server"",
    ""rootNamespace"": """",
    ""references"": %REFERENCES%,
    ""includePlatforms"": [
        ""Editor""
    ],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": true,
    ""precompiledReferences"": %PRECOMPILED%,
    ""autoReferenced"": false,
    ""defineConstraints"": [],
    ""versionDefines"": [],
    ""noEngineReferences"": true
}";
#if KS_DEVELOPMENT
            template = template.Replace("%REFERENCES%", "[\"KSScripts-Common\", \"Reactor\", \"ReactorEditor\"]")
                .Replace("%PRECOMPILED%", "[]");
#else
            template = template.Replace("%REFERENCES%", "[\"KSScripts-Common\"]")
                .Replace("%PRECOMPILED%", "[\"KSCommon.dll\", \"KSReactor.dll\"]");
#endif
            Write(ksPaths.ServerScriptsAsmDef, template);
        }

        /// <summary>Generates the KSServerRuntime solution.</summary>
        private void GenerateSolution(Guid projectGUID)
        {
            string template = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 17
VisualStudioVersion = 17.6.33723.286
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""KSServerRuntime"", ""KSServerRuntime.csproj"", ""{%PROJECT_GUID%}""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		LocalDebug|Any CPU = LocalDebug|Any CPU
		LocalRelease|Any CPU = LocalRelease|Any CPU
		OnlineDebug|Any CPU = OnlineDebug|Any CPU
        OnlineRelease|Any CPU = OnlineRelease|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{%PROJECT_GUID%}.LocalDebug|Any CPU.ActiveCfg = LocalDebug|Any CPU
		{%PROJECT_GUID%}.LocalDebug|Any CPU.Build.0 = LocalDebug|Any CPU
        {%PROJECT_GUID%}.LocalRelease|Any CPU.ActiveCfg = LocalRelease|Any CPU
		{%PROJECT_GUID%}.LocalRelease|Any CPU.Build.0 = LocalRelease|Any CPU
		{%PROJECT_GUID%}.OnlineDebug|Any CPU.ActiveCfg = OnlineDebug|Any CPU
		{%PROJECT_GUID%}.OnlineDebug|Any CPU.Build.0 = OnlineDebug|Any CPU
		{%PROJECT_GUID%}.OnlineRelease|Any CPU.ActiveCfg = OnlineRelease|Any CPU
		{%PROJECT_GUID%}.OnlineRelease|Any CPU.Build.0 = OnlineRelease|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
EndGlobal";

            template = template.Replace("%PROJECT_GUID%", projectGUID.ToString());
            Write(ksPaths.ServerRuntimeSolution, template);
        }

        /// <summary>Generates the KSServerRuntime project.</summary>
        private void GenerateProject(Guid projectGUID)
        {
            string outputPath;
            string proxyPath;
            string intermediatePath;
            GetRelativePaths(out outputPath, out proxyPath, out intermediatePath);
            string template = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project ToolsVersion=""15.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <Import Project=""$(MSBuildExtensionsPath)/$(MSBuildToolsVersion)/Microsoft.Common.props"" Condition=""Exists('$(MSBuildExtensionsPath)/$(MSBuildToolsVersion)/Microsoft.Common.props')"" />
  <PropertyGroup>
    <Configuration Condition="" '$(Configuration)' == '' "">LocalDebug</Configuration>
    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
    <ProjectGuid>{%PROJECT_GUID%}</ProjectGuid>
    <OutputType>Library</OutputType>
    <AppDesignerFolder>Properties</AppDesignerFolder>
    <RootNamespace>KSServerRuntime</RootNamespace>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
    <FileAlignment>512</FileAlignment>
    <TargetFrameworkProfile />
    <BaseIntermediateOutputPath>" + intermediatePath + @"</BaseIntermediateOutputPath>
  </PropertyGroup>
  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'LocalDebug|AnyCPU' "">
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <AssemblyName>KSServerRuntime.Local</AssemblyName>
    <OutputPath>" + outputPath + @"KSServerRuntime/</OutputPath>
    <DefineConstants>REACTOR_SERVER;REACTOR_LOCAL_SERVER;DEBUG;TRACE;</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
    <PlatformTarget>x64</PlatformTarget>
    <RunPostBuildEvent>OnOutputUpdated</RunPostBuildEvent>
    <PostBuildEvent>" + GetProxyCommand(proxyPath) + @"</PostBuildEvent>
  </PropertyGroup>
  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'LocalRelease|AnyCPU' "">
    <DebugType>pdbonly</DebugType>
    <Optimize>true</Optimize>
    <AssemblyName>KSServerRuntime.Local</AssemblyName>
    <OutputPath>" + outputPath + @"KSServerRuntime/</OutputPath>
    <DefineConstants>REACTOR_SERVER;REACTOR_LOCAL_SERVER;TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
    <PlatformTarget>x64</PlatformTarget>
    <RunPostBuildEvent>OnOutputUpdated</RunPostBuildEvent>
    <PostBuildEvent>" + GetProxyCommand(proxyPath) + @"</PostBuildEvent>
  </PropertyGroup>
  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'OnlineDebug|AnyCPU' "">
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <AssemblyName>KSServerRuntime</AssemblyName>
    <OutputPath>" + outputPath + @"KSServerRuntime/</OutputPath>
    <DefineConstants>REACTOR_SERVER;DEBUG;TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
    <PlatformTarget>x64</PlatformTarget>
  </PropertyGroup>
  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'OnlineRelease|AnyCPU' "">
    <DebugType>pdbonly</DebugType>
    <Optimize>true</Optimize>
    <AssemblyName>KSServerRuntime</AssemblyName>
    <OutputPath>" + outputPath + @"KSServerRuntime/</OutputPath>
    <DefineConstants>REACTOR_SERVER;TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
    <PlatformTarget>x64</PlatformTarget>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include=""System"" />
    <Reference Include=""System.Core"" />
    <Reference Include=""System.Xml.Linq"" />
    <Reference Include=""System.Data.DataSetExtensions"" />
    <Reference Include=""System.Data"" />
    <Reference Include=""System.Xml"" />
    <Reference Include=""KSCommon"">
      <HintPath>" + outputPath + @"KSCommon.dll</HintPath>
    </Reference>
    <Reference Include=""KSReactor"">
      <HintPath>" + outputPath + @"KSReactor.dll</HintPath>
    </Reference>
    <Reference Include=""KSLZMA"">
      <HintPath>" + outputPath + @"KSLZMA.dll</HintPath>
    </Reference>
  </ItemGroup>
  <ItemGroup>
    <Compile Include=""..\Common\**\*.cs"">
      <Link>%(RecursiveDir)%(FileName)</Link>
    </Compile>
  </ItemGroup>
  <Import Project=""$(MSBuildToolsPath)/Microsoft.CSharp.targets"" />
</Project>";
            template = template.Replace("%PROJECT_GUID%", projectGUID.ToString());
            Write(ksPaths.ServerRuntimeProject, template);
        }

        /// <summary>Writes to a file.</summary>
        /// <param name="path">Path to write to.</param>
        /// <param name="data">Data to write.</param>
        private void Write(string path, string data)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(path))
                {
                    writer.Write(data);
                    writer.Close();
                }
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Error writing to " + path, e);
            }
        }

        /// <summary>
        /// Attempt to get a GUID from a file using a regex pattern.
        /// </summary>
        /// <param name="path">Solution or project file.</param>
        /// <param name="regex">Regex which will match the project GUID for the file.</param>
        /// <param name="guid">Guid extracted from the file.</param>
        /// <returns>True if the guid was found.</returns>
        private bool TryGetGuidFromFile(string path, string regex, out Guid guid)
        {
            guid = Guid.Empty;
            try
            {
                if (File.Exists(path))
                {
                    string[] lines = File.ReadAllLines(path);
                    foreach (string line in lines)
                    {
                        Match match = Regex.Match(line, regex);
                        if (match != null)
                        {
                            GroupCollection groups = match.Groups;
                            if (groups != null && groups.Count == 2)
                            {
                                if (Guid.TryParse(groups[1].Value, out guid))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception) { } // Do nothing
            return false;
        }
    }
}
