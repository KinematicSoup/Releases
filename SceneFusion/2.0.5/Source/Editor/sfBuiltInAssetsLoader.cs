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
using System;
using UnityEditor;
using UnityEngine;
using KS.SF.Reactor;
using KS.SF.Unity.Editor;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>Helper class to load all assets of a given type.</summary>
    public class sfBuiltInAssetsLoader
    {
        /// <summary></summary>
        /// <returns>singleton instance.</returns>
        public static sfBuiltInAssetsLoader Get()
        {
            return m_instance;
        }
        private static sfBuiltInAssetsLoader m_instance = new sfBuiltInAssetsLoader();
        private ksReflectionObject m_findTypeByNameMethod;
        private ksReflectionObject m_persistentTypeIdProperty;
        private ksReflectionObject m_getBuiltinResourceListMethod;
        private ksReflectionObject m_idField;

        /// <summary>Constructor.</summary>
        private sfBuiltInAssetsLoader()
        {
            ksReflectionObject unityType = new ksReflectionObject("UnityEditor", "UnityEditor.UnityType");
            m_findTypeByNameMethod = unityType.GetMethod("FindTypeByName");
            m_persistentTypeIdProperty = unityType.GetProperty("persistentTypeID");
            ksReflectionObject builtinResourceType = new ksReflectionObject(typeof(EditorWindow).Assembly,
                "UnityEditor.BuiltinResource");
            m_getBuiltinResourceListMethod = new ksReflectionObject(
                typeof(EditorGUIUtility)).GetMethod("GetBuiltinResourceList");
#if UNITY_6000_3_OR_NEWER
            m_idField = builtinResourceType.GetField("m_EntityId");
#else
            m_idField = builtinResourceType.GetField("m_InstanceID");
#endif
        }

        /// <summary>Returns built-in assets of type T.</summary>
        /// <returns></returns>
        public T[] LoadBuiltInAssets<T>() where T : UnityEngine.Object
        {
            Type type = typeof(T);
            int classId = StringToClassId(type.Name);
            if (classId < 0)
            {
                ksLog.Warning(this, "Class '" + type.Name + "' not found");
                return new T[0];
            }
            else
            {
                object[] builtinResourceList = (object[])m_getBuiltinResourceListMethod.Invoke(new object[] { classId });
                if (builtinResourceList == null)
                {
                    return new T[0];
                }
                T[] assets = new T[builtinResourceList.Length];
                for (int i = 0; i < assets.Length; i++)
                {
#if UNITY_6000_3_OR_NEWER
                    EntityId id = (EntityId)m_idField.GetValue(builtinResourceList[i]);
#else
                    int id = (int)m_idField.GetValue(builtinResourceList[i]);
#endif
                    assets[i] = sfUnityUtils.GetUObject<T>(id);
                }
                return assets;
            }
        }


        /// <summary>Returns class id for given class name.</summary>
        /// <param name="className"></param>
        /// <returns></returns>
        public int StringToClassId(string className)
        {
            object typeByName = m_findTypeByNameMethod.Invoke(className);
            if (typeByName == null)
            {
                return 0;
            }
            return (int)m_persistentTypeIdProperty.GetValue(typeByName);
        }
    }
}
