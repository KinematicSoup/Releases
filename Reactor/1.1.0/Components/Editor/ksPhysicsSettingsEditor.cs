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
    /// <summary>Inspector editor for <see cref="ksPhysicsSettings"/>.</summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ksPhysicsSettings))]
    public class ksPhysicsSettingsEditor : UnityEditor.Editor
    {
        /// <summary>Create the GUI.</summary>
        public override void OnInspectorGUI()
        {
            ksPhysicsSettings settings = target as ksPhysicsSettings;
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                switch (iterator.name)
                {
                    case "m_Script":
                    {
                        enterChildren = false;
                        continue;
                    }
                    case "m_pvdAddress":
                    case "m_pvdPort":
                    {
                        if (settings.UsePhysXVisualDebugger)
                        {
                            EditorGUI.indentLevel = 1;
                            EditorGUILayout.PropertyField(iterator);
                            EditorGUI.indentLevel = 0;
                        }
                        break;
                    }
                    default:
                        EditorGUILayout.PropertyField(iterator);
                        enterChildren = false;
                        continue;
                }
                enterChildren = iterator.hasVisibleChildren && iterator.isExpanded;
            }

            if (GUI.changed)
            {
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
