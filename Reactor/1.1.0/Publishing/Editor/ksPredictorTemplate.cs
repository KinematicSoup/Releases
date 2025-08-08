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
    /// <summary>Template for generating predictor scripts.</summary>
    public class ksPredictorTemplate : ScriptableObject, ksIScriptTemplate
    {
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
@"[CreateAssetMenu(menuName = ksMenuNames.REACTOR + ""%NAME%"", order = ksMenuGroups.SCRIPT_ASSETS)]
public class %NAME% : ksPredictor
{
    %REQUIRES_CONTROLLER_TEMPLATE%
    // Initializes the predictor. Return false if initialization fails.
    public override bool Initialize()
    {
        return true;
    }
    %ENABLED_DISABLED_TEMPLATE%

    // Called when the predictor is removed. Performs clean up.
    public override void Detached()
    {
        // The base method clears the Entity/Room reference.
        base.Detached();
    }
    %PREDICTED_PROPERTY_TEMPLATE%

    // Called once per server frame. Perform any relevant calculations with server frame data here, and/or store
    // the relevant server frame data to be used in calculating future client state in the ClientUpdate method.
    // If it returns false and idle is true, this function will not be called again until the server transform
    // or a server property changes.
    public override bool ServerUpdate(ksReadOnlyTransformState state, Dictionary<uint, ksMultiType> properties, bool teleport, bool idle)
    {
        return false;
    }

    // Called once per client render frame to update the client transform and smoothed properties.
    // If it returns false, this function will not be called again until the server transform or a
    // server property changes.
    public override bool ClientUpdate(ksTransformState state, Dictionary<uint, ksMultiType> properties)
    {
        return false;
    }
    %INPUT_UPDATE_TEMPLATE%
}";

        private const string NAMESPACE_TEMPLATE =
@"namespace %NAMESPACE%
{
    %CLASS_TEMPLATE%
}";

        private const string REQUIRES_CONTROLLER_TEMPLATE =
    @"// Does this predictor require a player controller?
    public override bool RequiresController
    {
        get { return true; }
    }
    ";
        private const string ENABLED_DISABLED_TEMPLATE = @"
    // Called when the predictor is enabled.
    public override void Enabled()
    {
        
    }

    // Called when the predictor is disabled.
    public override void Disabled()
    {
        
    }";

        private const string PREDICTED_PROPERTY_TEMPLATE = @"
    // Return true if the predictor will predict the property value.
    public override bool IsPredictedProperty(uint propertyId)
    {
        return false;
    }";
        private const string INPUT_UPDATE_TEMPLATE = @"
    // Called when a new frame of input is generated. Only called for entities with player controllers.
    public override void InputUpdate(ksInput input)
    {
        
    }";

        private const uint IS_PREDICTED_PROPERTY = 0;
        private const uint INPUT_UPDATE = 1;
        private const uint REQUIRES_CONTROLLER = 2;
        private const uint ENABLED_DISABLED = 3;

        private static readonly ksOptionalMethod[] OPTIONAL_METHODS = new ksOptionalMethod[]
        {
            new ksOptionalMethod(ENABLED_DISABLED, "Enabled/Disabled", false),
            new ksOptionalMethod(IS_PREDICTED_PROPERTY, "IsPredictedProperty", false),
            new ksOptionalMethod(INPUT_UPDATE, "InputUpdate", false),
            new ksOptionalMethod(REQUIRES_CONTROLLER, "RequiresController", false)
        };

        [SerializeField]
        private GameObject[] m_gameObjects;

        /// <summary>Script generator</summary>
        public ksScriptGenerator Generator
        {
            get { return m_generator; }
            set { m_generator = value; }
        }
        private ksScriptGenerator m_generator;

        /// <summary>Default file name for scripts generated from this template.</summary>
        public string DefaultFileName
        {
            get { return "Predictor"; }
        }

        /// <summary>Default path for scripts generated from this template.</summary>
        public string DefaultPath
        {
            get { return ksPaths.ClientScripts; }
        }

        /// <summary>Optional methods the template can generate.</summary>
        public ksOptionalMethod[] OptionalMethods
        {
            get { return OPTIONAL_METHODS; }
        }

        /// <summary>Initialization</summary>
        /// <param name="gameObjects">Game objects to attach the script to once generated.</param>
        /// <return>this</return>
        public ksPredictorTemplate Initialize(GameObject[] gameObjects = null)
        {
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
        /// <param name="className">Name of generated class.</param>
        /// <param name="scriptNamespace">Namespace of generated class.</param>
        /// <param name="optionalMethods">Contains ids of optional method stubs to generate.</param>
        /// <returns>Script contents</returns>
        public string Generate(string path, string className, string scriptNamespace, HashSet<uint> optionalMethods)
        {
            string template = CLASS_TEMPLATE.Replace("%NAME%", className)
                .Replace("%ENABLED_DISABLED_TEMPLATE%",
                    optionalMethods.Contains(ENABLED_DISABLED) ? ENABLED_DISABLED_TEMPLATE : "%DELETE%")
                .Replace("%PREDICTED_PROPERTY_TEMPLATE%",
                    optionalMethods.Contains(IS_PREDICTED_PROPERTY) ? PREDICTED_PROPERTY_TEMPLATE : "%DELETE%")
                .Replace("%INPUT_UPDATE_TEMPLATE%",
                    optionalMethods.Contains(INPUT_UPDATE) ? INPUT_UPDATE_TEMPLATE : "%DELETE%")
                .Replace("%REQUIRES_CONTROLLER_TEMPLATE%",
                    optionalMethods.Contains(REQUIRES_CONTROLLER) ? REQUIRES_CONTROLLER_TEMPLATE : "%DELETE%");
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