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
using KS.SF.Reactor;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>A notification with a message, category and list of uobjects it applies to.</summary>
    public class sfNotification
    {
        /// <summary>Category</summary>
        public sfNotificationCategory Category
        {
            get { return m_category; }
        }
        private sfNotificationCategory m_category;

        /// <summary>Notifaction message</summary>
        public string Message
        {
            get { return m_message; }
        }
        private string m_message;

        /// <summary>List of uobjects the notification applies to.</summary>
        public ksConstList<UObject> Objects
        {
            get { return m_constObjects; }
        }
        private ksConstList<UObject> m_constObjects;

        private ksLinkedList<UObject> m_objects = new ksLinkedList<UObject>();
        private Dictionary<UObject, int> m_objectCounts = new Dictionary<UObject, int>();

        /// <summary>Is the notification in the notification window?</summary>
        public bool IsActive
        {
            get { return m_active; }
            internal set { m_active = value; }
        }
        private bool m_active = false;

        private static sfNotificationManager m_manager = sfNotificationManager.Get();

        /* Public API */

        /// <summary>Creates or finds an existing notification and adds uobjects to it.</summary>
        /// <param name="category">category for the notification.</param>
        /// <param name="message">message for the notification.</param>
        /// <param name="objects">objects to add to the notification.</param>
        /// <returns>notification</returns>
        public static sfNotification Create(sfNotificationCategory category, string message, params UObject[] objects)
        {
            return m_manager.Create(category, message, objects);
        }

        /// <summary>
        /// Removes a notification from some uobjects. If this removes all uobjects from the notification or is
        /// called without uobjects, removes the notification. If a notification was added to a uobject multiple
        /// times, it must be removed the same number of times to remove it.
        /// </summary>
        /// <param name="category">category of notification.</param>
        /// <param name="message">message of notification.</param>
        /// <param name="objects">
        /// objects to remove. Will remove the notification if no objects are
        /// provided.
        /// </param>
        public static void Remove(sfNotificationCategory category, string message, params UObject[] objects)
        {
            m_manager.Remove(category, message, objects);
        }

        /// <summary>
        /// Adds the notification to a uobject. A notification can be added to the same uobject multiple times
        /// and must be removed the same number of times to remove it.
        /// </summary>
        /// <param name="uobj">uobj to add the notification to.</param>
        /// <returns>
        /// number of times the notification was added to the uobject. It must be removed this number
        /// of times to fully remove it.
        /// </returns>
        public int AddToObject(UObject uobj)
        {
            return m_manager.AddNotificationTo(this, uobj);
        }

        /// <summary>
        /// Removes the notification from a uobject. A notification can be added to the same uobject multiple
        /// times and must be removed the same number of times to remove it.
        /// </summary>
        /// <param name="uobj">uobj to remove the notification from.</param>
        /// <returns>
        /// number of remaining times the notification was added to the uobject. It must be removed
        /// again this number of times to fully remove it. Returns -1 if the notification could not be removed
        /// because it wasn't found on the uobject.
        /// </returns>
        public int RemoveFromObject(UObject uobj)
        {
            return m_manager.RemoveNotificationFrom(this, uobj);
        }

        /// <summary>Removes the notification from all uobjects and the notification window.</summary>
        public void Clear()
        {
            m_manager.Remove(this);
        }

        /* Internal Methods */

        /// <summary>Constructor</summary>
        /// <param name="category"></param>
        /// <param name="message"></param>
        internal sfNotification(sfNotificationCategory category, string message)
        {
            m_category = category;
            m_message = message;
            m_constObjects = new ksConstList<UObject>(m_objects);
        }

        /// <summary>
        /// Adds a uobject to the notification, tracking how many times it has been added. It must be removed the
        /// same number of times it has been added to remove it. This should only be used by the NotificationManager.
        /// </summary>
        /// <param name="uobj">uobj to add.</param>
        /// <returns>number of times the uobject was added to this notification.</returns>
        internal int Add(UObject uobj)
        {
            int count;
            if (!m_objectCounts.TryGetValue(uobj, out count))
            {
                m_objects.Add(uobj);
                m_objectCounts[uobj] = 1;
                return 1;
            }
            m_objectCounts[uobj] = count + 1;
            return count + 1;
        }

        /// <summary>
        /// Removes a uobject from the notification. If the uobject was added multiple times, this must be called the
        /// same number of times to remove it unless forceRemove is true. This should only be used by the
        /// sfNotificationManager.
        /// </summary>
        /// <param name="uobj">uobj to remove.</param>
        /// <param name="forceRemove">if true, removes the notification even if it was added multiple times.</param>
        /// <returns>
        /// the number of remaining times the uobject was added to the notification, or -1 if it was
        /// never added.
        /// </returns>
        internal int Remove(UObject uobj, bool forceRemove)
        {
            int count;
            if (!m_objectCounts.TryGetValue(uobj, out count))
            {
                return -1;
            }
            count--;
            if (count <= 0 || forceRemove)
            {
                m_objects.Remove(uobj);
                m_objectCounts.Remove(uobj);
                return 0;
            }
            m_objectCounts[uobj] = count;
            return count;
        }

        /// <summary>
        /// Removes all objects from the notification. This should only be used by the NotificationManager.
        /// </summary>
        internal void ClearObjects()
        {
            m_objects.Clear();
            m_objectCounts.Clear();
        }
    }
}
