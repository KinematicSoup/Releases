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
using KS.SF.Reactor;
using UnityEngine;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>
    /// Wraps a <see cref="sfSerializableSessionInfo"/> and implements <see cref="ISerializationCallbackReceiver"/> so
    /// the session info can be serialized to a byte array that Unity can serialize.
    /// </summary>
    [Serializable]
    public struct sfSerializableSessionInfo : ISerializationCallbackReceiver
    {
        private static readonly string LOG_CHANNEL = typeof(sfSerializableSessionInfo).Name;

        /// <summary>Session info</summary>
        public sfSessionInfo Info
        {
            get { return m_info; }
            set { m_info = value; }
        }
        private sfSessionInfo m_info;
        private byte[] m_serializedInfo;

        /// <summary>Serializes the session info to a byte array that Unity can serialize.</summary>
        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            if (m_info == null)
            {
                m_serializedInfo = null;
            }
            else
            {
                ksStreamBuffer buffer = ksStreamBuffer.Create();
                m_info.Serialize(buffer);
                m_serializedInfo = buffer.ToArray();
                buffer.Release();
            }
        }

        /// <summary>Deserializes the session info from the Unity-serialized byte array.</summary>
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (m_serializedInfo == null || m_serializedInfo.Length == 0)
            {
                m_info = null;
            }
            else
            {
                try
                {
                    ksStreamBuffer buffer = new ksStreamBuffer(m_serializedInfo);
                    m_info = new sfSessionInfo();
                    m_info.Deserialize(buffer);
                }
                catch (Exception e)
                {
                    ksLog.Error(LOG_CHANNEL, "Error deserializing session info.", e);
                }
            }
        }
    }
}
