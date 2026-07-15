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
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KS.SF.Reactor;

namespace KS.SceneFusion.Client.Unity
{
    /// <summary>Attached to synced game objects with missing prefabs.</summary>
    [AddComponentMenu("")]
    public class sfMissingPrefab : sfBaseComponent
    {
        /// <summary>Prefab path</summary>
        public string PrefabPath
        {
            get { return m_prefabPath; }
            set { m_prefabPath = value; }
        }
        [SerializeField]
        private string m_prefabPath;

        /// <summary>
        /// The file id of this object in the missing prefab. 0 if this is the root of the missing prefab.
        /// </summary>
        public long FileId
        {
            get { return m_fileId; }
            set { m_fileId = value; }
        }
        [SerializeField]
        private long m_fileId = 0;

        /// <summary>Is this the root of the missing prefab?</summary>
        public bool IsRoot
        {
            get { return m_fileId == 0; }
        }

        /// <summary>Logs a warning for the missing prefab.</summary>
        private void Awake()
        {
            if (m_fileId < 0)
            {
                ksLog.Warning(this, gameObject.name + " has missing prefab '" + m_prefabPath + "'.", gameObject);
            }
            else if (transform.parent == null || transform.parent.GetComponent<sfMissingPrefab>() == null)
            {
                ksLog.Warning(this, gameObject.name + " has missing prefab child '" + m_prefabPath + "'.", gameObject);
            }
        }
    }
}
#endif
