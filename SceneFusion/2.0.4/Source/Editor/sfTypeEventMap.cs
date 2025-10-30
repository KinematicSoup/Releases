using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KS.SF.Reactor;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>Maps types to event handlers of delegate type T.</summary>
    public class sfTypeEventMap<T> : sfEventMap<Type, T> where T : class
    {
        /// <summary>Gets a delegate that combines all handlers for a type and its ancestors.</summary>
        /// <param name="type">type to get handlers for.</param>
        public override T GetHandlers(Type type)
        {
            return GetHandlers(type, true);
        }

        /// <summary>Gets a delegate that combines all event handlers for a type.</summary>
        /// <param name="type">type to get handlers for.</param>
        /// <param name="checkInheritance">if true, will also get handlers for ancestor types.</param>
        /// <returns>delegate for the handlers, or null if the type has no handlers.</returns>
        public T GetHandlers(Type type, bool checkInheritance)
        {
            T handlers = null;
            if (m_map != null)
            {
                if (checkInheritance)
                {
                    foreach (KeyValuePair<Type, ksEvent<T>> pair in m_map)
                    {
                        if (pair.Key.IsAssignableFrom(type))
                        {
                            if (handlers == null)
                            {
                                handlers = pair.Value.Execute;
                            }
                            else
                            {
                                handlers = (T)(object)Delegate.Combine(
                                    (Delegate)(object)handlers, (Delegate)(object)pair.Value.Execute);
                            }
                        }
                    }
                }
                else
                {
                    ksEvent<T> ev;
                    if (m_map.TryGetValue(type, out ev))
                    {
                        handlers = ev.Execute;
                    }
                }
            }
            return handlers;
        }

        /// <summary>Adds a handler for a type Key.</summary>
        /// <param name="handler">handler to add.</param>
        public void Add<Key>(T handler)
        {
            this[typeof(Key)] += handler;
        }

        /// <summary>Adds a handler for a type Key.</summary>
        /// <param name="type">type to add handler for.</param>
        /// <param name="handler">handler to add.</param>
        public void Add(Type type, T handler)
        {
            this[type] += handler;
        }

        /// <summary>Removes a handler for a type Key.</summary>
        /// <param name="handler">handler to remove.</param>
        public void Remove<Key>(T handler)
        {
            this[typeof(Key)] -= handler;
        }

        /// <summary>Removes a handler for a type Key.</summary>
        /// <param name="type">type to remove handler for.</param>
        /// <param name="handler">handler to remove.</param>
        public void Remove(Type type, T handler)
        {
            this[type] -= handler;
        }
    }
}
