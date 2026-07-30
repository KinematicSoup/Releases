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
using KS.SF.Reactor.Client;
using UnityEditor;
using UnityEngine;

namespace KS.SceneFusion.Client.Unity.Editor
{
    // Implementation specific to the cloud package. .Cloud is appended to the file name because Unity cannot save
    // ScriptableObject assets if there are multiple files with the same name as the class.
    public partial class sfConfig
    {
        public partial class NetworkSettings
        {
            [Tooltip("Override Server port")]
            public ushort OverrideServerPort = 0;
        }

        /// <summary>Active project ID</summary>
        public int ProjectId
        {
            get { return m_projectId; }
            set
            {
                if (m_projectId != value)
                {
                    m_projectId = value;
                    EditorUtility.SetDirty(this);
                }
            }
        }
        [SerializeField]
        [HideInInspector]
        private int m_projectId = -1;
    }
}
