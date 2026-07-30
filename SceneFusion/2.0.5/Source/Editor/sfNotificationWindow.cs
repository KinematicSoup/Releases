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
using KS.SF.Unity.Editor;
using KS.SF.Unity;
using KS.SF.Reactor;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>
    /// Editor window that displays notifications in categories. Notifications can be expanded so show all objects
    /// effected by the notification, which will be selected when clicked on.
    /// </summary>
    public class sfNotificationWindow : EditorWindow
    {
        // Minimum window width in pixels
        private const float MIN_WIDTH = 380f;
        // Minimum window height in pixels
        private const float MIN_HEIGHT = 100f;
        // Padding in pixels after object icon
        private const float ICON_PADDING = 0f;

        private sfNotificationManager m_manager = sfNotificationManager.Get();
        private ksSerializableHashSet<string> m_collapsedCategories = new ksSerializableHashSet<string>();
        private ksSerializableHashSet<string> m_expandedNotifications = new ksSerializableHashSet<string>();
        private sfNotification m_selectedNotification;
        private UObject m_anchorSelectionObject;
        private sfNotification m_anchorSelectionNotification;
        private int m_keyMovement = 0;
        private Vector2 m_scrollPosition;
        private bool m_verticalScrollBarShowing = false;
        private bool m_horizontalScrollBarShowing = false;
        private bool m_scrollToSelection = false;

        private static sfNotificationWindow m_window;

        /// <summary>Scene Fusion service</summary>
        private static sfService Service
        {
            get { return SceneFusion.Get().Service; }
        }

        /// <summary>Opens the notification window.</summary>
        public static void Open()
        {
            if (m_window == null)
            {
                m_window = GetWindow<sfNotificationWindow>(new Type[] { typeof(ksWindow) });
                m_window.titleContent.text = " Notifications";// start with a space so it looks good with the icon
                m_window.titleContent.image = sfTextures.Logo;
                m_window.minSize = new Vector2(MIN_WIDTH, MIN_HEIGHT);
            }
            m_window.Show();
            m_window.Focus();
        }

        /// <summary>Creates the GUI.</summary>
        private void OnGUI()
        {
            m_scrollPosition = EditorGUILayout.BeginScrollView(m_scrollPosition);

            if (m_manager.Count == 0)
            {
                EditorGUILayout.HelpBox("You have no notifications.", MessageType.Info);
                m_verticalScrollBarShowing = false;
            }
            else
            {
                if (HandleKeyEvents())
                {
                    m_scrollToSelection = true;
                }
                // We can only scroll to the selected notification on repaint events as that is the only time the
                // control rects have accurate positions.
                bool scrollToSelection = m_scrollToSelection && Event.current.type == EventType.Repaint;
                if (scrollToSelection)
                {
                    m_scrollToSelection = false;
                }

                foreach (KeyValuePair<sfNotificationCategory, ksLinkedList<sfNotification>> pair in m_manager)
                {
                    sfNotificationCategory category = pair.Key;
                    ksLinkedList<sfNotification> notifications = pair.Value;
                    if (DrawCategory(category, notifications.Count))
                    {
                        EditorGUI.indentLevel++;
                        foreach (sfNotification notification in notifications)
                        {
                            DrawNotification(notification, scrollToSelection);
                        }
                        EditorGUI.indentLevel--;
                    }
                }
            }

            if (Event.current.type == EventType.Repaint)
            {
                Rect rect = GUILayoutUtility.GetLastRect();
                float height = rect.y + rect.height;
                m_horizontalScrollBarShowing = rect.x + rect.width + 2.6f > position.width;
                EditorGUILayout.EndScrollView();
                rect = GUILayoutUtility.GetLastRect();
                m_verticalScrollBarShowing = height > (m_horizontalScrollBarShowing ? rect.height - 15f : rect.height);
            }
            else
            {
                EditorGUILayout.EndScrollView();
            }
            Repaint();
        }

        /// <summary>Draws a category header.</summary>
        /// <param name="category"></param>
        /// <param name="numNotifications">numNotifications in the category.</param>
        /// <returns>true if the category is expanded.</returns>
        private bool DrawCategory(sfNotificationCategory category, int numNotifications)
        {
            if (!string.IsNullOrEmpty(category.HelpUrl))
            {
                EditorGUILayout.BeginHorizontal();
            }
            string title = category.Name + " (" + numNotifications + ")";
            bool expanded = !m_collapsedCategories.Contains(category.Name);
            if (ksStyle.TitleFoldout(expanded, title) != expanded)
            {
                expanded = !expanded;
                if (expanded)
                {
                    m_collapsedCategories.Remove(category.Name);
                }
                else
                {
                    m_collapsedCategories.Add(category.Name);
                }
            }
            if (!string.IsNullOrEmpty(category.HelpUrl))
            {
                string text = "More Details";
                Rect rect = EditorGUILayout.GetControlRect();
                GUIStyle linkStyle = new GUIStyle(EditorStyles.label);
                Vector2 size = linkStyle.CalcSize(new GUIContent(text));
                float x = position.width - size.x - linkStyle.padding.right + m_scrollPosition.x;
                if (m_verticalScrollBarShowing)
                {
                    x -= 13f;
                }
                ksStyle.Link(new Vector2(x, rect.y), text, category.HelpUrl);
                EditorGUILayout.EndHorizontal();
            }
            return expanded;
        }

        /// <summary>Draws a notification.</summary>
        /// <param name="notification">notification to draw.</param>
        /// <param name="scrollToSelection">
        /// if true, will scroll the scrollview to put the notification in view if it
        /// is selected.
        /// </param>
        private void DrawNotification(sfNotification notification, bool scrollToSelection)
        {
            if (DrawNotificationFoldout(notification))
            {
                EditorGUI.indentLevel++;
                foreach (UObject uobj in notification.Objects)
                {
                    GUIStyle style = EditorStyles.label;
                    if (uobj == null)
                    {
                        style = new GUIStyle(style);
                        style.normal.textColor = ksStyle.MissingPrefabColour;
                        EditorGUILayout.LabelField("Missing Object", style);
                        continue;
                    }
                    Rect rect = EditorGUILayout.GetControlRect();
                    if (IsSelected(uobj))
                    {
                        Rect highlightRect = rect;
                        highlightRect.y -= 1f;
                        highlightRect.height += 2f;
                        EditorGUI.DrawRect(highlightRect, ksStyle.SelectionHighlightColour);
                    }
                    else if (rect.Contains(Event.current.mousePosition))
                    {
                        Rect highlightRect = rect;
                        highlightRect.y -= 1f;
                        highlightRect.height += 2f;
                        EditorGUI.DrawRect(highlightRect, ksStyle.HighlightColour);
                    }
                    if (scrollToSelection && uobj == m_anchorSelectionObject)
                    {
                        m_scrollPosition.y = Math.Max(m_scrollPosition.y, rect.y + rect.height - position.height +
                            (m_horizontalScrollBarShowing ? 15f : 0f));
                        m_scrollPosition.y = Math.Min(m_scrollPosition.y, rect.y);
                    }
                    rect = EditorGUI.IndentedRect(rect);
                    if (PrefabUtility.IsPartOfNonAssetPrefabInstance(uobj))
                    {
                        style = new GUIStyle(style);
                        style.normal.textColor = ksStyle.PrefabColour;
                        style.hover = style.normal;
                    }
                    else if (PrefabUtility.IsPrefabAssetMissing(uobj))
                    {
                        style = new GUIStyle(style);
                        style.normal.textColor = ksStyle.MissingPrefabColour;
                        style.hover = style.normal;
                    }
                    
                    string name = uobj.name;
                    if (!(uobj is GameObject))
                    {
                        name += " (" + uobj.GetType().Name + ")";
                    }
                    string path = AssetDatabase.GetAssetPath(uobj);
                    if (!string.IsNullOrEmpty(path))
                    {
                        name += " '" + path + "'";
                    }
                    rect.x += rect.height + ICON_PADDING;
                    rect.width -= rect.height - ICON_PADDING;
                    if (GUI.Button(rect, name, style))
                    {
                        HandleSelection(notification, uobj);
                    }

                    Texture2D icon = AssetPreview.GetMiniThumbnail(uobj);
                    if (icon != null)
                    {
                        rect.width = rect.height;
                        rect.x -= rect.height + ICON_PADDING;
                        GUI.DrawTexture(rect, icon);
                    }
                }
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>Draws the toggleable foldout and message of a notification.</summary>
        /// <param name="notification">notification to draw.</param>
        /// <returns>true if the notification is expanded.</returns>
        private bool DrawNotificationFoldout(sfNotification notification)
        {
            string text = notification.Message + " (" + notification.Objects.Count + ")";
            bool expanded = m_expandedNotifications.Contains(notification.Message);
            if (ksStyle.Foldout(expanded, text) != expanded)
            {
                expanded = !expanded;
                if (expanded)
                {
                    m_expandedNotifications.Add(notification.Message);
                    m_selectedNotification = notification;
                }
                else
                {
                    m_expandedNotifications.Remove(notification.Message);
                    if (m_selectedNotification == notification)
                    {
                        m_selectedNotification = null;
                    }
                }
                return true;
            }
            return expanded;
        }

        /// <summary>
        /// Moves the object selection up and down when the arrow keys are pressed. Deletes selection objects when
        /// delete is pressed.
        /// </summary>
        /// <returns>
        /// true if the selection changed due to keyboard events. If selection did not change or was
        /// deleted, returns false.
        /// </returns>
        private bool HandleKeyEvents()
        {
            if (Event.current.type != EventType.KeyDown || m_selectedNotification == null || 
                m_collapsedCategories.Contains(m_selectedNotification.Category.Name))
            {
                return false;
            }
            if (Event.current.keyCode == KeyCode.Delete)
            {
                if (m_anchorSelectionObject != null && m_anchorSelectionNotification == m_selectedNotification &&
                    IsSelected(m_anchorSelectionObject))
                {
                    List<UObject> assets = new List<UObject>();
                    foreach (UObject obj in Selection.objects)
                    {
                        if (obj != null)
                        {
                            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(obj)))
                            {
                                assets.Add(obj);
                            }
                            else
                            {
                                Undo.DestroyObjectImmediate(obj);
                            }
                        }
                    }
                    // Confirm before deleting assets
                    if (assets.Count > 0)
                    {
                        string message = "Delete the following asset(s)?";
                        foreach (UObject asset in assets)
                        {
                            message += "\n    " + AssetDatabase.GetAssetPath(asset);
                        }
                        if (EditorUtility.DisplayDialog("Confirm Delete", message, "OK", "Cancel"))
                        {
                            foreach (UObject asset in assets)
                            {
                                Undo.DestroyObjectImmediate(asset);
                            }
                        }
                    }
                }
                return false;
            }
            if (Event.current.keyCode != KeyCode.DownArrow && Event.current.keyCode != KeyCode.UpArrow)
            {
                return false;
            }
            sfNotification notification = m_selectedNotification;
            int index = notification.Objects.IndexOf(m_anchorSelectionObject);
            int direction = Event.current.keyCode == KeyCode.DownArrow ? 1 : -1;
            UObject uobj = null;
            while (uobj == null)
            {
                index += direction;
                if (index < 0 || index >= notification.Objects.Count)
                {
                    ksLinkedList<sfNotification> notifications = m_manager.GetNotifications(notification.Category);
                    int noteIndex = notifications.IndexOf(notification);
                    if (noteIndex < 0)
                    {
                        return false;
                    }
                    while (index < 0)
                    {
                        // Moved passed the first object in the notification.
                        // Select the last object in the previous notification.
                        noteIndex--;
                        if (noteIndex < 0)
                        {
                            return false;
                        }
                        notification = notifications[noteIndex];
                        index = notification.Objects.Count - 1;
                    }
                    while (index >= notification.Objects.Count)
                    {
                        // Moved passed the last object in the notification.
                        // Select the first object in the next notification.
                        noteIndex++;
                        if (noteIndex >= notifications.Count)
                        {
                            return false;
                        }
                        notification = notifications[noteIndex];
                        index = 0;
                    }
                }
                if (Event.current.control || Event.current.shift)
                {
                    // When control or shift is pressed, we need to track how far we've moved from where we started
                    m_keyMovement += direction;
                }
                else
                {
                    m_keyMovement = 0;
                }
                uobj = notification.Objects[index];
            }
            // increasing is true if we're moving farther away from our starting position and ctrl or shift is pressed.
            bool increasing = m_keyMovement != 0 && (m_keyMovement > 0 == direction > 0);
            if (Event.current.control)
            {
                // When control is pressed, we add the current object to our selection if it is not selected, and remove
                // it if it is. When moving away from our start position, the current object is the one we move onto,
                // and when moving back it's the one we moved off of.
                List<UObject> selection = new List<UObject>(Selection.objects);
                UObject obj = increasing ? uobj : m_anchorSelectionObject;
                if (!selection.Remove(obj))
                {
                    selection.Add(obj);
                }
                Selection.objects = selection.ToArray();
            }
            else if (Event.current.shift)
            {
                // When shift is pressed, we add objects to the selection when moving away from the start position, and
                // remove them when moving back.
                List<UObject> selection = new List<UObject>(Selection.objects);
                if (increasing)
                {
                    selection.Add(uobj);
                }
                else
                {
                    selection.Remove(m_anchorSelectionObject);
                }
                Selection.objects = selection.ToArray();
            }
            else
            {
                Selection.activeObject = uobj;
            }
            m_anchorSelectionObject = uobj;
            m_anchorSelectionNotification = notification;
            if (m_selectedNotification != notification)
            {
                m_selectedNotification = notification;
                m_expandedNotifications.Add(notification.Message);
            }
            Event.current.Use();
            return true;
        }

        /// <summary>Handles object selection changes due to click events.</summary>
        /// <param name="notification">notification that was clicked.</param>
        /// <param name="uobj">uobj that was clicked.</param>
        private void HandleSelection(sfNotification notification, UObject uobj)
        {
            if (Event.current.shift && m_anchorSelectionNotification == notification)
            {
                // When shift is pressed we select all the objects between the clicked object and the last
                // selected object, if both objects belong to the same notification.
                List<UObject> toSelect = new List<UObject>();
                int anchorCount = 0;
                // Look for the last selected object and the newly clicked one in the notification, and add all objects
                // in between to a list.
                foreach (UObject obj in notification.Objects)
                {
                    if (anchorCount == 1 && obj != null)
                    {
                        toSelect.Add(obj);
                    }
                    if (obj == uobj || obj == m_anchorSelectionObject)
                    {
                        anchorCount++;
                        if (anchorCount > 1)
                        {
                            break;
                        }
                        toSelect.Add(obj);
                    }
                }
                if (anchorCount == 2)
                {
                    // We found both object endpoints in the notification
                    if (Event.current.control)
                    {
                        // If control is pressed, add to the current selection.
                        HashSet<UObject> selectionHash = new HashSet<UObject>(Selection.objects);
                        foreach (UObject obj in toSelect)
                        {
                            selectionHash.Add(obj);
                        }
                        Selection.objects = selectionHash.ToArray();
                    }
                    else
                    {
                        Selection.objects = toSelect.ToArray();
                    }
                    return;
                }
            }
            m_anchorSelectionNotification = notification;
            m_anchorSelectionObject = uobj;
            if (m_selectedNotification != notification)
            {
                m_selectedNotification = notification;
                m_expandedNotifications.Add(notification.Message);
            }
            m_keyMovement = 0;
            if (Event.current.control)
            {
                // If control is pressed, add the clicked object to the selection if it is not already selected, and
                // remove it if it is.
                List<UnityEngine.Object> selection = new List<UnityEngine.Object>(Selection.objects);
                if (!selection.Remove(uobj))
                {
                    selection.Add(uobj);
                }
                Selection.objects = selection.ToArray();
            }
            else
            {
                Selection.activeObject = uobj;
            }
        }

        /// <summary>Checks if an object is selected.</summary>
        /// <param name="uobj">uobj to check.</param>
        /// <returns>true if the object is selected.</returns>
        private bool IsSelected(UObject uobj)
        {
            // Use the selection watcher if we're connected because it's faster.
            return Service.IsConnected ? sfSelectionWatcher.Get().IsSelected(uobj) :
                Selection.Contains(uobj);
        }
    }
}
