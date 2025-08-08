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
using System.Reflection;
using System.Collections.Generic;
using KS.Unity.Editor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>
    /// Base class used to override Unity collider editors. This class appends extra collider data
    /// used by Reactor to the Unity collider in the inspector.
    /// </summary>
    public class ksColliderEditor: ksOverrideEditor
    {
        protected static GUIContent m_foldoutLabel;
        private static bool m_foldout = false;
        private static string m_contactOffsetTooltip;
        private static List<ksColliderData> m_colliderDatas = new List<ksColliderData>();

        /// <summary>Static initialization</summary>
        static ksColliderEditor()
        {
            ksInspectorNames.Get().Add((Collider collider) => GetName(collider));
        }

        /// <summary>
        /// Gets the name of a collider component to display in the inspector. Client and server-only colliders will
        /// have " (Reactor Client Only)" and " (Reactor Server Only)" appended to the name respectively.
        /// </summary>
        /// <param name="component">
        /// Collider component to get name for. Should be a <see cref="Collider"/>, <see cref="ksCylinderCollider"/>,
        /// or <see cref="ksConeCollider"/>.
        /// </param>
        /// <returns>
        /// The name of the <paramref name="component"/> to display in the inspector. Null to use the default name.
        /// </returns>
        public static string GetName(Component component)
        {
            ksEntityComponent entityComponent = component.gameObject.GetComponent<ksEntityComponent>();
            if (entityComponent == null)
            {
                return null;
            }
            ksColliderData data = entityComponent.GetColliderData(component);
            if (data == null)
            {
                return null;
            }
            switch (data.Existence)
            {
                case ksExistenceModes.CLIENT_ONLY:
                {
                    return ksInspectorNames.Get().GetDefaultName(component.GetType()) + " (Reactor Client Only)";
                }
                case ksExistenceModes.SERVER_ONLY:
                {
                    return ksInspectorNames.Get().GetDefaultName(component.GetType()) + " (Reactor Server Only)";
                }
            }
            return null;
        }

        /// <summary>
        /// Draw the base inspector without changes and append inspector GUI elements for the <see cref="ksColliderData"/>
        /// associated with the target collider.
        /// </summary>
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            DrawColliderData(targets);
        }

        /// <summary>
        /// Draw a <see cref="ksColliderData"/> property when the viewed game object has a
        /// <see cref="ksEntityComponent"/> attached.
        /// </summary>
        /// <param name="targets">Colliders to draw collider data for.</param>
        /// <param name="hideFields">Optional set of fields to hide.</param>
        public static void DrawColliderData(UnityEngine.Object[] targets, HashSet<string> hideFields = null)
        {
            if (m_foldoutLabel == null)
            {
                m_foldoutLabel = new GUIContent(" Reactor Collider Data", ksTextures.Logo);
            }

            m_colliderDatas.Clear();
            foreach (Component collider in targets)
            {
                ksColliderData colliderData = FindColliderData(collider);
                if (colliderData != null)
                {
                    m_colliderDatas.Add(colliderData);
                }
            }
            if (m_colliderDatas.Count == 0)
            {
                return;
            }
            
            SerializedObject serializedObjects = new SerializedObject(m_colliderDatas.ToArray());
            SerializedProperty spColliderData = serializedObjects.GetIterator();
            if (spColliderData != null)
            {
                bool enterChildren = true;
                int depth = -1;
                m_foldout = EditorGUILayout.BeginFoldoutHeaderGroup(m_foldout, m_foldoutLabel);
                if (m_foldout)
                {
                    EditorGUI.indentLevel++;
                    while (spColliderData.NextVisible(enterChildren))
                    {
                        if (depth == -1)
                        {
                            enterChildren = false;
                            depth = spColliderData.depth;
                        }
                        else if (depth != spColliderData.depth)
                        {
                            break;
                        }
                        if (spColliderData.name == "m_Script" ||
                            (hideFields != null && hideFields.Contains(spColliderData.name)))
                        {
                            continue;
                        }
                        if (spColliderData.name == "ContactOffset")
                        {
                            if (m_contactOffsetTooltip == null)
                            {
                                // Get the tooltip using reflection because of a Unity bug that causes the tooltip to
                                // be null on the SerializedProperty.
                                FieldInfo field = new ksReflectionObject(typeof(ksColliderData))
                                    .GetField("ContactOffset").FieldInfo;
                                TooltipAttribute tooltipAttribute = field == null ?
                                    null : field.GetCustomAttribute<TooltipAttribute>();
                                m_contactOffsetTooltip = tooltipAttribute == null ? "" : tooltipAttribute.tooltip;
                            }
                            ksStyle.NumericOverrideField(spColliderData, .01f, m_contactOffsetTooltip);
                        }
                        else
                        {
                            if (spColliderData.name == "IsSimulation")
                            {
                                // Hide the IsSimulation flag if all targets are triggers.
                                bool allTriggers = true;
                                foreach (Component collider in targets)
                                {
                                    if (!IsTrigger(collider))
                                    {
                                        allTriggers = false;
                                        break;
                                    }
                                }
                                if (allTriggers)
                                {
                                    continue;
                                }
                            }
                            EditorGUILayout.PropertyField(spColliderData);
                        }
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
                serializedObjects.ApplyModifiedProperties();
            }
        }

        /// <summary>
        /// Finds the collider data for a collider component. Destroys collider data if the game object has no entity
        /// component.
        /// </summary>
        /// <param name="collider">Collider component to get collider data for.</param>
        /// <returns>Collider data, or null if none was found or the entity component was disabled.</returns>
        private static ksColliderData FindColliderData(Component collider)
        {
            ksEntityComponent entityComponent = collider.GetComponent<ksEntityComponent>();
            if (entityComponent == null)
            {
                foreach (ksColliderData colliderData in collider.GetComponents<ksColliderData>())
                {
                    DestroyImmediate(colliderData, true);
                }
                return null;
            }
            if (!entityComponent.enabled)
            {
                return null;
            }
            return entityComponent.GetColliderData(collider);
        }

        /// <summary>
        /// Checks if a component is a <see cref="Collider"/> or a <see cref="ksICollider"/> trigger.
        /// </summary>
        /// <param name="component">Component to check.</param>
        /// <returns>True if the component is a trigger.</returns>
        private static bool IsTrigger(Component component)
        {
            if (component == null)
            {
                return false;
            }

            Collider collider = component as Collider;
            if (collider != null)
            {
                return collider.isTrigger;
            }

            ksICollider icollider = component as ksICollider;
            if (icollider != null)
            {
                return icollider.IsTrigger;
            }

            return false;
        }
    }

    [CustomEditor(typeof(SphereCollider))]
    [CanEditMultipleObjects]
    public class ksSphereColliderEditor : ksColliderEditor
    {
        protected override void OnEnable() { LoadBaseEditor("SphereColliderEditor"); }
    }

    [CustomEditor(typeof(BoxCollider))]
    [CanEditMultipleObjects]
    public class ksBoxColliderEditor : ksColliderEditor
    {
        protected override void OnEnable() { LoadBaseEditor("BoxColliderEditor"); }
    }

    [CustomEditor(typeof(CapsuleCollider))]
    [CanEditMultipleObjects]
    public class ksCapsuleColliderEditor : ksColliderEditor
    {
        protected override void OnEnable() { LoadBaseEditor("CapsuleColliderEditor"); }
    }

    [CustomEditor(typeof(MeshCollider))]
    [CanEditMultipleObjects]
    public class ksMeshColliderEditor : ksColliderEditor
    {
        protected override void OnEnable() { LoadBaseEditor("MeshColliderEditor"); }
    }

    [CustomEditor(typeof(TerrainCollider))]
    [CanEditMultipleObjects]
    public class ksTerrainColliderEditor : ksColliderEditor
    {
        protected override void OnEnable() { LoadBaseEditor("TerrainColliderEditor"); }
    }
}