using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>
    /// Stores a map of dependencies between elements of type <typeparamref name="T"/>, and provides a method for
    /// sorting the elements so all dependencies of A are iterated before A. Elements without dependencies will not be
    /// iterated, eg. If A depends on B and C and B depends on D, B will be iterated and then A, but C and D will not
    /// because they have no dependencies. Circular dependencies are not allowed.
    /// </summary>
    /// <typeparam name="T">Type of object to track dependencies for.</typeparam>
    public class sfDependencySorter<T> : IEnumerable<T>
    {
        /// <summary>The number of elements with dependencies.</summary>
        public int Count
        {
            get { return m_dependencyMap.Count; }
        }

        private Dictionary<T, List<T>> m_dependencyMap = new Dictionary<T, List<T>>();

        /// <summary>Adds a dependency from <paramref name="item"/> to <paramref name="dependency"/>.</summary>
        /// <param name="item">Item that depends on <paramref name="dependency"/></param>
        /// <param name="dependency">Dependency that <paramref name="item"/> depends on.</param>
        /// <returns>False if the dependency was not added because it would create a circular dependency.</returns>
        public bool Add(T item, T dependency)
        {
            // Don't add the dependency if it would create a circular dependency.
            if (DependsOn(dependency, item))
            {
                return false;
            }

            List<T> dependencies;
            if (!m_dependencyMap.TryGetValue(item, out dependencies))
            {
                dependencies = new List<T>();
                m_dependencyMap[item] = dependencies;
            }
            if (!dependencies.Contains(dependency))
            {
                dependencies.Add(dependency);
            }
            return true;
        }

        /// <summary>
        /// Checks if <paramref name="item"/> depends on <paramref name="dependencyCheck"/>. Dependencies are transitive
        /// so if A depends on B and B depends on C, then A depends on C.
        /// </summary>
        /// <param name="item">Item to check dependencies for.</param>
        /// <param name="dependencyCheck">Dependency to check for.</param>
        /// <returns>True if <paramref name="item"/> depends on <paramref name="dependencyCheck"/>.</returns>
        public bool DependsOn(T item, T dependencyCheck)
        {
            if (EqualityComparer<T>.Default.Equals(item, dependencyCheck))
            {
                return true;
            }
            List<T> dependencies;
            if (m_dependencyMap.TryGetValue(item, out dependencies))
            {
                for (int i = 0; i < dependencies.Count; i++)
                {
                    if (DependsOn(dependencies[i], dependencyCheck))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>Clears all dependencies.</summary>
        public void Clear()
        {
            m_dependencyMap.Clear();
        }

        /// <summary>
        /// Returns a sorted list of dependencies such that all dependencies of an element occur in the list before that
        /// element. Dependencies that do not themselves have any dependencies are not included in the list.
        /// </summary>
        /// <returns>Sorted list of elements with dependencies.</returns>
        public List<T> Sort()
        {
            List<T> list = new List<T>();
            HashSet<T> visited = new HashSet<T>();
            foreach (KeyValuePair<T, List<T>> pair in m_dependencyMap)
            {
                AddSorted(list, pair.Key, pair.Value, visited);
            }
            return list;
        }

        /// <summary>
        /// If <paramref name="item"/> is not in the <paramref name="visited"/> set, adds all of its dependencies to
        /// <paramref name="list"/> that have dependencies and aren't already in the <paramref name="visited"/> set,
        /// then adds <paramref name="item"/> to the <paramref name="list"/>.
        /// </summary>
        /// <param name="list">List to add items to.</param>
        /// <param name="item">Item to add.</param>
        /// <param name="dependencies">Dependencies of item.</param>
        /// <param name="visited">Set of visited items</param>
        private void AddSorted(List<T> list, T item, List<T> dependencies, HashSet<T> visited)
        {
            if (!visited.Add(item))
            {
                return;
            }
            foreach (T dependency in dependencies)
            {
                List<T> nextDependencies;
                if (m_dependencyMap.TryGetValue(dependency, out nextDependencies))
                {
                    AddSorted(list, dependency, nextDependencies, visited);
                }
            }
            list.Add(item);
        }

        /// <summary>
        /// Gets an enumerator to iterate the sorted elements with dependencies. <see cref="Sort"/>.
        /// </summary>
        /// <returns>Enumerator</returns>
        public IEnumerator<T> GetEnumerator()
        {
            return Sort().GetEnumerator();
        }

        /// <summary>
        /// Gets an enumerator to iterate the sorted elements with dependencies. <see cref="Sort"/>.
        /// </summary>
        /// <returns>Enumerator</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
