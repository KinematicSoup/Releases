using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using KS.SF.Unity.Editor;
using KS.SF.Reactor;
using KS.SceneFusion2.Client;
using KS.SceneFusion;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>
    /// Handles syncing of undo/redo operations. Maintains undo and redo stacks that can sync the changes made by each
    /// undo operation. Changes made through Unity's serialized property system are automatically detected and recorded
    /// on the stack. Other changes must be recorded when detected by translators by calling Record() with a
    /// sfBaseUndoOperation.
    /// </summary>
    public class sfUndoManager
    {
        /// <summary></summary>
        /// <returns>singleton instance.</returns>
        public static sfUndoManager Get()
        {
            return m_instance;
        }
        private static sfUndoManager m_instance = new sfUndoManager();

        /// <summary>Stores all property modifications and operations that occurred in an undo operation.</summary>
        private class Transaction
        {
            /// <summary>Property modifications. May be null.</summary>
            public UndoPropertyModification[] Modifications;

            /// <summary>Undo operations. May be null.</summary>
            public List<sfBaseUndoOperation> Operations;

            /// <summary>Constructor</summary>
            /// <param name="modifications"></param>
            /// <param name="operations"></param>
            public Transaction(UndoPropertyModification[] modifications, List<sfBaseUndoOperation> operations)
            {
                Modifications = modifications;
                Operations = operations;
            }

            /// <summary>Syncs changes caused by undoing or redoing the transaction.</summary>
            /// <param name="isUndo">true if this is an undo operation, false if it is a redo.</param>
            public void HandleUndoRedo(bool isUndo)
            {
                if (Modifications != null)
                {
                    RelockObjects();
                    sfUnityEventDispatcher.Get().InvokeOnModifyProperties(Modifications);
                }
                if (Operations != null)
                {
                    foreach (sfBaseUndoOperation op in Operations)
                    {
                        op.HandleUndoRedo(isUndo);
                        RelockObjects(op.GameObjects);
                    }
                }
            }

            /// <summary>
            /// Undo/redo operations that modified properties will unlock locked objects, so this relocks any locked
            /// objects that had modified properties.
            /// </summary>
            private void RelockObjects()
            {
                foreach (UndoPropertyModification modification in Modifications)
                {
                    Component component = modification.currentValue.target as Component;
                    GameObject gameObject = component == null ?
                        modification.currentValue.target as GameObject : component.gameObject;
                    if (gameObject == null)
                    {
                        continue;
                    }
                    sfObject obj = sfObjectMap.Get().GetSFObject(gameObject);
                    if (obj != null && obj.IsLocked)
                    {
                        sfUnityUtils.AddFlags(gameObject, HideFlags.NotEditable);
                    }
                }
            }

            /// <summary>
            /// Game objects modified by an undo operation will have their hideflags reset to the state they were
            /// recorded with. This relocks or unlocks game objects to match their sfObject lock state.
            /// </summary>
            /// <param name="gameObjects">gameObjects to relock or unlock.</param>
            private void RelockObjects(GameObject[] gameObjects)
            {
                if (gameObjects == null)
                {
                    return;
                }
                foreach (GameObject gameObject in gameObjects)
                {
                    sfObject obj = sfObjectMap.Get().GetSFObject(gameObject);
                    if (obj != null)
                    {
                        if (obj.IsLocked)
                        {
                            sfUnityUtils.AddFlags(gameObject, HideFlags.NotEditable);
                        }
                        else
                        {
                            sfUnityUtils.RemoveFlags(gameObject, HideFlags.NotEditable);
                        }
                    }
                }
            }
        }

        /// <summary>Register undo handler.</summary>
        public delegate void RegisterUndoHandler();

        /// <summary>Invoked when an operation is registered on Unity's undo stack.</summary>
        public event RegisterUndoHandler OnRegisterUndo;

        /// <summary>Are we syncing changes made by an undo/redo operation?</summary>
        public bool IsHandlingUndoRedo
        {
            get { return m_activeTransaction != null; }
        }

        /// <summary>
        /// True if there are pending property modifications to be added to the latest transaction, or if there is an
        /// active transaction (being undone or redone) with property modifications.
        /// </summary>
        public bool HasPendingPropertyModifications
        {
            get 
            { 
                return (m_pendingModifications != null && m_pendingModifications.Length > 0) ||
                    (m_activeTransaction != null && m_activeTransaction.Modifications != null && 
                    m_activeTransaction.Modifications.Length > 0); 
            }
        }

        /// <summary>
        /// The pending property modifications to be added to the latest transaction, or if there is an active
        /// transaction (being undone or redone), the property modifications from that transaction.
        /// </summary>
        public UndoPropertyModification[] PendingPropertyModifications
        {
            get
            {
                UndoPropertyModification[] modifications = m_activeTransaction == null ? 
                    m_pendingModifications : m_activeTransaction.Modifications;
                return modifications != null && modifications.Length > 0 ? modifications : null;
            }
        }

        /// <summary>If true, the next operation to be registered on the undo stack will be undone.</summary>
        public bool UndoNextOperation
        {
            get { return m_undoNextOperation; }
            set { m_undoNextOperation = value; }
        }

        private List<Transaction> m_undoStack = new List<Transaction>();
        private List<Transaction> m_redoStack = new List<Transaction>();
        private Transaction m_activeTransaction;
        private int m_undoCount = 0;
        private int m_redoCount = 0;
        private int m_lastGroupIndex;
        private bool m_undoNextOperation = false;
        private UndoPropertyModification[] m_pendingModifications;
        private List<sfBaseUndoOperation> m_pendingOperations;
        private ksReflectionObject m_getRecordsMethod;

        /// <summary>Constructor</summary>
        private sfUndoManager()
        {
            m_getRecordsMethod = new ksReflectionObject(typeof(Undo)).GetMethod("GetRecords", 
                paramTypes: new Type[] { typeof(List<string>), typeof(List<string>) });
        }

        /// <summary>Starts monitoring undo operations.</summary>
        public void Start()
        {
            // We don't allow undoing transactions made before a session started since we don't know what changed in
            // those transactions.
            Undo.ClearAll();
            Undo.postprocessModifications += OnRegister;
            Undo.undoRedoPerformed += OnUndoRedo;
            ObjectChangeEvents.changesPublished += OnChangesPublished;
            sfUnityEventDispatcher.Get().OnUpdate += Update;
            m_lastGroupIndex = -1;
        }

        /// <summary>Stops monitoring undo operations.</summary>
        public void Stop()
        {
            Undo.postprocessModifications -= OnRegister;
            Undo.undoRedoPerformed -= OnUndoRedo;
            ObjectChangeEvents.changesPublished -= OnChangesPublished;
            sfUnityEventDispatcher.Get().OnUpdate -= Update;
            m_undoStack.Clear();
            m_redoStack.Clear();
        }

        /// <summary>
        /// Records an operation on the undo stack. If we are currently handling an undo/redo, the operation will not be
        /// recorded.
        /// </summary>
        /// <param name="operation"></param>
        public void Record(sfBaseUndoOperation operation)
        {
            if (IsHandlingUndoRedo)
            {
                return;
            }
            // All operations are added to a list of pending operations. When a new undo is recorded on Unity's undo
            // stack, we create our own undo transaction with all pending operations.
            if (m_pendingOperations == null)
            {
                m_pendingOperations = new List<sfBaseUndoOperation>();
                m_pendingOperations.Add(operation);
            }
            else
            {
                AddOperation(m_pendingOperations, operation);
            }
        }

        /// <summary>Clears the redo stack.</summary>
        public void ClearRedo()
        {
            // Create a new game object and register an undo operation for it which clears the redo stack, then revert
            // the undo operation we just created, removing it from the undo stack.
            Undo.IncrementCurrentGroup();
            GameObject gameObject = new GameObject("SF Clear Redo");
            Undo.RegisterCreatedObjectUndo(gameObject, "Clear Redo");
            Undo.RevertAllInCurrentGroup();
        }

        /// <summary>Called when properties are modified. Sets the pending property modifications.</summary>
        /// <param name="modifications"></param>
        /// <returns>modifications</returns>
        private UndoPropertyModification[] OnRegister(UndoPropertyModification[] modifications)
        {
            if (!m_undoNextOperation)
            {
                m_pendingModifications = AddModifications(m_pendingModifications, modifications);
            }
            return modifications;
        }

        /// <summary>
        /// Adds an operation to a list of operations. Tries to combine the operation with one of the operations in the
        /// list if it can.
        /// </summary>
        /// <param name="operations">operations to add operation to.</param>
        /// <param name="operation">operation to add.</param>
        private void AddOperation(List<sfBaseUndoOperation> operations, sfBaseUndoOperation operation)
        {
            if (operation.CanCombine)
            {
                for (int i = 0; i < operations.Count; i++)
                {
                    if (operations[i].CombineWith(operation))
                    {
                        return;
                    }
                }
            }
            operations.Add(operation);
        }

        /// <summary>
        /// Called every frame. Checks for changes to Unity's undo/redo stacks and updates ours accordingly.
        /// </summary>
        /// <param name="deltaTime">deltaTime since the last frame.</param>
        private void Update(float deltaTime)
        {
            List<string> undoList = new List<string>();
            List<string> redoList = new List<string>();
            m_getRecordsMethod.Invoke(undoList, redoList);
            if (undoList.Count == 0 && redoList.Count == 0 && m_undoCount + m_redoCount > 0)
            {
                m_undoStack.Clear();
                m_redoStack.Clear();
                m_pendingModifications = null;
                m_pendingOperations = null;
            }
            int groupIndex = Undo.GetCurrentGroup();
            if (undoList.Count > m_undoCount && m_lastGroupIndex != groupIndex)
            {
                // A new transaction was recorded on Unity's undo stack. Record one on ours.
                m_undoStack.Add(new Transaction(m_pendingModifications, m_pendingOperations));
                // It's possible to record an operation on Unity's undo stack without clearing the redo stack if you
                // deselect an object, so we need to check if the redo stack was cleared before clearing ours.
                if (redoList.Count == 0)
                {
                    m_redoStack.Clear();
                }
                m_pendingModifications = null;
                m_pendingOperations = null;
                m_lastGroupIndex = groupIndex;
                m_undoCount = undoList.Count;
                m_redoCount = redoList.Count;

                if (OnRegisterUndo != null)
                {
                    OnRegisterUndo();
                }
                if (m_undoNextOperation)
                {
                    m_undoNextOperation = false;
                    // Undo without registering a redo operation.
                    Undo.RevertAllInCurrentGroup();
                }
            }
            else
            {
                m_undoCount = undoList.Count;
                m_redoCount = redoList.Count;
            }
            if (m_pendingModifications == null && m_pendingOperations == null)
            {
                return;
            }
            if (m_undoStack.Count == 0)
            {
                ksLog.Warning(this, "pending changes with empty undo stack");
                m_pendingModifications = null;
                m_pendingOperations = null;
                return;
            }
            Transaction transaction = m_undoStack[m_undoStack.Count - 1];
            if (m_pendingModifications != null)
            {
                transaction.Modifications = AddModifications(transaction.Modifications, m_pendingModifications);
                m_pendingModifications = null;
            }
            if (m_pendingOperations != null)
            {
                // Add pending operations to the latest undo transaction
                if (transaction.Operations == null)
                {
                    transaction.Operations = m_pendingOperations;
                }
                else
                {
                    for (int i = 0; i < m_pendingOperations.Count; i++)
                    {
                        AddOperation(transaction.Operations, m_pendingOperations[i]);
                    }
                }
                m_pendingOperations = null;
            }
        }

        /// <summary>
        /// Adds an array of new modifications to an existing array of modifications, updating the value for the
        /// existing modification if it already exists and appending it if it does not.
        /// </summary>
        /// <param name="currentModifications">currentModifications to update.</param>
        /// <param name="newModifications">newModifications to add.</param>
        /// <returns>combined modifications.</returns>
        public UndoPropertyModification[] AddModifications(
            UndoPropertyModification[] currentModifications,
            UndoPropertyModification[] newModifications)
        {
            if (currentModifications == null)
            {
                return newModifications;
            }
            List<UndoPropertyModification> toAdd = new List<UndoPropertyModification>();
            foreach (UndoPropertyModification pending in newModifications)
            {
                bool found = false;
                for (int i = 0; i < currentModifications.Length; i++)
                {
                    UndoPropertyModification mod = currentModifications[i];
                    if (mod.currentValue.propertyPath == pending.currentValue.propertyPath &&
                        mod.currentValue.target == pending.currentValue.target)
                    {
                        mod.currentValue = pending.currentValue;
                        currentModifications[i] = mod;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    toAdd.Add(pending);
                }
            }
            if (toAdd.Count > 0)
            {
                currentModifications = currentModifications.Concat(toAdd).ToArray();
            }
            return currentModifications;
        }

        /// <summary>Called after an undo or redo occurs. Syncs the changes made by the undo/redo.</summary>
        private void OnUndoRedo()
        {
            List<string> undoList = new List<string>();
            List<string> redoList = new List<string>();
            m_getRecordsMethod.Invoke(undoList, redoList);
            if (redoList.Count > m_redoCount)
            {
                // This is an undo
                if (undoList.Count >= m_undoCount)
                {
                    // The transaction is a new one we haven't recorded on the undo stack yet. Record it.
                    m_undoStack.Add(new Transaction(m_pendingModifications, m_pendingOperations));
                    m_pendingModifications = null;
                    m_pendingOperations = null;
                }
                if (m_undoStack.Count == 0)
                {
                    m_redoStack.Add(null);
                    ksLog.Warning(this, "undo with empty stack");
                    return;
                }
                // pop the top undo transaction and push it on the redo stack
                Transaction transaction = m_undoStack[m_undoStack.Count - 1];
                m_undoStack.RemoveAt(m_undoStack.Count - 1);
                m_redoStack.Add(transaction);
                m_activeTransaction = transaction;
                try
                {
                    transaction.HandleUndoRedo(true);
                }
                catch (Exception e)
                {
                    ksLog.Error(this, "Error handling undo", e);
                }
            }
            else if (redoList.Count < m_redoCount)
            {
                // This is a redo
                if (m_redoStack.Count == 0)
                {
                    return;
                }
                // pop the top redo transaction and push it on the undo stack
                Transaction transaction = m_redoStack[m_redoStack.Count - 1];
                m_redoStack.RemoveAt(m_redoStack.Count - 1);
                m_undoStack.Add(transaction);
                m_activeTransaction = transaction;
                try
                {
                    transaction.HandleUndoRedo(false);
                }
                catch (Exception e)
                {
                    ksLog.Error(this, "Error handling redo", e);
                }
            }
            m_undoCount = undoList.Count;
            m_redoCount = redoList.Count;
        }

        /// <summary>
        /// Called when operations were recorded on the undo stack. Calls the sfUnityEventDispatcher to invoke events
        /// and clears the active transaction.
        /// </summary>
        /// <param name="stream">stream of undo operations</param>
        private void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            if (sfUnityEventDispatcher.Get().Enabled)
            {
                sfUnityEventDispatcher.Get().InvokePostModifyProperties(PendingPropertyModifications);
                sfUnityEventDispatcher.Get().InvokeChangeEvents(ref stream);
            }
            m_activeTransaction = null;
        }
    }
}
