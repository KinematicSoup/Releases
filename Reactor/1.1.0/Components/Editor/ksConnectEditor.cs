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

using UnityEditor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Editor used for rendering the the ksConnect component in inspector or editor windows.</summary>
    [CustomEditor(typeof(ksConnect))]
    [CanEditMultipleObjects]
    public class ksConnectEditor : UnityEditor.Editor
    {
        private SerializedProperty m_spConnectProtocol;
        private SerializedProperty m_spConnectMode;
        private SerializedProperty m_spRemoteHost;
        private SerializedProperty m_spRemotePort;
        private SerializedProperty m_spSecureLocalWebSockets;
        private SerializedProperty m_spConnectOnStart;
        private SerializedProperty m_spOnGetRooms;
        private SerializedProperty m_spOnConnect;
        private SerializedProperty m_spOnDisconnect;

        /// <summary>Find serialized properties.</summary>
        void OnEnable()
        {
            m_spConnectProtocol = serializedObject.FindProperty("ConnectProtocol");
            m_spConnectMode = serializedObject.FindProperty("ConnectMode");
            m_spRemoteHost = serializedObject.FindProperty("RemoteHost");
            m_spRemotePort = serializedObject.FindProperty("RemotePort");
            m_spSecureLocalWebSockets = serializedObject.FindProperty("SecureLocalWebSockets");
            m_spConnectOnStart = serializedObject.FindProperty("ConnectOnStart");
            m_spOnGetRooms = serializedObject.FindProperty("OnGetRooms");
            m_spOnConnect = serializedObject.FindProperty("OnConnect");
            m_spOnDisconnect = serializedObject.FindProperty("OnDisconnect");
        }

        /// <summary>Render serialized properties in the inspector.</summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(m_spConnectOnStart);
            EditorGUILayout.PropertyField(m_spConnectProtocol);
            EditorGUILayout.PropertyField(m_spConnectMode);
            if (m_spConnectMode.enumValueIndex == (int)ksConnect.ConnectModes.REMOTE)
            {
                EditorGUI.indentLevel = 1;
                EditorGUILayout.PropertyField(m_spRemoteHost);
                EditorGUILayout.PropertyField(m_spRemotePort);
                EditorGUILayout.Space();
                EditorGUI.indentLevel = 0;
            }
            if (m_spConnectMode.enumValueIndex == (int)ksConnect.ConnectModes.LOCAL)
            {
                EditorGUI.indentLevel = 1;
                EditorGUILayout.PropertyField(m_spSecureLocalWebSockets);
                EditorGUI.indentLevel = 0;
            }
            EditorGUILayout.PropertyField(m_spOnGetRooms);
            EditorGUILayout.PropertyField(m_spOnConnect);
            EditorGUILayout.PropertyField(m_spOnDisconnect);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
