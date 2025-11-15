using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KS.SF.Reactor;

namespace KS.SceneFusion2.Unity.Editor
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
