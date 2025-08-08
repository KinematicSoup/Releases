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
using KS.Unity.Editor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Custom editor for <see cref="ksAutoSpawn"/>.</summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ksAutoSpawn))]
    public class ksAutoSpawnEditor : UnityEditor.Editor
    {
        /// <summary>Draws the inspector GUI.</summary>
        public override void OnInspectorGUI()
        {
            string warning = GetWarningMessage();
            if (warning != null)
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }

            serializedObject.Update();

            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            bool spawnOwned = false;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                switch (iterator.name)
                {
                    case "m_Script": break;
                    case nameof(ksAutoSpawn.SpawnOwned):
                    {
                        EditorGUILayout.PropertyField(iterator);
                        spawnOwned = iterator.boolValue;
                        break;
                    }
                    case nameof(ksAutoSpawn.OwnerPermissions):
                    {
                        if (spawnOwned)
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

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>Gets a warning message if the script is on an object it cannot be used on.</summary>
        /// <returns>Warning message, or null if the script can be used on the object it is on.</returns>
        private string GetWarningMessage()
        {
            ksAutoSpawn script = target as ksAutoSpawn;
            if (script == null)
            {
                return "Target is not a " + nameof(ksAutoSpawn) + ".";
            }
            ksEntityComponent entity = script.GetComponent<ksEntityComponent>();
            if (entity == null || (!ksEditorUtils.IsInResourcesOrAssetBundle(entity) && 
                !ksEditorUtils.IsResourceOrAssetBundlePrefabInstance(entity)))
            {
                return nameof(ksAutoSpawn) + 
                    " can only be used on prefabs with a " + nameof(ksEntityComponent) + 
                    " in Resources or asset bundles or their prefab instances.";
            }
            if (entity.IsPermanent && entity.GetComponent<Rigidbody>() == null && 
                !PrefabUtility.IsPartOfPrefabAsset(entity))
            {
                return nameof(ksAutoSpawn) + " cannot be used on permanent scene entities.";
            }
            return null;
        }
    }
}
