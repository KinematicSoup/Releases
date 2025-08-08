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

using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Inspector editor for <see cref="ksProxyScript"/>.</summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ksProxyScript), true)]
    public class ksProxyScriptEditor : UnityEditor.Editor
    {
        /// <summary>
        /// Adds a warning to the default inspector if the required <see cref="ksEntityComponent"/> 
        /// or <see cref="ksRoomType"/> component is missing from the selected game object.
        /// </summary>
        public override void OnInspectorGUI()
        {
            if (target is ksProxyEntityScript)
            {
                CheckComponent<ksEntityComponent>();
            }
            else if (target is ksProxyRoomScript || target is ksProxyPlayerScript)
            {
                CheckComponent<ksRoomType>();
            }
            foreach (ksWarningAttribute tag in target.GetType().GetCustomAttributes<ksWarningAttribute>())
            {
                EditorGUILayout.HelpBox(tag.Message, MessageType.Warning);
            }
            DrawDefaultInspector();
        }

        /// <summary>
        /// Displays a warning if no script of type <typeparamref="T"/> is found on the selected game object.
        /// </summary>
        /// <typeparam name="ComponentType">Component Type</typeparam>
        private void CheckComponent<ComponentType>()
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
