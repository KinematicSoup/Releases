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
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using KS.Unity.Editor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>
    /// Override editor for the Unity RigidbodyEditor. Editor appends GUIs for extra physics data used in Reactor
    /// <see cref="ksRigidBody"/> scripts from the <see cref="ksEntityComponent"/>.
    /// </summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(Rigidbody))]
    public class ksRigidBodyEditor : ksOverrideEditor
    {
        protected static GUIContent m_foldoutLabel;
        private static bool m_foldout = false;
        private static ksReflectionObject m_reflectionObj;
        private static List<ksEntityComponent> m_entityComponents = new List<ksEntityComponent>();

        /// <summary>Static initialization</summary>
        static ksRigidBodyEditor()
        {
            ksInspectorNames.Get().Add<Rigidbody>(GetName);
        }

        /// <summary>
        /// Gets the name of the rigid body to display in the inspector. Appends " (Reactor Client Only)" or
        /// " (Reactor Server Only)" to the name if the rigid body is client-only or server-only respectively.
        /// </summary>
        /// <param name="rigidBody">Rigidbody to get name for.</param>
        /// <returns>
        /// Name of the <paramref name="rigidBody"/> to display in the inspector. Null to use the default name.
        /// </returns>
        private static string GetName(Rigidbody rigidBody)
        {
            ksEntityComponent entityComponent = rigidBody.GetComponent<ksEntityComponent>();
            if (entityComponent == null)
            {
                return null;
            }
            switch (entityComponent.RigidBodyExistence)
            {
                case ksExistenceModes.CLIENT_ONLY:
                {
                    return ksInspectorNames.Get().GetDefaultName<Rigidbody>() + " (Reactor Client Only)";
                }
                case ksExistenceModes.SERVER_ONLY:
                {
                    return ksInspectorNames.Get().GetDefaultName<Rigidbody>() + " (Reactor Server Only)";
                }
            }
            return null;
        }

        /// <summary>
        /// Load the Unity RigidbodyEditor as the base editor when this editor is enabled.
        /// </summary>
        protected override void OnEnable() { LoadBaseEditor("RigidbodyEditor"); }

        /// <summary>
        /// Draw the base inspector and append the GUI to include <see cref="ksPhysicsSettings"/>
        /// properties used by <see cref="ksRigidBody"/> scripts.
        /// </summary>
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            DrawRigidBodyData(targets);
        }

        /// <summary>
        /// Draw <see cref="ksPhysicsSettings"/> property GUIs used by <see cref="ksRigidBody"/> scripts when a 
        /// <see cref="ksEntityComponent"/> is also attached to the game object.
        /// </summary>
        /// <param name="targets">Rigidbodies to draw rigid body data for.</param>
        private static void DrawRigidBodyData(UnityEngine.Object[] targets)
        {
            if (m_foldoutLabel == null)
            {
                m_foldoutLabel = new GUIContent(" Reactor RigidBody Data", ksTextures.Logo);
            }

            if (m_reflectionObj == null)
            {
                m_reflectionObj = new ksReflectionObject(new ksEntityPhysicsSettings());
            }

            m_entityComponents.Clear();
            foreach (Component component in targets)
            {
                ksEntityComponent entityComponent = component.GetComponent<ksEntityComponent>();
                if (entityComponent != null && entityComponent.enabled)
                {
                    m_entityComponents.Add(entityComponent);
                }
            }
            if (m_entityComponents.Count == 0)
            {
                return;
            }

            SerializedObject soEntity = new SerializedObject(m_entityComponents.ToArray());
            SerializedProperty spRigidBodyData = soEntity.FindProperty("PhysicsOverrides");
            SerializedProperty spExistance = soEntity.FindProperty("RigidBodyExistence");
            if (spRigidBodyData != null)
            {
                bool enterChildren = true;
                int depth = -1;
                m_foldout = EditorGUILayout.BeginFoldoutHeaderGroup(m_foldout, m_foldoutLabel);
                if (m_foldout)
                {
                    EditorGUI.indentLevel++;
                    if (spExistance != null)
                    {
                        EditorGUILayout.PropertyField(spExistance, new GUIContent("Existence", spExistance.tooltip));
                    }
                    while (spRigidBodyData.NextVisible(enterChildren))
                    {
                        if (depth == -1)
                        {
                            enterChildren = false;
                            depth = spRigidBodyData.depth;
                        }
                        else if (depth != spRigidBodyData.depth)
                        {
                            break;
                        }
                        if (spRigidBodyData.name != "ContactOffset")
                        {
                            // Get the tooltip using reflection because of a Unity bug that causes the tooltip to
                            // be null on the SerializedProperty.
                            ksReflectionObject field = m_reflectionObj.GetField(spRigidBodyData.name);
                            TooltipAttribute tooltipAttribute = field.FieldInfo == null ?
                                null : field.FieldInfo.GetCustomAttribute<TooltipAttribute>();
                            string tooltip = tooltipAttribute == null ? null : tooltipAttribute.tooltip;
                            ksDisplayNameAttribute nameAttribute = field.FieldInfo == null ?
                                null : field.FieldInfo.GetCustomAttribute<ksDisplayNameAttribute>();
                            string name = nameAttribute == null ? null : nameAttribute.DisplayName;
                            ksStyle.NumericOverrideField(spRigidBodyData, field.GetValue(), tooltip, name);
                        }
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }
            soEntity.ApplyModifiedProperties();
        }
    }
}