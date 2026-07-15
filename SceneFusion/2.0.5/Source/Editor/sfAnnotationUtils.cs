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
using System.Collections.Generic;
using UnityEngine;
using KS.SF.Unity.Editor;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>Utils class to check if a game object has a component with an icon in the scene view.</summary>
    public class sfAnnotationUtils
    {
        /// <summary></summary>
        /// <returns>singleton instance.</returns>
        public static sfAnnotationUtils Get()
        {
            return m_instance;
        }
        private static sfAnnotationUtils m_instance = new sfAnnotationUtils();

        private ksReflectionObject m_getAnnotationsMethod = null;
        private ksReflectionObject m_classIdField = null;
        private ksReflectionObject m_scriptClassField = null;
        private ksReflectionObject m_iconEnabledField = null;
        private ksReflectionObject m_annotationWindowLastClosedTimeField;

        private long m_annotationWindowLastClosedTime = 0;
        private Array m_annotations = null;
        private Dictionary<int, object> m_builtinAnnotations = new Dictionary<int, object>();
        private Dictionary<string, object> m_scriptAnnotations = new Dictionary<string, object>();

        /// <summary>Constructor</summary>
        public sfAnnotationUtils()
        {
            m_getAnnotationsMethod = new ksReflectionObject(
                "UnityEditor", "UnityEditor.AnnotationUtility").GetMethod("GetAnnotations");
            ksReflectionObject annotationType = new ksReflectionObject("UnityEditor", "UnityEditor.Annotation");
            m_classIdField = annotationType.GetField("classID");
            m_scriptClassField = annotationType.GetField("scriptClass");
            m_iconEnabledField = annotationType.GetField("iconEnabled");
            m_annotationWindowLastClosedTimeField = new ksReflectionObject(
                "UnityEditor", "UnityEditor.AnnotationWindow").GetField("s_LastClosedTime");
        }

        /// <summary>Checks if the game object has a component with an icon in the scene view.</summary>
        /// <param name="gameObject"></param>
        /// <returns>true if the game object has a component with an icon in the scene view.</returns>
        public bool HasComponentIcon(GameObject gameObject)
        {
            foreach (Component component in gameObject.GetComponents<Component>())
            {
                if (component != null && IsIconEnabled(component.GetType()))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Checks if the given type's icon is enabled in Unity.</summary>
        /// <param name="type">type to check.</param>
        /// <returns>true if the given type's icon is enabled.</returns>
        private bool IsIconEnabled(Type type)
        {
            int classId = sfBuiltInAssetsLoader.Get().StringToClassId(type.Name);
            object annotation = FindAnnotation(classId, type.Name);
            if (annotation == null)
            {
                return false;
            }
            return (int)m_iconEnabledField.GetValue(annotation) == 1;
        }

        /// <summary>Gets the annotation information for the given class.</summary>
        /// <param name="classId"></param>
        /// <param name="className"></param>
        /// <returns></returns>
        private object FindAnnotation(int classId, string className)
        {
            UpdateAnnotationCache();
            object annotation = null;
            if (classId == 0)
            {
                m_scriptAnnotations.TryGetValue(className, out annotation);
            }
            else
            {
                m_builtinAnnotations.TryGetValue(classId, out annotation);
            }
            return annotation;
        }

        /// <summary>
        /// Fetches the annotations if the cache is null or the annotation window was opened and closed since last time.
        /// Returns true if the annotations cache is updated.
        /// </summary>
        /// <returns></returns>
        public bool UpdateAnnotationCache()
        {
            long lastCloseTime = (long)m_annotationWindowLastClosedTimeField.GetValue();
            if (m_annotations == null || lastCloseTime != m_annotationWindowLastClosedTime)
            {
                m_annotationWindowLastClosedTime = lastCloseTime;
                m_annotations = (Array)m_getAnnotationsMethod.Invoke();
                foreach (object annotation in m_annotations)
                {
                    string scriptClass = (string)m_scriptClassField.GetValue(annotation);
                    if (scriptClass != "")
                    {
                        m_scriptAnnotations[scriptClass] = annotation;
                    }
                    else
                    {
                        m_builtinAnnotations[(int)m_classIdField.GetValue(annotation)] = annotation;
                    }
                }
                return true;
            }
            return false;
        }
    }
}
