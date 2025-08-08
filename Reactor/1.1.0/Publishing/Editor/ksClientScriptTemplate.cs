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
using UnityEngine;
using UnityEditor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Template for generating client scripts.</summary>
    public class ksClientScriptTemplate : ScriptableObject, ksIScriptTemplate
    {
        /// <summary>Script types that can be generated.</summary>
        public enum ScriptType
        {
            /// <summary>Create a <see cref="ksEntityScript"/></summary>
            ENTITY = 0,
            /// <summary>Create a <see cref="ksRoomScript"/></summary>
            ROOM = 1,
            /// <summary>Create a <see cref="ksPlayerScript"/></summary>
            PLAYER = 2
        }

        private const string TEMPLATE =
@"using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using KS.Reactor.Client.Unity;
using KS.Reactor.Client;
using KS.Reactor;

%INNER_TEMPLATE%";

        private const string CLASS_TEMPLATE =
@"public class %NAME% : %BASE%
{
    %ATTACHED%
    // Called after all other scripts/entities are attached/spawned.
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
        private const string ATTACHED_TEMPLATE =
@"// Called when the script is attached, before other scripts/entities are attached/spawned.
    public override void Attached()
    {
        
    }
    ";

        private const string UPDATE_TEMPLATE = @"
    // Called every frame.
    private void Update()
    {
        
    }";
        private const string PLAYER_JOIN_LEAVE_TEMPLATE = @"
    // Called when a player connects.
    private void PlayerJoin(ksPlayer player)
    {
        
    }
    
    // Called when a player disconnects.
    private void PlayerLeave(ksPlayer player)
    {
        
    }";
        // Optional method IDs
        private const uint UPDATE = 0;
        private const uint PLAYER_JOIN_LEAVE = 1;
        private const uint ATTACHED = 2;
        private static readonly ksOptionalMethod[] OPTIONAL_METHODS = 
            new ksOptionalMethod[] 
        {
            new ksOptionalMethod(ATTACHED, "Attached", false),
            new ksOptionalMethod(UPDATE, "Update", true)
        };
        private static readonly ksOptionalMethod[] OPTIONAL_ROOM_METHODS = new ksOptionalMethod[]
        {
            new ksOptionalMethod(ATTACHED, "Attached", false),
            new ksOptionalMethod(UPDATE, "Update", true),
            new ksOptionalMethod(PLAYER_JOIN_LEAVE, "OnPlayerJoin/Leave", false)
        };

        private ksScriptGenerator m_generator;

        [SerializeField]
        private ScriptType m_scriptType;

        [SerializeField]
        private GameObject[] m_gameObjects;

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
                        return "ClientEntityScript";
                    case ScriptType.ROOM:
                        return "ClientRoomScript";
                    case ScriptType.PLAYER:
                        return "ClientPlayerScript";
                    default:
                        return "ClientScript";
                }
            }
        }

        ///<summary>Default path for scripts generated from this template.</summary>
        public string DefaultPath
        {
            get { return ksPaths.ClientScripts; }
        }

        /// <summary>Optional methods the template can generate.</summary>
        public ksOptionalMethod[] OptionalMethods
        {
            get { return m_scriptType == ScriptType.ROOM ? OPTIONAL_ROOM_METHODS : OPTIONAL_METHODS; }
        }

        /// <summary>Initialization</summary>
        /// <param name="scriptType">Script type to generate.</param>
        /// <param name="gameObjects">Game objects to attach the script to once generated.</param>
        /// <return>this</return>
        public ksClientScriptTemplate Initialize(ScriptType scriptType, GameObject[] gameObjects = null)
        {
            m_scriptType = scriptType;
            m_gameObjects = gameObjects;
            return this;
        }

        /// <summary>This function is called when the object becomes enabled and active.</summary>
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
            string baseClass;
            switch (m_scriptType)
            {
                case ScriptType.ENTITY:
                    baseClass = "ksEntityScript";
                    break;
                case ScriptType.ROOM:
                    baseClass = "ksRoomScript";
                    break;
                case ScriptType.PLAYER:
                    baseClass = "ksPlayerScript";
                    break;
                default:
                    return "";
            }
            List<string> functions = new List<string>();
            List<string> initializeBody = new List<string>();
            List<string> detachedBody = new List<string>();
            if (optionalMethods.Contains(UPDATE))
            {
                functions.Add(UPDATE_TEMPLATE);
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
        /// <param name="className">Name of generated class.</param>
        /// <param name="scriptNamespace">Namespace of generated class.</param>
        public void HandleCreate(string path, string className, string scriptNamespace)
        {
            if (m_gameObjects != null)
            {
                m_generator.SaveAttachments(ksPaths.ClientScripts + className + ".cs", m_gameObjects);
            }
            AssetDatabase.Refresh();
        }
    }
}
