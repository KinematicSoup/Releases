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

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Inspector editor for <see cref="ksColliderData"/>. Sets the hideflags to hide the component.</summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ksColliderData), true)]
    public class ksColliderDataEditor : UnityEditor.Editor
    {
        /// <summary>Sets the hideflags to hide the target components.</summary>
        public override void OnInspectorGUI()
        {
            foreach (Component component in targets)
            {
                if (component != null)
                {
                    component.hideFlags = HideFlags.HideInInspector;
                }
            }
        }
    }
}
