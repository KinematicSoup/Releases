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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using System;

#if !UNITY_6000_0_OR_NEWER
using PhysicsMaterial = UnityEngine.PhysicMaterial;
#endif

namespace KS.Reactor.Client.Unity
{
    /// <summary>Does the component exist on the client, server, or both?</summary>
    public enum ksExistenceModes
    {
        /// <summary>
        /// The component is included in server configs and is not deleted on the client when the entity is created.
        /// </summary>
        CLIENT_AND_SERVER = 0,
        /// <summary>The component is not included in server configs.</summary>
        CLIENT_ONLY = 1,
        /// <summary>The component is deleted on the client when the entity is created.</summary>
        SERVER_ONLY = 2
    }

    /// <summary>
    /// Attach this to your GameObjects to make the server aware of them when you publish your scene.
    /// </summary>
    [ksHideInOwnershipManager]
    [DisallowMultipleComponent]
    [AddComponentMenu(ksMenuNames.REACTOR + "ksEntityComponent")]
    public class ksEntityComponent : ksScriptsComponent<ksEntity, ksEntityScript>
    {
        /// <summary>Collision filter for entity groups, notifications, and interactions.</summary>
        [Tooltip("Collision filter for entity groups, notifications, and interactions.")]
        [ksAsset]
        public ksCollisionFilterAsset CollisionFilter = null;

        /// <summary>The physics material for colliders on this entity to use if they do not have a material set.</summary>
        [Tooltip("The physics material for colliders on this entity to use if they do not have a material set.")]
        [ksAsset]
        public PhysicsMaterial PhysicsMaterial;

        [Tooltip("Permanent entities are static scene entities that can never move or be deleted. They are not synced " +
            "over the network since the client will already know where they are. Permanent entities can use Unity's " +
            "static batching to optimize draw calls.")]
        [SerializeField]
        private bool m_isPermanent = true;

        /// <summary>
        /// If true, the gameobject will be destroyed when the server destroys the entity or when 
        /// <see cref="ksBaseRoom{Player, Entity}.CleanUp"/> is called after disconnecting. Not applicable for
        /// permanent entities. If false, you should call <see cref="CleanUp"/> when you no longer need the
        /// game object to ensure it gets cleaned up properly.
        /// </summary>
        [Tooltip("If true, the gameobject will be destroyed when the server destroys the entity or when " +
            "Room.CleanUp is called after disconnecting. If false, you should call ReleaseGameObject when you no " +
            "longer need the game object to ensure it gets cleaned up properly")]
        public bool DestroyWithServer = true;

        /// <summary>If true, the entity will be destroyed when its owner disconnects.</summary>
        [Tooltip("If true, the entity will be destroyed when its owner disconnects.")]
        public bool DestroyOnOwnerDisconnect = true;

        [Tooltip("Default sync group the entity will be placed in. This entity will not be synced to players who " +
            "are not in the same sync group. Sync group 0 entities are always synced to all players.")]
        [SerializeField]
        public uint SyncGroup = 0;

        /// <summary>
        /// If true, uses <see cref="Predictor"/> instead of the default from 
        /// <see cref="ksRoomType.DefaultEntityPredictor"/>.
        /// </summary>
        public bool OverridePredictor = false;

        /// <summary>
        /// Predictor to use when the entity does not have a player controller that uses input prediction. If 
        /// <see cref="OverridePredictor"/> is false, uses <see cref="ksRoomType.DefaultEntityPredictor"/> instead.
        /// </summary>
        [Tooltip("Predictor to use when the entity does not have a player controller that uses input prediction. " +
            "(unchecked = use default room settings)")]
        [ksAsset]
        public ksPredictor Predictor;

        /// <summary>
        /// If true, uses <see cref="ControllerPredictor"/> instead of the default from 
        /// <see cref="ksRoomType.DefaultEntityControllerPredictor"/>.
        /// </summary>
        public bool OverrideControllerPredictor = false;

        /// <summary>
        /// Predictor to use when the entity has a player controller that uses input prediction. If
        /// <see cref="OverrideControllerPredictor"/> is false, uses
        /// <see cref="ksRoomType.DefaultEntityControllerPredictor"/> instead.
        /// </summary>
        [Tooltip("Predictor to use when the entity has a player controller that uses input prediction." +
            "(unchecked = use default room settings)")]
        [ksAsset]
        public ksPredictor ControllerPredictor;

        /// <summary>Does the <see cref="Rigidbody"/> exist on the client, server, or both?</summary>
        [HideInInspector]
        [ksEnum]
        [Tooltip("Does this rigid body exist on the client, server, or both?")]
        [FormerlySerializedAs("RigidBodyExistance")]
        public ksExistenceModes RigidBodyExistence;

        /// <summary>
        /// Override room <see cref="ksPhysicsSettings"/> per entity. A value of -1 means that the 
        /// value in the room <see cref="ksPhysicsSettings"/> will be used.
        /// </summary>
        [Tooltip("Override room phyiscs settings per entity. A value of -1 means that the value in the room" +
            "physics settings will be used.")]
        public ksEntityPhysicsSettings PhysicsOverrides = new ksEntityPhysicsSettings(
            -1f, -1f, -1f, -1f, -1f, -1, -1, ksRigidBodyModes.DEFAULT);

        /// <summary>
        /// Override the precision for this entity used when syncing transform data from servers to clients. 
        /// A value of -1 means that the value in the <see cref="ksRoomType"/> will be used.
        /// </summary>
        [Tooltip("Override the precision for this entity used when syncing transform data from servers to clients.")]
        public ksTransformPrecisions TransformPrecisionOverrides = new ksTransformPrecisions(-1f, -1f, -1f);

        /// <summary>Asset Id</summary>
        [ksReadOnly]
        public uint AssetId = 0;

        /// <summary>Entity Id</summary>
        [ksReadOnly]
        public uint EntityId = 0;

        /// <summary>Entity Type</summary>
        [HideInInspector]
        public string Type = "";

        /// <summary>Is Static</summary>
        [HideInInspector]
        public bool IsStatic = false;

        /// <summary>Entity</summary>
        public ksEntity Entity
        {
            get { return ParentObject; }
        }

        /// <summary>Is this a permanent entity? Permanent entities never move and cannot be deleted.</summary>
        public bool IsPermanent
        {
            get { return IsStatic && m_isPermanent; }
            set { m_isPermanent = value; }
        }

        /// <summary>Is this entity component part of the initial scene?</summary>
        public bool IsSceneEntity
        {
            get { return m_isSceneEntity; }
            internal set { m_isSceneEntity = value; }
        }
        private bool m_isSceneEntity;

        /// <summary>
        /// Is this game object being used for an entity? Set to true when linked to an entity, and to false again when
        /// <see cref="CleanUp"/> is called after the entity is destroyed.
        /// </summary>
        public bool IsInUse
        {
            get { return m_isInUse; }
            internal set { m_isInUse = value; }
        }
        private bool m_isInUse;

        /// <summary>Entity factory used to spawn this entity.</summary>
        public ksEntityFactory Factory
        {
            get { return m_factory; }
            internal set { m_factory = value; }
        }
        private ksEntityFactory m_factory;

        /// <summary>
        /// Determines which entities to collide with and notify of collision/overlap events.
        /// </summary>
        public ksCollisionFilter GetCollisionFilter()
        {
            return CollisionFilter == null ?
                ksCollisionFilter.Default : GetCollisionFilterFromProxy(CollisionFilter);
        }

        /// <summary>Gets the collision filter for a collider.</summary>
        /// <param name="collider">Collider to get collision filter for.</param>
        /// <returns>Collision filter for the collider.</returns>
        public ksCollisionFilter GetCollisionFilter(Component collider)
        {
            ksCollisionFilterAsset filter = GetCollisionFilterAsset(collider, false);
            if (filter != null)
            {
                return GetCollisionFilterFromProxy(filter);
            }
            if (Entity != null)
            {
                return Entity.CollisionFilter == null ? ksCollisionFilter.Default : Entity.CollisionFilter;
            }
            return CollisionFilter == null ?
                ksCollisionFilter.Default : GetCollisionFilterFromProxy(CollisionFilter);
        }

        /// <summary>Set the collision filter for a collider.</summary>
        /// <param name="collider">Collider to set the collision filter for.</param>
        /// <param name="filter">Collision filter value</param>
        public void SetCollisionFilter(Component collider, ksCollisionFilter filter)
        {
            ksColliderData colliderData = GetColliderData(collider);
            if (colliderData == null)
            {
                return;
            }
            if (filter == null)
            {
                colliderData.Filter = null;
            }
            else
            {
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(filter.AssetPath))
                {
                    colliderData.Filter = UnityEditor.AssetDatabase.LoadAssetAtPath<ksCollisionFilterAsset>(
                        "Assets/" + filter.AssetPath);
                    if (colliderData.Filter != null)
                    {
                        return;
                    }
                }
#endif
                colliderData.Filter = ScriptableObject.CreateInstance<ksCollisionFilterAsset>();
                colliderData.Filter.Group = filter.Group;
                colliderData.Filter.Notify = filter.Notify;
                colliderData.Filter.Collide = filter.Collide;
            }
        }

        /// <summary>
        /// Converts a <see cref="ksCollisionFilterAsset"/> to a <see cref="ksCollisionFilter"/>. Loads the cached
        /// asset from the <see cref="ksAssetLoader"/> if it is avaiable.
        /// </summary>
        /// <param name="proxy">Proxy to convert</param>
        /// <returns>Collision filter</returns>
        private ksCollisionFilter GetCollisionFilterFromProxy(ksCollisionFilterAsset proxy)
        {
            if (!Application.isPlaying)
            {
                return new ksCollisionFilter(proxy.Group, proxy.Notify, proxy.Collide);
            }
            return ksReactor.Assets.FromProxy<ksCollisionFilter>(proxy);
        }

        /// <summary>Initializes the scene if it is not already initialized.</summary>
        private void Awake()
        {
            ksReactor.GetEntityLinker(gameObject.scene).Initialize();
        }

        /// <summary>
        /// Called when the component is destroyed. If the entity was not destroyed, flags the entity as being deleted
        /// locally so a deletion request will be sent if the local player
        /// owns the entity with the destroy permision.
        /// </summary>
        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Entity != null)
            {
                Entity.ComponentDestroyed();
            }
        }


        /// <summary>
        /// Detaches all scripts and detaches the parent object. Clears <see cref="EntityId"/> if the object is not a
        /// scene entity.
        /// </summary>
        internal override void Detach()
        {
            if (!m_isSceneEntity)
            {
                EntityId = 0;
            }
            base.Detach();
        }

        /// <summary>
        /// Sets <see cref="IsInUse"/> to false. Does nothing else if the game object is a permanent entity. Otherwise
        /// calls <see cref="ksEntityFactory.ReleaseGameObject(ksEntityComponent)"/> on the factory that was used to
        /// spawn this entity, which for scene objects will return them to the linker so they can be reused if the
        /// entity is respawned, and for other object will destroy the game object if it's the default factory. You
        /// should call this if <see cref="DestroyWithServer"/> is false when you are done with the game object after
        /// the entity is destroyed. Does nothing if the game object is linked to a non-destroyed entity or
        /// <see cref="IsInUse"/> is false.
        public void CleanUp()
        {
            if ((Entity == null || Entity.IsDestroyed) && m_isInUse)
            {
                m_isInUse = false;
                if (!IsPermanent)
                {
                    if (m_factory != null)
                    {
                        m_factory.ReleaseGameObject(this);
                    }
                    else
                    {
                        Destroy(gameObject);
                    }
                }
            }
        }

        /// <summary>Gets all colliders on an entity that are supported by Reactor.</summary>
        /// <returns>List of colliders.</returns>
        public List<ksIUnityCollider> GetColliders()
        {
            List<ksIUnityCollider> colliders = new List<ksIUnityCollider>();
            foreach (Component component in GetComponents<Component>())
            {
                ksIUnityCollider collider = GetCollider(component);
                if (collider != null)
                {
                    colliders.Add(collider);
                }
            }
            return colliders;
        }

        /// <summary>Converts a Unity component into a <see cref="ksIUnityCollider"/>.</summary>
        /// <param name="component">component to convert</param>
        /// <returns>Collider, or null if the component cannot be converted.</returns>
        public ksIUnityCollider GetCollider(Component component)
        {
            ksIUnityCollider collider = component as ksIUnityCollider;
            if (collider != null)
            {
                return collider;
            }

            Collider unityCollider = component as Collider;
            if (ksUnityCollider.IsSupported(unityCollider))
            {
                return new ksUnityCollider(unityCollider);
            }

            return null;
        }

        /// <summary>Gets the first enabled collider of the given type on an entity.</summary>
        /// <typeparam name="ColliderType">Unity collider type.</typeparam>
        /// <returns>
        /// First enabled collider of <typeparamref name="ColliderType"/> on the entity, or null if none exist.
        /// </returns>
        public ColliderType GetCollider<ColliderType>() where ColliderType : Collider
        {
            ColliderType[] colliders = GetComponents<ColliderType>();
            foreach (ColliderType collider in colliders)
            {
                if (collider.enabled)
                    return collider;
            }
            return null;
        }

        /// <summary>Gets the first enabled monobehaviour of the given type on an entity.</summary>
        /// <typeparam name="MonobehaviourType">Unity Monobehaviour type.</typeparam>
        /// <returns>
        /// First enabled collider of <typeparamref name="MonobehaviourType"/> on the entity, or null if none exist.
        /// </returns>
        public MonobehaviourType GetMonoBehaviour<MonobehaviourType>() where MonobehaviourType : MonoBehaviour
        {
            MonobehaviourType[] behaviours = GetComponents<MonobehaviourType>();
            foreach (MonobehaviourType behaviour in behaviours)
            {
                if (behaviour.enabled)
                    return behaviour;
            }
            return null;
        }

        /// <summary>
        /// Get the collider data component for a collider. Optionally creates collider data is none is found and
        /// <paramref name="collider"/> is one of the supported collider types.
        /// </summary>
        /// <param name="collider">Collider to get collider data for.</param>
        /// <param name="create">
        /// True to create the collider data if it is not found and <paramref name="collider"/> is one of the supported
        /// collider types.
        /// </param>
        /// <returns>
        /// Collider data for the collider. Null if the collider is not one of the supported types or
        /// <paramref name="create"/> was false and none was found.
        /// </returns>
        public ksColliderData GetColliderData(Component collider, bool create = true)
        {
            foreach (ksColliderData data in GetComponents<ksColliderData>())
            {
                if (data.Collider == collider)
                {
                    return data;
                }
                // Destroy collider data for missing colliders.
                if (data.Collider == null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(data);
                    }
                    else
                    {
                        DestroyImmediate(data, true);
                    }
                }
            }
            if (create && (collider is ksICollider || ksUnityCollider.IsSupported(collider as Collider)))
            {
                ksColliderData data = gameObject.AddComponent<ksColliderData>();
                data.Collider = collider;
                return data;
            }
            return null;
        }

        /// <summary>Destroys a collider and its associated <see cref="ksColliderData"/>.</summary>
        /// <param name="collider">Collider to destroy</param>
        public void DestroyCollider(Collider collider)
        {
            ksColliderData data = GetColliderData(collider, false);
            if (data != null)
            {
                Destroy(data);
            }
            Destroy(collider);
        }

        /// <summary>Returns the collision filter for a collider.</summary>
        /// <param name="collider">Collider to get <see cref="ksCollisionFilterAsset"/> for.</param>
        /// <param name="defaultFromEntity">
        /// If the collision filter for the collider isn't set, returns the filter from the entity 
        /// if this is true, and null if this is false.
        /// </param> 
        /// <returns>Collision filter assigned to the entity collider.</returns>
        public ksCollisionFilterAsset GetCollisionFilterAsset(Component collider, bool defaultFromEntity = true)
        {
            ksCollisionFilterAsset filter = GetColliderData(collider).Filter;
            if (filter != null)
            {
                return filter;
            }
            return defaultFromEntity ? CollisionFilter : null;
        }

        /// <summary>Sets the shape ID for a collider.</summary>
        /// <param name="collider">Collider to set the shape ID on.</param>
        /// <param name="shapeId">Shape ID</param>
        /// <returns>True if the shape ID changed.</returns>
        internal bool SetShapeId(ksIUnityCollider collider, int shapeId)
        {
            ksColliderData data = GetColliderData(collider.Component);
            if (data.ShapeId != shapeId)
            {
                data.ShapeId = shapeId;
                return true;
            }
            return false;
        }

        /// <summary>Detects the shape type of this entity.</summary>
        /// <returns>Shape type</returns>
        public ksShape.ShapeTypes DetectShapeType()
        {
            if (GetCollider<SphereCollider>() != null)
                return ksShape.ShapeTypes.SPHERE;
            if (GetCollider<BoxCollider>() != null)
                return ksShape.ShapeTypes.BOX;
            if (GetCollider<CapsuleCollider>() != null)
                return ksShape.ShapeTypes.CAPSULE;
            if (GetMonoBehaviour<ksCylinderCollider>() != null)
                return ksShape.ShapeTypes.CYLINDER;
            if (GetMonoBehaviour<ksConeCollider>() != null)
                return ksShape.ShapeTypes.CONE;
            if (GetCollider<MeshCollider>() != null)
            {
                if (GetCollider<MeshCollider>().convex)
                {
                    return ksShape.ShapeTypes.CONVEX_MESH;
                }
                else
                {
                    return ksShape.ShapeTypes.TRIANGLE_MESH;
                }
            }
            if (GetCollider<TerrainCollider>() != null)
                return ksShape.ShapeTypes.HEIGHT_FIELD;
            if (GetComponent<CharacterController>() != null)
                return ksShape.ShapeTypes.CAPSULE_CONTROLLER;
            return ksShape.ShapeTypes.NO_COLLIDER;
        }

        /// <summary>Handle character controller collider hit event.</summary>
        /// <param name="hit">Hit information.</param>
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (Entity != null)
            {
                ksUnityCharacterController controller = Entity.CharacterController as ksUnityCharacterController;
                if (controller != null)
                {
                    controller.InvokeColliderHitHandler(hit);
                }
            }
        }
    }
}