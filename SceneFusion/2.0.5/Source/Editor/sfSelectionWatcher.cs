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
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>Checks for uobject selection changes and invokes OnSelect and OnDeselect events.</summary>
    public class sfSelectionWatcher
    {
        /// <summary>Selection event handler.</summary>
        /// <param name="uobj">uobj that was selected.</param>
        public delegate void SelectionHandler(UObject uobj);

        /// <summary>Deselection event handler.</summary>
        /// <param name="that">that was deselected. May be a deleted object.</param>
        public delegate void DeselectionHandler(UObject uobj);

        /// <summary>Invoked when a uobject is selected.</summary>
        public event SelectionHandler OnSelect;

        /// <summary>Invoked when a uobject is deselected or a selected uobject is deleted.</summary>
        public event DeselectionHandler OnDeselect;

        private HashSet<UObject> m_previousSelection = new HashSet<UObject>();

        /// <summary></summary>
        /// <returns>singleton instance</returns>
        public static sfSelectionWatcher Get()
        {
            return m_instance;
        }
        private static sfSelectionWatcher m_instance = new sfSelectionWatcher();

        /// <summary>Singleton constructor</summary>
        private sfSelectionWatcher()
        {

        }

        /// <summary>Starts checking for selection changes.</summary>
        public void Start()
        {
            Selection.selectionChanged += OnSelectionChange;
            OnSelectionChange();
        }

        /// <summary>Stops checking for selection changes.</summary>
        public void Stop()
        {
            Selection.selectionChanged -= OnSelectionChange;
            m_previousSelection.Clear();
        }

        /// <summary>
        /// Checks if a uobject is selected. If the uobject is a component, checks if the component's game object is
        /// selected.
        /// </summary>
        /// <param name="uobj"></param>
        /// <returns>true if the uobject is selected.</returns>
        public bool IsSelected(UObject uobj)
        {
            GameObject gameObject = uobj as GameObject;
            if (gameObject == null)
            {
                Component component = uobj as Component;
                if (component == null)
                {
                    return Selection.Contains(uobj);
                }
                gameObject = component.gameObject;
            }
            return Selection.Contains(gameObject);
        }

        /// <summary>
        /// Compares the set of currently selected uobjects with the set from the last time Update was called and
        /// invokes events for changes.
        /// </summary>
        private void OnSelectionChange()
        {
            HashSet<UObject> selectedObjects = new HashSet<UObject>(Selection.objects);
            if (OnSelect != null)
            {
                foreach (UObject uobj in selectedObjects)
                {
                    if (uobj != null && !m_previousSelection.Contains(uobj))
                    {
                        OnSelect(uobj);
                    }
                }
            }

            if (OnDeselect != null)
            {
                foreach (UObject uobj in m_previousSelection)
                {
                    if (uobj.IsDestroyed() || !selectedObjects.Contains(uobj))
                    {
                        OnDeselect(uobj);
                    }
                }
            }
            m_previousSelection = selectedObjects;
        }
    }
}
