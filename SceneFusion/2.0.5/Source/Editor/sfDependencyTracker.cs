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
using UnityEngine.Tilemaps;
using KS.SF.Reactor;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>
    /// Stores a hard-coded mapping of dependencies between Unity components. Unity does not expose dependencies for
    /// their own components.
    /// </summary>
    public partial class sfDependencyTracker
    {
        /// <summary></summary>
        /// <returns>singleton instance</returns>
        public static sfDependencyTracker Get()
        {
            return m_instance;
        }
        private static sfDependencyTracker m_instance = new sfDependencyTracker();

        // Maps component types to the types they depend on.
        private Dictionary<Type, List<Type>> m_dependencies = new Dictionary<Type, List<Type>>();

        /// <summary>Singleton constructor</summary>
        private sfDependencyTracker()
        {
#pragma warning disable CS0618// Ignore obsolete warning. FlareLayer is obsolete in 6.5 but still has a dependency on Camera.
            RegisterDependency<FlareLayer, Camera>();
#pragma warning restore CS0618
            RegisterDependency<StreamingController, Camera>();
            RegisterDependency<Joint, Rigidbody>();
            RegisterDependency<ConstantForce, Rigidbody>();
            RegisterDependency<Joint2D, Rigidbody2D>();
            RegisterDependency<ConstantForce2D, Rigidbody2D>();
            RegisterDependency<CompositeCollider2D, Rigidbody2D>();
            RegisterDependency<Cloth, SkinnedMeshRenderer>();
            RegisterDependency<TextMesh, MeshRenderer>();
            RegisterDependency<TilemapRenderer, Tilemap>();
            RegisterDependency<TilemapCollider2D, Tilemap>();
        }

        /// <summary>Checks if component type1 depends on type2 or one of its base types.</summary>
        /// <param name="type1"></param>
        /// <param name="type2"></param>
        /// <param name="requiredType">
        /// requiredType that type1 depends on. This is either type2, a base type of type2, or null
        /// if type1 does not depend on type2 or any of its base types.
        /// </param>
        /// <returns>true if type1 depends on type2 or one of its base types.</returns>
        public bool DependsOn(Type type1, Type type2, out Type requiredType)
        {
            List<Type> dependencies = GetDependencies(type1);
            if (dependencies.Count > 0)
            {
                requiredType = type2;
                while (requiredType != null && requiredType != typeof(Component))
                {
                    if (dependencies.Contains(requiredType))
                    {
                        return true;
                    }
                    requiredType = requiredType.BaseType;
                }
            }
            requiredType = null;
            return false;
        }

        /// <summary>Gets the required component types for a component type.</summary>
        /// <param name="type">type to get required types for.</param>
        /// <returns>required types for the type.</returns>
        private List<Type> GetDependencies(Type type)
        {
            List<Type> dependencies = new List<Type>();
            if (type.IsSubclassOf(typeof(MonoBehaviour)))
            {
                // Monobehaviour dependencies are declared using the RequireComponent attribute
                while (type != typeof(MonoBehaviour))
                {
                    object[] requirements = type.GetCustomAttributes(typeof(RequireComponent), true);
                    foreach (RequireComponent dependency in requirements)
                    {
                        if (dependency.m_Type0 != null)
                        {
                            dependencies.Add(dependency.m_Type0);
                        }
                        if (dependency.m_Type1 != null)
                        {
                            dependencies.Add(dependency.m_Type1);
                        }
                        if (dependency.m_Type2 != null)
                        {
                            dependencies.Add(dependency.m_Type2);
                        }
                    }
                    type = type.BaseType;
                }
            }
            else
            {
                // Check hard-coded map for Unity component dependencies
                while (type != null && type != typeof(Component))
                {
                    List<Type> list;
                    if (m_dependencies.TryGetValue(type, out list))
                    {
                        foreach (Type t in list)
                        {
                            dependencies.Add(t);
                        }
                    }
                    type = type.BaseType;
                }
            }
            return dependencies;
        }

        /// <summary>Registers a dependency between two component types.</summary>
        private void RegisterDependency<Dependent, Required>()
            where Dependent : Component
            where Required : Component
        {
            Type dependentType = typeof(Dependent);
            Type requiredType = typeof(Required);
            List<Type> dependencies;
            if (!m_dependencies.TryGetValue(dependentType, out dependencies))
            {
                dependencies = new List<Type>();
                m_dependencies[dependentType] = dependencies;
            }
            dependencies.Add(requiredType);
        }
    }
}
