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
using UnityEngine;
using UnityEditor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Custom editor for <see cref="ksNetStatsMonitor"/>.</summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ksNetStatsMonitor))]
    public class ksNetStatsMonitorEditor : UnityEditor.Editor
    {
        /// <summary>Draws the inspector GUI.</summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty iterator = serializedObject.GetIterator();
            bool logStats = false;
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                switch (iterator.name)
                {
                    case "m_Script": break;
                    case nameof(ksNetStatsMonitor.LogStats):
                    {
                        logStats = iterator.boolValue;
                        EditorGUILayout.PropertyField(iterator);
                        break;
                    }
                    case nameof(ksNetStatsMonitor.MultiLine):
                    {
                        if (logStats)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(iterator);
                            EditorGUI.indentLevel--;
                        }
                        break;
                    }
                    default: EditorGUILayout.PropertyField(iterator); break;
                }
            }

            if (GUI.changed)
            {
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}