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
using UnityEditor;
using UnityEngine;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Template for generating script assets.</summary>
    public class ksScriptAssetTemplate : ScriptableObject, ksIScriptTemplate
    {
        private const string TEMPLATE =
@"using System;
using System.Collections.Generic;
using System.Collections;
using KS.Reactor;
%USING%

%INNER_TEMPLATE%";

        private const string CLASS_TEMPLATE =
@"%TAGS%
public class %NAME% : ksScriptAsset
{
    
}";
        private const string NAMESPACE_TEMPLATE =            
@"namespace %NAMESPACE%
{
    %CLASS_TEMPLATE%
}";

        private const string PROXY_TEMPLATE =
@"/* This file was auto-generated. DO NOT MODIFY THIS FILE. */
using System;
using UnityEngine;
using KS.Reactor.Client.Unity;
using KS.Reactor;

namespace KSProxies.Scripts%NAMESPACE%
{
    [CreateAssetMenu(menuName = ksMenuNames.REACTOR + ""%NAME%"", order = ksMenuGroups.SCRIPT_ASSETS)]
    public class %NAME% : ksProxyScriptAsset
    {
    }
}";

        /// <summary>Script generator</summary>
        private ksScriptGenerator m_generator;
        public ksScriptGenerator Generator
        {
            get { return m_generator; }
            set { m_generator = value; }
        }

        /// <summary>Default file name for scripts generated from this template.</summary>
        public string DefaultFileName
        {
            get { return "ScriptAsset"; }
        }

        /// <summary>Default path for scripts generated from this template.</summary>
        public string DefaultPath
        {
            get { return ksPaths.CommonScripts; }
        }

        /// <summary>Optional methods the template can generate.</summary>
        public ksOptionalMethod[] OptionalMethods
        {
            get { return null; }
        }

        /// <summary>Indicates if the generated script should be attached when it is generated.</summary>
        public bool Attach
        {
            get { return false; }
        }

        /// <summary>Unity on enable</summary>
        public void OnEnable()
        {
            hideFlags = HideFlags.HideAndDontSave;
        }

        /// <summary>Generates the contents of a script.</summary>
        /// <param name="path">Path the script will be written to.</param>
        /// <param name="className">Name of the generated class.</param>
        /// <param name="scriptNamespace">Namespace of the generated class.</param>
        /// <param name="optionalMethods">Contains ids of optional method stubs to generate.</param>
        /// <returns>Script contents</returns>
        public string Generate(string path, string className, string scriptNamespace, HashSet<uint> optionalMethods)
        {
            ksServerProjectWatcher.Get().Ignore(path);
            bool isServerScript = ksPaths.IsServerPath(path);
            string template = CLASS_TEMPLATE
                .Replace("%TAGS%", isServerScript ? "%DELETE%" : "[ksSharedData]")
                .Replace("%NAME%", className);
            if (!string.IsNullOrWhiteSpace(scriptNamespace))
            {
                template = template.Replace("\n", "\n    ");
                template = NAMESPACE_TEMPLATE
                    .Replace("%NAMESPACE%", scriptNamespace)
                    .Replace("%CLASS_TEMPLATE%", template);
            }
            template = TEMPLATE
                .Replace("%USING%", isServerScript ? "using KS.Reactor.Server;" : "%DELETE%")
                .Replace("%INNER_TEMPLATE%", template);
            template = m_generator.DeleteMatchingLines(template, "%DELETE%");
            return template;
        }

        /// <summary>
        /// Called after the script file is written. Generates a proxy for the new script and updates the server
        /// runtime project.
        /// </summary>
        /// <param name="path">Path the script was written to.</param>
        /// <param name="className">Name of the generated class.</param>
        /// <param name="scriptNamespace">Namespace of the generated class.</param>
        public void HandleCreate(string path, string className, string scriptNamespace)
        {
            GenerateProxyScript(className, scriptNamespace);
            ksServerProjectUpdater.Instance.AddFileToProject(path);
            AssetDatabase.Refresh();
        }

        /// <summary>Generates a proxy script.</summary>
        /// <param name="className">Name of the class to generate proxy for.</param>
        /// <param name="scriptNamespace">Namespace of the class to generate proxy for.</param>
        private void GenerateProxyScript(string className, string scriptNamespace)
        {
            string path = ksPaths.Proxies + "Scripts/";
            if (scriptNamespace.Length > 0)
            {
                path += scriptNamespace.Replace('.', '/') + "/";
                scriptNamespace = "." + scriptNamespace;
            }
            path += className + ".cs";
            string template = PROXY_TEMPLATE
                .Replace("%NAMESPACE%", scriptNamespace)
                .Replace("%NAME%", className);
            m_generator.Write(path, template);
        }
    }
}