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
using UnityEditor;
using UnityEngine.SceneManagement;
using KS.SF.Unity.Editor;
using KS.SF.Reactor;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>
    /// Provides events for handling hierarchy drag operations and prevents the rename box from disappearing when the
    /// hierarchy is changed.
    /// </summary>
    public class sfHierarchyWatcher
    {
        // Unity adds this number when converting object ids to control ids
        private const ulong OBJECT_ID_TO_CONTROL_ID = 10000000;

        /// <summary>Drag start event handler</summary>
        public delegate void DragStartHandler();

        /// <summary>Drag cancel event handler</summary>
        public delegate void DragCancelHandler();

        /// <summary>Drag complete event handler</summary>
        /// <param name="target">target parent for the dragged objects.</param>
        /// <param name="scene">scene target for the dragged objects.</param>
        public delegate void DragCompleteHandler(GameObject target, Scene scene);

        /// <summary>Drag validator.</summary>
        /// <param name="target">target parent for the dragged objects.</param>
        /// <param name="childIndex">childIndex the dragged objects will be inserted at.</param>
        /// <returns>false is the drag should be prevented.</returns>
        public delegate bool DragValidator(GameObject target, int childIndex);

        /// <summary>Invoked when a hierarchy drag operation begins.</summary>
        public event DragStartHandler OnDragStart;

        /// <summary>Invoked when a hierarchy drag operation is cancelled.</summary>
        public event DragCancelHandler OnDragCancel;

        /// <summary>Invoked when a hierarchy drag operation is completed.</summary>
        public event DragCompleteHandler OnDragComplete;

        /// <summary>
        /// Invoked to validate a drag operation. If any handlers return false, the drag is prevented.
        /// </summary>
        public event DragValidator OnValidateDrag
        {
            add
            {
                m_dragValidators.Add(value);
            }

            remove
            {
                m_dragValidators.Remove(value);
            }
        }
        private List<DragValidator> m_dragValidators = new List<DragValidator>();

        /// <summary>Is the user dragging objects in the hierarchy?</summary>
        public bool Dragging
        {
            get { return m_dragging; }
        }

        private bool m_hierarchyStale = false;
        private bool m_preventingScroll = false;
        private bool m_dragging = false;
        private bool m_dragUpdated = false;
        private Vector2 m_lastScrollPosition;
        private EditorWindow m_hierarchyWindow;
        private ksReflectionObject m_treeViewField;
        private ksReflectionObject m_editFieldRectField;
        private ksReflectionObject m_isRenamingMethod;
        private ksReflectionObject m_scrollPosField;

        /// <summary></summary>
        /// <returns>singleton instance</returns>
        public static sfHierarchyWatcher Get()
        {
            return m_instance;
        }
        private static sfHierarchyWatcher m_instance = new sfHierarchyWatcher();

        /// <summary>Singleton constructor</summary>
        private sfHierarchyWatcher()
        {

        }

        /// <summary>Starts checking for hierarchy drag events.</summary>
        public void Start()
        {
#if UNITY_6000_4_OR_NEWER
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += HandleDragEvents;
#else
            EditorApplication.hierarchyWindowItemOnGUI += HandleDragEvents;
#endif
            MarkHierarchyStale();
        }

        /// <summary>Stops checking for hierarchy drag events.</summary>
        public void Stop()
        {
#if UNITY_6000_4_OR_NEWER
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI -= HandleDragEvents;
#else
            EditorApplication.hierarchyWindowItemOnGUI -= HandleDragEvents;
#endif
            m_dragging = false;
            m_dragUpdated = false;
            MarkHierarchyStale();
        }

        /// <summary>Refreshes the hierarchy window on the next update.</summary>
        public void MarkHierarchyStale()
        {
            m_hierarchyStale = true;
        }

        /// <summary>Called every pre-update. Validates the current drag target.</summary>
        /// <param name="deltaTime">deltaTime in seconds since the last update.</param>
        public void PreUpdate(float deltaTime)
        {
            if (m_dragUpdated)
            {
                // Validate drag
                m_dragUpdated = false;
                int childIndex;
                Scene scene;
                GameObject target = GetDragTarget(out childIndex, out scene);
                if (!AllowDrag(target, childIndex))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                }
            }
        }

        /// <summary>
        /// Called every update. Refreshes the hierarchy window if it was marked stale, and prevents the rename box from
        /// closing.
        /// </summary>
        /// <param name="deltaTime">deltaTime in seconds since the last update.</param>
        public void Update(float deltaTime)
        {
            if (m_hierarchyStale)
            {
                // Refresh hierarchy window
                m_hierarchyStale = false;
                EditorApplication.DirtyHierarchyWindowSorting();
            }
            if (FindHierarchyWindow())
            {
                if (m_preventingScroll)
                {
                    // Reset scroll position
                    m_preventingScroll = false;
                    SetScrollPosition(m_lastScrollPosition);
                }
                // This must be called after DirtyHierarchyWindowSorting
                KeepRenameBox();
            }
        }

        /// <summary>Prevents the hierarchy window from scrolling for the rest of the frame.</summary>
        private void PreventScroll()
        {
            if (!m_preventingScroll && FindHierarchyWindow())
            {
                m_preventingScroll = true;
                m_lastScrollPosition = GetScrollPosition();
            }
        }

        /// <summary>
        /// This is a hack to keep the rename box open if it's currently open when the hierarchy changes.
        /// </summary>
        private void KeepRenameBox()
        {
            if (m_treeViewField != null && m_editFieldRectField != null && m_isRenamingMethod != null &&
                (bool)m_isRenamingMethod.Invoke())
            {
                // Prevent rename box from scrolling out of view
                PreventScroll();
                Rect renameRect = (Rect)m_editFieldRectField.GetValue();
                if (renameRect.y > 0)
                {
                    // If the rename box is about to disappear below the end of the view, Unity seperates the rename
                    // box from its game object. We do not want this and set it back.
                    if (m_treeViewField.GetValue() != null && Selection.activeGameObject != null)
                    {
                        // Because the tree view field instance can change we need to redo the reflection here
                        int row = (int)m_treeViewField.GetProperty("data")
                            .Call("GetRow", sfUnityUtils.GetUnityId(Selection.activeGameObject)).GetValue();
                        float rowY = (float)m_treeViewField.Call("GetTopPixelOfRow", row).GetValue();
                        if (rowY != renameRect.y)
                        {
                            renameRect.y = rowY;
                            m_editFieldRectField.SetValue(renameRect);
                        }
                    }
                    m_lastScrollPosition.y = Math.Min(m_lastScrollPosition.y, renameRect.y);
                    m_lastScrollPosition.y = Math.Max(m_lastScrollPosition.y,
                        renameRect.y - m_hierarchyWindow.position.height + 34);

                }

                // Unity closes the rename box if it's open and the tree view is not null when the hierarchy changes.
                // So every frame the rename box is open we set the tree view to null. Unity will initialize a new tree
                // view with the same tree state object, but only after it checks if it should close the rename box.
                m_treeViewField.SetValue(null);
            }
        }

        /// <summary>Finds the hierarchy window and initializes reflection objects.</summary>
        /// <returns>true if the hierarchy window is open and initialization succeeded.</returns>
        private bool FindHierarchyWindow()
        {
            if (m_hierarchyWindow != null)
            {
                return true;
            }
            try
            {
                EditorWindow hierarchyWindow = sfUI.Get().FindWindow("SceneHierarchyWindow");
                if (hierarchyWindow == null)
                {
                    return false;
                }

                m_treeViewField
                    = new ksReflectionObject(hierarchyWindow).GetField("m_SceneHierarchy").GetField("m_TreeView");

                ksReflectionObject stateProperty = m_treeViewField.GetProperty("state");
                if (stateProperty.Container == null)
                {
                    return false;
                }

                ksReflectionObject renameOverlayProperty = stateProperty.GetProperty("renameOverlay");
                m_isRenamingMethod = renameOverlayProperty.GetMethod("IsRenaming");
                m_editFieldRectField = renameOverlayProperty.GetField("m_EditFieldRect");
                m_scrollPosField = stateProperty.GetField("scrollPos");
                m_hierarchyWindow = hierarchyWindow;
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Error getting hierarchy window scroll position", e);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Handles Unity hierarchy drag events and invokes our own drag events. This is called from
        /// hierarchyWindowItemOnGUI, which is called for each visible object in hierarchy window, because this is the
        /// only place we can intercept Unity drag events.
        /// </summary>
        /// <param name="uobjectId">Id of gameObject. Unused.</param>
        /// <param name="area">area for gameObject in hierarchy window. Unused.</param>
#if UNITY_6000_4_OR_NEWER
        private void HandleDragEvents(EntityId uobjectId, Rect area)
#else
        private void HandleDragEvents(int uobjectId, Rect area)
#endif
        {
            if (!FindHierarchyWindow())
            {
                return;
            }
            switch (Event.current.type)
            {
                case EventType.DragUpdated:
                {
                    m_dragUpdated = true;
                    if (!m_dragging)
                    {
                        m_dragging = true;
                        if (OnDragStart != null)
                        {
                            OnDragStart();
                        }
                    }
                    break;
                }
                case EventType.DragPerform:
                {
                    if (m_dragging)
                    {
                        m_dragging = false;
                        int childIndex;
                        Scene scene;
                        GameObject target = GetDragTarget(out childIndex, out scene);
                        if (!AllowDrag(target, childIndex))
                        {
                            Event.current.Use();
                            if (OnDragCancel != null)
                            {
                                OnDragCancel();
                            }
                        }
                        else if (OnDragComplete != null)
                        {
                            OnDragComplete(target, scene);
                        }
                    }
                    break;
                }
                case EventType.DragExited:
                {
                    if (m_dragging)
                    {
                        m_dragging = false;
                        if (OnDragCancel != null)
                        {
                            OnDragCancel();
                        }
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Gets the object that will become the parent of the dragged objects if a drag operation were to complete now.
        /// </summary>
        /// <param name="childIndex">childIndex the dragged objects will be inserted at.</param>
        /// <param name="scene">scene target for the dragged objects.</param>
        /// <returns>target parent for the dragged objects.</returns>
        private GameObject GetDragTarget(out int childIndex, out Scene scene)
        {
            // Because the tree view field instance can change we need to redo the reflection here
            ksReflectionObject draggingProperty = m_treeViewField.GetProperty("dragging");
            int targetId = (int)draggingProperty.Call("GetDropTargetControlID").GetValue();
            if (targetId > 0)
            {
                GameObject target = sfUnityUtils.GetUObject<GameObject>((ulong)targetId - OBJECT_ID_TO_CONTROL_ID);
                childIndex = target == null ? 0 : target.transform.childCount;
                scene = target == null ? new Scene() : target.scene;
                return target;
            }
            targetId = (int)draggingProperty.Call("GetRowMarkerControlID").GetValue();
            if (targetId > 0)
            {
                GameObject target = sfUnityUtils.GetUObject<GameObject>((ulong)targetId + OBJECT_ID_TO_CONTROL_ID);
                if (target != null)
                {
                    childIndex = target.transform.GetSiblingIndex();
                    if (!(bool)draggingProperty.GetProperty("drawRowMarkerAbove").GetValue())
                    {
                        childIndex++;
                    }
                    scene = target.scene;
                    return target.transform.parent == null ? null : target.transform.parent.gameObject;
                }
            }
            childIndex = 0;
            scene = new Scene();
            return null;
        }

        /// <summary>Calls event handles to validate a drag operation.</summary>
        /// <param name="target">target parent for the dragged objects.</param>
        /// <param name="childIndex">childIndex the dragged objects will be inserted at.</param>
        /// <returns>true if the drag should be allowed.</returns>
        private bool AllowDrag(GameObject target, int childIndex)
        {
            foreach (DragValidator validator in m_dragValidators)
            {
                if (!validator(target, childIndex))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary></summary>
        /// <returns>scroll position of the hierarchy window.</returns>
        private Vector2 GetScrollPosition()
        {
            return m_scrollPosField == null ? Vector2.zero : (Vector2)m_scrollPosField.GetValue();
        }

        /// <summary>Sets the scroll position of the hierarchy window.</summary>
        /// <param name="value">value to set.</param>
        private void SetScrollPosition(Vector2 value)
        {
            if (m_scrollPosField != null)
            {
                m_scrollPosField.SetValue(value);
            }
        }
    }
}
