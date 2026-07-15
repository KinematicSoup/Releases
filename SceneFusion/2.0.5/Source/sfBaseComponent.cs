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
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KS.SceneFusion.Client.Unity
{
    /// <summary>
    /// Base class for our components. Sets <see cref="HideFlags.DontSaveInBuild"/> to prevent the component from being
    /// included in builds.
    /// </summary>
    public abstract class sfBaseComponent : MonoBehaviour
    {
#if UNITY_EDITOR
        /// <summary>Constructor</summary>
        public sfBaseComponent()
        {
            // We cannot set the hidelflags from the constructor, so we register a delayCall delegate and do it from
            // there.
            EditorApplication.delayCall += Initialize;
        }

        /// <summary>Initialization. Sets <see cref="HideFlags.DontSaveInBuild"/>.</summary>
        protected void Initialize()
        {
            if (this != null)
            {
                hideFlags |= HideFlags.DontSaveInBuild;
            }
        }
#endif
    }
}
