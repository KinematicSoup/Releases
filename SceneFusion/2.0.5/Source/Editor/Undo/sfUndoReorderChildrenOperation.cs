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
using System.Linq;
using System.Text;
using UnityEngine;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>
    /// Invokes <see cref="sfUnityEventDispatcher.OnReorderChildren"/> for an undo reorder children operation.
    /// </summary>
    public class sfUndoReorderChildrenOperation : sfBaseUndoOperation
    {
        private GameObject m_gameObject;

        /// <summary>Constructor</summary>
        /// <param name="gameObject">Game object whose children were reordered.</param>
        public sfUndoReorderChildrenOperation(GameObject gameObject)
        {
            m_gameObject = gameObject;
        }

        /// <summary>
        /// Invokes <see cref="sfUnityEventDispatcher.OnReorderChildren"/> for the game object whose children were 
        /// reordered by the undo/redo operation.
        /// </summary>
        /// <param name="isUndo">True if this is an undo operation, false if it is a redo.</param>
        public override void HandleUndoRedo(bool isUndo)
        {
            sfUnityEventDispatcher.Get().InvokeOnReorderChildren(m_gameObject);
        }
    }
}
