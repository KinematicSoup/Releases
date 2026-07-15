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
        /// <summary>Width of animation parameter type column.</summary>
        private const float TYPE_WIDTH = 45f;
        /// <summary>Max width of animation parameter precision column.</summary>
        private const float MAX_PRECISION_WIDTH = 120f;
        /// <summary>
        /// Width of animation parameter precision toggle. Precision colum width will not be less than this.
        /// </summary>
        private const float TOGGLE_WIDTH = 20f;
        /// <summary>
        /// This portion of the remaining width  up to <see cref="MAX_PRECISION_WIDTH"/> is given to the precision
        /// column, and the rest goes to the name colum.
        /// </summary>
        private const float PRECISION_WIDTH_PORTION = .6f;
        /// <summary>Minimum width of animation parameter name column.</summary>
        private const float MIN_NAME_WIDTH = 40f;
        /// <summary>Padding added to the measured width of the highest id to the get the id column width.</summary>
        private const float ID_WIDTH_PAD = 4f;

        private static GUIContent m_animationPropertiesLabel;
        private static GUIContent m_precisionOverrideLabel;
        private static GUIContent m_precisionValueLabel;
        private static bool m_showProperties = false;

        /// <summary>
        /// Animator property info struct with name and type data. Can refer to an animation parameter or a layer
        /// state.
        /// </summary>
        private struct AnimatorPropertyInfo
        {
            /// <summary>Property type string.</summary>
            public string Type;

            /// <summary>Property name.</summary>
            public string Name;

            /// <summary>Property name hash. Zero for layer states because they do not use name hash lookups.</summary>
            public int Hash;

            /// <summary>Constructor that constructs from an <see cref="AnimatorControllerParameter"/>.</summary>
            /// <param name="param">Animation parameter</param>
            public AnimatorPropertyInfo(AnimatorControllerParameter param)
            {
                Type = param.type.ToString();
                Name = param.name;
                Hash = param.nameHash;
            }

            /// <summary>Constructor that sets <see cref="Hash"/> to zero.</summary>
            /// <param name="type">Type string</param>
            /// <param name="name">Proprety name</param>
            public AnimatorPropertyInfo(string type, string name)
            {
                Type = type;
                Name = name;
                Hash = 0;
            }
        }

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
            long lastPropertyId = -1;
            Animator animator = null;
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                switch (iterator.name)
                {
                    case "m_precisionOverrides":
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
                        animator = GetAnimator();
                        int count = iterator.hasMultipleDifferentValues ? 
                            0 : GetPropertyCount(animator, sync.SyncLayerStates);
                        if (count > 0)
                        {
                            lastPropertyId = iterator.longValue + count - 1;
                            EditorGUILayout.LabelField("to " + lastPropertyId);
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

            // Animator is null if there is no animator or multiple targets are selected with different animators.
            if (animator != null)
            {
                m_showProperties = EditorGUILayout.Foldout(m_showProperties, "Animation Properties", true);
                if (m_showProperties)
                {
                    DrawAnimationProperties(sync, animator, lastPropertyId);
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
        /// Gets an animator with a controller used by all targets, or null if there are multiple targets using
        /// different animator controllers.
        /// </summary>
        /// <returns>Animator, or null if there are multiple targets using different animator controllers.</returns>
        private Animator GetAnimator()
        {
            Animator animator = null;
            AnimatorController controller = null;
            for (int i = 0; i < targets.Length; i++)
            {
                ksAnimationSync sync = targets[i] as ksAnimationSync;
                animator = GetAnimator(sync);
                if (animator == null)
                {
                    return null;
                }
                AnimatorController con = GetController(animator);
                if (con == null || (controller != null && con != controller))
                {
                    return null;
                }
                controller = con;
            }
            return animator;
        }

        /// <summary>Gets the number of properties an animator will use to sync.</summary>
        /// <param name="animator">Animator to get property count from.</param>
        /// <param name="includeLayerStates">If true, includes a property for each layer.</param>
        /// <returns>The number of properties the animator will use to sync.</returns>
        private int GetPropertyCount(Animator animator, bool includeLayerStates)
        {
            if (animator == null)
            {
                return 0;
            }
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
        /// Iterates animation property info from animator parameters and optionally layer states.
        /// </summary>
        /// <param name="animator">Animator to iterate parameters for.</param>
        /// <param name="includeLayerStates">True to also iterate layer states.</param>
        /// <returns>Animator property info iterator</returns>
        private IEnumerable<AnimatorPropertyInfo> IterateProperties(Animator animator, bool includeLayerStates)
        {
            // Unity's API for getting properyties/layers directly from the API is bugged and misses layers and is also
            // not supported for prefab assets, so we get the parameters and layers from the animator controller, which
            // we get using serialized properties.
            AnimatorController controller = GetController(animator);
            for (int i = 0; i < controller.parameters.Length; i++)
            {
                yield return new AnimatorPropertyInfo(controller.parameters[i]);
            }

            if (includeLayerStates)
            {
                for (int i = 0; i < controller.layers.Length; i++)
                {
                    yield return new AnimatorPropertyInfo("Layer", controller.layers[i].name);
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
        /// <param name="sync">Animation sync script</param>
        /// <param name="animator">Animator to draw properties for.</param>
        /// <param name="lastPropertyId">
        /// The highest property id, or -1 if there are multiple targets with different
        /// <see cref="ksAnimationSync.AnimationPropertiesStart"/> values.
        /// </param>
        private void DrawAnimationProperties(ksAnimationSync sync, Animator animator, long lastPropertyId)
        {
            // Measure the width of the highest id, or "—" if there are multiple targets with different property start
            // ids, then add the ID_WIDTH_PAD to get the id column width.
            string measureStr = lastPropertyId < 0 ? "—" : lastPropertyId.ToString();
            float idWidth = GUI.skin.label.CalcSize(new GUIContent(measureStr)).x + ID_WIDTH_PAD;

            //-36 comes from Unity padding.
            float remainingWidth = EditorGUIUtility.currentViewWidth - idWidth - TYPE_WIDTH - 36f;
            float precisionWidth = Mathf.Clamp(remainingWidth * PRECISION_WIDTH_PORTION, TOGGLE_WIDTH,
                MAX_PRECISION_WIDTH);
            float nameWidth = Mathf.Max(MIN_NAME_WIDTH, remainingWidth  - precisionWidth);

            // Create the column headers
            EditorGUILayout.BeginHorizontal();
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontStyle = FontStyle.Bold;
            GUILayout.Label("Id", style, GUILayout.Width(idWidth));
            GUILayout.Label("Type", style, GUILayout.Width(TYPE_WIDTH));
            GUILayout.Label("Name", style, GUILayout.Width(nameWidth));
            GUILayout.Label("Precision Override", style);
            EditorGUILayout.EndHorizontal();

            // Draw the properties
            uint propertyId = sync.AnimationPropertiesStart;
            foreach (AnimatorPropertyInfo animProp in IterateProperties(animator, sync.SyncLayerStates))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(lastPropertyId < 0 ? "—" : propertyId.ToString(), GUILayout.Width(idWidth));
                GUILayout.Label(animProp.Type, GUILayout.Width(TYPE_WIDTH));
                if (animProp.Type == "Float")
                {
                    GUILayout.Label(animProp.Name, GUILayout.Width(nameWidth));
                    DrawPrecisionOverride(animProp.Hash);
                }
                else
                {
                    GUILayout.Label(animProp.Name);
                }
                EditorGUILayout.EndHorizontal();

                propertyId++;
            }
        }

        /// <summary>
        /// Draws a precision override toggle for a float animation parameter and a float field if the toggle is
        /// checked.
        /// </summary>
        /// <param name="animProp">Name hash of property to draw precision override for.</param>
        private void DrawPrecisionOverride(int nameHash)
        {
            // Draw the toggle.
            bool mixedOverride;// true if some targets are overriding the value and some aren't.
            float precision = GetPrecisionOverride(nameHash, out mixedOverride);
            EditorGUI.showMixedValue = mixedOverride;
            EditorGUI.BeginChangeCheck();
            if (m_precisionValueLabel == null)
            {
                m_precisionOverrideLabel = new GUIContent("",
                    "Override the precision value for this property. Unchecked = use default 'Precision' value.");
                m_precisionValueLabel = new GUIContent("",
                    "The accuracy of the quantized value when it is synced over the network. " +
                    "0 = full float precision.");
            }
            bool isOverriding = EditorGUILayout.Toggle(precision >= 0, GUILayout.Width(TOGGLE_WIDTH));
            // Adding an empty label with a tooltip to the toggle doesn't work, but doing it this way does.
            Rect rect = GUILayoutUtility.GetLastRect();
            GUI.Label(rect, m_precisionOverrideLabel);
            if (EditorGUI.EndChangeCheck())
            {
                // Change the toggle value.
                mixedOverride = false;
                EditorGUI.showMixedValue = false;
                if (isOverriding)
                {
                    // Set to the default precision when the toggle becomes checked.
                    SetPrecisionOverride(nameHash, ksAnimationSync.DEFAULT_PRECISION);
                }
                else
                {
                    RemovePrecisionOverride(nameHash);
                }
            }
            // Draw the float field if the toggle is checked or has mixed values.
            if (isOverriding || mixedOverride)
            {
                EditorGUI.BeginChangeCheck();
                precision = EditorGUILayout.FloatField(precision);
                if (EditorGUI.EndChangeCheck())
                {
                    SetPrecisionOverride(nameHash, Mathf.Max(0f, precision));
                }
                // Adding an empty label with a tooltip to the float field doesn't work, but doing it this way does.
                rect = GUILayoutUtility.GetLastRect();
                GUI.Label(rect, m_precisionValueLabel);
            }
            EditorGUI.showMixedValue = false;
        }

        /// <summary>Sets a precision override on all targets using serialized properties.</summary>
        /// <param name="nameHash">Parameter name hash to set precision override for.</param>
        /// <param name="value">Precision override value. If negative, removes the override.</param>
        private void SetPrecisionOverride(int nameHash, float value)
        {
            if (targets.Length == 1)
            {
                SetPrecisionOverride(serializedObject, nameHash, value);
            }
            else
            {
                for (int i = 0; i < targets.Length; i++)
                {
                    SetPrecisionOverride(new SerializedObject(targets[i]), nameHash, value);
                }
            }
        }

        /// <summary>Sets a precision override on a serialized object.</summary>
        /// <param name="serializedObj">Serialized object.</param>
        /// <param name="nameHash">Parameter name hash to set precision override for.</param>
        /// <param name="value">Precision override value. If negative, removes the override.</param>
        private void SetPrecisionOverride(SerializedObject serializedObj, int nameHash, float value)
        {
            SerializedProperty listProp = serializedObj.FindProperty("m_precisionOverrides")
                    .FindPropertyRelative("m_list");
            for (int i = 0; i < listProp.arraySize; i++)
            {
                SerializedProperty elemProp = listProp.GetArrayElementAtIndex(i);
                if (elemProp.FindPropertyRelative("Key").intValue == nameHash)
                {
                    if (value < 0)
                    {
                        listProp.DeleteArrayElementAtIndex(i);
                    }
                    else
                    {
                        elemProp.FindPropertyRelative("Value").floatValue = value;
                    }
                    serializedObj.ApplyModifiedProperties();
                    return;
                }
            }
            if (value >= 0)
            {
                listProp.InsertArrayElementAtIndex(listProp.arraySize);
                SerializedProperty elemProp = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
                elemProp.FindPropertyRelative("Key").intValue = nameHash;
                elemProp.FindPropertyRelative("Value").floatValue = value;
                serializedObj.ApplyModifiedProperties();
            }
        }

        /// <summary>Removes a precision override from all targets using serialized properties.</summary>
        /// <param name="nameHash">Parameter name hash to remove override for.</param>
        private void RemovePrecisionOverride(int nameHash)
        {
            SetPrecisionOverride(nameHash, -1f);
        }

        /// <summary>Gets a precision override value.</summary>
        /// <param name="nameHash">Parameter name hash to get precision override for.</param>
        /// <param name="mixedOverride">
        /// Set to true if there are multiple targets where some have precision overrides and some don't.
        /// </param>
        /// <returns>Precision override, or -1 if there are multiple targets with different values.</returns>
        private float GetPrecisionOverride(int nameHash, out bool mixedOverride)
        {
            float value = -1f;
            bool allOverride = true;
            mixedOverride = false;
            for (int i = 0; i < targets.Length; i++)
            {
                ksAnimationSync sync = (ksAnimationSync)targets[i];
                float precision;
                bool isOverride = sync.TryGetPrecisionOverride(nameHash, out precision);
                if (i == 0)
                {
                    allOverride = isOverride;
                    value = precision;
                }
                else if (value != precision)
                {
                    if (!isOverride || !allOverride)
                    {
                        mixedOverride = true;
                        return -1f;
                    }
                    value = -1f;
                }
            }
            return value;
        }
    }
}
