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

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KS.Reactor.Client.Unity
{
    /// <summary>
    /// Unity implementation of ksBaseRoom. Rooms are used to connect to server rooms. The room maintains 
    /// a simulation state that is regularly synced with the server.
    /// </summary>
    public class ksRoom : ksBaseRoom<ksPlayer, ksEntity>
    {
        /// <summary>Game object for attaching room scripts.</summary>
        public GameObject GameObject
        {
            get { return m_gameObject; }
        }

        /// <summary>Is the scene for this room loaded?</summary>
        public bool IsSceneLoaded
        {
            get { return m_isSceneLoaded; }
        }

        private GameObject m_gameObject;
        private bool m_isTempGameObject = false;
        private bool m_isSceneLoaded;
        private ksEntityFactory m_defaultEntityFactory = new ksEntityFactory();

        // List of entity type regex pattern paired with entity factories. A list is used to preserve the order the pairs are added. 
        private List<KeyValuePair<string, ksEntityFactory>> m_regexFactories = new List<KeyValuePair<string, ksEntityFactory>>();

        // Mapping of asset IDs to entity factories.  When a entity type is matched using the regex factories for the first time, they are then
        // added to this dictionary. This speeds up factory lookups for known entity types.
        private Dictionary<uint, ksEntityFactory> m_assetFactories = new Dictionary<uint, ksEntityFactory>();

        /// <summary>Reactor physics API.</summary>
        public override ksBasePhysics Physics
        {
            get { return m_physics; }
        }
        private ksPhysicsAdaptor m_physics;

        /// <summary>Gravity applied in physics simulations.</summary>
        protected override ksVector3 Gravity
        {
            get { return m_physics.Gravity; }
            set { m_physics.Gravity = value; }
        }

        /// <summary>
        /// Are players allowed to spawn entities? If you change the value to true at runtime, you must also change
        /// Room.AllowPlayerSpawns on the server.
        /// </summary>
        public bool AllowPlayerSpawning
        {
            get { return m_allowPlayerSpawning; }
            set { m_allowPlayerSpawning = value; }
        }
        private bool m_allowPlayerSpawning;

        /// <summary>The room type component for this room.</summary>
        public ksRoomType RoomType
        {
            get { return m_roomType; }
        }

        private delegate bool ValidateScriptHandler<T>(T script) where T : MonoBehaviour;

        private ksRoomType m_roomType;
        private ksRoomComponent m_component;

        /// <summary>Constructor</summary>
        /// <param name="roomInfo">Room connection information.</param>
        public ksRoom(ksRoomInfo roomInfo)
            : base(roomInfo)
        {
            m_physics = new ksPhysicsAdaptor(this);
        }

        /// <summary>Default constructor.</summary>
        public ksRoom()
            : this(new ksRoomInfo())
        {

        }

        /// <summary>
        /// Destroys all entities and players in the room. Call this after disconnecting. If called while connected,
        /// does nothing.
        /// </summary>
        /// <returns>Returns false if the room was in a state which prevented cleanup.</returns>
        public override bool CleanUp()
        {
            // Exit if the base method failed to cleanup due to being in a connected state.
            if (!base.CleanUp())
            {
                return false;
            }

            if (m_component != null)
            {
                Component.Destroy(m_component);
                m_component = null;
            }

            if (m_gameObject != null)
            {
                if (m_isTempGameObject)
                {
                    GameObject.Destroy(m_gameObject);
                }
                m_gameObject = null;
            }
            return true;
        }

        /// <summary>Connect to the server room.</summary>
        /// <param name="session">Player session</param>
        /// <param name="authArgs">Room authentication arguments.</param>
        public void Connect(ksPlayerAPI.Session session, params ksMultiType[] authArgs)
        {
            if (!Application.isPlaying || ksReactor.IsQuitting)
            {
                ksLog.Warning(this, "You can only connect to a room while the game is playing.");
                return;
            }
            if (ksReactor.Service != null)
            {
                ksReactor.Service.JoinRoom(this, session, authArgs);
            }
        }

        /// <summary>Connect to the server room.</summary>
        /// <param name="authArgs">Room authentication arguments.</param>
        public void Connect(params ksMultiType[] authArgs)
        {
            if (!Application.isPlaying || ksReactor.IsQuitting)
            {
                ksLog.Warning(this, "You can only connect to a room while the game is playing.");
                return;
            }
            if (ksReactor.Service != null)
            {
                ksReactor.Service.JoinRoom(this, null, authArgs);
            }
        }

        /// <summary>Disconnect from the server.</summary>
        /// <param name="immediate">
        /// If immediate is false, then disconnection will be delayed until all queued RPC
        /// calls have been sent.
        /// </param>
        public void Disconnect(bool immediate = false)
        {
            if (ksReactor.Service != null)
            {
                ksReactor.Service.LeaveRoom(this, immediate);
            }
        }

        /// <summary>
        /// Called when a connection attempt starts. Constructs the room's game object and initializes the
        /// <see cref="ksEntityLinker"/> for the scene. Does nothing if the room's game object already exists.
        /// </summary>
        protected override bool ConnectionStarted()
        {
            if (!ksReactorConfig.Instance.AutoRunInBackground && !Application.runInBackground)
            {
                ksLog.Error(this, "Cannot connect when Application.runInBackground is false. Enable " +
                    "Auto Run In Background in the Reactor Settings to automatically set runInBackground to true " +
                    "when connecting.");
                return false;
            }
            return InitializeGameObject();
        }

        /// <summary>
        /// Called when a succesful connection is made. Invokes the <see cref="ksReactor.OnRoomConnect"/> event.
        /// </summary>
        protected override void Connected()
        {
            ksReactor.HandleRoomConnect(this);
        }

        /// <summary>
        /// Called when the room disconnects. Invokes the <see cref="ksReactor.OnRoomDisconnect"/> event.
        /// </summary>
        protected override void Disconnected()
        {
            ksReactor.HandleRoomDisconnect(this);
        }

        /// <summary>
        /// Called after room scripts and entities are initialized after the initial sync with the server. Invokes the
        /// <see cref="ksReactor.OnRoomInitialized"/> event.
        /// </summary>
        protected override void Initialized()
        {
            ksReactor.InvokeRoomInitialized(this);
        }

        /// <summary>
        /// Constructs the room's game object and initializes the <see cref="ksEntityLinker"/> for the scene. Does
        /// nothing if the room's game object already exists. Normally the room's game object is constructed when a
        /// connection attempt starts. You can call this sooner if you want the configure the game object before
        /// connecting.
        /// </summary>
        /// <returns>Returns false if the room gameobject could not be initialized.</returns>
        private bool InitializeGameObject()
        {
            if (m_gameObject != null)
            {
                return false;
            }
            Scene scene = SceneManager.GetSceneByName(Info.Scene);
            m_isSceneLoaded = scene.IsValid() && scene.isLoaded;
            ksEntityLinker linker = null;
            if (m_isSceneLoaded)
            {
                linker = ksReactor.GetEntityLinker(scene);
                linker.Initialize();
            }
            else
            {
                ksLog.Warning(this, "The scene '" + Info.Scene + "' for the room is not loaded. If your scene " +
                    "uses instance entities or permanent entities, the scene will not sync properly.");
            }

            if (string.IsNullOrEmpty(Info.Type))
            {
                m_gameObject = new GameObject("ksRoom");
                m_isTempGameObject = true;
            }
            else if (m_roomType == null && (linker == null || !linker.GetRoomType(Info.Type, out m_roomType)))
            {
                m_gameObject = new GameObject("ksRoom(" + Info.Type + ")");
                m_isTempGameObject = true;
            }
            else
            {
                if (m_roomType.GetComponent<ksRoomComponent>() != null)
                {
                    return false;
                }

                m_gameObject = m_roomType.gameObject;
                m_allowPlayerSpawning = m_roomType.AllowPlayerSpawning;
                m_isTempGameObject = false;
                SendRate = m_roomType.ClientSendRate;

                if (Predictor == null && m_roomType.DefaultRoomPredictor != null)
                {
                    Predictor = m_roomType.DefaultRoomPredictor.CreateInstance();
                }

                ksPhysicsSettings settings = m_roomType.GetComponent<ksPhysicsSettings>();
                if (settings != null)
                {
                    m_physics.AutoSync = settings.AutoSync;
                    ksCollisionFilter.Default = ksReactor.Assets.FromProxy<ksCollisionFilter>(
                        settings.DefaultCollisionFilter);
                    ksBaseUnityRigidBody.DefaultType = settings.DefaultEntityPhysics.RigidBodyMode;
                }
            }
            m_gameObject.transform.position = ksVector3.Zero;
            m_gameObject.transform.rotation = ksQuaternion.Identity;
            m_gameObject.transform.localScale = ksVector3.One;

            m_component = m_gameObject.AddComponent<ksRoomComponent>();
            m_component.ParentObject = this;

            if (m_isSceneLoaded && m_isTempGameObject)
            {
                SceneManager.MoveGameObjectToScene(m_gameObject, scene);
            }
            m_gameObject.SetActive(true);
            m_component.AttachScripts();
            return true;
        }

        /// <summary>Create a new <see cref="ksPlayer"/></summary>
        /// <param name="id">Player ID</param>
        /// <returns>Player object</returns>
        protected override ksPlayer CreatePlayer(uint id)
        {
            return new ksPlayer(id);
        }

        /// <summary>Loads permanent entities from the scene.</summary>
        /// <param name="entities">
        /// List to load entities that have <see cref="ksEntityComponent.IsPermanent"/> set to true into.
        /// </param>
        protected override void LoadPermanentEntities(List<ksEntity> entities)
        {
            ksEntityLinker linker = ksReactor.GetEntityLinker(m_gameObject.scene);
            foreach (ksEntityComponent component in linker.GetPermanentObjects())
            {
                if (component.Entity != null)
                {
                    ksLog.Warning(this, "Permanent entities will not load properly because they are already loaded " +
                        " by another room. This happens when you are connected to multiple rooms running the same " +
                        " scene or you reconnect to a new room without calling CleanUp on the old room that with " +
                        " the same scene.");
                    return;
                }
                component.enabled = true;
                ksEntity entity = ksObjectPool<ksEntity>.Instance.Fetch();
                entity.InitializePermanentEntity(component, this);
                entities.Add(entity);
            }
        }

        /// <summary>Create a new <see cref="ksEntity"/></summary>
        /// <returns>Entity object</returns>
        protected override ksEntity CreateEntity()
        {
            return ksObjectPool<ksEntity>.Instance.Fetch();
        }

        /// <summary>Loads and attaches player scripts to a player.</summary>
        /// <param name="player">Player to attach the <see cref="ksPlayerScript"/> components to.</param>
        protected override void LoadPlayerScripts(ksPlayer player)
        {
            player.SetGameObject(
                CloneScripts<ksPlayerScript>(m_gameObject, player.ValidateScript));
            player.GameObject.name = player.IsLocal ? "Local Player" : "Player " + player.Id;

            // Load the predictor
            if (m_roomType != null && m_roomType.DefaultPlayerPredictor != null)
            {
                player.Predictor = m_roomType.DefaultPlayerPredictor.CreateInstance();
            }
            player.GameObject.transform.parent = GameObject.transform;
            player.GameObject.SetActive(true);
            player.Component.AttachScripts();
        }

        /// <summary>Initializes room scripts.</summary>
        protected override void InitializeScripts()
        {
            m_component.InitializeScripts();
        }

        /// <summary>Does nothing in Unity. Updates are handled by Unity's MonoBehaviour Update() methods.</summary>
        protected override void UpdateScripts()
        {
        }

        /// <summary>
        /// Clones a game object without its children and with only scripts of type <typeparamref name="Script"/>
        /// copied. Takes an optional validator delegate for more control of which scripts are copied. The cloned
        /// object will be disabled so Awake isn't called on the scripts.
        /// </summary>
        /// <typeparam name="Script">Script type</typeparam>
        /// <param name="source">Source object to clone the scripts from</param>
        /// <param name="validator">
        /// Callback to invoke with each script of the template type. If it returns false, the script will not be copied.
        /// </param>
        /// <returns>Cloned game object.</returns>
        private GameObject CloneScripts<Script>(
            GameObject source,
            ValidateScriptHandler<Script> validator = null) where Script : MonoBehaviour
        {
            GameObject gameObject = new GameObject();
            gameObject.SetActive(false);// Disable the game object so Awake isn't called as soon as we add the scripts.
            foreach (Script script in source.GetComponents<Script>())
            {
                if (validator == null || validator(script))
                {
                    ksUnityReflectionUtils.CloneScript(script, gameObject);
                }
            }
            return gameObject;
        }

        /// <summary>Invoke managed RPCs on all room scripts.</summary>
        /// <param name="rpcId">RPC ID</param>
        /// <param name="rpcArgs">RPC arguments</param>
        /// <returns>True if the method invoked any RPC handler</returns>
        protected override bool InvokeRPC(uint rpcId, ksMultiType[] rpcArgs)
        {
            bool called = false;
            if (ksRPCManager<ksRPCAttribute>.Instance.HasRegisteredRPC(rpcId))
            {
                foreach (ksRoomScript script in m_component.Scripts)
                {
                    if (script == null)
                    {
                        m_component.Scripts.RemoveCurrent();
                        continue;
                    }
                    called |= ksRPCManager<ksRPCAttribute>.Instance.InvokeRPC(
                        rpcId, script, script.InstanceType, this, rpcArgs);
                }
            }
            return called;
        }

        /// <summary>
        /// Factories can only be added once. This is to allow mapping the asset ID to a factory after a match is 
        /// found in <see cref="ksRoom.GetEntityFactory(uint, string)"/>. This avoids repetative regex tests against 
        /// the entity type after a match has been found.
        /// </summary>
        /// <param name="regexPattern"></param>
        /// <param name="factory"></param>
        public void AddEntityFactory(string regexPattern, ksEntityFactory factory)
        {
            if (string.IsNullOrEmpty(regexPattern))
            {
                ksLog.Warning(this, "Entity type pattern cannot be null or empty.");
                return;
            }

            if (factory == null)
            {
                ksLog.Warning(this, "Factory cannot be null.");
                return;
            }

            for (int i = 0; i < m_regexFactories.Count; ++i)
            {
                if (m_regexFactories[i].Key == regexPattern)
                {
                    ksLog.Warning(this, "A factory for this pattern has already been registered.");
                    return;
                }
            }
            m_regexFactories.Add(new KeyValuePair<string, ksEntityFactory>(regexPattern, factory));
        }

        /// <summary>
        /// Look for a factory mapped to the asset ID. If one is not found, then find the first factory mapped 
        /// to a regex pattern that matches the entity type. If a factory was found, add it to the asset mapping
        /// and return the factory. Else if a factory still has not been found, then use the default factory and 
        /// add it to the asset mapping before returning it.
        /// </summary>
        /// <param name="assetId"></param>
        /// <param name="entityType"></param>
        /// <returns></returns>
        public ksEntityFactory GetEntityFactory(uint assetId, string entityType)
        {
            ksEntityFactory factory = null;
            if (m_assetFactories.TryGetValue(assetId, out factory))
            {
                return factory;
            }

            for (int i = m_regexFactories.Count - 1; i >= 0; --i)
            {
                if (Regex.IsMatch(entityType, m_regexFactories[i].Key)) 
                {
                    factory = m_regexFactories[i].Value;
                    break;
                }
            }

            if (factory == null)
            {
                factory = m_defaultEntityFactory;
            }
            m_assetFactories.Add(assetId, factory);
            return factory;
        }

        /// <summary>
        /// Spawns an entity on the client and sends a request to spawn it on the server. The entity will have id zero
        /// until the server confirms the spawn.
        /// </summary>
        /// <param name="prefab">
        /// Prefab to spawn. Must have a <see cref="ksEntityComponent"/> with a non-zero asset id. If asset is is zero,
        /// make sure your prefab is in a Resources folder or asset bundles.
        /// </param>
        /// <param name="args">
        /// Additional arguments to send to the server. These will be available in the server player spawn entity event.
        /// </param>
        /// <returns>Spawned entity</returns>
        public ksEntity SpawnEntity(GameObject prefab, params ksMultiType[] args)
        {
            ksClientSpawnParams spawnParams = ksClientSpawnParams.Create(prefab);
            ksEntity entity = SpawnEntity(spawnParams, args);
            spawnParams.Release();
            return entity;
        }

        /// <summary>
        /// Spawns an entity on the client and sends a request to spawn it on the server. The entity will have id zero
        /// until the server confirms the spawn.
        /// </summary>
        /// <param name="prefab">
        /// Prefab to spawn. Must have a <see cref="ksEntityComponent"/> with a non-zero asset id. If asset is is zero,
        /// make sure your prefab is in a Resources folder or asset bundles.
        /// </param>
        /// <param name="position">Position to spawn at.</param>
        /// <param name="args">
        /// Additional arguments to send to the server. These will be available in the server player spawn entity event.
        /// </param>
        /// <returns>Spawned entity</returns>
        public ksEntity SpawnEntity(GameObject prefab, ksVector3 position, params ksMultiType[] args)
        {
            ksClientSpawnParams spawnParams = ksClientSpawnParams.Create(prefab);
            spawnParams.Transform.Position = position;
            ksEntity entity = SpawnEntity(spawnParams, args);
            spawnParams.Release();
            return entity;
        }

        /// <summary>
        /// Spawns an entity on the client and sends a request to spawn it on the server. The entity will have id zero
        /// until the server confirms the spawn.
        /// </summary>
        /// <param name="prefab">
        /// Prefab to spawn. Must have a <see cref="ksEntityComponent"/> with a non-zero asset id. If asset is is zero,
        /// make sure your prefab is in a Resources folder or asset bundles.
        /// </param>
        /// <param name="position">Position to spawn at.</param>
        /// <param name="args">
        /// Additional arguments to send to the server. These will be available in the server player spawn entity event.
        /// </param>
        /// <returns>Spawned entity</returns>
        public ksEntity SpawnEntity(
            GameObject prefab,
            ksVector3 position,
            ksQuaternion rotation,
            params ksMultiType[] args)
        {
            ksClientSpawnParams spawnParams = ksClientSpawnParams.Create(prefab);
            spawnParams.Transform.Position = position;
            spawnParams.Transform.Rotation = rotation;
            ksEntity entity = SpawnEntity(spawnParams, args);
            spawnParams.Release();
            return entity;
        }

        /// <summary>
        /// Spawns an entity on the client and sends a request to spawn it on the server. The entity will have id zero
        /// until the server confirms the spawn.
        /// </summary>
        /// <param name="prefab">
        /// Prefab to spawn. Must have a <see cref="ksEntityComponent"/> with a non-zero asset id. If asset is is zero,
        /// make sure your prefab is in a Resources folder or asset bundles.
        /// </param>
        /// <param name="position">Position to spawn at.</param>
        /// <param name="rotation">Rotation to spawn at.</param>
        /// <param name="scale">Absolute scale to spawn at.</param>
        /// <param name="args">
        /// Additional arguments to send to the server. These will be available in the server player spawn entity event.
        /// </param>
        /// <returns>Spawned entity</returns>
        public ksEntity SpawnEntity(
            GameObject prefab,
            ksVector3 position,
            ksQuaternion rotation,
            ksVector3 scale,
            params ksMultiType[] args)
        {
            ksClientSpawnParams spawnParams = ksClientSpawnParams.Create(prefab);
            spawnParams.Transform.Position = position;
            spawnParams.Transform.Rotation = rotation;
            spawnParams.Transform.Scale = scale;
            spawnParams.ScaleMode = ksSpawnParams.ScaleModes.ABSOLUTE;
            ksEntity entity = SpawnEntity(spawnParams, args);
            spawnParams.Release();
            return entity;
        }

        /// <summary>
        /// Spawns an entity on the client and sends a request to spawn it on the server. The entity will have id zero
        /// until the server confirms the spawn.
        /// </summary>
        /// <param name="prefab">
        /// Prefab to spawn. Must have a <see cref="ksEntityComponent"/> with a non-zero asset id. If asset is is zero,
        /// make sure your prefab is in a Resources folder or asset bundles.
        /// </param>
        /// <param name="position">Position to spawn at.</param>
        /// <param name="rotation">Rotation to spawn at.</param>
        /// <param name="scale">Scale to spawn at.</param>
        /// <param name="scaleMode">Determines if <paramref name="scale"/> is relative or absolute.</param>
        /// <param name="args">
        /// Additional arguments to send to the server. These will be available in the server player spawn entity event.
        /// </param>
        /// <returns>Spawned entity</returns>
        public ksEntity SpawnEntity(
            GameObject prefab,
            ksVector3 position,
            ksQuaternion rotation,
            ksVector3 scale,
            ksSpawnParams.ScaleModes scaleMode,
            params ksMultiType[] args)
        {
            ksClientSpawnParams spawnParams = ksClientSpawnParams.Create(prefab);
            spawnParams.Transform.Position = position;
            spawnParams.Transform.Rotation = rotation;
            spawnParams.Transform.Scale = scale;
            spawnParams.ScaleMode = scaleMode;
            ksEntity entity = SpawnEntity(spawnParams, args);
            spawnParams.Release();
            return entity;
        }

        /// <summary>
        /// Spawns an entity on the client and sends a request to spawn it on the server. The entity will have id zero
        /// until the server confirms the spawn.
        /// </summary>
        /// <param name="prefab">
        /// Prefab to spawn. Must have a <see cref="ksEntityComponent"/> with a non-zero asset id. If asset is is zero,
        /// make sure your prefab is in a Resources folder or asset bundles.
        /// </param>
        /// <param name="position">Position to spawn at.</param>
        /// <param name="args">
        /// Additional arguments to send to the server. These will be available in the server player spawn entity event.
        /// </param>
        /// <returns>Spawned entity</returns>
        public ksEntity SpawnEntity(GameObject prefab, ksVector2 position, params ksMultiType[] args)
        {
            ksClientSpawnParams2D spawnParams = ksClientSpawnParams2D.Create(prefab);
            spawnParams.Transform.Position = position;
            ksEntity entity = SpawnEntity(spawnParams, args);
            spawnParams.Release();
            return entity;
        }

        /// <summary>
        /// Spawns an entity on the client and sends a request to spawn it on the server. The entity will have id zero
        /// until the server confirms the spawn.
        /// </summary>
        /// <param name="prefab">
        /// Prefab to spawn. Must have a <see cref="ksEntityComponent"/> with a non-zero asset id. If asset is is zero,
        /// make sure your prefab is in a Resources folder or asset bundles.
        /// </param>
        /// <param name="position">Position to spawn at.</param>
        /// <param name="rotation">Rotation to spawn at.</param>
        /// <param name="args">
        /// Additional arguments to send to the server. These will be available in the server player spawn entity event.
        /// </param>
        /// <returns>Spawned entity</returns>
        public ksEntity SpawnEntity(
            GameObject prefab,
            ksVector2 position,
            float rotation,
            params ksMultiType[] args)
        {
            ksClientSpawnParams2D spawnParams = ksClientSpawnParams2D.Create(prefab);
            spawnParams.Transform.Position = position;
            spawnParams.Transform.Rotation = rotation;
            ksEntity entity = SpawnEntity(spawnParams, args);
            spawnParams.Release();
            return entity;
        }

        /// <summary>
        /// Spawns an entity on the client and sends a request to spawn it on the server. The entity will have id zero
        /// until the server confirms the spawn.
        /// </summary>
        /// <param name="prefab">
        /// Prefab to spawn. Must have a <see cref="ksEntityComponent"/> with a non-zero asset id. If asset is is zero,
        /// make sure your prefab is in a Resources folder or asset bundles.
        /// </param>
        /// <param name="position">Position to spawn at.</param>
        /// <param name="rotation">Rotation to spawn at.</param>
        /// <param name="scale">Absolute scale to spawn at.</param>
        /// <param name="args">
        /// Additional arguments to send to the server. These will be available in the server player spawn entity event.
        /// </param>
        /// <returns>Spawned entity</returns>
        public ksEntity SpawnEntity(
            GameObject prefab,
            ksVector2 position,
            float rotation,
            ksVector2 scale,
            params ksMultiType[] args)
        {
            ksClientSpawnParams2D spawnParams = ksClientSpawnParams2D.Create(prefab);
            spawnParams.Transform.Position = position;
            spawnParams.Transform.Rotation = rotation;
            spawnParams.Transform.Scale = scale;
            spawnParams.ScaleMode = ksSpawnParams.ScaleModes.ABSOLUTE;
            ksEntity entity = SpawnEntity(spawnParams, args);
            spawnParams.Release();
            return entity;
        }

        /// <summary>
        /// Spawns an entity on the client and sends a request to spawn it on the server. The entity will have id zero
        /// until the server confirms the spawn.
        /// </summary>
        /// <param name="prefab">
        /// Prefab to spawn. Must have a <see cref="ksEntityComponent"/> with a non-zero asset id. If asset is is zero,
        /// make sure your prefab is in a Resources folder or asset bundles.
        /// </param>
        /// <param name="position">Position to spawn at.</param>
        /// <param name="rotation">Rotation to spawn at.</param>
        /// <param name="scale">Scale to spawn at.</param>
        /// <param name="scaleMode">Determines if <paramref name="scale"/> is relative or absolute.</param>
        /// <param name="args">
        /// Additional arguments to send to the server. These will be available in the server player spawn entity event.
        /// </param>
        /// <returns>Spawned entity</returns>
        public ksEntity SpawnEntity(
            GameObject prefab,
            ksVector2 position,
            float rotation,
            ksVector2 scale,
            ksSpawnParams.ScaleModes scaleMode,
            params ksMultiType[] args)
        {
            ksClientSpawnParams2D spawnParams = ksClientSpawnParams2D.Create(prefab);
            spawnParams.Transform.Position = position;
            spawnParams.Transform.Rotation = rotation;
            spawnParams.Transform.Scale = scale;
            spawnParams.ScaleMode = scaleMode;
            ksEntity entity = SpawnEntity(spawnParams, args);
            spawnParams.Release();
            return entity;
        }

        /// <summary>
        /// Spawns an entity on the client and sends a request to spawn it on the server. The entity will have id zero
        /// until the server confirms the spawn.
        /// </summary>
        /// <param name="spawnParams">Spawn parameters</param>
        /// <param name="args">
        /// Additional arguments to send to the server. These will be available in the server player spawn entity event.
        /// </param>
        /// <returns>Spawned entity</returns>
        public ksEntity SpawnEntity(ksClientSpawnParams spawnParams, params ksMultiType[] args)
        {
            if (!m_allowPlayerSpawning)
            {
                ksLog.Warning(this, "Player entity spawning is disabled. You must configure the "
                    + nameof(ksRoomType) + " to allow players to spawn entities.");
                return null;
            }
            return CallSpawnRPC(spawnParams, args);
        }

        /// <summary>Apply the server time scale to the unity time object.</summary>
        /// <param name="timeScale">Amount to scale time by.</param>
        protected override void ApplyServerTimeScale(float timeScale)
        {
            if (m_roomType != null && m_roomType.ApplyServerTimeScale)
            {
                UnityEngine.Time.timeScale = timeScale;
            }
        }
    }
}
