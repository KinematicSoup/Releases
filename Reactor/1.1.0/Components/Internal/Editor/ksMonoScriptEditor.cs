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

using UnityEditor;
using UnityEngine;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Inspector editor for client scripts.</summary>
    /// <typeparam name="Parent">Type the script can be attached to.</typeparam>
    /// <typeparam name="Script">Script type</typeparam>
    public abstract class ksMonoScriptEditor<Parent, Script> : UnityEditor.Editor
        where Parent : class
        where Script : ksMonoScript<Parent, Script>
    {
        /// <summary>Unity method to create the GUI.</summary>
        public override void OnInspectorGUI()
        {
            if (!(target is Script))
            {
                // This can happen if the script is deleted while this editor is in the inspector
                EditorGUILayout.HelpBox("Script is not a " + typeof(Script).Name, MessageType.Warning, true);
                return;
            }
            Validate();
            DrawDefaultInspector();
        }

        /// <summary>
        /// Validates the selected game object and displays warnings if something is missing.
        /// </summary>
        protected abstract void Validate();

        /// <summary>
        /// Displays a warning if no script of type T is found on the selected game object.
        /// </summary>
        /// <typeparam name="ComponentType">Component Type</typeparam>
        protected void CheckComponent<ComponentType>()
        {
            if (((MonoBehaviour)target).GetComponent<ComponentType>() == null)
            {
                string type = typeof(ComponentType).Name;
                EditorGUILayout.HelpBox("No " + type + " found on this game object. " +
                    "You cannot use this script without a " + type + ".", MessageType.Warning);
            }
        }
    }
}
