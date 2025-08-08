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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Custom editor for <see cref="ksOwnershipScriptManager"/>.</summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ksOwnershipScriptManager))]
    public class ksOwnershipScriptManagerEditor : UnityEditor.Editor
    {
        // Option labels for scripts that can be enabled/disabled.
        private static string[] m_scriptOptions;
        // Option labels for components that cannot be enabled/disabled.
        private static string[] m_componentOptions;
        // Option labels for rigid bodies.
        private static string[] m_rigidBodyOptions;
        private static ksOwnershipScriptManager.Rules[] m_scriptValues;
        private static ksOwnershipScriptManager.Rules[] m_componentValues;

        /// <summary>Draws the inspector GUI.</summary>
        public override void OnInspectorGUI()
        {
            ksOwnershipScriptManager script = target as ksOwnershipScriptManager;
            if (script == null)
            {
                return;
            }
            ksEntityComponent entityComponent = script.GetComponentInParent<ksEntityComponent>(true);
            if (entityComponent == null)
            {
                EditorGUILayout.HelpBox(typeof(ksOwnershipScriptManager).Name + " can only be used on an object with a " +
                    typeof(ksEntityComponent).Name + " or one of its descendant objects.", MessageType.Warning);
            }

            serializedObject.Update();

            // If the script is not on the same game object as the entity component script, draw the rule field for
            // the game object.
            SerializedProperty sprop;
            if (entityComponent == null || entityComponent.gameObject != script.gameObject)
            {
                sprop = serializedObject.FindProperty(nameof(ksOwnershipScriptManager.GameObjectRule));
                EditorGUILayout.PropertyField(sprop, new GUIContent("Game Object", sprop.tooltip));
            }

            if (m_scriptOptions == null)
            {
                CreateOptions();
            }

            // Change the default option label to show what the default option is.
            sprop = serializedObject.FindProperty(nameof(ksOwnershipScriptManager.DefaultRule));
            EditorGUILayout.PropertyField(sprop, new GUIContent("Default", sprop.tooltip));
            if (sprop.hasMultipleDifferentValues)
            {
                m_scriptOptions[0] = m_componentOptions[0] = m_rigidBodyOptions[0] = "Default";
            }
            else
            {
                ksOwnershipScriptManager.Rules rule = (ksOwnershipScriptManager.Rules)sprop.intValue;
                m_scriptOptions[0] = m_componentOptions[0] = "Default (" +
                    ksEnumDrawer.GetEnumDisplayName(rule.ToString()) + ")";
                switch (rule)
                {
                    case ksOwnershipScriptManager.Rules.DISABLE_WHEN_UNOWNED:
                        m_rigidBodyOptions[0] = "Default (Make Kinematic When Unowned)"; break;
                    case ksOwnershipScriptManager.Rules.DISABLE_WHEN_OWNED:
                        m_rigidBodyOptions[0] = "Default (Make Kinematic When Owned)"; break;
                    default:
                        m_rigidBodyOptions[0] = m_scriptOptions[0]; break;
                }
            }

            serializedObject.ApplyModifiedProperties();
            DrawComponentRules();
        }

        /// <summary>Creates the option labels.</summary>
        private static void CreateOptions()
        {
            // Script options are the same as component options without the disable options, and rigid body options are
            // the same as the script options but with disable changed to make kinematic.
            Array enumValues = Enum.GetValues(typeof(ksOwnershipScriptManager.Rules));
            m_scriptOptions = new string[enumValues.Length + 1];
            m_scriptValues = new ksOwnershipScriptManager.Rules[enumValues.Length];
            m_componentOptions = new string[enumValues.Length];
            m_componentValues = new ksOwnershipScriptManager.Rules[enumValues.Length - 1];
            m_rigidBodyOptions = new string[m_scriptOptions.Length];
            int ruleIndex = 0;
            int componentIndex = 0;
            m_scriptOptions[0] = m_componentOptions[0] = m_rigidBodyOptions[0] = "Default";
            foreach (object value in enumValues)
            {
                ksOwnershipScriptManager.Rules rule = (ksOwnershipScriptManager.Rules)value;
                m_scriptValues[ruleIndex++] = rule;
                string label = ksEnumDrawer.GetEnumDisplayName(Enum.GetName(typeof(ksOwnershipScriptManager.Rules), value));
                m_scriptOptions[ruleIndex] = label;
                switch (rule)
                {
                    case ksOwnershipScriptManager.Rules.DISABLE_WHEN_UNOWNED:
                        m_rigidBodyOptions[ruleIndex] = "Make Kinematic When Unowned"; break;
                    case ksOwnershipScriptManager.Rules.DISABLE_WHEN_OWNED:
                        m_rigidBodyOptions[ruleIndex] = "Make Kinematic When Owned"; break;
                    default:
                    {
                        m_componentValues[componentIndex++] = rule;
                        m_componentOptions[componentIndex] = label;
                        m_rigidBodyOptions[ruleIndex] = label;
                        break;
                    }
                }
            }
        }

        /// <summary>Draws rule properties for the components on the object.</summary>
        private void DrawComponentRules()
        {
            // Build the list of component arrays, with one array in the list for each target containing the components
            // that are present on all targets.
            List<Component>[] componentLists = BuildComponentLists();

            // Iterate the components of the first target and draw the rule properties.
            for (int i = 0; i < componentLists[0].Count; i++)
            {
                Component component = componentLists[0][i];
                string label = ksInspectorNames.Get().GetDefaultName(component.GetType());
                string tooltip = "What happens to the " + label + "?";
                string[] options;
                ksOwnershipScriptManager.Rules[] values;

                // If the component is a behaviour, collider, or renderer, it can be enabled/disabled and we use the
                // script options.
                if (component is Behaviour || component is Collider || component is Renderer)
                {
                    options = m_scriptOptions;
                    values = m_scriptValues;
                }
                // Use the rigid body options for rigid bodies
                else if (component is Rigidbody || component is Rigidbody2D)
                {
                    if (component is Rigidbody)
                    {
                        tooltip += " Rigidbody components on the root of the entity are always forced to be kinematic when " +
                            " the local player does not own the entity and have the transform permission.";
                    }
                    else
                    {
                        tooltip += " static Rigidbody2D components will ignore the make kinematic rule.";
                    }
                    options = m_rigidBodyOptions;
                    values = m_scriptValues;
                }
                // Use the component options for everything else.
                else
                {
                    options = m_componentOptions;
                    values = m_componentValues;
                }

                // Get the index of the selected option.
                int index = 0;
                if (HasMixedValues(componentLists, i))
                {
                    EditorGUI.showMixedValue = true;
                }
                else
                {
                    ksOwnershipScriptManager.Rules rule;
                    if (((ksOwnershipScriptManager)target).ComponentRules.TryGetValue(component, out rule))
                    {
                        index = Array.IndexOf(values, rule) + 1;
                    }
                }

                // Draw the property.
                EditorGUI.BeginChangeCheck();
                GUIContent content = new GUIContent(label, tooltip);
                index = EditorGUILayout.Popup(content, index, options) - 1;
                EditorGUI.showMixedValue = false;

                // If the selected option changed, change the value on all targets.
                if (EditorGUI.EndChangeCheck())
                {
                    for (int j = 0; j < componentLists.Length; j++)
                    {
                        if (componentLists[j] == null)
                        {
                            continue;
                        }
                        ksOwnershipScriptManager script = (ksOwnershipScriptManager)targets[j];
                        component = componentLists[j][i];
                        if (index < 0)
                        {
                            RemoveComponentRule(script, component);
                        }
                        else
                        {
                            SetComponentRule(script, component, (int)values[index]);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks if the component at the given index has different rule values on different targets.
        /// </summary>
        /// <param name="componentLists">List of shared component arrays for each target.</param>
        /// <param name="index">Index of the component to check.</param>
        /// <returns>
        /// True if the component at the given index has different rule values on different targets.
        /// </returns>
        private bool HasMixedValues(List<Component>[] componentLists, int index)
        {
            if (componentLists.Length > 1)
            {
                // Get the expected value from the first target.
                int expectedValue = -1;
                ksOwnershipScriptManager script = (ksOwnershipScriptManager)target;
                ksOwnershipScriptManager.Rules rule;
                if (script.ComponentRules.TryGetValue(componentLists[0][index], out rule))
                {
                    expectedValue = (int)rule;
                }

                // Check if any of the other targets have a different value.
                for (int i = 1; i < componentLists.Length; i++)
                {
                    if (componentLists[i] == null)
                    {
                        continue;
                    }
                    script = (ksOwnershipScriptManager)targets[i];
                    int value = -1;
                    if (script.ComponentRules.TryGetValue(componentLists[i][index], out rule))
                    {
                        value = (int)rule;
                    }
                    if (value != expectedValue)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Builds a list of component arrays, with one array in the list for each target containing the components
        /// that are present on all targets. Transforms and some Reactor components are excluded. Also removes
        /// destroyed components from the <see cref="ksOwnershipScriptManager.ComponentRules"/> of each target.
        /// </summary>
        /// <returns>List of shared component arrays.</returns>
        private List<Component>[] BuildComponentLists()
        {
            // Build the list of configurable components for the first target.
            ksOwnershipScriptManager script0 = (ksOwnershipScriptManager)target;
            RemoveDestroyedComponents(script0);
            List<Component> components0 = GetConfigurableComponents(script0.gameObject);
            List<Component>[] componentLists = new List<Component>[targets.Length];
            componentLists[0] = components0;

            // Iterate the remaining targets and build their configurable component lists.
            for (int i = 1; i < targets.Length; i++)
            {
                ksOwnershipScriptManager script = targets[i] as ksOwnershipScriptManager;
                if (script == null)
                {
                    continue;
                }
                RemoveDestroyedComponents(script);
                List<Component> components = GetConfigurableComponents(script.gameObject);
                componentLists[i] = new List<Component>();

                // Iterate the components from the first target's list.
                for (int j = 0; j < components0.Count; j++)
                {
                    // Look for a matching component on the current target.
                    Component match = FindAndRemove(components0[j].GetType(), components);
                    if (match == null)
                    {
                        // We didn't find a match. Remove this component from all lists.
                        for (int k = 0; k < i; k++)
                        {
                            if (componentLists[k] != null)
                            {
                                componentLists[k].RemoveAt(j);
                            }
                        }
                        if (components0.Count == 0)
                        {
                            return null;
                        }
                    }
                    else
                    {
                        // Add the matching component to the current target's list.
                        componentLists[i].Add(match);
                    }
                }
            }
            return componentLists;
        }

        /// <summary>
        /// Finds and removes the first component of the given type from a list of components. The type must be an exact
        /// match.
        /// </summary>
        /// <param name="type">Type of component to look for.</param>
        /// <param name="components">Component list to find and remove component from.</param>
        /// <returns>The first component of the given <paramref name="type"/>, or null if none was found.</returns>
        private Component FindAndRemove(Type type, List<Component> components)
        {
            for (int i = 0; i < components.Count; i++)
            {
                if (components[i].GetType() == type)
                {
                    Component component = components[i];
                    components.RemoveAt(i);
                    return component;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets a list of components from a game object that can have their rule configured.
        /// Transforms and some Reactor components are excluded.
        /// </summary>
        /// <param name="gameObj">Game object to get configurable components from.</param>
        /// <returns>List of configurable components</returns>
        private List<Component> GetConfigurableComponents(GameObject gameObj)
        {
            List<Component> components = new List<Component>();
            foreach (Component component in gameObj.GetComponents<Component>())
            {
                if (ksOwnershipScriptManager.IsConfigurableComponent(component))
                {
                    components.Add(component);
                }
            }
            return components;
        }

        /// <summary>Sets a component's rule using serialized properties.</summary>
        /// <param name="script">Ownership script manager</param>
        /// <param name="component">Component to set rule for</param>
        /// <param name="rule">
        /// Rule value to set. Negative values will remove the component from 
        /// <see cref="ksOwnershipScriptManager.ComponentRules"/>.
        /// </param>
        private void SetComponentRule(
            ksOwnershipScriptManager script,
            Component component,
            int rule)
        {
            SerializedObject so = new SerializedObject(script);
            SerializedProperty listProp = FindListProperty(so);
            for (int i = 0; i < listProp.arraySize; i++)
            {
                SerializedProperty elemProp = listProp.GetArrayElementAtIndex(i);
                if (elemProp.FindPropertyRelative("Key").objectReferenceValue == component)
                {
                    if (rule < 0)
                    {
                        listProp.DeleteArrayElementAtIndex(i);
                    }
                    else
                    {
                        elemProp.FindPropertyRelative("Value").intValue = rule;
                    }
                    so.ApplyModifiedProperties();
                    return;
                }
            }
            if (rule >= 0)
            {
                listProp.InsertArrayElementAtIndex(listProp.arraySize);
                SerializedProperty elemProp = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
                elemProp.FindPropertyRelative("Key").objectReferenceValue = component;
                elemProp.FindPropertyRelative("Value").intValue = rule;
                so.ApplyModifiedProperties();
            }
        }

        /// <summary>Removes a component's rule using serialized properties.</summary>
        /// <param name="script">Ownership script manager</param>
        /// <param name="component">Component to remove rule for</param>
        private void RemoveComponentRule(ksOwnershipScriptManager script, Component component)
        {
            SetComponentRule(script, component, -1);
        }

        /// <summary>
        /// Finds the <see cref="ksOwnershipScriptManager.ComponentRules"/> list property.
        /// </summary>
        /// <param name="so">Serialized object to get property from</param>
        /// <returns>Serialized list property</returns>
        private SerializedProperty FindListProperty(SerializedObject so)
        {
            return so.FindProperty("m_componentRules").FindPropertyRelative("m_list");
        }

        /// <summary>
        /// Checks if a <see cref="ksOwnershipScriptManager"/> has any rules for destroyed components.
        /// </summary>
        /// <param name="script">Ownership script manager to check for destroyed components.</param>
        /// <returns>True if the script has destroyed components.</returns>
        private bool HasDestroyedComponent(ksOwnershipScriptManager script)
        {
            foreach (Component component in script.ComponentRules.Keys)
            {
                if (component == null)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Removes destroyed components from a <see cref="ksOwnershipScriptManager"/> using serialized properties.
        /// </summary>
        /// <param name="script">Ownership script manager to remove destroyed components from.</param>
        private void RemoveDestroyedComponents(ksOwnershipScriptManager script)
        {
            if (!HasDestroyedComponent(script))
            {
                return;
            }
            SerializedObject so = new SerializedObject(script);
            SerializedProperty listProp = FindListProperty(so);
            for (int i = listProp.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty elemProp = listProp.GetArrayElementAtIndex(i);
                if (elemProp.FindPropertyRelative("Key").objectReferenceValue == null)
                {
                    listProp.DeleteArrayElementAtIndex(i);
                }
            }
            so.ApplyModifiedProperties();
        }
    }
}
