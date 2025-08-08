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
    /// <summary>
    /// Custom editor that opens a new server room script dialog and deletes the target scripts.
    /// </summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ksNewServerRoomScript))]
    public class ksNewServerRoomScriptEditor : UnityEditor.Editor
    {
        /// <summary>
        /// Opens a new server room script dialog and deletes the target scripts.
        /// </summary>
        public void OnEnable()
        {
            ksNewScriptMenu.Open(ScriptableObject.CreateInstance<ksServerScriptTemplate>().Initialize(
                    ksServerScriptTemplate.ScriptType.ROOM, Selection.gameObjects));
            foreach (ksNewServerRoomScript script in targets)
            {
                DestroyImmediate(script, true);
            }
        }
    }
}
