/*
KINEMATICSOUP CONFIDENTIAL
 Copyright(c) 2014-2025 KinematicSoup Technologies Incorporated 
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

using UnityEngine;
using UnityEngine.Serialization;

#if !UNITY_6000_0_OR_NEWER
using PhysicsMaterial = UnityEngine.PhysicMaterial;
#endif

namespace KS.Reactor.Client.Unity
{
    /// <summary>
    /// Physics configuration settings.
    /// For more information about these properties see the PhysX documentation.
    /// <see href="https://gameworksdocs.nvidia.com/PhysX/4.1/documentation/physxguide/Index.html"/>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu(ksMenuNames.REACTOR + "ksPhysicsSettings")]
    public class ksPhysicsSettings : MonoBehaviour
    {
        /// <summary>
        /// PhysX solvers used for physics simulations.
        /// </summary>
        public enum SolverTypes
        {
            /// <summary>Iterative solver using Projected Gauss Seidel Algorithm.</summary>
            ProjectedGaussSeidel,
            /// <summary>
            /// Newer non-linear iterative solver, better convergence rate but may have instability for constraints 
            /// with very lowor zero tolerances.
            /// </summary>
            TemporalGaussSeidel
        }

        [SerializeField]
        [Tooltip("Gravity applied to all dynamic entities in the scene.")]
        private Vector3 m_gravity = new ksVector3(0.0f, -9.81f, 0.0f);
        /// <summary>
        /// Gravity applied to all dynamic entities in the scene.
        /// </summary>
        public Vector3 Gravity { get { return m_gravity; } }

        [SerializeField]
        [Tooltip("A contact with a relative velocity below this will not bounce.")]
        [Min(0f)]
        private float m_bounceThreshold = 2.0f;
        /// <summary>
        /// A contact with a relative velocity below this will not bounce.
        /// </summary>
        public float BounceThreshold { get { return m_bounceThreshold; } }

        [SerializeField]
        [Tooltip("Report hits with back faces of mesh triangles.")]
        private bool m_queryHitBackFaces = false;
        /// <summary>
        /// Report hits with back faces of mesh triangles.
        /// </summary>
        public bool QueryHitBackFaces { get { return m_queryHitBackFaces; } }

        [SerializeField]
        [Tooltip("Enable adaptive forces to accelerate convergence of the solver.")]
        private bool m_enableAdaptiveForce = false;
        /// <summary>
        /// Enable adaptive forces to accelerate convergence of the solver.
        /// </summary>
        public bool EnableAdaptiveForce { get { return m_enableAdaptiveForce; } }

        [SerializeField]
        [Tooltip("Provides improved determinism at the expense of performance.")]
        private bool m_enableEnhancedDeterminism = false;
        /// <summary>
        /// Provides improved determinism at the expense of performance.
        /// </summary>
        public bool EnableEnhancedDeterminism { get { return m_enableEnhancedDeterminism; } }

        [Tooltip("Whether or not to automatically sync transform changes with the physic system before doing " +
           "physics queries, and to recalculate inertia tensor and center of mass when they are accessed for rigid " +
           "bodies with modified colliders.")]
        /// <summary>
        /// Whether or not to automatically sync transform changes with the physic system before doing physics queries,
        /// and to recalculate inertia tensor and center of mass when they are accessed for rigidbodies with modified
        /// colliders.
        /// </summary>
        public bool AutoSync = true;

        [SerializeField]
        [Tooltip("See PhysX documentation")]
        private SolverTypes m_solverType = SolverTypes.ProjectedGaussSeidel;
        /// <summary>
        /// See PhysX documentation
        /// </summary>
        public SolverTypes SolverType { get { return m_solverType; } }

        [Tooltip("The default collision filter that will be used if a collider does not have its own collision filter assigned.")]
        /// <summary>
        /// The default collision filter that will be used if a collider does not have its own collision filter assigned.
        /// </summary>
        public ksCollisionFilterAsset DefaultCollisionFilter;

        [FormerlySerializedAs("m_defaultMaterial")]
        [Tooltip("The default physics material that will be used if a collider does not have its own physics material assigned.")]
        /// <summary>
        /// The default physics material that will be used if a collider does not have its own physics material assigned.
        /// </summary>  
        public PhysicsMaterial DefaultMaterial = null;

        [Tooltip("The default physics settings for entities in the scene. These can be overriden per entity in ksEntityComponent components.")]
        /// <summary>
        /// The default physics settings for entities in the scene. These can be overriden per entity in ksEntityComponent components.
        /// </summary>
        public ksEntityPhysicsSettings DefaultEntityPhysics = new ksEntityPhysicsSettings();

        [Header("Debugging")]
        [SerializeField]
        [Tooltip("If enabled, local physx servers will attempt to connect to a running instance of a PhysX Visual Debugger")]
        private bool m_usePhysXVisualDebugger = false;
        /// <summary>
        /// If enabled, local physx servers will attempt to connect to a running instance of a PhysX Visual Debugger"
        /// </summary>
        public bool UsePhysXVisualDebugger
        {
            get { return m_usePhysXVisualDebugger; }
            set { m_usePhysXVisualDebugger = value; }
        }

        [SerializeField]
        [Tooltip("Visual debugger host address")]
        private string m_pvdAddress = "127.0.0.1";
        /// <summary>
        /// Visual debugger host address
        /// </summary>
        public string PVDAddress { get { return m_pvdAddress; } }

        [SerializeField]
        [Tooltip("Visual debugger port")]
        private ushort m_pvdPort = 5425;
        /// <summary>
        /// Visual debugger port
        /// </summary>
        public ushort PVDPort { get { return m_pvdPort; } }
    }
}
