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
using System;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Inspector editor for <see cref="ksCylinderCollider"/>.</summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ksCylinderCollider))]
    public class ksCylinderColliderEditor : UnityEditor.Editor
    {
        /// <summary>Static initialization</summary>
        static ksCylinderColliderEditor()
        {
            ksInspectorNames.Get().Add((ksCylinderCollider collider) => ksColliderEditor.GetName(collider));
        }

        /// <summary>
        /// Unity OnEnable. Initializes scale to match the game object's mesh for uninitialized colliders.
        /// </summary>
        public void OnEnable()
        {
            foreach (ksCylinderCollider cylinder in targets)
            {
                if (!cylinder.Initialized)
                {
                    InitializeScale(cylinder);
                    cylinder.Initialized = true;
                }
            }
        }

        /// <summary>Creates the inspector GUI.</summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                switch (iterator.name)
                {
                    case "m_Script":
                    case "m_initialized":
                        enterChildren = false;
                        continue;
                    case "m_radius":
                    case "m_height":
                        EditorGUILayout.PropertyField(iterator);
                        if (!iterator.hasMultipleDifferentValues)
                        {
                            iterator.floatValue = Math.Abs(iterator.floatValue);
                        }
                        break;
                    case "m_numSides":
                        EditorGUILayout.PropertyField(iterator);
                        if (!iterator.hasMultipleDifferentValues)
                        {
                            iterator.intValue = Math.Max(3, iterator.intValue);
                        }
                        break;
                    default:
                        EditorGUILayout.PropertyField(iterator);
                        break;
                }
                enterChildren = iterator.hasVisibleChildren && iterator.isExpanded;
            }
            ksColliderEditor.DrawColliderData(targets);
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>Draws a collider wireframe in the scene view.</summary>
        public void OnSceneGUI()
        {
            ksCylinderCollider cylinder = (ksCylinderCollider)target;
            Transform transform = cylinder.transform;
            Handles.color = new Color(0.5f, 1.0f, 0.5f, 1f);
            Vector3[] vertices = ksCylinderMeshFactory.GenerateVertices(
                cylinder.Radius, cylinder.Height, cylinder.Direction, cylinder.NumSides
            );
            for (int i = 0; i < cylinder.NumSides; i++)
            {
                int p = i * 2;
                Vector3 p0 = transform.TransformPoint(vertices[p] + cylinder.Center);
                Vector3 p1 = transform.TransformPoint(vertices[p + 1] + cylinder.Center);
                Vector3 p2 = transform.TransformPoint(vertices[(p + 2) % vertices.Length] + cylinder.Center);
                Vector3 p3 = transform.TransformPoint(vertices[(p + 3) % vertices.Length] + cylinder.Center);
                Handles.DrawLine(p0, p1);
                Handles.DrawLine(p0, p2);
                Handles.DrawLine(p1, p3);
            }
        }

        /// <summary>
        /// Initializes cylinder scaling to match the game object's mesh if it has one.
        /// </summary>
        /// <param name="cylinder">Cylinder to scale.</param>
        private void InitializeScale(ksCylinderCollider cylinder)
        {
            MeshFilter meshFilter = cylinder.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                Vector3 meshBounds = meshFilter.sharedMesh.bounds.size;
                cylinder.Height = (float)Math.Round(meshBounds.y, 3);
                cylinder.Radius = (float)Math.Round(Math.Max(meshBounds.x, meshBounds.z) * 0.5f, 3);
            }
        }
    }
}