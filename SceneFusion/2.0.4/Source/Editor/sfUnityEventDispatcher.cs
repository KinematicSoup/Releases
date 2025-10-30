using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using KS.SceneFusion;
using KS.SceneFusion2.Client;
using KS.SF.Reactor;
using KS.SF.Unity.Editor;
using UObject = UnityEngine.Object;

#if !UNITY_2021_3_OR_NEWER
using UnityEngine.Experimental.TerrainAPI;
#endif

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>
    /// Listens for and dispatches Unity events, in some cases changing the parameters of the event. Allows all events
    /// to be enabled or disabled. Register with this class instead of directly against the Unity events to ensure you
    /// do not respond to events that were triggered by Scene Fusion.
    /// </summary>
    public class sfUnityEventDispatcher
    {
        /// <summary></summary>
        /// <returns>singleton instance</returns>
        public static sfUnityEventDispatcher Get()
        {
            return m_instance;
        }
        private static sfUnityEventDispatcher m_instance = new sfUnityEventDispatcher();

        /// <summary>Are events enabled?</summary>
        public bool Enabled
        {
            get { return m_enabled; }
        }
        private bool m_enabled = false;

        /// <summary>Update event handler.</summary>
        /// <param name="deltaTime">deltaTime in seconds since the last update.</param>
        public delegate void UpdateHandler(float deltaTime);

        /// <summary>Invoked every update before processing server RPCs.</summary>
        public event UpdateHandler PreUpdate;

        /// <summary>Invoked every update after processing server RPCs.</summary>
        public event UpdateHandler OnUpdate;

        /// <summary>Invoked when a scene is opened or a new scene is created.</summary>
        public event EditorSceneManager.SceneOpenedCallback OnOpenScene;

        /// <summary>Invoked when a scene is closed.</summary>
        public event EditorSceneManager.SceneClosingCallback OnCloseScene;

        /// <summary>Create game object event callback.</summary>
        /// <param name="gameObject">gameObject that was created.</param>
        public delegate void CreateCallback(GameObject gameObject);

        /// <summary>
        /// Invoked when a game object is created. Only invoked if an undo operation is registered for the object
        /// creation.
        /// </summary>
        public event CreateCallback OnCreate;

        /// <summary>Create prefab child event callback.</summary>
        /// <param name="parent">parent that had one or more child objects created.</param>
        public delegate void CreatePrefabChildCallback(GameObject parent);

        /// <summary>Invoked when one or more child game objects are created in a prefab.</summary>
        public event CreatePrefabChildCallback OnCreatePrefabChild;

        /// <summary>Delete game object event callback.</summary>
        /// <param name="instanceId">instanceId of game object that was deleted.</param>
        public delegate void DeleteCallback(int instanceId);

        /// <summary>Invoked when a game object is deleted.</summary>
        public event DeleteCallback OnDelete;

        /// <summary>Hierarchy structure change event callback.</summary>
        /// <param name="gameObject">
        /// gameObject whose hierarchy structure changed. This object and and of its descendants may
        /// have changed.
        /// </param>
        public delegate void HierarchyStructureChangeCallback(GameObject gameObject);

        /// <summary>
        /// Invoked when an action is performed that changes a game object and possibly any of its descendants, and we
        /// don't know what specifically changed. Examples of such actions are unpacking or reverting a prefab instance,
        /// or any changes made after calling Undo.RegisterFullObjectHierarchyUndo.
        /// </summary>
        public event HierarchyStructureChangeCallback OnHierarchyStructureChange;

        /// <summary>Update prefab instance callback.</summary>
        /// <param name="gameObject">
        /// prefab root instance that was updated because its source prefab was
        /// modified.
        /// </param>
        public delegate void UpdatePrefabInstanceCallback(GameObject gameObject);

        /// <summary>
        /// Invoked when a prefab instance is updated because its source prefab was modified. This is always called with
        /// the prefab root instance.
        /// </summary>
        public event UpdatePrefabInstanceCallback OnUpdatePrefabInstance;

        /// <summary>Properties changed event callback.</summary>
        /// <param name="uobj">uobj whose properties changed.</param>
        public delegate void PropertiesChangedCallback(UObject uobj);

        /// <summary>Invoked when properties on a uobject changed, but we don't know which properties.</summary>
        public event PropertiesChangedCallback OnPropertiesChanged;

        /// <summary>Add or remove components event callback.</summary>
        /// <param name="gameObject">gameObject with added and/or removed components.</param>
        public delegate void AddOrRemoveComponentsCallback(GameObject gameObject);

        /// <summary>Invoked when components are added to or removed from a game object.</summary>
        public event AddOrRemoveComponentsCallback OnAddOrRemoveComponents;

        /// <summary>Parent change event callback.</summary>
        /// <param name="gameObject">gameObject whose parent changed.</param>
        public delegate void ParentChangeCallback(GameObject gameObject);

        /// <summary>Invoked when a game object's parent changes.</summary>
        public event ParentChangeCallback OnParentChange;

        /// <summary>Reorder children event callback.</summary>
        /// <param name="gameObject">gameObject whose children were reordered.</param>
        public delegate void ReorderChildrenCallback(GameObject gameObject);

        /// <summary>Invoked when a game object's children are reordered.</summary>
        public event ReorderChildrenCallback OnReorderChildren;

        /// <summary>Update prefab instances event handler.</summary>
        public delegate void UpdatePrefabInstancesHandler();

        /// <summary>
        /// Invoked when prefab instances are updated because their prefab was updated, before on update prefab instance
        /// is invoked for each updated prefab instance.
        /// </summary>
        public event UpdatePrefabInstancesHandler OnUpdatePrefabInstances;

        /// <summary>Modify object event handler.</summary>
        /// <param name="instanceId">instanceId of modified object.</param>
        public delegate void ModifyObjectHandler(int instanceId);

        /// <summary>
        /// Invoked when an object is modified and an undo operation is recorded for it. This is not fired on modified
        /// descendants for hierarchy structure change events.
        /// </summary>
        public event ModifyObjectHandler OnModifyObject;

        /// <summary>Open prefab stage event handler.</summary>
        /// <param name="stage">stage that opened.</param>
        public delegate void OpenPrefabStageHandler(PrefabStage stage);

        /// <summary>Invoked when a prefab stage is opened.</summary>
        public event OpenPrefabStageHandler OnOpenPrefabStage;

        /// <summary>Close prefab stage event handler.</summary>
        /// <param name="stage">stage that closed.</param>
        public delegate void ClosePrefabStageHandler(PrefabStage stage);

        /// <summary>Invoked when a prefab stage is closed.</summary>
        public event ClosePrefabStageHandler OnClosePrefabStage;

        /// <summary>Save prefab stage event handler.</summary>
        /// <param name="gameObject">gameObject root of the prefab stage that was saved.</param>
        public delegate void SavePrefabStageHandler(GameObject gameObject);

        /// <summary>Invoked when a prefab stage is saved.</summary>
        public event SavePrefabStageHandler OnSavePrefabStage;

        /// <summary>Invoked when assets are imported or renamed.</summary>
        public event ksEditorEvents.ImportAssetsHandler OnImportAssets;

        /// <summary>Invoked when a terrain's heightmap is changed.</summary>
        public event TerrainCallbacks.HeightmapChangedCallback OnTerrainHeightmapChange;

        /// <summary>Invoked when a terrain's textures are changed</summary>
        public event TerrainCallbacks.TextureChangedCallback OnTerrainTextureChange;

        /// <summary>Terrain detail change event handler.</summary>
        /// <param name="terrainData">terrainData whose details changed.</param>
        /// <param name="changeArea">changeArea of details that changed.</param>
        /// <param name="layer">layer whose details changed.</param>
        public delegate void TerrainDetailChangedCallback(TerrainData terrainData, RectInt changeArea, int layer);

        /// <summary>Invoked when a terrain's details are changed.</summary>
        public event TerrainDetailChangedCallback OnTerrainDetailChange;

        /// <summary>Terrain tree change event handler.</summary>
        /// <param name="terrainData">terrainData whose trees changed.</param>
        /// <param name="hasRemovals">
        /// true if trees were removed. Trees may have also been added when this is true.
        /// </param>
        public delegate void TerrainTreeChangedCallback(TerrainData terrainData, bool hasRemovals);

        /// <summary>Invoked when a terrain's trees are changed.</summary>
        public event TerrainTreeChangedCallback OnTerrainTreeChange;

        /// <summary>Terrain check event handler.</summary>
        /// <param name="terrain">Terrain to check for changes.</param>
        public delegate void TerrainCheckCallback(Terrain terrain);

        /// <summary>
        /// Invoked periodically when a terrain component is inspected to check for terrain changes that don't have
        /// Unity events.
        /// </summary>
        public event TerrainCheckCallback OnTerrainCheck;

        /// <summary>
        /// Invoked when properties are modified. When properties are edited by dragging, this will fire continuosuly
        /// as the values change, and PostModifyProperties will fire once when dragging stops.
        /// </summary>
        public event Undo.PostprocessModifications OnModifyProperties
        {
            add
            {
                if (value != null)
                {
                    m_propertyModificationHandlers.Add(value);
                }
            }
            remove
            {
                if (value != null)
                {
                    m_propertyModificationHandlers.Remove(value);
                }
            }
        }
        private List<Undo.PostprocessModifications> m_propertyModificationHandlers =
            new List<Undo.PostprocessModifications>();

        /// <summary>Post modify properties event handler.</summary>
        /// <param name="modifications"></param>
        public delegate void PostModifyPropertiesHandler(UndoPropertyModification[] modifications);

        /// <summary>
        /// Invoked after properties are modified. When properties are edited by dragging, OnModifyProperties will fire
        /// continuously as the values change, and this will fire once when dragging stops.
        /// </summary>
        public event PostModifyPropertiesHandler PostModifyProperties;

        /// <summary>
        /// Types of events that can be invoked from InvokeChangeEvents. Some events are combinations of other event
        /// flags. When these events are invoked, it prevents the other events from also firing.
        /// </summary>
        [Flags]
        private enum Events
        {
            NONE = 0,
            CREATE = 0xFF,
            CREATE_CHILD = 1 << 0,
            ADD_REMOVE_COMPONENT = 1 << 1,
            CHANGE_PROPERTIES = 1 << 2,
            CHANGE_HIERARCHY_STRUCTURE = 1 << 3 | ADD_REMOVE_COMPONENT | CHANGE_PROPERTIES | CHANGE_PARENT | 
                REORDER_CHILDREN | CREATE_CHILD,
            CHANGE_PARENT = 1 << 4,
            REORDER_CHILDREN = 1 << 5,
            DELETE = 1 << 6
        }

        // Tracks all distinct events called upon a specific UObject during an InvokeChangesEvents call. Used to
        // prevent double invoking events. Keys are instance ids.
        private Dictionary<int, Events> m_invokedEventMap = 
            new Dictionary<int, Events>();

        /// <summary>Constructor</summary>
        internal sfUnityEventDispatcher()
        {
            
        }

        /// <summary>Enables events. Starts listening for Unity events.</summary>
        public void Enable()
        {
            if (m_enabled)
            {
                return;
            }
            m_enabled = true;
            EditorSceneManager.newSceneCreated += InvokeOnOpenScene;
            EditorSceneManager.sceneOpened += InvokeOnOpenScene;
            EditorSceneManager.sceneClosing += InvokeOnCloseScene;
            Undo.postprocessModifications += InvokeOnModifyProperties;
            TerrainCallbacks.heightmapChanged += InvokeOnHeightmapChange;
            TerrainCallbacks.textureChanged += InvokeOnTextureChange;
            PrefabStage.prefabSaved += InvokeOnSavePrefabStage;
            PrefabStage.prefabStageOpened += InvokeOnOpenPrefabStage;
            PrefabStage.prefabStageClosing += InvokeOnClosePrefabStage;
            ksEditorEvents.OnImportAssets += InvokeOnImportAssets;
        }

        /// <summary>Disables events. Stops listening for Unity events.</summary>
        public void Disable()
        {
            if (!m_enabled)
            {
                return;
            }
            m_enabled = false;
            EditorSceneManager.newSceneCreated -= InvokeOnOpenScene;
            EditorSceneManager.sceneOpened -= InvokeOnOpenScene;
            EditorSceneManager.sceneClosing -= InvokeOnCloseScene;
            Undo.postprocessModifications -= InvokeOnModifyProperties;
            TerrainCallbacks.heightmapChanged -= InvokeOnHeightmapChange;
            TerrainCallbacks.textureChanged -= InvokeOnTextureChange;
            PrefabStage.prefabSaved -= InvokeOnSavePrefabStage;
            PrefabStage.prefabStageOpened -= InvokeOnOpenPrefabStage;
            PrefabStage.prefabStageClosing -= InvokeOnClosePrefabStage;
            ksEditorEvents.OnImportAssets -= InvokeOnImportAssets;
        }

        /// <summary>
        /// If the dispatcher is already enabled, calls the callback. Otherwise enables the dispatcher before calling
        /// the callback and disables it again afterwards.
        /// </summary>
        /// <param name="callback">callback to call with the dispatcher enabled.</param>
        public void TempEnable(Action callback)
        {
            if (m_enabled)
            {
                callback();
                return;
            }
            Enable();
            try
            {
                callback();
            }
            finally
            {
                Disable();
            }
        }

        /// <summary>Invokes the pre update event.</summary>
        /// <param name="deltaTime">deltaTime in seconds since the last update.</param>
        internal void InvokePreUpdate(float deltaTime)
        {
            if (PreUpdate != null)
            {
                PreUpdate(deltaTime);
            }
        }

        /// <summary>Invokes the on update event.</summary>
        /// <param name="deltaTime">deltaTime in seconds since the last update.</param>
        internal void InvokeOnUpdate(float deltaTime)
        {
            if (OnUpdate != null)
            {
                OnUpdate(deltaTime);
            }
        }

        /// <summary>Invokes the on open scene event.</summary>
        /// <param name="scene">scene that was created.</param>
        /// <param name="setup"></param>
        /// <param name="mode">mode the scene was created with.</param>
        private void InvokeOnOpenScene(Scene scene, NewSceneSetup setup, NewSceneMode mode)
        {
            InvokeOnOpenScene(scene, mode == NewSceneMode.Additive ? OpenSceneMode.Additive : OpenSceneMode.Single);
        }

        /// <summary>Invokes the on open scene event.</summary>
        /// <param name="scene">scene that was opened.</param>
        /// <param name="mode">mode the scene was opened with.</param>
        internal void InvokeOnOpenScene(Scene scene, OpenSceneMode mode)
        {
            if (OnOpenScene != null)
            {
                OnOpenScene(scene, mode);
            }
        }

        /// <summary>Invokes the on close scene event.</summary>
        /// <param name="scene">scene that was closed.</param>
        /// <param name="removed">true if the scene was removed.</param>
        internal void InvokeOnCloseScene(Scene scene, bool removed)
        {
            if (OnCloseScene != null)
            {
                OnCloseScene(scene, removed);
            }
        }

        /// <summary>Invokes the on modify properties event.</summary>
        /// <param name="modifications.">
        /// modifications. Remove modifications from the returned array to prevent
        /// them.
        /// </param>
        /// <returns>modifications that are allowed.</returns>
        public UndoPropertyModification[] InvokeOnModifyProperties(UndoPropertyModification[] modifications)
        {
            foreach (Undo.PostprocessModifications handler in m_propertyModificationHandlers)
            {
                modifications = handler(modifications);
            }
            return modifications;
        }

        /// <summary>Invokes the post modify properties event.</summary>
        /// <param name="modifications"></param>
        public void InvokePostModifyProperties(UndoPropertyModification[] modifications)
        {
            if (PostModifyProperties != null && modifications != null)
            {
                PostModifyProperties(modifications);
            }
        }

        /// <summary>Invokes the on save prefab stage event.</summary>
        /// <param name="gameObject">gameObject root of the saved prefab stage.</param>
        internal void InvokeOnSavePrefabStage(GameObject gameObject)
        {
            if (OnSavePrefabStage != null && gameObject != null)
            {
                OnSavePrefabStage(gameObject);
            }
        }

        /// <summary>Invokes the on open prefab stage event.</summary>
        /// <param name="stage">stage that opened.</param>
        internal void InvokeOnOpenPrefabStage(PrefabStage stage)
        {
            if (OnOpenPrefabStage != null)
            {
                OnOpenPrefabStage(stage);
            }
        }

        /// <summary>Invokes the on close prefab stage event.</summary>
        /// <param name="stage">stage that closed.</param>
        internal void InvokeOnClosePrefabStage(PrefabStage stage)
        {
            if (OnClosePrefabStage != null)
            {
                OnClosePrefabStage(stage);
            }
        }

        /// <summary>Invokes the on import assets event.</summary>
        /// <param name="paths">paths to imported assets.</param>
        internal void InvokeOnImportAssets(string[] paths)
        {
            if (OnImportAssets != null && paths != null)
            {
                OnImportAssets(paths);
            }
        }

        /// <summary>Called when operations were recorded on the undo stack. Invokes events for changes.</summary>
        /// <param name="stream">stream of change events.</param>
        /// <param name="fromPrefabStage">
        /// true if the events were converted from prefab stage object events. If true,
        /// will invoke the on create prefab child event instead of the on create event. If false, will invoke
        /// the on add or remove components event for prefab variants with a changed properties event, since
        /// Unity invokes a changed properties event instead of a structure change event when a removed prefab
        /// component is reverted on a prefab variant.
        /// </param>
        internal void InvokeChangeEvents(ref ObjectChangeEventStream stream, bool fromPrefabStage = false)
        {
            //ksLog.Info("Invoke events " + fromPrefabStage);
            for (int i = 0; i < stream.length; i++)
            {
                try
                {
                    //ksLog.Info("ev: " + stream.GetEventType(i));
                    InvokeEvent(ref stream, i, fromPrefabStage);
                }
                catch (Exception e)
                {
                    ksLog.Error("Error handling " + stream.GetEventType(i) + " event.", e);
                }
            }
            m_invokedEventMap.Clear();
        }

        /// <summary>
        /// Invokes an event for the change event at the given index in the stream if an event can be invoked for it.
        /// The types of events that can be invoked are:
        /// - Game object creation
        /// - Game object deletion
        /// - Change game object structure hierarchy
        /// - Change properties (invoked when we don't know which properties changed)
        /// - Add and/or remove components
        /// - Change game object parent
        /// - Reorder game object children
        /// </summary>
        /// <param name="stream">stream of change events.</param>
        /// <param name="index">index of event in stream to invoke event for.</param>
        /// <param name="fromPrefabStage">
        /// true if the event was converted from a prefab stage object event. If true,
        /// will invoke the on create prefab child event instead of the on create event. If false, will invoke
        /// the on add or remove components event for prefab variants with a changed properties event, since
        /// Unity invokes a changed properties event instead of a structure change event when a removed prefab
        /// component is reverted on a prefab variant.
        /// </param>
        private void InvokeEvent(ref ObjectChangeEventStream stream, int index, bool fromPrefabStage = false)
        {
            switch (stream.GetEventType(index))
            {
                case ObjectChangeKind.CreateGameObjectHierarchy:
                {
                    if (fromPrefabStage)
                    {
                        InvokeCreatePrefabChildEvent(ref stream, index);
                    }
                    else
                    {
                        InvokeCreateEvent(ref stream, index);
                    }
                    break;
                }
                case ObjectChangeKind.DestroyGameObjectHierarchy:
                    InvokeDeleteEvent(ref stream, index); break;
                case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                    InvokeHierarchyStructureChangeEvent(ref stream, index); break;
                case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                    InvokePropertiesChangedEvent(ref stream, index, !fromPrefabStage); break;
                case ObjectChangeKind.ChangeAssetObjectProperties:
                    InvokePropertiesChangedEventForAsset(ref stream, index); break;
                case ObjectChangeKind.ChangeGameObjectStructure: 
                    InvokeAddOrRemoveComponentsEvent(ref stream, index); break;
                case ObjectChangeKind.ChangeGameObjectParent:
                    InvokeParentChangeEvent(ref stream, index); break;
#if UNITY_2022_2_OR_NEWER
                case ObjectChangeKind.ChangeChildrenOrder:
                    InvokeReorderChildrenEvent(ref stream, index); break;
#endif
                case ObjectChangeKind.UpdatePrefabInstances:
                {
                    InvokeOnUpdatePrefabInstances();
                    InvokeUpdatePrefabInstanceEvents(ref stream, index); 
                    break;
                }
            }
        }

        /// <summary>Invokes the on create event.</summary>
        /// <param name="stream">stream of change events.</param>
        /// <param name="index">index of event in stream to invoke event for.</param>
        private void InvokeCreateEvent(ref ObjectChangeEventStream stream, int index)
        {
            CreateGameObjectHierarchyEventArgs data;
            stream.GetCreateGameObjectHierarchyEvent(index, out data);
            SetEventFlag(data.instanceId, Events.CREATE);
            if (OnCreate == null)
            {
                return;
            }
            GameObject gameObj = EditorUtility.InstanceIDToObject(data.instanceId) as GameObject;
            if (gameObj != null)
            {
                SetEventFlag(data.instanceId, Events.CREATE);
                OnCreate(gameObj);
            }
        }

        /// <summary>
        /// Invokes the on create prefab child event if it hasn't already been invoked for the game object.
        /// </summary>
        /// <param name="stream">stream of change events.</param>
        /// <param name="index">index of event in stream to invoke event for.</param>
        private void InvokeCreatePrefabChildEvent(ref ObjectChangeEventStream stream, int index)
        {
            CreateGameObjectHierarchyEventArgs data;
            stream.GetCreateGameObjectHierarchyEvent(index, out data);
            if (!SetEventFlag(data.instanceId, Events.CREATE_CHILD) || OnCreatePrefabChild == null)
            {
                return;
            }
            GameObject gameObj = EditorUtility.InstanceIDToObject(data.instanceId) as GameObject;
            if (gameObj != null)
            {
                OnCreatePrefabChild(gameObj);
            }
        }

        /// <summary>Invokes the on create event.</summary>
        /// <param name="gameObject">gameObject that was created.</param>
        public void InvokeOnCreate(GameObject gameObject)
        {
            if (gameObject != null && OnCreate != null)
            {
                OnCreate(gameObject);
            }
        }

        /// <summary>Invokes the on create prefab child event.</summary>
        /// <param name="gameObject">gameObject in prefab that had one or more new child objects created.</param>
        public void InvokeOnCreatePrefabChild(GameObject gameObject)
        {
            if (gameObject != null && OnCreatePrefabChild != null)
            {
                OnCreatePrefabChild(gameObject);
            }
        }

        /// <summary>Invokes the on delete event.</summary>
        /// <param name="stream">stream of change events.</param>
        /// <param name="index">index of event in stream to invoke event for.</param>
        private void InvokeDeleteEvent(ref ObjectChangeEventStream stream, int index)
        {
            DestroyGameObjectHierarchyEventArgs data;
            stream.GetDestroyGameObjectHierarchyEvent(index, out data);
            SetEventFlag(data.instanceId, Events.DELETE);
            if (OnDelete != null)
            {
                OnDelete(data.instanceId);
            }
        }

        /// <summary>Invokes the on delete event.</summary>
        /// <param name="instanceId">instanceId of game object that was deleted.</param>
        public void InvokeOnDelete(int instanceId)
        {
            if (OnDelete != null)
            {
                OnDelete(instanceId);
            }
        }

        /// <summary>
        /// Invokes the on hierarchy structure change event if it hasn't already been invoked for the game object.
        /// </summary>
        /// <param name="stream">stream of change events.</param>
        /// <param name="index">index of event in stream to invoke event for.</param>
        private void InvokeHierarchyStructureChangeEvent(ref ObjectChangeEventStream stream, int index)
        {
            ChangeGameObjectStructureHierarchyEventArgs data;
            stream.GetChangeGameObjectStructureHierarchyEvent(index, out data);
            if (!SetEventFlag(data.instanceId, Events.CHANGE_HIERARCHY_STRUCTURE) ||
                OnHierarchyStructureChange == null)
            {
                return;
            }
            GameObject gameObj = EditorUtility.InstanceIDToObject(data.instanceId) as GameObject;
            if (gameObj != null)
            {
                OnHierarchyStructureChange(gameObj);
            }
        }

        /// <summary>
        /// Invokes the on update prefab instance event for each updated prefab instance if it hasn't already been
        /// invoked for that object.
        /// </summary>
        /// <param name="stream">stream of change events.</param>
        /// <param name="index">index of event in stream to invoke events for.</param>
        private void InvokeUpdatePrefabInstanceEvents(ref ObjectChangeEventStream stream, int index)
        {
            UpdatePrefabInstancesEventArgs data;
            stream.GetUpdatePrefabInstancesEvent(index, out data);
            foreach (int id in data.instanceIds)
            {
                if (SetEventFlag(id, Events.CHANGE_HIERARCHY_STRUCTURE) && OnUpdatePrefabInstance != null)
                {
                    GameObject gameObj = EditorUtility.InstanceIDToObject(id) as GameObject;
                    if (gameObj != null)
                    {
                        OnUpdatePrefabInstance(gameObj);
                    }
                }
            }
        }

        /// <summary>Invokes the on update prefab instance event for a game object.</summary>
        /// <param name="gameObject">
        /// prefab root instance that was updated because its source prefab was
        /// modified.
        /// </param>
        public void InvokeOnUpdatePrefabInstance(GameObject gameObject)
        {
            if (OnUpdatePrefabInstance != null && gameObject != null)
            {
                OnUpdatePrefabInstance(gameObject);
            }
        }

        /// <summary>Invokes the on update prefab instances event.</summary>
        public void InvokeOnUpdatePrefabInstances()
        {
            if (OnUpdatePrefabInstances != null)
            {
                OnUpdatePrefabInstances();
            }
        }

        /// <summary>
        /// Invokes the on hierarchy strcture change event. This event means anything may have changed on the game
        /// object or any of its descendants.
        /// </summary>
        /// <param name="whose">whose hierarchy structure changed.</param>
        public void InvokeOnHierarchyStructureChange(GameObject gameObject)
        {
            if (OnHierarchyStructureChange != null && gameObject != null)
            {
                OnHierarchyStructureChange(gameObject);
            }
        }

        /// <summary>
        /// Invokes the on parent change event if it hasn't already been invoked for the game object. If the instance id
        /// is zero and the previous parent and new parent ids from the event are the same, invokes the on reorder
        /// children event instead.
        /// </summary>
        /// <param name="stream">stream of change events.</param>
        /// <param name="index">index of event in stream to invoke event for.</param>
        private void InvokeParentChangeEvent(ref ObjectChangeEventStream stream, int index)
        {
            ChangeGameObjectParentEventArgs data;
            stream.GetChangeGameObjectParentEvent(index, out data);

            // Unity's API is missing a function to add a children order change event to an event stream, so instead we
            // store a parent change event in the prefab event manager with instance id 0 and the previous and next
            // parent id set to the id of the object whose children changed for child order change events.
            GameObject gameObj;
            if (data.instanceId == 0 && data.previousParentInstanceId == data.newParentInstanceId)
            {
                if (!SetEventFlag(data.newParentInstanceId, Events.REORDER_CHILDREN) || OnReorderChildren == null)
                {
                    return;
                }
                gameObj = EditorUtility.InstanceIDToObject(data.newParentInstanceId) as GameObject;
                if (gameObj != null)
                {
                    OnReorderChildren(gameObj);
                }
                return;
            }

            if (!SetEventFlag(data.instanceId, Events.CHANGE_PARENT) || OnParentChange == null)
            {
                return;
            }
            gameObj = EditorUtility.InstanceIDToObject(data.instanceId) as GameObject;
            if (gameObj != null)
            {
                OnParentChange(gameObj);
            }
        }

        /// <summary>Invokes the on parent change event.</summary>
        /// <param name="gameObject">gameObject whose parent changed.</param>
        public void InvokeOnParentChange(GameObject gameObject)
        {
            if (OnParentChange != null && gameObject != null)
            {
                OnParentChange(gameObject);
            }
        }

#if UNITY_2022_2_OR_NEWER
        /// <summary>
        /// Invokes the on reorder children event if it hasn't already been invoked for the game object.
        /// </summary>
        /// <param name="stream">stream of change events.</param>
        /// <param name="index">index of event in stream to invoke event for.</param>
        private void InvokeReorderChildrenEvent(ref ObjectChangeEventStream stream, int index)
        {
            ChangeChildrenOrderEventArgs data;
            stream.GetChangeChildrenOrderEvent(index, out data);
            if (!SetEventFlag(data.instanceId, Events.REORDER_CHILDREN) || OnReorderChildren == null)
            {
                return;
            }
            GameObject gameObj = EditorUtility.InstanceIDToObject(data.instanceId) as GameObject;
            if (gameObj != null)
            {
                OnReorderChildren(gameObj);
            }
        }
#endif

        /// <summary>Invokes the on reorder children event for a game object.</summary>
        /// <param name="gameObj">gameObj to invoke the event for.</param>
        public void InvokeOnReorderChildren(GameObject gameObj)
        {
            if (OnReorderChildren != null && gameObj != null)
            {
                OnReorderChildren(gameObj);
            }
        }

        /// <summary>
        /// Invokes the on properties change event if we don't know which properties changed and it hasn't already been
        /// invoked for the game object. Usually when properties change, Unity fires other events to tell us which
        /// properties changed, however in some cases such as when reverting prefab instance overrides or when using
        /// Undo.RegsiterCompleteObjectUndo, Unity does not tell us what changed so we invoke this event.
        /// </summary>
        /// <param name="stream">stream of change events.</param>
        /// <param name="index">index of event in stream to invoke event for.</param>
        /// <param name="invokeAddRemoveComponentForPrefabVariant">
        /// if true, will also invoke the on add or remove
        /// components event if the uobject is a prefab variant and we don't know which properties changed.
        /// Unity fires a properties changed event with no property modifications when you revert a removed
        /// component on a prefab variant. Unity fires the structure change event when modifying a prefab
        /// variant in the prefab stage, so we don't need to do this for events from the prefab stage.
        /// </param>
        private void InvokePropertiesChangedEvent(
            ref ObjectChangeEventStream stream,
            int index,
            bool invokeAddRemoveComponentsForPrefabVariant)
        {
            ChangeGameObjectOrComponentPropertiesEventArgs data;
            stream.GetChangeGameObjectOrComponentPropertiesEvent(index, out data);
            bool canInvoke = SetEventFlag(data.instanceId, Events.CHANGE_PROPERTIES);
            if (sfUndoManager.Get().HasPendingPropertyModifications)
            {
                return;
            }
            UObject uobj = EditorUtility.InstanceIDToObject(data.instanceId);
            if (uobj != null)
            {
                if (canInvoke && OnPropertiesChanged != null)
                {
                    OnPropertiesChanged(uobj);
                }
                if (invokeAddRemoveComponentsForPrefabVariant)
                {
                    GameObject gameObj = uobj as GameObject;
                    if (gameObj != null && PrefabUtility.IsPartOfPrefabAsset(gameObj) &&
                        PrefabUtility.IsPartOfPrefabInstance(gameObj) &&
                        SetEventFlag(data.instanceId, Events.ADD_REMOVE_COMPONENT) && OnAddOrRemoveComponents != null)
                    {
                        OnAddOrRemoveComponents(gameObj);
                    }
                }
            }
        }

        /// <summary>
        /// Invokes the on properties change event for an asset if we don't know which properties changed and it hasn't
        /// already been invoked for the asset. Usually when properties change, Unity fires other events to tell us
        /// which properties changed, however in some cases such as when editing material shader propertis or using
        /// Undo.RegsiterCompleteObjectUndo, Unity does not tell us what changed so we invoke this event.
        /// </summary>
        /// <param name="stream">stream of change events.</param>
        /// <param name="index">index of event in stream to invoke event for.</param>
        private void InvokePropertiesChangedEventForAsset(ref ObjectChangeEventStream stream, int index)
        {
            ChangeAssetObjectPropertiesEventArgs data;
            stream.GetChangeAssetObjectPropertiesEvent(index, out data);
            if (!SetEventFlag(data.instanceId, Events.CHANGE_PROPERTIES) || OnPropertiesChanged == null ||
                sfUndoManager.Get().HasPendingPropertyModifications)
            {
                return;
            }
            UObject uobj = EditorUtility.InstanceIDToObject(data.instanceId);
            if (uobj != null)
            {
                OnPropertiesChanged(uobj);
            }
        }

        /// <summary>
        /// Invokes the on properties changed event. This fires when properties changed but we don't know which
        /// properties changed.
        /// </summary>
        /// <param name="uobj">uobj whose properties changed.</param>
        public void InvokeOnPropertiesChanged(UObject uobj)
        {
            if (OnPropertiesChanged != null && uobj != null)
            {
                OnPropertiesChanged(uobj);
            }
        }

        /// <summary>
        /// Invokes the on add or remove components event if it or a create event hasn't already been invoked for the
        /// game object.
        /// </summary>
        /// <param name="stream">stream of change events.</param>
        /// <param name="index">index of event in stream to invoke event for.</param>
        private void InvokeAddOrRemoveComponentsEvent(ref ObjectChangeEventStream stream, int index)
        {
            ChangeGameObjectStructureEventArgs data;
            stream.GetChangeGameObjectStructureEvent(index, out data);
            if (!SetEventFlag(data.instanceId, Events.ADD_REMOVE_COMPONENT) || OnAddOrRemoveComponents == null)
            {
                return;
            }
            GameObject gameObject = EditorUtility.InstanceIDToObject(data.instanceId) as GameObject;
            if (gameObject != null)
            {
                OnAddOrRemoveComponents(gameObject);
            }
        }

        /// <summary>Invokes the on add or remove components event.</summary>
        /// <param name="gameObject">gameObject with added or removed components.</param>
        public void InvokeOnAddOrRemoveComponents(GameObject gameObject)
        {
            if (OnAddOrRemoveComponents != null && gameObject != null)
            {
                OnAddOrRemoveComponents(gameObject);
            }
        }

        /// <summary>Invokes the on modify object event.</summary>
        /// <param name="instanceId">instanceId of modified uobject.</param>
        public void InvokeOnModifyObject(int instanceId)
        {
            if (OnModifyObject != null)
            {
                OnModifyObject(instanceId);
            }
        }

        /// <summary>Sets event flag(s) in the invoked event map for a uobject.</summary>
        /// <param name="instanceId">instanceId of uobject to set event for.</param>
        /// <param name="flags">flags to set.</param>
        /// <returns>false if the flag(s) were already set.</returns>
        private bool SetEventFlag(int instanceId, Events flags)
        {
            Events events;
            if (m_invokedEventMap.TryGetValue(instanceId, out events) && (events & flags) == flags)
            {
                return false;
            }
            m_invokedEventMap[instanceId] = events | flags;
            if (events == Events.NONE && OnModifyObject != null)
            {
                OnModifyObject(instanceId);
            }
            return true;
        }

        /// <summary>Invokes the on heightmap change event. This fires when the terrain heightmap changed.</summary>
        /// <param name="terrain">the Terrain object that references a changed TerrainData asset.</param>
        /// <param name="changeArea">the area that the heightmap changed.</param>
        /// <param name="synced">indicates whether the changes were fully synchronized back to CPU memory.</param>
        public void InvokeOnHeightmapChange(Terrain terrain, RectInt changeArea, bool synced)
        {
            if (OnTerrainHeightmapChange != null && terrain != null)
            {
                OnTerrainHeightmapChange(terrain, changeArea, synced);
            }
        }

        /// <summary>Invokes the on texture change event. This fires when the terrain textures changed.</summary>
        /// <param name="terrain">the Terrain object that references a changed TerrainData asset.</param>
        /// <param name="textureName">the name of the texture that changed.</param>
        /// <param name="changeArea">the region of the Terrain texture that changed, in texel coordinates.</param>
        /// <param name="synced">indicates whether the changes were fully synchronized back to CPU memory.</param>
        public void InvokeOnTextureChange(Terrain terrain, string textureName, RectInt changeArea, bool synced)
        {
            if (OnTerrainTextureChange != null && terrain != null)
            {
                OnTerrainTextureChange(terrain, textureName, changeArea, synced);
            }
        }

        /// <summary>Invokes the on terrain detail change event.</summary>
        /// <param name="terrain">terrain component that references a changed TerrainData asset.</param>
        /// <param name="changeArea">changeArea of the details that changed.</param>
        /// <param name="layer">layer containing the changes.</param>
        public void InvokeOnTerrainDetailChange(Terrain terrain, RectInt changeArea, int layer)
        {
            if (OnTerrainDetailChange != null && terrain != null)
            {
                OnTerrainDetailChange(terrain.terrainData, changeArea, layer);
            }
        }

        /// <summary>Invokes the on terrain tree change event.</summary>
        /// <param name="terrain">terrain component that references a changed TerrainData asset.</param>
        /// <param name="hasRemovals">
        /// true if trees were removed. Trees may have also been added when this is true.
        /// </param>
        public void InvokeOnTerrainTreeChange(Terrain terrain, bool hasRemovals)
        {
            if (OnTerrainTreeChange != null && terrain != null)
            {
                OnTerrainTreeChange(terrain.terrainData, hasRemovals);
            }
        }

        /// <summary>
        /// Invokes the on terrain check event. This fires periodically when a terrain component is inspected to check
        /// for terrain changes that don't have Unity events.
        /// </summary>
        /// <param name="terrain">terrain to check for changes.</param>
        internal void InvokeTerrainCheck(Terrain terrain)
        {
            if (OnTerrainCheck != null && terrain != null)
            {
                OnTerrainCheck(terrain);
            }
        }
    }
}
