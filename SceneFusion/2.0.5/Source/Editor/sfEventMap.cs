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
using KS.SF.Reactor;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>Maps Keys of type K to event handlers of delegate type T.</summary>
    public class sfEventMap<K, T> where T : class
    {
        protected Dictionary<K, ksEvent<T>> m_map;

        /// <summary>Gets the event for a key.</summary>
        public ksEvent<T> this[K key]
        {
            get
            {
                if (m_map == null)
                {
                    m_map = new Dictionary<K, ksEvent<T>>();
                }
                ksEvent<T> ev;
                if (!m_map.TryGetValue(key, out ev))
                {
                    ev = new ksEvent<T>();
                    m_map[key] = ev;
                }
                return ev;
            }

            set
            {
                // Do nothing. This setter is needed to make += and -= syntax work.
            }
        }

        /// <summary>Gets a delegate that combines all event handlers for a key.</summary>
        /// <param name="key">key to get handlers for.</param>
        /// <returns>delegate for the handlers, or null if the key has no handlers.</returns>
        public virtual T GetHandlers(K key)
        {
            T handlers = null;
            if (m_map != null)
            {
                ksEvent<T> ev;
                if (m_map.TryGetValue(key, out ev))
                {
                    handlers = ev.Execute;
                }
            }
            return handlers;
        }
    }
}
