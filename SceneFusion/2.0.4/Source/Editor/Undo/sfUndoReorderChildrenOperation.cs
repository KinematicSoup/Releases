using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KS.SceneFusion2.Unity.Editor
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
