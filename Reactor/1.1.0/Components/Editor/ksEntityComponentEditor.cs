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
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using KS.Unity.Editor;

#if UNITY_2019
using UnityEditor.Experimental.SceneManagement;
#else
using UnityEditor.SceneManagement;
#endif

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Inspector editor for <see cref="ksEntityComponent"/>.</summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ksEntityComponent))]
    public class ksEntityComponentEditor : UnityEditor.Editor
    {
        // Properties with these names will be skipped when rendering the inspector
        private readonly HashSet<string> m_skipProperties = new HashSet<string>() { 
            "m_Script", "m_editorData", "Type"
        };

        // Properties with these names will be only shown if the object is static
        private readonly HashSet<string> m_showWhenStatic = new HashSet<string>() {
            "m_isPermanent"
        };

        // Properties with these names are hidden if the object is a permanent scene entity.
        private readonly HashSet<string> m_hideWhenPermanent = new HashSet<string>()
        {
            "Predictor", "ControllerPredictor", "OverridePredictor", "OverrideControllerPredictor", "DestroyWithServer",
            "DestroyOnOwnerDisconnect", "SyncGroup", "TransformPrecisionOverrides"
        };

        // Properties with these names will be rendered in a debug group
        private readonly HashSet<string> m_debugProperties = new HashSet<string>() { 
            "AssetId", "EntityId", "CenterOfMass"
        };

        // This is appended to the tooltip for precision overrides.
        private static readonly string OVERRIDE_TOOLTIP = " (unchecked = use default room setting)";

        private static bool m_showDebug = false;
        private static bool m_showPrecisionOverrides = false;

        /// <summary>Creates the inspector GUI.</summary>
        public override void OnInspectorGUI()
        {
            CheckEntityPrefabResourcePaths();

            serializedObject.Update();
            bool hasRoomType = false;
            bool isStatic = true;
            bool isPermanent = true;
            foreach (ksEntityComponent entity in targets)
            {
                hasRoomType = hasRoomType || entity.GetComponent<ksRoomType>() != null;
                entity.IsStatic = entity.GetComponent<Rigidbody>() == null;
                isStatic = isStatic && entity.IsStatic;
                isPermanent = isPermanent && entity.IsPermanent && !PrefabUtility.IsPartOfPrefabAsset(entity) &&
                    PrefabStageUtility.GetPrefabStage(entity.gameObject) == null;
                CheckColliderData(entity);
            }
            isPermanent = isPermanent && isStatic;

            if (targets.Length == 1)
            {
                EditorGUILayout.LabelField("Type", GetEntityType());
            }

            DrawProperties(isStatic, isPermanent);

            if (hasRoomType)
            {
                EditorGUILayout.HelpBox("ksRoomType detected on this object. " +
                    "KSEntities are ignored for objects with KSRoomTypes.", MessageType.Error, true);
            }
        }

        /// <summary>Get the entity type.</summary>
        /// <returns>Entity type</returns>
        private string GetEntityType()
        {
            ksEntityComponent entity = targets[0] as ksEntityComponent;
            if (EditorApplication.isPlaying)
            {
                return entity.Type;
            }
            GameObject prefab = null;
            if (PrefabUtility.IsPartOfNonAssetPrefabInstance(entity))
            {
                prefab = PrefabUtility.GetCorrespondingObjectFromSource(entity.gameObject) as GameObject;
            }
            else if (PrefabUtility.IsPartOfPrefabAsset(entity))
            {
                prefab = entity.gameObject;
            }
            string assetPath;
            if (prefab == null)
            {
                PrefabStage prefabStage = PrefabStageUtility.GetPrefabStage(entity.gameObject);
                if (prefabStage == null)
                {
                    return "";
                }
#if UNITY_2020_1_OR_NEWER
                assetPath = prefabStage.assetPath;
#else
                assetPath = prefabStage.prefabAssetPath;
#endif
            }
            else
            {
                assetPath = AssetDatabase.GetAssetPath(prefab.GetInstanceID()).Replace('\\', '/');
            }
            string assetBundle = GetAssetBundle(assetPath);
            return ksPaths.GetEntityType(assetPath, assetBundle);
        }

        /// <summary>
        /// Gets the name of the asset bundle the asset at a path belongs to.
        /// </summary>
        /// <param name="assetPath">asset path to get asset bundle name for.</param>
        /// <returns>Name of the asset bundle, or null if the asset does not belong to an asset bundle.</returns>
        private string GetAssetBundle(string assetPath)
        {
            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            if (!string.IsNullOrEmpty(importer.assetBundleName))
            {
                if (!string.IsNullOrEmpty(importer.assetBundleVariant))
                {
                    return importer.assetBundleName + "." + importer.assetBundleVariant;
                }
                return importer.assetBundleName;
            }
            int index = assetPath.LastIndexOf('/');
            if (index < 0)
            {
                return null;
            }
            assetPath = assetPath.Substring(0, index);
            return assetPath == "Assets" ? null : GetAssetBundle(assetPath);
        }

        /// <summary>Draw editor widgets for the entity component properties.</summary>
        /// <param name="isStatic">Are the selected entities static?</param>
        /// <param name="isPermanent">Are the selected entities permanent?</param>
        private void DrawProperties(bool isStatic, bool isPermanent)
        {
            List<SerializedProperty> debugGroup = new List<SerializedProperty>();

            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (m_skipProperties.Contains(iterator.name))
                {
                    continue;
                }

                if (m_showWhenStatic.Contains(iterator.name) && !isStatic)
                {
                    continue;
                }

                if (m_hideWhenPermanent.Contains(iterator.name) && isPermanent)
                {
                    continue;
                }

                if (m_debugProperties.Contains(iterator.name))
                {
                    debugGroup.Add(iterator.Copy());
                    continue;
                }

                switch (iterator.name)
                {
                    case "ColliderData": break;
                    case "PhysicsOverrides": break;
                    case "OverridePredictor":
                    {
                        if (DrawToggleableProperty(iterator))
                        {
                            ksPredictor predictor = iterator.objectReferenceValue as ksPredictor;
                            if (predictor != null && predictor.RequiresController)
                            {
                                EditorGUILayout.HelpBox(predictor.GetType().Name + " can only be used with controllers.",
                                    MessageType.Warning);
                            }
                        }
                        break;
                    }
                    case "OverrideControllerPredictor":
                    {
                        DrawToggleableProperty(iterator);
                        break;
                    }
                    case "TransformPrecisionOverrides":
                    {
                        m_showPrecisionOverrides = EditorGUILayout.Foldout(m_showPrecisionOverrides,
                            new GUIContent(iterator.displayName, iterator.tooltip));
                        if (m_showPrecisionOverrides)
                        {
                            EditorGUI.indentLevel++;
                            iterator.NextVisible(true);
                            ksStyle.NumericOverrideField(iterator, ksCompressionConstants.DEFAULT_POSITION_PRECISION, iterator.tooltip + 
                                OVERRIDE_TOOLTIP);
                            iterator.NextVisible(false);
                            ksStyle.NumericOverrideField(iterator, ksCompressionConstants.DEFAULT_ROTATION_PRECISION, iterator.tooltip +
                                OVERRIDE_TOOLTIP);
                            iterator.NextVisible(false);
                            ksStyle.NumericOverrideField(iterator, ksCompressionConstants.DEFAULT_SCALE_PRECISION, iterator.tooltip +
                                OVERRIDE_TOOLTIP);
                            EditorGUI.indentLevel--;
                        }
                        break;
                    }
                    default: EditorGUILayout.PropertyField(iterator); break;
                }
            }

            DrawPropertyGroup("Debugging", debugGroup, ref m_showDebug);

            foreach (ksEntityComponent entity in targets)
            {
                entity.TransformPrecisionOverrides.Validate();
                entity.PhysicsOverrides.Validate();
                entity.SyncGroup = Math.Min(entity.SyncGroup, ksFixedDataWriter.ENCODE_4BYTE);
            }

            if (GUI.changed)
            {
                serializedObject.ApplyModifiedProperties();

                foreach (UnityEngine.Object target in targets)
                {
                    ksEntityComponent entity = target as ksEntityComponent;
                    if (entity == null || entity.Entity == null)
                    {
                        continue;
                    }
                }
            }
        }

        /// <summary>
        /// Draws the next two properties in one line, with the second property's label. The first property must be a
        /// bool. If it is unchecked, the second property is not drawn.
        /// </summary>
        /// <param name="property">Bool property to draw. Will point to the next property after this is called.</param>
        /// <returns>True if the bool property is checked.</returns>
        private bool DrawToggleableProperty(SerializedProperty property)
        {
            EditorGUILayout.BeginHorizontal();
            SerializedProperty toggle = property.Copy();
            property.NextVisible(false);
            toggle.boolValue = EditorGUILayout.Toggle(
                new GUIContent(property.displayName, property.tooltip), toggle.boolValue);
            if (toggle.boolValue)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(""));
            }
            EditorGUILayout.EndHorizontal();
            return toggle.boolValue;
        }

        /// <summary>Draw a list of serialized properties contained by a foldout.</summary>
        /// <param name="groupName">Foldout group name.</param>
        /// <param name="properties">List of serialized properties.</param>
        /// <param name="expanded">Expanded state of the group.</param>
        private void DrawPropertyGroup(string groupName, List<SerializedProperty> properties, ref bool expanded)
        {
            expanded = EditorGUILayout.Foldout(expanded, groupName);
            if (expanded)
            {
                EditorGUI.indentLevel = 1;
                foreach (SerializedProperty property in properties)
                {
                    EditorGUILayout.PropertyField(property);
                }
                EditorGUI.indentLevel = 0;
            }
        }

        /// <summary>
        /// Get the path of a prefab that owns an an entity which is not an instance.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public string GetPrefabPath(ksEntityComponent entity)
        {
            // If the object is in a prefab stage, then use the stage to get the path.
            PrefabStage prefabStage = PrefabStageUtility.GetPrefabStage(entity.gameObject);
            if (prefabStage != null)
            {
#if UNITY_2020_1_OR_NEWER
                return prefabStage.assetPath;
#else
                return prefabStage.prefabAssetPath;
#endif
            }

            // If the object is persitent (a prefab which is not an instance) then return the path.
            if (PrefabUtility.IsPartOfPrefabAsset(entity))
            {
                return AssetDatabase.GetAssetPath(entity);
            }
            return null;
        }

        /// <summary>
        /// Write warnings if the entity is a prefab and is not in a Resources folder or asset bundle.
        /// </summary>
        private void CheckEntityPrefabResourcePaths()
        {
            List<string> misplacedPrefabs = new List<string>();
            foreach (UnityEngine.Object target in targets)
            {
                ksEntityComponent entity = target as ksEntityComponent;
                if (PrefabUtility.IsPartOfPrefabAsset(entity) && !ksEditorUtils.IsInResourcesOrAssetBundle(entity))
                {
                    misplacedPrefabs.Add(ksEditorUtils.GetAssetPath(entity));
                }
            }
            if (misplacedPrefabs.Count > 0)
            {
                EditorGUILayout.HelpBox("Entity prefabs cannot be instantiated by the server unless " +
                    " they are located under a Resources folder or in an asset bundle.\n- " +
                    string.Join("\n- ", misplacedPrefabs),
                    MessageType.Warning
                );
            }
        }

        /// <summary>
        /// Adds <see cref="ksColliderData"/> components for new colliders and removes <see cref="ksColliderData"/>
        /// components for missing colliders.
        /// </summary>
        /// <param name="entity"><see cref="ksEntityComponent"/> to check the colliders on.</param>
        public static void CheckColliderData(ksEntityComponent entity)
        {
            bool modified = false;
            List<ksIUnityCollider> colliders = entity.GetColliders();
            foreach (ksColliderData colliderData in entity.GetComponents<ksColliderData>())
            {
                if (colliderData.Collider == null)
                {
                    DestroyImmediate(colliderData, true);
                    modified = true;
                    continue;
                }
                for (int i = 0; i < colliders.Count; i++)
                {
                    if (colliders[i].Component == colliderData.Collider)
                    {
                        colliders.RemoveAt(i);
                        break;
                    }
                }
            }

            foreach (ksIUnityCollider collider in colliders)
            {
                ksColliderData colliderData = entity.gameObject.AddComponent<ksColliderData>();
                colliderData.Collider = collider.Component;
                modified = true;
            }

            if (modified)
            {
                EditorUtility.SetDirty(entity.gameObject);
            }
        }

        /// <summary>
        /// Get the index for <see cref="ksColliderData"/> associated with a collider component
        /// from a serialized property list of <see cref="ksColliderData"/>.
        /// </summary>
        /// <param name="collider">Collider component</param>
        /// <param name="spColliderDataList"></param>
        /// <returns>
        ///     Index of the <see cref="ksColliderData"/> in the <paramref name="spColliderDataList"/>.
        ///     Returns -1 if nothing was found.
        /// </returns>
        public static int GetColliderDataIndex(Component collider, SerializedProperty spColliderDataList)
        {
            if (collider == null || spColliderDataList == null)
            {
                return -1;
            }
            for (int i = 0; i < spColliderDataList.arraySize; ++i)
            {
                SerializedProperty sp = spColliderDataList.GetArrayElementAtIndex(i);
                if (sp.FindPropertyRelative("Collider").objectReferenceValue == collider)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}