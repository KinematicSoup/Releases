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
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Property drawer for <see cref="ksScriptAssetReference{T}"/>.</summary>
    [CustomPropertyDrawer(typeof(ksScriptAssetReference<>))]
    public class ksScriptAssetReferenceDrawer : PropertyDrawer
    {
        private static Assembly m_assembly;
        private static bool m_loadedAssembly = false;

        // Maps script type to proxy types for built-in types.
        private static Dictionary<Type, Type> m_typeMap = new Dictionary<Type, Type>();

        /// <summary>Static initialization.</summary>
        static ksScriptAssetReferenceDrawer()
        {
            m_typeMap[typeof(ksCollisionFilter)] = typeof(ksCollisionFilterAsset);
        }

        /// <summary>Draws the property.</summary>
        /// <param name="position">Position to draw at.</param>
        /// <param name="property">Property to draw.</param>
        /// <param name="label">Label for the property.</param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!m_loadedAssembly)
            {
                try
                {
                    m_assembly = Assembly.Load("Assembly-CSharp");
                }
                catch (Exception e)
                {
                    ksLog.Error(this, "Unable to load Assembly-CSharp", e);
                }
                m_loadedAssembly = true;
            }

            EditorGUI.BeginProperty(position, label, property);
            Type type = fieldInfo.FieldType.GenericTypeArguments[0];
            Type proxyType;
            if (!m_typeMap.TryGetValue(type, out proxyType))
            {
                proxyType = type == typeof(ksScriptAsset) || m_assembly == null ? null :
                    m_assembly.GetType("KSProxies.Scripts." + type.FullName);
            }
            if (proxyType == null)
            {
                proxyType = typeof(ksProxyScriptAsset);
            }
            SerializedProperty subProperty = property.FindPropertyRelative("m_proxy");
            EditorGUI.BeginChangeCheck();
            UnityEngine.Object obj = EditorGUI.ObjectField(position, label, subProperty.objectReferenceValue,
                proxyType, false);
            if (EditorGUI.EndChangeCheck())
            {
                subProperty.objectReferenceValue = obj;
            }
            EditorGUI.EndProperty();
        }
    }
}