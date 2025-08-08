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
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Custom inspector for <see cref="ksConvergingInputPredictorAsset"/>.</summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ksConvergingInputPredictorAsset))]
    public class ksConvergingInputPredictorEditor : UnityEditor.Editor
    {
        /// <summary>
        /// Called by Unity to draw/update the inspector UI. Adds a toggle to the 
        /// <see cref="ksConvergingInputPredictorAsset.VelocityTolerance"/> field for setting it to
        /// <see cref="ksConvergingInputPredictor.ConfigData.AUTO"/>.
        /// </summary>
        public override void OnInspectorGUI()
        {
            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.name == "m_Script")
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.PropertyField(property);
                    EditorGUI.EndDisabledGroup();
                    continue;
                }
                if (property.name == "SweepAndSlideIterations")
                {
                    EditorGUILayout.PropertyField(property);
                    if (property.intValue <= 0)
                    {
                        // If the value is 0 or less, collision prediction is disabled. Skip the remaining collision
                        // properties.
                        property.NextVisible(false);
                    }
                    continue;
                }
                if (property.name == "TunnelDistance")
                {
                    GUIContent label = new GUIContent("Auto Tunnel Distance",
                        "Use the maximum collider size along the X and Z axes as the tunnel distance.");
                    if (EditorGUILayout.Toggle(label,
                        property.floatValue < 0f))
                    {
                        property.floatValue = -1f;
                        continue;
                    }
                    if (property.floatValue < 0f)
                    {
                        property.floatValue = 1f;
                    }
                }
                else if (property.name == "VelocityTolerance")
                {
                    GUIContent label = new GUIContent("Auto Velocity Tolerance", 
                        "Use the maximum collider size along the X and Z axes as the velocity tolerance.");
                    if (EditorGUILayout.Toggle(label,
                        property.floatValue < 0f))
                    {
                        property.floatValue = -1f;
                        continue;
                    }
                    if (property.floatValue < 0f)
                    {
                        property.floatValue = 0f;
                    }
                }
                EditorGUILayout.PropertyField(property);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
