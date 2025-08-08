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
using UnityEditor;
using UnityEngine;
using KS.Unity.Editor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Template for generating server scripts.</summary>
    public class ksServerScriptTemplate : ScriptableObject, ksIScriptTemplate
    {
        /// <summary>Script types that can be generated.</summary>
        public enum ScriptType
        {
            /// <summary>Create a <see cref="Server.ksServerEntityScript"/></summary>
            ENTITY = 0,
            /// <summary>Create a <see cref="Server.ksServerRoomScript"/></summary>
            ROOM = 1,
            /// <summary>Create a <see cref="Server.ksServerPlayerScript"/></summary>
            PLAYER = 2
        }

        private const string TEMPLATE =
@"using System;
using System.Collections.Generic;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using KS.Reactor.Server;
using KS.Reactor;

%INNER_TEMPLATE%";

        private const string CLASS_TEMPLATE =
@"public class %NAME% : %BASE%
{
    %ATTACHED%
    // Called after all other scripts on all entities are attached.
    public override void Initialize()
    {
        %INITIALIZE_BODY%
    }
    
    // Called when the script is detached.
    public override void Detached()
    {
        %DETACHED_BODY%
    }
    %FUNCTIONS%
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
    public class %NAME% : %BASE%
    {
    }
}";
        private const string ATTACHED_TEMPLATE =
@"// Called when the script is attached, before other scripts on all entities are attached.
    public override void Attached()
    {
        
    }
    ";
        private const string AUTHENTICATE_TEMPLATE = @"
    // Authenticates a player. Return a non-zero result to fail authentication.
    private async Task<ksAuthenticationResult> Authenticate(ksIServerPlayer player, ksMultiType[] args, CancellationToken cancellationToken)
    {
        return await Task.FromResult(new ksAuthenticationResult(0));
    }";
        private const string PROCESS_INPUT_TEMPLATE = @"
    // Process input and update player controllers. Return true to stop further processing of the input.
    public bool ProcessInput(ksInput input, int numUpdates)
    {
        return true;
    }";
        private const string UPDATE_TEMPLATE = @"
    // Called during the update cycle
    private void Update()
    {
        
    }";
        private const string PLAYER_JOIN_LEAVE_TEMPLATE = @"
    // Called when a player connects.
    private void PlayerJoin(ksIServerPlayer player)
    {
        
    }
    
    // Called when a player disconnects.
    private void PlayerLeave(ksIServerPlayer player)
    {
        
    }";

        // Optional method IDs
        private const int UPDATE = 0;
        private const int AUTHENTICATE = 1;
        private const int PLAYER_JOIN_LEAVE = 2;
        private const int PROCESS_INPUT = 3;
        private const int ATTACHED = 4;

        private static readonly ksOptionalMethod[] ENTITY_OPTIONS = new ksOptionalMethod[] 
        { 
            new ksOptionalMethod(ATTACHED, "Attached", false),
            new ksOptionalMethod(UPDATE, "Update", true)
        };
        private static readonly ksOptionalMethod[] ROOM_OPTIONS = new ksOptionalMethod[]
        { 
            new ksOptionalMethod(ATTACHED, "Attached", false),
            new ksOptionalMethod(UPDATE, "Update", true),
            new ksOptionalMethod(AUTHENTICATE, "Authenticate", false),
            new ksOptionalMethod(PLAYER_JOIN_LEAVE, "OnPlayerJoin/Leave", false)
        };
        private static readonly ksOptionalMethod[] PLAYER_OPTIONS = new ksOptionalMethod[]
        {
            new ksOptionalMethod(ATTACHED, "Attached", false),
            new ksOptionalMethod(UPDATE, "Update", true),
            new ksOptionalMethod(PROCESS_INPUT, "ksIInputProcessor", false)
        };

        private ksScriptGenerator m_generator;

        [SerializeField]
        private ScriptType m_scriptType;

        [SerializeField]
        private GameObject[] m_gameObjects;

        private ksServerProjectUpdater m_csprojUpdater = ksServerProjectUpdater.Instance;

        /// <summary>Script generator</summary>
        public ksScriptGenerator Generator
        {
            get { return m_generator; }
            set { m_generator = value; }
        }

        /// <summary>Default file name for scripts generated from this template.</summary>
        public string DefaultFileName
        {
            get
            {
                switch (m_scriptType)
                {
                    case ScriptType.ENTITY:
                        return "ServerEntityScript";
                    case ScriptType.ROOM:
                        return "ServerRoomScript";
                    case ScriptType.PLAYER:
                        return "ServerPlayerScript";
                    default:
                        return "ServerScript";
                }
            }
        }

        /// <summary>Default path for scripts generated from this template.</summary>
        public string DefaultPath
        {
            get { return ksPaths.ServerScripts; }
        }

        /// <summary>Optional methods the template can generate.</summary>
        public ksOptionalMethod[] OptionalMethods
        {
            get
            {
                switch (m_scriptType)
                {
                    case ScriptType.ROOM:
                        return ROOM_OPTIONS;
                    case ScriptType.PLAYER:
                        return PLAYER_OPTIONS;
                    default:
                        return ENTITY_OPTIONS;
                }
            }
        }

        /// <summary>Initialization</summary>
        /// <param name="scriptType">Script type to generate.</param>
        /// <param name="gameObjects">Game objects to attach the script to once generated.</param>
        /// <return>Reference to this object.</return>
        public ksServerScriptTemplate Initialize(ScriptType scriptType, GameObject[] gameObjects = null)
        {
            m_scriptType = scriptType;
            m_gameObjects = gameObjects;
            return this;
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

            string baseClass;
            switch (m_scriptType)
            {
                case ScriptType.ENTITY:
                    baseClass = "ksServerEntityScript";
                    break;
                case ScriptType.ROOM:
                    baseClass = "ksServerRoomScript";
                    break;
                case ScriptType.PLAYER:
                    baseClass = "ksServerPlayerScript";
                    break;
                default:
                    return "";
            }
            List<string> functions = new List<string>();
            List<string> initializeBody = new List<string>();
            List<string> detachedBody = new List<string>();
            if (optionalMethods.Contains(AUTHENTICATE))
            {
                functions.Add(AUTHENTICATE_TEMPLATE);
                initializeBody.Add("Room.OnAuthenticate += Authenticate;");
                detachedBody.Add("Room.OnAuthenticate -= Authenticate;");
            }
            if (optionalMethods.Contains(PROCESS_INPUT))
            {
                functions.Add(PROCESS_INPUT_TEMPLATE);
                baseClass += ", ksIInputProcessor";
            }
            if (optionalMethods.Contains(UPDATE))
            {
                functions.Add(UPDATE_TEMPLATE);
                initializeBody.Add("Room.OnUpdate[0] += Update;");
                detachedBody.Add("Room.OnUpdate[0] -= Update;");
            }
            if (optionalMethods.Contains(PLAYER_JOIN_LEAVE))
            {
                functions.Add(PLAYER_JOIN_LEAVE_TEMPLATE);
                initializeBody.Add("Room.OnPlayerJoin += PlayerJoin;");
                initializeBody.Add("Room.OnPlayerLeave += PlayerLeave;");
                detachedBody.Add("Room.OnPlayerJoin -= PlayerJoin;");
                detachedBody.Add("Room.OnPlayerLeave -= PlayerLeave;");
            }
            string template = CLASS_TEMPLATE
                .Replace("%NAME%", className)
                .Replace("%BASE%", baseClass)
                .Replace("%ATTACHED%", optionalMethods.Contains(ATTACHED) ? ATTACHED_TEMPLATE : "%DELETE%")
                .Replace("%INITIALIZE_BODY%", string.Join(Environment.NewLine + "        ", initializeBody))
                .Replace("%DETACHED_BODY%", string.Join(Environment.NewLine + "        ", detachedBody))
                .Replace("%FUNCTIONS%", functions.Count == 0 ? 
                    "%DELETE%" : string.Join(Environment.NewLine + "    ", functions));
            if (!string.IsNullOrWhiteSpace(scriptNamespace))
            {
                template = template.Replace("\n", "\n    ");
                template = NAMESPACE_TEMPLATE
                    .Replace("%NAMESPACE%", scriptNamespace)
                    .Replace("%CLASS_TEMPLATE%", template);
            }
            template = TEMPLATE.Replace("%INNER_TEMPLATE%", template);
            template = m_generator.DeleteMatchingLines(template, "%DELETE%");
            return template;
        }

        /// <summary>Called after the script file is written.</summary>
        /// <param name="path">Path the script was written to.</param>
        /// <param name="className">Name of the generated class.</param>
        /// <param name="scriptNamespace">Namespace of the generated class.</param>
        public void HandleCreate(string path, string className, string scriptNamespace)
        {
            string proxyPath = GenerateProxyScript(className, scriptNamespace);
            AssetDatabase.Refresh();
            if (m_gameObjects != null && proxyPath != null)
            {
                m_generator.SaveAttachments(ksPathUtils.ToAssetPath(proxyPath), m_gameObjects);
            }
            m_csprojUpdater.AddFileToProject(path);
        }

        /// <summary>Generates a proxy script.</summary>
        /// <param name="className">Name of the class to generate proxy for.</param>
        /// <param name="scriptNamespace">Namespace of the class to generate proxy for.</param>
        /// <returns>Proxy script path.</returns>
        private string GenerateProxyScript(string className, string scriptNamespace)
        {
            string proxyScriptName = null;
            switch (m_scriptType)
            {
                case ScriptType.ENTITY:
                    proxyScriptName = "ksProxyEntityScript";
                    break;
                case ScriptType.ROOM:
                    proxyScriptName = "ksProxyRoomScript";
                    break;
                case ScriptType.PLAYER:
                    proxyScriptName = "ksProxyPlayerScript";
                    break;
                default:
                    return null;
            }

            string path = ksPaths.Proxies + "Scripts/";
            if (scriptNamespace.Length > 0)
            {
                path += scriptNamespace.Replace('.', '/') + "/";
                scriptNamespace = "." + scriptNamespace;
            }
            path += className + ".cs";
            string template = PROXY_TEMPLATE
                .Replace("%NAMESPACE%", scriptNamespace)
                .Replace("%NAME%", className)
                .Replace("%BASE%", proxyScriptName);
            if (m_generator.Write(path, template))
            {
                return path;
            }
            return null;
        }
    }
}
