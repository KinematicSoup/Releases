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
using KS.SF.Unity.Editor;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>
    /// This class checks if the user is working in Unity by detecting if mouse is moving inside of focused window.
    /// </summary>
    class sfIdleChecker
    {
        private long m_lastEditingTime = 0;
        private Event m_currentEvent;
        private Vector2 m_lastMousePosition;
        private ksReflectionObject m_reflectionObject;

        /* Is IdleChecker checking for idling? */
        public bool Checking
        {
            get
            {
                return m_checking;
            }
        }
        private bool m_checking = false;

        /// <summary>Start checking.</summary>
        public void Start()
        {
            if (m_checking) return;
            if (m_currentEvent == null)
            {
                m_reflectionObject = new ksReflectionObject(typeof(Event)).GetField("s_Current");
                if (m_reflectionObject == ksReflectionObject.Void)
                {
                    return;
                }
                else
                {
                    m_currentEvent = m_reflectionObject.GetValue() as Event;
                }
            }
            m_checking = true;
            m_lastEditingTime = DateTime.Now.Ticks;
            EditorApplication.update += Update;
        }

        /// <summary>Stop checking.</summary>
        public void Stop()
        {
            if (!m_checking) return;
            m_checking = false;
            EditorApplication.update -= Update;
        }

        /// <summary>Check mouse position at each update.</summary>
        public void Update()
        {
            if(m_currentEvent == null)
            {
                m_currentEvent = m_reflectionObject.GetValue() as Event;
            }
            if (EditorWindow.focusedWindow != null && m_currentEvent != null)
            {
                if (m_currentEvent.mousePosition != m_lastMousePosition)
                {
                    m_lastMousePosition = m_currentEvent.mousePosition;
                    Rect rect = EditorWindow.focusedWindow.position;
                    rect.x = 0f;
                    rect.y = 0f;
                    if (rect.Contains(m_lastMousePosition))
                    {
                        m_lastEditingTime = DateTime.Now.Ticks;
                    }
                }
            }
        }

        /// <summary>Get idling time in seconds.</summary>
        /// <returns>idling time in seconds</returns>
        public float GetIdlingTime()
        {
            if (!m_checking) return 0f;
            return (DateTime.Now.Ticks- m_lastEditingTime) / (float)TimeSpan.TicksPerSecond;
        }
    }
}
