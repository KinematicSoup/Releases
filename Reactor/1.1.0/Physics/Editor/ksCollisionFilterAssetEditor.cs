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

using UnityEngine;
using UnityEditor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Inspector editor for <see cref="ksCollisionFilterAsset"/>.</summary>
    [CustomEditor(typeof(ksCollisionFilterAsset))]
    [CanEditMultipleObjects]
    public class ksCollisionFilterAssetEditor : UnityEditor.Editor
    {
        private const float LABEL_MIN_WIDTH = 70f;
        private const float LABEL_MAX_WIDTH = 200f;
        private const float TOGGLE_WIDTH = 50f;
        private const float TOGGLE_INDENT = 17f;

        /// <summary>Creates the GUI.</summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty groupProperty = serializedObject.FindProperty("m_group");
            SerializedProperty notifyProperty = serializedObject.FindProperty("m_notify");
            SerializedProperty collideProperty = serializedObject.FindProperty("m_collide");

            uint group, notify, collide;
            group = (uint)groupProperty.longValue;
            notify = (uint)notifyProperty.longValue;
            collide = (uint)collideProperty.longValue;

            // Prepare styles and layout options
            GUILayoutOption labelMinWidth = GUILayout.Width(LABEL_MIN_WIDTH);
            GUILayoutOption labelMaxWidth = GUILayout.MaxWidth(LABEL_MAX_WIDTH);
            GUILayoutOption toggleWidth = GUILayout.Width(TOGGLE_WIDTH);
            GUIStyle centeredLabel = new GUIStyle(GUI.skin.GetStyle("Label"));
            centeredLabel.alignment = TextAnchor.UpperCenter;

            // Draw headers
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("", labelMinWidth, labelMaxWidth);
            GUILayout.Label(new GUIContent("Group", 
                "Groups that this filter belongs to."), centeredLabel, toggleWidth);
            GUILayout.Label(new GUIContent("Notify", 
                "Groups that this filter reports collision and overlap events with."), centeredLabel, toggleWidth);
            GUILayout.Label(new GUIContent("Collide", "Groups that this filter will simulate collisions with. " +
                "Other collision filter must also include one of the groups that this filter belongs to in its " +
                "\"Collide\" rule for the collision to occur."), centeredLabel, toggleWidth);
            GUILayout.Space(TOGGLE_INDENT);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Draw labels and toggles
            Rect highlight;
            string label;
            string newLabel;
            for (int i = 0; i < ksReactorConfig.Instance.CollisionGroups.Length; i++)
            {
                label = ksReactorConfig.Instance.CollisionGroups[i];
                uint mask = 1u << i;

                EditorGUILayout.BeginHorizontal();
                newLabel = GUILayout.TextField(label, labelMinWidth, labelMaxWidth);
                GUILayout.Space(TOGGLE_INDENT);
                CreateToggle(ref group, mask, toggleWidth);
                CreateToggle(ref notify, mask, toggleWidth);
                CreateToggle(ref collide, mask, toggleWidth);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                // Save new label
                if (newLabel != label)
                {
                    ksReactorConfig.Instance.CollisionGroups[i] = newLabel;
                    EditorUtility.SetDirty(ksReactorConfig.Instance);
                }

                // Highlight every other row
                highlight = GUILayoutUtility.GetLastRect();
                if (i%2 == 1){
                    EditorGUI.DrawRect(highlight, new Color(0.0f, 0.0f, 0.0f, 0.1f));
                }
            }

            // Draw toggle "All" buttons
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("", labelMinWidth, labelMaxWidth);
            CreateButton(ref group, 0xffffffff, "All", toggleWidth);
            CreateButton(ref notify, 0xffffffff, "All", toggleWidth);
            CreateButton(ref collide, 0xffffffff, "All", toggleWidth);
            GUILayout.Space(TOGGLE_INDENT);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Draw toggle "None" buttons
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("", labelMinWidth, labelMaxWidth);
            CreateButton(ref group, 0, "None", toggleWidth);
            CreateButton(ref notify, 0, "None", toggleWidth);
            CreateButton(ref collide, 0, "None", toggleWidth);
            GUILayout.Space(TOGGLE_INDENT);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Save changes
            if (GUI.changed)
            {
                groupProperty.longValue = group;
                notifyProperty.longValue = notify;
                collideProperty.longValue = collide;
                serializedObject.ApplyModifiedProperties();
            }
        }


        /// <summary>Creates a toggle for a bit in a uint.</summary>
        /// <param name="value">Value the toggle is for.</param>
        /// <param name="mask">Determines which bit is toggled.</param>
        /// <param name="options">Layout options appled to the toggle.</param>
        private void CreateToggle(ref uint value, uint mask, params GUILayoutOption[] options)
        {
            if (GUILayout.Toggle((mask & value) != 0, GUIContent.none, options))
            {
                value |= mask;
            }
            else
            {
                value &= ~mask;
            }
        }

        /// <summary>Creates a button that sets a value when clicked.</summary>
        /// <param name="value">Value to update when the button is clicked.</param>
        /// <param name="newValue">New value to assign when but button is clicked.</param>
        /// <param name="label">Label for the button.</param>
        /// <param name="options">Layout options appled to the button.</param>
        private void CreateButton(ref uint value, uint newValue, string label, params GUILayoutOption[] options)
        {
            if (GUILayout.Button(label, options))
            {
                value = newValue;
            }
        }
    }
}
