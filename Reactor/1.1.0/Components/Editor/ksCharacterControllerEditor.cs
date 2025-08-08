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
using UnityEditor;
using UnityEngine;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>
    /// A class that used to override Unity character controller editor. This class appends extra collider data
    /// used by Reactor to the Unity character controller in the inspector.
    /// </summary>
    [CustomEditor(typeof(CharacterController))]
    [CanEditMultipleObjects]
    public class ksCharacterControllerEditor : ksOverrideEditor
    {
        private static GUIContent m_foldoutLabel;

        // Don't show these fields.
        private static readonly HashSet<string> m_hideFields = new HashSet<string>
        {
            "ShapeId", "IsSimulation", "IsQuery", "ContactOffset"
        };

        /// <summary>Static initialization</summary>
        static ksCharacterControllerEditor()
        {
            ksInspectorNames.Get().Add((CharacterController controller) => ksColliderEditor.GetName(controller));
        }

        protected override void OnEnable() { LoadBaseEditor("CharacterControllerEditor"); }

        /// <summary>
        /// Draw the base inspector without changes and append inspector GUI elements for the <see cref="ksColliderData"/>
        /// associated with the target collider.
        /// </summary>
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            ksColliderEditor.DrawColliderData(targets, m_hideFields);
        }
    }
}
