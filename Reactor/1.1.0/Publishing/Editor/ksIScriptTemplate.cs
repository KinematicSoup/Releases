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
using System.Collections.Generic;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Interface for templates from which scripts can be generated.</summary>
    public interface ksIScriptTemplate
    {
        /// <summary>Script generator</summary>
        ksScriptGenerator Generator { get; set; }

        /// <summary>Default file name for scripts generated from this template.</summary>
        string DefaultFileName { get; }

        /// <summary>Default path for scripts generated from this template.</summary>
        string DefaultPath { get; }

        /// <summary>Optional methods the template can generate.</summary>
        ksOptionalMethod[] OptionalMethods { get; }

        /// <summary>Generates the contents of a script.</summary>
        /// <param name="path">Path the script will be written to.</param>
        /// <param name="className">Name of generated class.</param>
        /// <param name="scriptNamespace">Namespace of generated class.</param>
        /// <param name="optionalMethods">Contains ids of optional method stubs to generate.</param>
        /// <returns>Script contents</returns>
        string Generate(string path, string className, string scriptNamespace, HashSet<uint> optionalMethods);

        /// <summary>Called after the script file is written.</summary>
        /// <param name="path">Path the script was written to.</param>
        /// <param name="className">Name of generated class.</param>
        /// <param name="scriptNamespace">Namespace of generated class.</param>
        void HandleCreate(string path, string className, string scriptNamespace);
    }
}
