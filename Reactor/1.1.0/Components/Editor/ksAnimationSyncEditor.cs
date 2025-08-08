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
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Custom editor for <see cref="ksAnimationSync"/>.</summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ksAnimationSync))]
    public class ksAnimationSyncEditor : ksEntityScriptEditor
    {
        private static GUIContent m_animationPropertiesLabel;
        private static bool m_showProperties = false;

        /// <summary>Draws the inspector GUI.</summary>
        public override void OnInspectorGUI()
        {
            ksAnimationSync sync = target as ksAnimationSync;
            if (sync == null)
            {
                return;
            }

            // Check for entity component and animator.
            Validate();
            if (GetAnimator(sync) == null)
            {
                EditorGUILayout.HelpBox("No Animator component found. You must add an Animator to this game object " +
                    "or assign one from a different game object to the Animator field.", MessageType.Warning);
            }

            serializedObject.Update();
            bool syncLayerStates = false;
            int propertyCount = -1;
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                switch (iterator.name)
                {
                    case "m_Script": continue;
                    case "m_syncLayerStates":
                    {
                        EditorGUILayout.PropertyField(iterator);
                        syncLayerStates = iterator.boolValue || iterator.hasMultipleDifferentValues;
                        break;
                    }
                    case "m_crossFadeDuration":
                    {
                        if (syncLayerStates)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(iterator);
                            EditorGUI.indentLevel--;
                        }
                        break;
                    }
                    case "m_animationPropertiesStart":
                    {
                        if (m_animationPropertiesLabel == null)
                        {
                            m_animationPropertiesLabel = new GUIContent("Animation Property Ids",
                                "The range of ids used to sync animation properties. Make sure you do not use " +
                                "property ids in this range for other properties or your properties will be " +
                                "overwritten. It is recommended to leave some unused properties after the end of " +
                                "this range so if you add more animation properties, they won't collide with your " +
                                "other property ids.");
                        }

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PropertyField(iterator, m_animationPropertiesLabel);
                        propertyCount = iterator.hasMultipleDifferentValues ? -1 : GetPropertyCount();
                        if (propertyCount >= 0)
                        {
                            EditorGUILayout.LabelField("to " + (iterator.longValue + propertyCount - 1));
                        }
                        else
                        {
                            EditorGUILayout.LabelField("to —");
                        }
                        EditorGUILayout.EndHorizontal();
                        break;
                    }
                    default:
                    {
                        EditorGUILayout.PropertyField(iterator);
                        break;
                    }
                }
            }

            if (GUI.changed)
            {
                serializedObject.ApplyModifiedProperties();
            }

            // Property count is -1 if there is no animator or there are multiple animators with different numbers of
            // properties or different property start values.
            if (propertyCount >= 0)
            {
                m_showProperties = EditorGUILayout.Foldout(m_showProperties, "Animation Properties", true);
                if (m_showProperties)
                {
                    EditorGUI.indentLevel++;
                    DrawAnimationProperties();
                    EditorGUI.indentLevel--;
                }
            }
        }

        /// <summary>
        /// Gets the animator for a <see cref="ksAnimationSync"/>. If the <paramref name="sync"/> has an 
        /// <see cref="ksAnimationSync.Animator"/> returns it. If not, looks for an animator on the same game object as
        /// the <paramref name="sync"/>.
        /// </summary>
        /// <param name="sync">Script to get animator.</param>
        /// <returns>Animator</returns>
        private Animator GetAnimator(ksAnimationSync sync)
        {
            if (sync == null)
            {
                return null;
            }
            if (sync.Animator != null)
            {
                return sync.Animator;
            }
            return sync.GetComponent<Animator>();
        }

        /// <summary>
        /// Gets the number of properties the animator will use to sync. If there are multiple animators using different
        /// numbers of properties or if there are no animators, returns negative one.
        /// </summary>
        /// <returns>
        /// The number of properties the animator will use to sync, or negative one if there are multiple animators
        /// using different numbers of properties or if there are no animators.
        /// </returns>
        private int GetPropertyCount()
        {
            int count = -1;
            for (int i = 0; i < targets.Length; i++)
            {
                ksAnimationSync sync = targets[i] as ksAnimationSync;
                Animator animator = GetAnimator(sync);
                if (animator == null)
                {
                    continue;
                }
                int num = GetPropertyCount(animator, sync.SyncLayerStates);
                if (count < 0)
                {
                    count = num;
                }
                else if (count != num)
                {
                    return -1;
                }
            }
            return count;
        }

        /// <summary>Gets the number of properties an animator will use to sync.</summary>
        /// <param name="animator">Animator to get property count from.</param>
        /// <param name="includeLayerStates">If true, includes a property for each layer.</param>
        /// <returns>The number of properties the animator will use to sync.</returns>
        private int GetPropertyCount(Animator animator, bool includeLayerStates)
        {
            // If the animator is not part of a prefab, we can get the parameter and layer count directly from the
            // animator.
            int count;
            if (!PrefabUtility.IsPartOfPrefabAsset(animator))
            {
                 count = animator.parameterCount;
                if (includeLayerStates)
                {
                    count += animator.layerCount;
                }
                return count;
            }
            // Parameter and layer count do not work when the animator is part of a prefab, so we have to get the counts
            // from the animator controller, which we get from the animator's serialized properties.
            AnimatorController controller = GetController(animator);
            if (controller == null)
            {
                return 0;
            }
            count = controller.parameters.Length;
            if (includeLayerStates)
            {
                count += controller.layers.Length;
            }
            return count;
        }

        /// <summary>
        /// Iterates strings of the animator parameter types and names in the format "[Type] [Name]". Optionally
        /// iterates the layer state names as well.
        /// </summary>
        /// <param name="animator">Animator to iterate parameter type names for.</param>
        /// <param name="includeLayerStates">True to also iterate layer state names.</param>
        /// <returns>Parameter type name iterator</returns>
        private IEnumerable<string> IteratePropertyTypeNames(Animator animator, bool includeLayerStates)
        {
            // If the animator is not part of a prefab, we can get the parameters and layers directly from the
            // animator.
            if (!PrefabUtility.IsPartOfPrefabAsset(animator))
            {
                for (int i = 0; i < animator.parameterCount; i++)
                {
                    AnimatorControllerParameter parameter = animator.GetParameter(i);
                    yield return parameter.type + " " + parameter.name;
                }

                if (includeLayerStates)
                {
                    for (int i = 0; i < animator.layerCount; i++)
                    {
                        yield return "LayerState " + animator.GetLayerName(i);
                    }
                }
            }
            else
            {
                // We have to get the parameters and layers from the animator controller, which we get using serialized
                // properties.
                AnimatorController controller = GetController(animator);
                for (int i = 0; i < controller.parameters.Length; i++)
                {
                    AnimatorControllerParameter parameter = controller.parameters[i];
                    yield return parameter.type + " " + parameter.name;
                }

                if (includeLayerStates)
                {
                    for (int i = 0; i < controller.layers.Length; i++)
                    {
                        yield return "LayerState " + controller.layers[i].name;
                    }
                }
            }
        }

        /// <summary>Gets the animator controller from an animator using serialized properties.</summary>
        /// <param name="animator">Animator to get controller from.</param>
        /// <returns>Animator controller</returns>
        private AnimatorController GetController(Animator animator)
        {
            SerializedObject so = new SerializedObject(animator);
            SerializedProperty sprop = so.FindProperty("m_Controller");
            return sprop.objectReferenceValue as AnimatorController;
        }

        /// <summary>Draws the synced animation property ids, types, and names.</summary>
        private void DrawAnimationProperties()
        {
            Animator animator = null;
            ksAnimationSync sync = null;
            for (int i = 0; i < targets.Length; i++)
            {
                sync = targets[i] as ksAnimationSync;
                animator = GetAnimator(sync);
                if (animator != null)
                {
                    break;
                }
            }
            if (animator == null)
            {
                return;
            }
            uint propertyId = sync.AnimationPropertiesStart;
            foreach (string propertyTypeName in IteratePropertyTypeNames(animator, sync.SyncLayerStates))
            {
                EditorGUILayout.LabelField(propertyId.ToString(), propertyTypeName);
                propertyId++;
            }
        }
    }
}
