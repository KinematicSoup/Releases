using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>
    /// Base class for syncing an undo operation. Undo operations are recorded on our custom undo stack by calling
    /// sfUndoManager.Get().Record(sfIUndoOperation). When the operation is undone or redone, the corresponding method
    /// is called on the operation to sync the changes.
    /// </summary>
    public abstract class sfBaseUndoOperation
    {
        /// <summary>Game objects affected by the operation.</summary>
        public virtual GameObject[] GameObjects
        {
            get { return null; }
        }

        /// <summary>Can this undo operation be combined with another operation?</summary>
        public virtual bool CanCombine
        {
            get { return false; }
        }

        /// <summary>Called to sync changes by an undo or redo operation.</summary>
        /// <param name="isUndo">true if this is an undo operation, false if it is a redo.</param>
        public abstract void HandleUndoRedo(bool isUndo);

        /// <summary>Combines another operation with this one.</summary>
        /// <returns>true if the operations could be combined.</returns>
        public virtual bool CombineWith(sfBaseUndoOperation other)
        {
            return false;
        }
    }
}
