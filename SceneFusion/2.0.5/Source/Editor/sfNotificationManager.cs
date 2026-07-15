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
using KS.SF.Reactor;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>Manages notifications that are applied to uobjects.</summary>
    public class sfNotificationManager : IEnumerable<KeyValuePair<sfNotificationCategory, ksLinkedList<sfNotification>>>
    {
        /// <summary>Singleton instance</summary>
        public static sfNotificationManager Get()
        {
            return m_instance;
        }
        private static sfNotificationManager m_instance = new sfNotificationManager();

        /// <summary>Change event handler</summary>
        public delegate void ChangeHandler();

        /// <summary>
        /// Invoked when a notifiction is added or removed, or uobjects are added or removed to a notification.
        /// </summary>
        public event ChangeHandler OnChange;

        private ksLinkedList<sfNotificationCategory> m_categories = new ksLinkedList<sfNotificationCategory>();
        private Dictionary<sfNotificationCategory, ksLinkedList<sfNotification>> m_categoryNotificationMap =
            new Dictionary<sfNotificationCategory, ksLinkedList<sfNotification>>();
        private Dictionary<UObject, ksLinkedList<sfNotification>> m_objectNotificationMap =
            new Dictionary<UObject, ksLinkedList<sfNotification>>();
        private Dictionary<KeyValuePair<sfNotificationCategory, string>, sfNotification> m_notifications =
            new Dictionary<KeyValuePair<sfNotificationCategory, string>, sfNotification>();

        /// <summary>Do we have any notifications?</summary>
        public bool HasNotifications
        {
            get { return m_categories.Count > 0; }
        }

        /// <summary>Last notification</summary>
        public sfNotificationCategory LastNotificationCategory
        {
            get
            {
                return m_categories.Last;
            }
        }

        /// <summary>Number of notifications</summary>
        public int Count
        {
            get
            {
                return m_count;
            }
        }
        private int m_count = 0;

        /// <summary>Creates or finds an existing notification and adds uobjects to it.</summary>
        /// <param name="category">category for the notification.</param>
        /// <param name="message">message for the notification.</param>
        /// <param name="objects">objects to add to the notification.</param>
        /// <returns>notification</returns>
        public sfNotification Create(sfNotificationCategory category, string message, params UObject[] objects)
        {
            KeyValuePair<sfNotificationCategory, string> pair = 
                new KeyValuePair<sfNotificationCategory, string>(category, message);
            sfNotification notification;
            if (!m_notifications.TryGetValue(pair, out notification))
            {
                notification = new sfNotification(category, message);
                m_notifications[pair] = notification;
            }
            Add(notification);
            foreach (UObject uobj in objects)
            {
                AddNotificationTo(notification, uobj);
            }
            return notification;
        }

        /// <summary>
        /// Removes a notification from some uobjects. If this is called with no uobjects or the notification
        /// has no uobjects, removes the notification. If a notification was added to a uobject multiple times,
        /// it must be removed the same number of times to remove it.
        /// </summary>
        /// <param name="category">category of notification.</param>
        /// <param name="message">message of notification.</param>
        /// <param name="objects">
        /// objects to remove. Will remove the notification if no objects are
        /// provided.
        /// </param>
        public void Remove(sfNotificationCategory category, string message, params UObject[] objects)
        {
            KeyValuePair<sfNotificationCategory, string> pair =
                new KeyValuePair<sfNotificationCategory, string>(category, message);
            sfNotification notification;
            if (!m_notifications.TryGetValue(pair, out notification))
            {
                return;
            }
            if (objects.Length == 0)
            {
                Remove(notification);
                notification.ClearObjects();
                return;
            }
            foreach (UObject uobj in objects)
            {
                RemoveNotificationFrom(notification, uobj);
            }
        }

        /// <summary>Clears all notifications.</summary>
        public void Clear()
        {
            foreach (KeyValuePair<sfNotificationCategory, ksLinkedList<sfNotification>> pair in m_categoryNotificationMap)
            {
                foreach (sfNotification notification in pair.Value)
                {
                    notification.IsActive = false;
                    notification.ClearObjects();
                }
            }
            m_categories.Clear();
            m_categoryNotificationMap.Clear();
            m_objectNotificationMap.Clear();
            m_count = 0;
        }

        /// <summary>Gets all active notifications for a category.</summary>
        /// <param name="category">category to get notifications for.</param>
        /// <returns>
        /// notifications for the category, or null if the category has no
        /// notifications.
        /// </returns>
        public ksLinkedList<sfNotification> GetNotifications(sfNotificationCategory category)
        {
            ksLinkedList<sfNotification> notifications;
            m_categoryNotificationMap.TryGetValue(category, out notifications);
            return notifications;
        }

        /// <summary>Gets all active notifications for a uobject.</summary>
        /// <param name="uobj">uobj to get notifications for.</param>
        /// <param name="includeComponentNotifications">
        /// if true and the uobj is a game object, will also get
        /// notifications for the game object's components.
        /// </param>
        /// <returns>
        /// notifications for the uobject, or null if the uobject has no
        /// notifications.
        /// </returns>
        public ksLinkedList<sfNotification> GetNotifications(UObject uobj, bool includeComponentNotifications = false)
        {
            ksLinkedList<sfNotification> notifications;
            m_objectNotificationMap.TryGetValue(uobj, out notifications);
            if (includeComponentNotifications)
            {
                GameObject gameObject = uobj as GameObject;
                if (gameObject != null)
                {
                    // Combine all notifications from the game object and its components.
                    HashSet<sfNotification> notificationSet = null;
                    foreach (Component component in gameObject.GetComponents<Component>())
                    {
                        if (component == null)
                        {
                            continue;
                        }
                        ksLinkedList<sfNotification> componentNotifications = new ksLinkedList<sfNotification>();
                        if (m_objectNotificationMap.TryGetValue(component, out componentNotifications))
                        {
                            if (notifications == null)
                            {
                                notifications = componentNotifications;
                            }
                            else
                            {
                                if (notificationSet == null)
                                {
                                    notificationSet = new HashSet<sfNotification>(notifications);
                                }
                                foreach (sfNotification notification in componentNotifications)
                                {
                                    notificationSet.Add(notification);
                                }
                            }
                        }
                    }
                    if (notificationSet != null)
                    {
                        notifications = new ksLinkedList<sfNotification>(notificationSet);
                    }
                }
            }
            return notifications;
        }

        /// <summary>Adds a notification.</summary>
        /// <param name="notification">notification to add.</param>
        public void Add(sfNotification notification)
        {
            if (notification.IsActive)
            {
                return;
            }
            notification.IsActive = true;
            ksLog.Warning(this, notification.Message);
            m_count++;

            // Add notification to uobjects
            foreach (UObject uobj in notification.Objects)
            {
                AddObjectNotification(uobj, notification);
            }

            // Add notification to category
            ksLinkedList<sfNotification> notifications = GetNotifications(notification.Category);
            if (notifications == null)
            {
                notifications = new ksLinkedList<sfNotification>();
                m_categoryNotificationMap[notification.Category] = notifications;
                m_categories.Add(notification.Category);
            }
            notifications.Add(notification);
            if (OnChange != null)
            {
                OnChange();
            }
        }

        /// <summary>Removes a notification.</summary>
        /// <param name="notification">notification to remove.</param>
        public void Remove(sfNotification notification)
        {
            if (!notification.IsActive)
            {
                return;
            }
            notification.IsActive = false;
            m_count--;

            // Remove notification from uobjects
            foreach (UObject uobj in notification.Objects)
            {
                RemoveObjectNotification(uobj, notification);
            }

            // Remove notification from category
            ksLinkedList<sfNotification> notifications = GetNotifications(notification.Category);
            if (notifications != null && notifications.Remove(notification) && notifications.Count == 0)
            {
                m_categoryNotificationMap.Remove(notification.Category);
                m_categories.Remove(notification.Category);
            }
            if (OnChange != null)
            {
                OnChange();
            }
        }

        /// <summary>Adds a notification to a uobject.</summary>
        /// <param name="notification">notification to add.</param>
        /// <param name="uobj">uobj to add notification to.</param>
        /// <returns>
        /// number of times the notification was added to the uobject. It must be removed this number
        /// of times to fully remove it.
        /// </returns>
        public int AddNotificationTo(sfNotification notification, UObject uobj)
        {
            int count = notification.Add(uobj);
            if (count != 1)
            {
                return count;
            }
            if (notification.IsActive)
            {
                AddObjectNotification(uobj, notification);
            }
            else
            {
                Add(notification);
            }
            if (OnChange != null)
            {
                OnChange();
            }
            return count;
        }

        /// <summary>
        /// Removes a notification from a uobject. If a notification was added to a uobject multiple times, it
        /// must be removed to same number of times to remove it, unless forceRemove is true.
        /// </summary>
        /// <param name="notification">notification to remove.</param>
        /// <param name="uobj">uobj to remove notification from.</param>
        /// <param name="removeIfNoObjects">
        /// if true, will remove the notification if the notification has no objects.
        /// </param>
        /// <param name="forceRemove">
        /// if true, will remove the notification from the uobject even if it was added
        /// multiple times.
        /// </param>
        /// <returns>
        /// number of remaining times the notification was added to the uobject. It must be removed
        /// again this number of times to fully remove it. Returns -1 if the notification could not be removed
        /// because it wasn't found on the uobject.
        /// </returns>
        public int RemoveNotificationFrom(sfNotification notification,
            UObject uobj,
            bool removeIfNoObjects = true,
            bool forceRemove = false)
        {
            int count = notification.Remove(uobj, forceRemove);
            if (count == 0 && notification.IsActive)
            {
                RemoveObjectNotification(uobj, notification);
                if (notification.Objects.Count == 0 && removeIfNoObjects)
                {
                    Remove(notification);
                }
                if (OnChange != null)
                {
                    OnChange();
                }
            }
            return count;
        }

        /// <summary>Removes all notifications for a uobject.</summary>
        /// <param name="uobj">uobj to remove notifications for.</param>
        public void RemoveNotificationsFor(UObject uobj)
        {
            ksLinkedList<sfNotification> notifications = GetNotifications(uobj);
            if (notifications == null)
            {
                return;
            }
            foreach (sfNotification notification in notifications)
            {
                RemoveNotificationFrom(notification, uobj, true, true);
            }
        }

        /// <summary></summary>
        /// <returns>
        /// enumerator for the
        /// notification categories.
        /// </returns>
        public IEnumerator<KeyValuePair<sfNotificationCategory, ksLinkedList<sfNotification>>> GetEnumerator()
        {
            foreach (sfNotificationCategory category in m_categories)
            {
                ksLinkedList<sfNotification> notifications = GetNotifications(category);
                yield return
                    new KeyValuePair<sfNotificationCategory, ksLinkedList<sfNotification>>(category, notifications);
            }
        }

        /// <summary></summary>
        /// <returns>enumerator for the notification categories.</returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>Adds a notification for a uobject to the object notification map.</summary>
        /// <param name="uobj">uobj to add notification for.</param>
        /// <param name="notification">notification to add.</param>
        private void AddObjectNotification(UObject uobj, sfNotification notification)
        {
            ksLinkedList<sfNotification> objectNotifications;
            if (!m_objectNotificationMap.TryGetValue(uobj, out objectNotifications))
            {
                objectNotifications = new ksLinkedList<sfNotification>();
                m_objectNotificationMap[uobj] = objectNotifications;
                sfUI.Get().MarkIconWindowsStale(uobj);
            }
            objectNotifications.Add(notification);
        }

        /// <summary>Removes a notification for a uobject from the object notification map.</summary>
        /// <param name="uobj">uobj to remove notification for.</param>
        /// <param name="notification">notification to remove.</param>
        private void RemoveObjectNotification(UObject uobj, sfNotification notification)
        {
            ksLinkedList<sfNotification> objectNotifications;
            if (m_objectNotificationMap.TryGetValue(uobj, out objectNotifications))
            {
                objectNotifications.Remove(notification);
                if (objectNotifications.Count == 0)
                {
                    m_objectNotificationMap.Remove(uobj);
                    sfUI.Get().MarkIconWindowsStale(uobj);
                }
            }
        }
    }
}
