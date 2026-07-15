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
using UnityEditor;
using UnityEngine;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>Custom editor for sfMissingPrefab.</summary>
    [CustomEditor(typeof(sfMissingPrefab))]
    internal class sfMissingPrefabEditor : UnityEditor.Editor
    {
        /// <summary>Creates the GUI. Displays the path to the missing prefab in a warning box.</summary>
        public override void OnInspectorGUI()
        {
            sfMissingPrefab script = target as sfMissingPrefab;
            if (script != null)
            {
                if (SceneFusion.Get().Service.IsConnected)
                {
                    // Prevent removing the script while in a session.
                    script.hideFlags |= HideFlags.NotEditable;
                }
                else
                {
                    script.hideFlags &= ~HideFlags.NotEditable;
                }
                string message = "Missing prefab: " + script.PrefabPath;
                if (script.FileId != 0)
                {
                    message += "\nLocal file id: " + script.FileId;
                }
                // End disabled group so the warning box does not appear faded when the component is not editable.
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.HelpBox(message, MessageType.Warning);
                EditorGUI.BeginDisabledGroup((script.hideFlags & HideFlags.NotEditable) == HideFlags.NotEditable);
            }
        }   
    }
}
