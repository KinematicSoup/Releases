using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using KS.SceneFusion2.Client;
using KS.SceneFusion;
using KS.SF.Reactor;
using KS.SF.Unity.Editor;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>Manages syncing of game objects.</summary>
    public class sfGameObjectTranslator : sfBaseUObjectTranslator
    {
        /// <summary>Lock type</summary>
        public enum LockType
        {
            NOT_SYNCED,
            UNLOCKED,
            PARTIALLY_LOCKED,
            FULLY_LOCKED
        }

        /// <summary>Lock state change event handler.</summary>
        /// <param name="gameObject">gameObject whose lock state changed.</param>
        /// <param name="lockType"></param>
        /// <param name="user">user who owns the lock, or null if the object is not fully locked.</param>
        public delegate void OnLockStateChangeHandler(GameObject gameObject, LockType lockType, sfUser user);

        /// <summary>Invoked when a game object's lock state changes.</summary>
        public event OnLockStateChangeHandler OnLockStateChange;

        /// <summary>Missing prefab asset event handler.</summary>
        /// <param name="gameObj">prefab game object instance that is missing its prefab asset.</param>
        public delegate void MissingPrefabHandler(GameObject gameObj);

        /// <summary>
        /// Invoked when a prefab game object instance with a missing prefab asset is found. This is not the same as a
        /// prefab stand-in (a game object with a sfMissingPrefab component).
        /// </summary>
        public event MissingPrefabHandler OnMissingPrefab;

        // Don't sync gameobjects with these types of components
        private HashSet<Type> m_blacklist = new HashSet<Type>();

        /// <summary>Determines the target of a file id.</summary>
        private enum FileIdTarget
        {
            PREFAB_ASSET = 0,
            PREFAB_SOURCE = 1,
            PREFAB_INSTANCE_HANDLE = 2
        }

        /// <summary>
        /// Stores the sfObject a game object has as its parent and its child index. Used to reattach the game object as
        /// a child of the parent. This is used with unsynced game objects that do not have an sfObject containing the
        /// parent relationship.
        /// </summary>
        private struct AttachmentInfo
        {
            /// <summary>
            /// Parents sfObject. This is a transform Component sfObject, or a Hierarchy sfObject for root game objects.
            /// </summary>
            public sfObject Parent;

            /// <summary>The child game object.</summary>
            public GameObject Child;

            /// <summary>The child index.</summary>
            public int Index;

            /// <summary>Constructor</summary>
            /// <param name="child">
            /// child to create attachment info for. Throws an ArgumentException if this is a root
            /// object.
            /// </param>
            public AttachmentInfo(GameObject child)
            {
                if (child.transform.parent == null)
                {
                    throw new ArgumentException("Child cannot be a root game object.");
                }
                Parent = sfObjectMap.Get().GetSFObject(child.transform.parent);
                Child = child;
                Index = child.transform.GetSiblingIndex();
            }

            /// <summary>Reattaches the child to its parent. If the parent doesn't exist, destroys the child.</summary>
            public void Restore()
            {
                if (Child == null)
                {
                    return;
                }
                Transform parent = sfObjectMap.Get().Get<Transform>(Parent);
                if (parent == null)
                {
                    UObject.DestroyImmediate(Child);
                    return;
                }
                // We need to apply serialized properties before modifying the parent's children, otherwise the child
                // modifications may be lost when we apply serialized properties, corrupting the hierarchy.
                sfPropertyManager.Get().ApplySerializedProperties(parent);
                Child.transform.SetParent(parent);
                if (Index < parent.childCount - 1)
                {
                    Child.transform.SetSiblingIndex(Index);
                }
            }
        }

        private bool m_reachedObjectLimit = false;
        private bool m_relockObjects = false;
        private List<sfObject> m_recreateList = new List<sfObject>();
        private List<AttachmentInfo> m_reattachList = new List<AttachmentInfo>();

        // Maps prefab variant paths to prefab source paths that need to be created before the prefab variant.
        private sfDependencySorter<string> m_prefabDependencies = new sfDependencySorter<string>();
        // Maps prefab variant paths to sfObjects whose game object creation was delayed because the prefab source
        // wasn't created yet.
        private Dictionary<string, List<sfObject>> m_prefabObjectCreateMap = new Dictionary<string, List<sfObject>>();

        private HashSet<GameObject> m_tempUnlockedObjects = new HashSet<GameObject>();
        private HashSet<sfObject> m_parentsWithNewChildren = new HashSet<sfObject>();
        private HashSet<sfObject> m_serverHierarchyChangedSet = new HashSet<sfObject>();
        private HashSet<sfObject> m_localHierarchyChangedSet = new HashSet<sfObject>();
        private HashSet<GameObject> m_applyPropertiesSet = new HashSet<GameObject>();
        private HashSet<sfObject> m_revisedPrefabs = new HashSet<sfObject>();
        private List<GameObject> m_prefabInstanceUpdateList = new List<GameObject>();
        private HashSet<sfObject> m_lockedPrefabInstancesWithUpdates = new HashSet<sfObject>();
        private Dictionary<int, sfObject> m_instanceIdToSFObjectMap = new Dictionary<int, sfObject>();
        // Maps missing prefab paths to notifications.
        private Dictionary<string, sfNotification> m_missingPrefabNotificationMap = 
            new Dictionary<string, sfNotification>();

        /// <summary>Initialization</summary>
        public override void Initialize()
        {
            sfSessionsMenu.CanSync = IsSyncable;

            sfPropertyManager.Get().SyncedHiddenProperties.Add<GameObject>("m_IsActive");

            DontSyncObjectsWith<sfGuidList>();
            DontSyncObjectsWith<sfIgnore>();

            PostPropertyChange.Add<GameObject>("m_Name",
                (UObject uobj, sfBaseProperty prop) => sfHierarchyWatcher.Get().MarkHierarchyStale());
            PostPropertyChange.Add<GameObject>("m_Icon",
                (UObject uobj, sfBaseProperty prop) => sfLockManager.Get().RefreshLock((GameObject)uobj));
            PostPropertyChange.Add<GameObject>("m_IsActive",
                 (UObject uobj, sfBaseProperty prop) => sfUI.Get().MarkSceneViewStale());

            m_propertyChangeHandlers.Add<GameObject>(sfProp.PrefabSourcePath, (UObject uobj, sfBaseProperty prop) =>
            {
                OnPrefabPathChange((GameObject)uobj, prop);
                return true;
            });
        }

        /// <summary>Called after connecting to a session.</summary>
        public override void OnSessionConnect()
        {
            sfSelectionWatcher.Get().OnSelect += OnSelect;
            sfSceneSaveWatcher.Get().PreSave += PreSave;
            sfSceneSaveWatcher.Get().PostSave += RelockObjects;
            sfUnityEventDispatcher.Get().PreUpdate += PreUpdate;
            sfUnityEventDispatcher.Get().OnUpdate += Update;
            sfUnityEventDispatcher.Get().OnCreate += OnCreateGameObject;
            sfUnityEventDispatcher.Get().OnCreate += OnCreateGameObject;
            sfUnityEventDispatcher.Get().OnDelete += OnDeleteGameObject;
            sfUnityEventDispatcher.Get().OnCreatePrefabChild += OnCreatePrefabChild;
            sfUnityEventDispatcher.Get().OnAddOrRemoveComponents += OnAddOrRemoveComponents;
            sfUnityEventDispatcher.Get().OnHierarchyStructureChange += OnHierarchyStructureChange;
            sfUnityEventDispatcher.Get().OnUpdatePrefabInstance += HandleUpdatePrefabInstance;
            sfUnityEventDispatcher.Get().OnParentChange += MarkParentHierarchyStale;
            sfUnityEventDispatcher.Get().OnReorderChildren += MarkHierarchyStale;
            sfUnityEventDispatcher.Get().OnOpenPrefabStage += HandleOpenPrefabStage;
            sfUnityEventDispatcher.Get().OnClosePrefabStage += HandleClosePrefabStage;
            sfHierarchyWatcher.Get().OnDragCancel += RelockObjects;
            sfHierarchyWatcher.Get().OnDragComplete += OnHierarchyDragComplete;
            sfHierarchyWatcher.Get().OnValidateDrag += ValidateHierarchyDrag;
            ksEditorEvents.OnImportAssets += HandleImportAssets;
        }

        /// <summary>Called after disconnecting from a session.</summary>
        public override void OnSessionDisconnect()
        {
            m_reachedObjectLimit = false;
            m_recreateList.Clear();
            m_reattachList.Clear();
            m_prefabInstanceUpdateList.Clear();
            m_tempUnlockedObjects.Clear();
            m_parentsWithNewChildren.Clear();
            m_serverHierarchyChangedSet.Clear();
            m_localHierarchyChangedSet.Clear();
            m_applyPropertiesSet.Clear();
            m_instanceIdToSFObjectMap.Clear();
            m_missingPrefabNotificationMap.Clear();
            m_prefabDependencies.Clear();
            m_prefabObjectCreateMap.Clear();
            m_revisedPrefabs.Clear();
            m_lockedPrefabInstancesWithUpdates.Clear();

            sfSelectionWatcher.Get().OnSelect -= OnSelect;
            sfSceneSaveWatcher.Get().PreSave -= PreSave;
            sfSceneSaveWatcher.Get().PostSave -= RelockObjects;
            sfUnityEventDispatcher.Get().PreUpdate -= PreUpdate;
            sfUnityEventDispatcher.Get().OnUpdate -= Update;
            sfUnityEventDispatcher.Get().OnCreate -= OnCreateGameObject;
            sfUnityEventDispatcher.Get().OnDelete -= OnDeleteGameObject;
            sfUnityEventDispatcher.Get().OnCreatePrefabChild -= OnCreatePrefabChild;
            sfUnityEventDispatcher.Get().OnAddOrRemoveComponents -= OnAddOrRemoveComponents;
            sfUnityEventDispatcher.Get().OnHierarchyStructureChange -= OnHierarchyStructureChange;
            sfUnityEventDispatcher.Get().OnUpdatePrefabInstance -= HandleUpdatePrefabInstance;
            sfUnityEventDispatcher.Get().OnParentChange -= MarkParentHierarchyStale;
            sfUnityEventDispatcher.Get().OnReorderChildren -= MarkHierarchyStale;
            sfUnityEventDispatcher.Get().OnOpenPrefabStage -= HandleOpenPrefabStage;
            sfUnityEventDispatcher.Get().OnClosePrefabStage -= HandleClosePrefabStage;
            sfHierarchyWatcher.Get().OnDragCancel -= RelockObjects;
            sfHierarchyWatcher.Get().OnDragComplete -= OnHierarchyDragComplete;
            sfHierarchyWatcher.Get().OnValidateDrag -= ValidateHierarchyDrag;
            ksEditorEvents.OnImportAssets -= HandleImportAssets;

            // Unlock all game objects
            foreach (GameObject gameObject in sfUnityUtils.IterateGameObjects())
            {
                sfObject obj = sfObjectMap.Get().GetSFObject(gameObject);
                if (obj != null && obj.IsLocked)
                {
                    Unlock(gameObject);
                }
            }
        }

        /// <summary>Called every pre-update.</summary>
        /// <param name="deltaTime">deltaTime in seconds since the last update.</param>
        private void PreUpdate(float deltaTime)
        {
            // Relock objects that were temporarily unlocked to make dragging in the hieararchy window work
            if (m_relockObjects)
            {
                RelockObjects();
                m_relockObjects = false;
            }

            // Send changes for prefab instances in the update list if they weren't already updated by someone else
            // (their revision numbers don't match their prefabs).
            if (m_prefabInstanceUpdateList.Count > 0)
            {
                foreach (GameObject gameObj in m_prefabInstanceUpdateList)
                {
                    UpdatePrefabInstanceObjects(gameObj);
                }
                m_prefabInstanceUpdateList.Clear();
            }

            RecreateGameObjects();

            // Sync the hierarchy for objects with local hierarchy changes
            SyncChangedHierarchies();

            // Reapply properties to game objects in the apply properties set and their components
            foreach (GameObject gameObject in m_applyPropertiesSet)
            {
                sfObject obj = sfObjectMap.Get().GetSFObject(gameObject);
                if (obj == null || gameObject == null)
                {
                    continue;
                }
                sfPropertyManager.Get().ApplyProperties(gameObject, (sfDictionaryProperty)obj.Property);
                foreach (Component component in gameObject.GetComponents<Component>())
                {
                    obj = sfObjectMap.Get().GetSFObject(component);
                    if (obj != null)
                    {
                        sfPropertyManager.Get().ApplyProperties(component, (sfDictionaryProperty)obj.Property);
                    }
                }
            }
            m_applyPropertiesSet.Clear();

            // Upload new game objects
            UploadGameObjects();

            m_revisedPrefabs.Clear();
        }

        /// <summary>Called every update.</summary>
        /// <param name="deltaTime">deltaTime in seconds since the last update.</param>
        private void Update(float deltaTime)
        {
            CreateDependentPrefabVariants();

            // Apply hierarchy changes from the server
            ApplyHierarchyChanges();

            if (!m_reachedObjectLimit)
            {
                sfSession session = SceneFusion.Get().Service.Session;
                if (session != null)
                {
                    uint limit = session.GetObjectLimit(sfType.GameObject);
                    if (limit != uint.MaxValue && session.GetObjectCount(sfType.GameObject) >= limit)
                    {
                        m_reachedObjectLimit = true;
                        EditorUtility.DisplayDialog("Game Object Limit Reached",
                            "You cannot create more game objects because you reached the " + limit +
                            " game object limit.", "OK");
                    }
                }
            }
        }

        /// <summary>Prevents game objects with components of type T from syncing.</summary>
        public void DontSyncObjectsWith<T>() where T : Component
        {
            m_blacklist.Add(typeof(T));
        }

        /// <summary>Prevents game objects with components of the given type from syncing.</summary>
        /// <param name="type">type of component whose game objects should not sync.</param>
        public void DontSyncObjectsWith(Type type)
        {
            m_blacklist.Add(type);
        }

        /// <summary>
        /// Checks if objects with the given component can be synced. Returns false if DontSyncObjectsWith was called
        /// with the component's type or one of its base types.
        /// </summary>
        /// <param name="component">component to check.</param>
        /// <returns>true if objects with the given component can be synced.</returns>
        public bool CanSyncObjectsWith(Component component)
        {
            if (component == null)
            {
                return true;
            }
            foreach (Type type in m_blacklist)
            {
                if (type.IsAssignableFrom(component.GetType()))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Checks if a game object can be synced. Objects can be synced if the following conditions are met:
        /// - They are not hidden
        /// - They can be saved in the editor
        /// - They have no components that prevent the object from syncing. sfIngore, sfGuidList, or a component type
        /// that DontSyncObjectsWith was called with will prevent objects from syncing.
        /// - They are not prefab stage objects.
        /// - They are not prefab assets or prefab syncing is enabled.
        /// </summary>
        /// <param name="gameObject"></param>
        /// <returns>true if the game object can be synced.</returns>
        public bool IsSyncable(GameObject gameObject)
        {
            return gameObject != null && 
                (gameObject.hideFlags & (HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor)) == HideFlags.None
                && !HasComponentThatPreventsSync(gameObject) && PrefabStageUtility.GetPrefabStage(gameObject) == null &&
                (sfConfig.Get().SyncPrefabs == sfConfig.PrefabSyncMode.FULL ||
                !PrefabUtility.IsPartOfPrefabAsset(gameObject));
        }

        /// <summary>
        /// Called when a uobject is replaced with another uobject, which happens when we change the local file id of an
        /// asset. Removes the mapping for the sfObject and old uobject and maps the sfObject to the new uobject,
        /// updates the instance id map, and updates references from the old uobject to reference the new uobject.
        /// </summary>
        /// <param name="obj">obj whose uobject was replaced.</param>
        /// <param name="old">old uobject.</param>
        /// <param name="new">new uobject.</param>
        public override void OnReplace(sfObject obj, UObject oldUObj, UObject newUObj)
        {
            GameObject oldGameObject = oldUObj as GameObject;
            GameObject newGameObject = newUObj as GameObject;
            if (newGameObject == null)
            {
                if (newUObj == null)
                {
                    ksLog.Error(this, "OnReplace called with null newUObj.");
                }
                ksLog.Error(this, "OnReplace called with non-gameobject newUObj " + newUObj + "."); 
                return;
            }
            RemoveMapping(oldGameObject);
            AddMapping(obj, newGameObject);
            sfReferenceProperty[] references = SceneFusion.Get().Service.Session.GetReferences(obj);
            sfPropertyManager.Get().SetReferences(newUObj, references);
        }

        /// <summary>
        /// Called when the prefab instance handle (which stores the overrides) for a prefab variant is replaced with
        /// a different instance because the local file id changed. Removes the mapping for the old game objects and
        /// components in the prefab variant and maps the new uobjects to their sfObjects, and updates reference from the
        /// old uobjects to the new uobjects.
        /// </summary>
        private void ReplacePrefabInstanceHandle(sfObject obj, UObject prefabHandle)
        {
            GameObject prefabRootInstance = sfUnityUtils.GetPrefabRootInstanceFromHandle(prefabHandle);
            if (prefabRootInstance == null)
            {
                ksLog.Warning(this, "Could not get prefab root instance from prefab instance handle " +
                    prefabHandle + ".");
                return;
            }
            ReplacePrefabVariant(obj, prefabRootInstance);
        }

        /// <summary>
        /// Called when a prefab variant is replaced by a different instance because the local file ids changed. If the
        /// given sfObject is mapped to a different game object, removes the old mapping and maps it to the new game
        /// object and the references to the old game object to reference the new game object, then updates the mapping
        /// and references for the descendants (components and game objects) to be mapped to the components and
        /// descendents of this game object.
        /// </summary>
        /// <param name="obj">obj to update mapping for.</param>
        /// <param name="gameObject">gameObject to map to the sfObject.</param>
        private void ReplacePrefabVariant(sfObject obj, GameObject gameObject)
        {
            GameObject oldGameObject = sfObjectMap.Get().Get<GameObject>(obj);
            if (oldGameObject == gameObject)
            {
                return;
            }
            OnReplace(obj, oldGameObject, gameObject);

            // Find the new components to map to the component sfObjects.
            sfComponentTranslator translator = sfObjectEventDispatcher.Get()
                .GetTranslator<sfComponentTranslator>(sfType.Component);
            sfComponentFinder finder = new sfComponentFinder(gameObject, translator);
            foreach (sfObject childObj in obj.Children)
            {
                if (childObj.Type != sfType.Component)
                {
                    continue;
                }
                Component component = finder.Find(childObj);
                if (component == null)
                {
                    continue;
                }
                Component oldComponent = sfObjectMap.Get().Get<Component>(childObj);
                if (oldComponent != component)
                {
                    translator.OnReplace(childObj, oldComponent, component);
                }
            }

            // Find child game objects that were replaced and update their mappings.
            Transform transform = gameObject.transform;
            sfObject transformObj = sfObjectMap.Get().GetSFObject(transform);
            if (transformObj == null)
            {
                return;
            }
            int index = 0;
            // Iterate the child game object sfObjects
            foreach (sfObject childObj in transformObj.Children)
            {
                if (childObj.Type != sfType.GameObject)
                {
                    continue;
                }
                // Get the prefab source file id. If there isn't one, this object isn't part of the prefab variant so we
                // can skip it.
                sfBaseProperty prop;
                if (!((sfDictionaryProperty)childObj.Property).TryGetField(sfProp.PrefabSourceFileId, out prop))
                {
                    continue;
                }
                long fileId = (long)prop;

                // Find the next child game object that is part of the prefab variant.
                GameObject child = null;
                GameObject prefab = null;
                while (index < transform.childCount)
                {
                    child = transform.GetChild(index).gameObject;
                    if (IsSyncable(child))
                    {
                        prefab = PrefabUtility.GetCorrespondingObjectFromSource(child);
                        if (prefab != null && prefab.transform.parent != null)
                        {
                            break;
                        }
                    }
                    index++;
                }
                if (index >= transform.childCount)
                {
                    break;
                }

                // Check if this is the child we were looking for by checking the prefab source file id.
                if (fileId == sfLoader.Get().GetLocalFileId(prefab))
                {
                    index++;
                }
                else
                {
                    // It's not the child we want. Find the child by checking all the children.
                    child = FindPrefabChild(transformObj, fileId, FileIdTarget.PREFAB_SOURCE);
                }

                if (child != null)
                {
                    ReplacePrefabVariant(childObj, child);
                }
            }
        }

        /// <summary>
        /// Adds a mapping between an sfObject and a game object to the sfObjectMap and the instance id map.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="gameObject"></param>
        private void AddMapping(sfObject obj, GameObject gameObject)
        {
            sfObjectMap.Get().Add(obj, gameObject);
            m_instanceIdToSFObjectMap[gameObject.GetInstanceID()] = obj;
        }

        /// <summary>
        /// Removes a mapping between an sfObject and a game object from the sfObjectMap and the instance id map.
        /// </summary>
        /// <param name="gameObject">gameObject to remove the mapping for.</param>
        private sfObject RemoveMapping(GameObject gameObject)
        {
            if ((object)gameObject == null)
            {
                return null;
            }
            sfObject obj = sfObjectMap.Get().Remove(gameObject);
            m_instanceIdToSFObjectMap.Remove(gameObject.GetInstanceID());
            return obj;
        }

        /// <summary>
        /// Removes a mapping between an sfObject and a game object from the sfObjectMap and the instance id map.
        /// </summary>
        /// <param name="obj">obj to remove the mapping for.</param>
        private GameObject RemoveMapping(sfObject obj)
        {
            GameObject gameObject = sfObjectMap.Get().Remove(obj) as GameObject;
            if ((object)gameObject != null)
            {
                m_instanceIdToSFObjectMap.Remove(gameObject.GetInstanceID());
            }
            return gameObject;
        }

        /// <summary>
        /// Iterates a game object and its descendants and creates deterministic guids for any game objects that do not
        /// have a guid.
        /// </summary>
        /// <param name="gameObject"></param>
        public void CreateGuids(GameObject gameObject)
        {
            if (!IsSyncable(gameObject))
            {
                return;
            }
            sfGuidManager.Get().GetGuid(gameObject, true);
            foreach (Transform child in gameObject.transform)
            {
                CreateGuids(child.gameObject);
            }
        }

        /// <summary>
        /// Applies server hierarchy changes to the local hierarchy (new children and child order) for objects with
        /// server hierarchy changes.
        /// </summary>
        public void ApplyHierarchyChanges()
        {
            foreach (sfObject parent in m_serverHierarchyChangedSet)
            {
                ApplyHierarchyChanges(parent);
            }
            m_serverHierarchyChangedSet.Clear();
        }

        /// <summary>
        /// Applies server hierarchy changes to the local hierarchy (new children and child order) of an object.
        /// </summary>
        /// <param name="parent">
        /// parent to apply hierarchy changes to. This should be a scene or transform object.
        /// </param>
        public void ApplyHierarchyChanges(sfObject parent)
        {
            int serverIndex = -1;
            int localIndex = 0;
            int childIndex = 0;
            List<GameObject> localChildren = GetChildGameObjects(parent);
            if (localChildren == null)
            {
                return;
            }
            Transform parentTransform = sfObjectMap.Get().Get<Transform>(parent);// null if the parent is a scene
            Scene scene = parentTransform == null ? sfObjectEventDispatcher.Get()
                .GetTranslator<sfSceneTranslator>(sfType.Scene).GetScene(parent) : parentTransform.gameObject.scene;
            // Apply serialized properties now so child order isn't lost when properties are applied later.
            sfPropertyManager.Get().ApplySerializedProperties(parentTransform);
            Dictionary<sfObject, int> childIndexes = null;
            HashSet<GameObject> skipped = null;
            // Unity only allows you to set the child index of one child at a time. Each time you set the child index
            // is O(n). A naive algorithm could easily become O(n^2) if it sets the child index on every child. This
            // algorithm minimizes the amount of child indexes changes for better performance in most cases, though the
            // worst case is still O(n^2).
            foreach (sfObject serverObj in parent.Children)
            {
                if (serverObj.Type != sfType.GameObject)
                {
                    continue;
                }
                serverIndex++;
                GameObject serverGameObject = sfObjectMap.Get().Get<GameObject>(serverObj);
                if (serverGameObject == null)
                {
                    continue;
                }
                if (serverGameObject.transform.parent != parentTransform ||
                    (parentTransform == null && serverGameObject.scene != scene))
                {
                    // The game object has a different parent. Set the parent and child index.
                    if (PrefabUtility.IsPartOfPrefabInstance(serverGameObject) &&
                        !PrefabUtility.IsOutermostPrefabInstanceRoot(serverGameObject))
                    {
                        // If the game object is a prefab child instance, Unity won't let us change the parent and will
                        // log an error if we try. Skip the object as it should get reparented for us when we update
                        // the source prefab asset.
                        continue;
                    }
                    sfComponentUtils.SetParent(serverGameObject.transform, parentTransform);
                    if (parentTransform == null && serverGameObject.scene != scene)
                    {
                        SceneManager.MoveGameObjectToScene(serverGameObject, scene);
                    }
                    serverGameObject.transform.SetSiblingIndex(childIndex);
                    sfObject transformObj = sfObjectMap.Get().GetSFObject(serverGameObject.transform);
                    if (transformObj != null)
                    {
                        sfPropertyManager.Get().ApplyProperties(serverGameObject.transform,
                            (sfDictionaryProperty)transformObj.Property);
                    }
                    sfHierarchyWatcher.Get().MarkHierarchyStale();
                    sfPrefabSaver.Get().MarkPrefabDirty(serverGameObject);
                    childIndex++;
                    continue;
                }

                if (skipped != null && skipped.Remove(serverGameObject))
                {
                    // We encountered this game object in the client list already and determined it should be moved
                    // when we found it in the server list. Set to chiledIndex -1 because its current index is lower
                    // and when we remove it, the destination index is decremented.
                    serverGameObject.transform.SetSiblingIndex(childIndex - 1);
                    sfHierarchyWatcher.Get().MarkHierarchyStale();
                    sfPrefabSaver.Get().MarkPrefabDirty(serverGameObject);
                    continue;
                }

                // serverDelta is how far to the left serverGameObject needs to move to get to the correct index. -1 means it
                // needs to be calculated. localDelta is the difference between serverIndex and the server child index of
                // localGameObject. We move the object with the greater delta as this gets us closer to the server state
                // and minimizes moves.
                int serverDelta = -1;
                while (localIndex < localChildren.Count)
                {
                    GameObject localGameObject = localChildren[localIndex];
                    if (localGameObject == serverGameObject)
                    {
                        // The game object does not need to be moved.
                        localIndex++;
                        childIndex++;
                        break;
                    }

                    sfObject localObj = sfObjectMap.Get().GetSFObject(localGameObject);
                    if (localObj == null || localObj.Parent != parent)
                    {
                        // The game object is not synced or has a different parent on the server. Its parent will
                        // change when we apply hierarchy changes to its new parent.
                        if (localObj != null && !m_serverHierarchyChangedSet.Contains(localObj.Parent))
                        {
                            ApplyHierarchyChanges(localObj.Parent);
                        }
                        localIndex++;
                        childIndex++;
                        continue;
                    }

                    if (childIndexes == null)
                    {
                        // Create map of sfObjects to child indexes for fast index lookups.
                        childIndexes = new Dictionary<sfObject, int>();
                        foreach (sfObject child in parent.Children)
                        {
                            childIndexes.Add(child, childIndexes.Count);
                        }
                    }

                    int localDelta = childIndexes[localObj] - serverIndex;
                    if (serverDelta < 0)
                    {
                        // Calculate serverDelta
                        for (int i = localIndex + 1; i < localChildren.Count; i++)
                        {
                            if (localChildren[i] == serverGameObject)
                            {
                                serverDelta = i - localIndex;
                                break;
                            }
                        }
                    }
                    else
                    {
                        // We moved childIndex one to the right, so serverDelta decreases by 1.
                        serverDelta--;
                    }
                    if (serverDelta > localDelta)
                    {
                        // Moving serverGameObject gets us closer to the server state than moving localGameObject. Move
                        // serverGameObject.
                        serverGameObject.transform.SetSiblingIndex(childIndex);
                        childIndex++;
                        // Since serverGameObject was moved we need to remove it from the client child list so we don't
                        // encounter it where it no longer is.
                        localChildren.RemoveAt(localIndex + serverDelta);
                        sfHierarchyWatcher.Get().MarkHierarchyStale();
                        sfPrefabSaver.Get().MarkPrefabDirty(serverGameObject);
                        break;
                    }
                    else
                    {
                        // Moving localGameObject gets us closer to the server state than moving serverGameObject. Add
                        // localGameObject to the skipped set and move it once we encounter it in the server list.
                        if (skipped == null)
                        {
                            skipped = new HashSet<GameObject>();
                        }
                        skipped.Add(localGameObject);
                        localIndex++;
                        childIndex++;
                    }
                }
            }
            while (localIndex < localChildren.Count)
            {
                GameObject localGameObject = localChildren[localIndex];
                sfObject localObj = sfObjectMap.Get().GetSFObject(localGameObject);
                if (localObj != null && localObj.IsSyncing && localObj.Parent != parent && 
                    !m_serverHierarchyChangedSet.Contains(localObj.Parent))
                {
                    // The game object has a different parent on the server. Apply hierarchy changes to its parent.
                    ApplyHierarchyChanges(localObj.Parent);
                }
                localIndex++;
            }
        }

        /// <summary>
        /// If the game object is synced, adds its sfObject to the local hierarchy changed set to have its child order
        /// synced in the next pre update.
        /// </summary>
        /// <param name="gameObject"></param>
        public void MarkHierarchyStale(GameObject gameObject)
        {
            if (gameObject != null)
            {
                sfObject obj = sfObjectMap.Get().GetSFObject(gameObject.transform);
                SyncHierarchyNextUpdate(obj);
            }
        }

        /// <summary>
        /// If the game object is synced, adds the game object's parent sfObject to the set of objects with local
        /// hierarchy changes to be synced in the next PreUpdate. If the sfObject is not synced but is syncable, adds
        /// the parent to the upload set to have new children uploaded in the next PreUpdate. If the game object is
        /// synced but the new parent is not, deletes the game object's sfObject.
        /// </summary>
        /// <param name="gameObject"></param>
        public void MarkParentHierarchyStale(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }
            sfObject obj = sfObjectMap.Get().GetSFObject(gameObject);
            if (obj == null || !obj.IsSyncing)
            {
                AddParentToUploadSet(gameObject);
                return;
            }
            sfObject parent;
            if (gameObject.transform.parent == null)
            {
                sfSceneTranslator translator = sfObjectEventDispatcher.Get().GetTranslator<sfSceneTranslator>(
                    sfType.Hierarchy);
                parent = translator.GetHierarchyObject(gameObject.scene);
            }
            else
            {
                parent = sfObjectMap.Get().GetSFObject(gameObject.transform.parent);
            }
            if (parent != null && parent.IsSyncing)
            {
                m_localHierarchyChangedSet.Add(parent);
            }
            else
            {
                SyncDeletedObject(obj);
            }
        }

        /// <summary>
        /// Sends hierarchy changes (new children and child order) for objects with local hierarchy changes. If the
        /// children are locked, reverts them to their server location.
        /// </summary>
        public void SyncChangedHierarchies()
        {
            foreach (sfObject parent in m_localHierarchyChangedSet)
            {
                SyncHierarchy(parent);
            }
            m_localHierarchyChangedSet.Clear();
        }

        /// <summary>
        /// Sends hierarchy changes (new children and child order) for an object to the server on the next update. If
        /// the children are locked, reverts them to their server location.
        /// </summary>
        public void SyncHierarchyNextUpdate(sfObject parent)
        {
            if (parent != null)
            {
                m_localHierarchyChangedSet.Add(parent);
            }
        }

        /// <summary>
        /// Sends hierarchy changes (new children and child order) for an object to the server. If the children are
        /// locked, reverts them to their server location.
        /// </summary>
        public void SyncHierarchy(sfObject parent)
        {
            if (!parent.IsSyncing)
            {
                return;
            }
            if (parent.IsFullyLocked)
            {
                // Put the parent in the server changed set so it is reverted to the server state at the end of the
                // frame.
                m_serverHierarchyChangedSet.Add(parent);
                return;
            }
            List<GameObject> localChildren = GetChildGameObjects(parent);
            if (localChildren == null)
            {
                return;
            }
            bool changed = false;
            Transform parentTransform = sfObjectMap.Get().Get<Transform>(parent);
            int serverIndex = 0;
            IEnumerator<sfObject> iter = parent.Children.GetEnumerator();
            bool iterValid = iter.MoveNext();
            // Iterate the client children
            for (int localIndex = 0; localIndex < localChildren.Count; localIndex++)
            {
                GameObject localGameObject = localChildren[localIndex];
                sfObject localObj = sfObjectMap.Get().GetSFObject(localGameObject);
                if (localObj == null || !localObj.IsSyncing)
                {
                    // localGameObject is not synced. Ignore it.
                    continue;
                }
                bool moved = true;
                // Iterate the server children
                while (iterValid)
                {
                    sfObject serverObj = iter.Current;
                    if (serverObj == localObj)
                    {
                        // We found the matching child. We don't need to move it.
                        moved = false;
                        serverIndex++;
                        iterValid = iter.MoveNext();
                        break;
                    }
                    GameObject serverGameObject = sfObjectMap.Get().Get<GameObject>(serverObj);
                    if (serverGameObject == null || serverGameObject.transform.parent != parentTransform)
                    {
                        // The server object has no game object or the game object has a different parent. The parent
                        // change will be sent when we sync the hierarchy for the new parent. Ignore it and continue
                        // iterating.
                        serverIndex++;
                        iterValid = iter.MoveNext();
                        continue;
                    }
                    // The child is not where we expected it. Either it needs to be moved or one or more other children
                    // need to be moved. eg. if the server has ABC and the client has CAB, you could move C to index 0,
                    // or you could move A to index 2, then B to index 2. We move the child if it is selected (since it
                    // needs to be selected to move it in the hierarchy), or if it has a different parent on the
                    // server.
                    if (localObj.Parent != parent || Selection.Contains(localGameObject))
                    {
                        break;
                    }
                    serverIndex++;
                    iterValid = iter.MoveNext();
                }
                if (!moved)
                {
                    continue;
                }
                if (!localObj.IsLocked)
                {
                    if (localObj.Parent != parent)
                    {
                        parent.InsertChild(serverIndex, localObj);
                        serverIndex++;
                        sfObject transformObj = sfObjectMap.Get().GetSFObject(localGameObject.transform);
                        if (transformObj != null)
                        {
                            sfPropertyManager.Get().SendPropertyChanges(localGameObject.transform,
                                (sfDictionaryProperty)transformObj.Property);
                        }
                    }
                    else
                    {
                        int oldIndex = parent.Children.IndexOf(localObj);
                        if (oldIndex < serverIndex)
                        {
                            serverIndex--;
                        }
                        localObj.SetChildIndex(serverIndex);
                        serverIndex++;
                    }
                    changed = true;
                }
                else
                {
                    // Put the object's parent in the server changed set so it is reverted to the server state at the
                    // end of the frame.
                    m_serverHierarchyChangedSet.Add(localObj.Parent);
                }
            }

            if (changed)
            {
                IncrementPrefabRevision(parent);
            }
        }

        /// <summary>
        /// Called when components are added or removed from a game object. Uploads the object if it became syncable
        /// because a component that prevented it from syncing was removed.
        /// </summary>
        /// <param name="gameObject">gameObject that had components added or removed.</param>
        private void OnAddOrRemoveComponents(GameObject gameObject)
        {
            sfObject obj = sfObjectMap.Get().GetSFObject(gameObject);
            if (obj == null || !obj.IsSyncing)
            {
                // This will upload the game object if it became syncable.
                AddParentToUploadSet(gameObject);
            }
        }

        /// <summary>
        /// Called when changes are made to a game object and possibly any descendants of the game object. Sends changes
        /// for the game object and all of its descendants to the server, or reverts the changes if the objects are locked.
        /// </summary>
        /// <param name="gameObject">gameObject that changed.</param>
        private void OnHierarchyStructureChange(GameObject gameObject)
        {
            SyncAll(gameObject, true, sfUndoManager.Get().IsHandlingUndoRedo);
        }

        /// <summary>
        /// Called when a prefab instance is updated because it's source prefab was modified. This is called regardless
        /// of whether the local user or another user modified the source prefab. Iterates the game objects in the
        /// prefab instance looking for prefab root instances (which may be nested) whose revision numbers do not match
        /// their source prefabs, and if sends changes for them and their descendants and updates their revision numbers
        /// to match their source prefabs, or if they are locked, adds them to a set of objects to send changes for when
        /// they become unlocked if their revision numbers still don't match.
        /// </summary>
        /// <param name="gameObject">prefab instance game object that was updated.</param>
        private void HandleUpdatePrefabInstance(GameObject gameObject)
        {
            sfUnityUtils.ForEachInPrefabInstance(gameObject, (GameObject go) =>
            {
                return !UpdatePrefabInstanceObjects(go);
            });
        }

        /// <summary>
        /// If the game object is a prefab instance root (could be nested) whose revision numbers do not match their
        /// source prefabs, sends updates for the game object's sfObject and its descendants to the server if it is
        /// unlocked. If it is locked, adds it to a set of objects to send changes for when it becomes unlocked if its
        /// revision numbers still don't match. If the game object isn't synced, uploads it in the next pre update. If
        /// the game object's source prefab is not synced, sends changes for it and its descendants.
        /// </summary>
        /// <param name="gameObject">
        /// prefab instance game object to send updates for if the revision numbers
        /// don't match the source prefab.
        /// </param>
        /// <returns>true if changes could be sent for the game object.</returns>
        private bool UpdatePrefabInstanceObjects(GameObject gameObject)
        {
            // If the game object isn't synced, upload it.
            sfObject obj = sfObjectMap.Get().GetSFObject(gameObject);
            if (obj == null || !obj.IsSyncing)
            {
                AddParentToUploadSet(gameObject);
                return true;
            }

            // If the game object is not a prefab instance, do nothing.
            GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
            if (prefab == null)
            {
                return false;
            }

            // If the source prefab is not synced, send changes.
            if (!sfObjectMap.Get().Contains(prefab))
            {
                SyncAll(gameObject, true, sfUndoManager.Get().IsHandlingUndoRedo, false);
                return true;
            }

            // Get the prefab instance's revision numbers.
            uint[] instanceRevisions = null;
            sfDictionaryProperty properties = (sfDictionaryProperty)obj.Property;
            sfBaseProperty prop;
            if (properties.TryGetField(sfProp.InstanceRevisions, out prop))
            {
                instanceRevisions = (uint[])prop;
            }

            // Get the prefab source's revision numbers.
            List<uint> prefabRevisions = GetPrefabRevisions(prefab);
            if ((instanceRevisions == null && prefabRevisions == null) || (instanceRevisions != null &&
                prefabRevisions != null && instanceRevisions.SequenceEqual(prefabRevisions)))
            {
                return false;
            }

            if (obj.IsLocked)
            {
                // We cannot update the prefab instance sfObject because it's locked. We will update it when it becomes
                // unlocked if the revision numbers still don't match.
                m_lockedPrefabInstancesWithUpdates.Add(obj);
                return false;
            }

            // We do not want to increment the revision number of a prefab variant if it was updated because its source
            // prefab changed. If the game object is a prefab variant, add the root sfObject to the revised prefabs set
            // to prevent its revision number from incrementing.
            sfObject root = obj.Root;
            if (root.Type == sfType.GameObject)
            {
                m_revisedPrefabs.Add(root);
            }
            
            // Send changes for the prefab instance and its descendants and update the prefab instance revision number.
            SyncAll(gameObject, true, sfUndoManager.Get().IsHandlingUndoRedo);
            if (prefabRevisions != null)
            {
                properties[sfProp.InstanceRevisions] = prefabRevisions.ToArray();
            }

            // Rebuild the prefab stage map if we are editing this prefab in a prefab stage.
            if (sfUnityUtils.IsOpenInPrefabStage(gameObject))
            {
                sfPrefabStageMap.Get().Rebuild();
            }
            return true;
        }

        /// <summary>
        /// Sends all changes for a game object and its components to the server, or reverts it to the server state if
        /// the object is locked. Optionally syncs changes for descendant game objects. Prefab source changes are only
        /// synced if descendants are synced recursively, as prefab source changes can change descendants and requires
        /// descendants to be synced. Game object creation and parent changes are synced in the next PreUpdate.
        /// </summary>
        /// <param name="gameObject">gameObject to sync all changes for.</param>
        /// <param name="if">
        /// if true, recursively syncs changes to all descendants, and syncs prefab source changes.
        /// </param>
        /// <param name="fixLockObjects">
        /// if true, creates lock objects for game objects missing lock objects if their
        /// sfObject is locked, and deletes lock objects from game objects with unlocked sfObjects. Lock objects
        /// are used to render lock shaders. This is needed when syncing changes made by undo, which can
        /// mess up the lock state of game objects.
        /// </param>
        /// <param name="syncInstanceRevision">
        /// if true and the game object is a prefab root instance (could be nested),
        /// updates its revision numbers to match its source prefab. The only reason to not do this is if we
        /// have or will update the revision numbers already and don't need to do it twice.
        /// </param>
        /// <param name="-">
        /// if true, will request a lock on the game object if it is unlocked and we haven't
        /// requested a lock before updating the sfObject, and will release the lock aftewards.
        /// </param>
        public void SyncAll(
            GameObject gameObject,
            bool recursive = false,
            bool fixLockObjects = false,
            bool syncInstanceRevisions = true,
            bool acquireTempLock = true)
        {
            sfObject obj = sfObjectMap.Get().GetSFObject(gameObject);
            if (obj == null || !obj.IsSyncing)
            {
                AddParentToUploadSet(gameObject);
                return;
            }

            // If acquireTempLock is true and the object is unlocked and we haven't requested a lock on the object,
            // lock in now and unlock it once we are done syncing changes.
            bool tempLock = acquireTempLock && !obj.IsLocked && !obj.IsLockRequested;
            if (tempLock)
            {
                obj.RequestLock();
            }

            sfComponentTranslator translator = sfObjectEventDispatcher.Get().GetTranslator<sfComponentTranslator>(
                sfType.Component);
            if (recursive)
            {
                if (SyncPrefabSource(gameObject))
                {
                    // SyncPrefabSource will update the prefab instance revision numbers if the prefab source changed,
                    // so we don't need to update them again.
                    syncInstanceRevisions = false;
                }
                // When we connect the game object to a prefab, we destroy it and recreate it later, so check if the
                // game object was destroyed.
                if (gameObject.IsDestroyed())
                {
                    return;
                }
            }

            if (PrefabUtility.GetPrefabInstanceStatus(gameObject) == PrefabInstanceStatus.MissingAsset)
            {
                ksLog.Warning(this, gameObject.name + " is missing its prefab asset. SyncAll will not sync " +
                    "properties for game objects with missing prefab assets.", gameObject);
            }
            else
            {
                SyncProperties(gameObject);
            }
            obj.FlushPropertyChanges();// Make sure prefab path changes are sent now.
            translator.SyncComponentOrder(gameObject);
            translator.SyncComponents(gameObject);

            sfObject parent = GetParentObject(gameObject);
            if (parent != null)
            {
                m_localHierarchyChangedSet.Add(parent);
            }

            // Set HideFlags.NotEditable to match lock state. If fixLockObjects is true, also create/delete lock objects
            // based on lock state. Prefab edit locking is handled by the sfGameObjectEditor, so we don't need to do
            // anything for prefab assets.
            if (!PrefabUtility.IsPartOfPrefabAsset(gameObject))
            {
                if (fixLockObjects)
                {
                    if (obj.IsLocked)
                    {
                        Lock(gameObject, obj);
                    }
                    else
                    {
                        Unlock(gameObject);
                    }
                }
                else if (obj.IsLocked && (gameObject.hideFlags & HideFlags.NotEditable) == 0)
                {
                    sfUnityUtils.AddFlags(gameObject, HideFlags.NotEditable);
                }
                else if (!obj.IsLocked && (gameObject.hideFlags & HideFlags.NotEditable) != 0)
                {
                    sfUnityUtils.RemoveFlags(gameObject, HideFlags.NotEditable);
                }
            }
            if (recursive)
            {
                for (int i = 0; i < gameObject.transform.childCount; i++)
                {
                    SyncAll(gameObject.transform.GetChild(i).gameObject, true, fixLockObjects, true,
                        acquireTempLock && !tempLock);
                }
                SyncDestroyedChildren(gameObject);
            }

            if (syncInstanceRevisions && !obj.IsLocked)
            {
                List<uint> revisions = GetPrefabSourceRevisions(gameObject);
                if (revisions != null)
                {
                    ((sfDictionaryProperty)obj.Property)[sfProp.InstanceRevisions] = revisions.ToArray();
                }
            }

            if (tempLock)
            {
                obj.ReleaseLock();
            }
        }

        /// <summary>
        /// Sends prefab path and prefab child index changes to the server if the game object's prefab source changed,
        /// or reverts the game object's prefab source to the server state if it is locked. This should be called on the
        /// prefab root instance first, and then on the descendants.
        /// </summary>
        /// <param name="gameObject">gameObject to sync prefab source for.</param>
        /// <returns>true if the prefab source changed.</returns>
        private bool SyncPrefabSource(GameObject gameObject)
        {
            sfObject obj = sfObjectMap.Get().GetSFObject(gameObject);
            if (obj == null || !obj.IsSyncing)
            {
                return false;
            }
            if (PrefabUtility.GetPrefabInstanceStatus(gameObject) == PrefabInstanceStatus.MissingAsset)
            {
                // Do not sync the prefab path for a broken prefab as we cannot determine what the prefab should be and
                // we don't want to unlink the prefab for other users who are not missing the prefab asset.
                return false;
            }
            string prefabPath;
            long fileId;
            GetPrefabInfo(gameObject, out prefabPath, out fileId);
            sfDictionaryProperty properties = (sfDictionaryProperty)obj.Property;
            sfBaseProperty prop;
            string currentPath = properties.TryGetField(sfProp.PrefabSourcePath, out prop) ? (string)prop : null;
            if (currentPath == prefabPath || (string.IsNullOrEmpty(currentPath) && string.IsNullOrEmpty(prefabPath)))
            {
                return false;
            }
            if (obj.IsLocked)
            {
                // Revert to the server state.
                OnPrefabPathChange(gameObject, prop);
            }
            else if (string.IsNullOrEmpty(prefabPath))
            {
                properties.RemoveField(sfProp.PrefabSourcePath);
                properties.RemoveField(sfProp.PrefabSourceFileId);
                properties.RemoveField(sfProp.InstanceRevisions);
                properties.RemoveField(sfProp.Prefab);

                // Remove the prefab source file ids from the components.
                foreach (Component component in gameObject.GetComponents<Component>())
                {
                    sfObject componentObj = sfObjectMap.Get().GetSFObject(component);
                    if (componentObj != null)
                    {
                        ((sfDictionaryProperty)componentObj.Property).RemoveField(sfProp.PrefabSourceFileId);
                    }
                }

                // We don't sync the file ids for prefab variant uobjects because they aren't written in the prefab
                // file and instead are generated deterministically from the prefab instance handle (the object
                // containing the prefab overrides) file id and the prefab source. If the uobject was a prefab
                // variant and no longer is, we need to sync the file ids for the game object and its components.
                if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
                {
                    properties[sfProp.FileId] = sfLoader.Get().GetLocalFileId(gameObject);
                    foreach (Component component in gameObject.GetComponents<Component>())
                    {
                        sfObject componentObj = sfObjectMap.Get().GetSFObject(component);
                        if (componentObj != null)
                        {
                            ((sfDictionaryProperty)componentObj.Property)[sfProp.FileId] = 
                                sfLoader.Get().GetLocalFileId(component);
                        }
                    }
                }
            }
            else
            {
                if (sfConfig.Get().SyncPrefabs == sfConfig.PrefabSyncMode.FULL)
                {
                    // Upload the prefab if it isn't already uploaded.
                    GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
                    if (prefab != null)
                    {
                        List<uint> revisions = GetPrefabSourceRevisions(gameObject, true);
                        if (revisions != null)
                        {
                            properties[sfProp.InstanceRevisions] = revisions.ToArray();
                        }

                        // We don't sync the file ids for prefab variants because they aren't written in the prefab file. If
                        // the object became a prefab variant, remove the file ids from the game object and its components,
                        // and sync the prefab instance handle file id that is used to generate the file ids of all uobjects
                        // in the prefab variant.
                        if (PrefabUtility.IsPartOfPrefabAsset(prefab) && string.IsNullOrEmpty(currentPath))
                        {
                            if (prefab.transform.parent == null)
                            {
                                // Sync the prefab instance handle file id on the root of the prefab variant.
                                UObject prefabHandle = PrefabUtility.GetPrefabInstanceHandle(gameObject);
                                properties[sfProp.FileId] = sfLoader.Get().GetLocalFileId(prefabHandle);
                            }
                            else
                            {
                                properties.RemoveField(sfProp.FileId);
                            }
                            foreach (Component component in gameObject.GetComponents<Component>())
                            {
                                sfObject componentObj = sfObjectMap.Get().GetSFObject(component);
                                if (componentObj != null)
                                {
                                    ((sfDictionaryProperty)componentObj.Property).RemoveField(sfProp.FileId);
                                }
                            }
                        }
                    }
                }

                properties[sfProp.PrefabSourcePath] = prefabPath;
                if (fileId == 0)
                {
                    properties.RemoveField(sfProp.PrefabSourceFileId);
                }
                else
                {
                    properties[sfProp.PrefabSourceFileId] = fileId;
                }
                // Sync the prefab source file ids for the components except for the Transform.
                Component[] components = gameObject.GetComponents<Component>();
                for (int i = 1; i < components.Length; i++)
                {
                    Component component = components[i];
                    fileId = sfLoader.Get().GetLocalFileId(PrefabUtility.GetCorrespondingObjectFromSource(component));
                    if (fileId == 0)
                    {
                        continue;
                    }
                    sfObject componentObj = sfObjectMap.Get().GetSFObject(component);
                    if (componentObj != null)
                    {
                        ((sfDictionaryProperty)componentObj.Property)[sfProp.PrefabSourceFileId] = fileId;
                    }
                }
            }
            return true;
        }

        /// <summary>Called when a game object's prefab path is changed by the server.</summary>
        /// <param name="gameObject">gameObject whose prefab path changed.</param>
        /// <param name="property">property that changed. Null if the game object no longer has a prefab path.</param>
        public void OnPrefabPathChange(GameObject gameObject, sfBaseProperty property)
        {
            // If the game object is a prefab instance whose revision numbers don't match its sources, revert unsynced
            // changes to the prefab instance that came from updating the prefab.
            if (IsPrefabInstanceRevisionStale(gameObject))
            {
                ApplyServerState(gameObject, true);
            }

            sfPrefabPreviewScene preview = null;
            try
            {
                // Unity does not let you unpack a prefab variant or nested prefab. If the object is a prefab variant or
                // nested prefab, we load a copy into a prefab scene and unpack that and save the changes to the prefab.
                GameObject unpackObject = gameObject;
                if (PrefabUtility.IsPartOfPrefabAsset(gameObject) &&
                    PrefabUtility.IsOutermostPrefabInstanceRoot(gameObject))
                {
                    preview = new sfPrefabPreviewScene(gameObject);
                    if (preview.RootObject == null)
                    {
                        ksLog.Error(this,
                            "Could not unpack prefab variant; could not load prefab into preview scene.");
                        return;
                    }
                    unpackObject = preview.FindEquivalentGameObject(gameObject);
                    if (unpackObject == null)
                    {
                        ksLog.Error(this, "Could not unpack prefab variant; could not find prefab object.");
                        return;
                    }
                }

                if (property == null)
                {
                    // Unpack the game object until it is no longer a prefab root instance. We unpack one level at a
                    // time in case the descendants are still part of a nested prefab instance.
                    while (PrefabUtility.IsOutermostPrefabInstanceRoot(unpackObject))
                    {
                        PrefabUtility.UnpackPrefabInstance(unpackObject, PrefabUnpackMode.OutermostRoot,
                            InteractionMode.AutomatedAction);
                        sfHierarchyWatcher.Get().MarkHierarchyStale();
                    }

                    sfMissingPrefab missingPrefab = unpackObject.GetComponent<sfMissingPrefab>();
                    if (missingPrefab != null)
                    {
                        UObject.DestroyImmediate(missingPrefab);
                        ksLinkedList<sfNotification> notifications = sfNotificationManager.Get()
                            .GetNotifications(gameObject);
                        if (notifications != null && notifications.Count > 0)
                        {
                            foreach (sfNotification notification in notifications)
                            {
                                if (notification.Category == sfNotificationCategory.MissingPrefab)
                                {
                                    sfNotificationManager.Get().RemoveNotificationFrom(notification, gameObject);
                                }
                            }
                        }
                    }
                    else
                    {
                        // Reapply properties next frame
                        m_applyPropertiesSet.Add(gameObject);
                    }

                    if (preview != null)
                    {
                        // Save the changes to the prefab.
                        preview.Save();
                    }
                }
                else
                {
                    string path = (string)property;
                    string currentPath;
                    // Unpack the prefab until we get the correct prefab or are no longer a prefab root instance.
                    while (PrefabUtility.IsOutermostPrefabInstanceRoot(unpackObject))
                    {
                        GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(unpackObject);
                        if (prefab != null)
                        {
                            currentPath = AssetDatabase.GetAssetPath(prefab);
                            if (path == currentPath)
                            {
                                // We unpacked to the correct prefab.
                                // Reapply properties next frame
                                m_applyPropertiesSet.Add(gameObject);
                                if (preview != null)
                                {
                                    // Save the changes to the prefab.
                                    preview.Save();
                                }
                                return;
                            }
                        }
                        PrefabUtility.UnpackPrefabInstance(unpackObject, PrefabUnpackMode.OutermostRoot,
                            InteractionMode.AutomatedAction);
                        sfHierarchyWatcher.Get().MarkHierarchyStale();
                    }
                    // Destroy the game object and recreate it as a prefab instance.
                    DestroyAndRecreate(gameObject);
                }
            }
            finally
            {
                if (preview != null)
                {
                    preview.Close();
                }
            }
        }

        /// <summary>
        /// Destroys a game object and adds it's sfObject to a list to be recreated in PreUpdate. This is used when we
        /// need to recreate a game object as a different prefab instance. We wait until PreUpdate before recreating it
        /// to ensure we have the updated properties--including the updated prefab path and child index--for this object
        /// and its children.
        /// </summary>
        /// <param name="gameObject">gameObject to destroy and recreate.</param>
        private void DestroyAndRecreate(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }
            sfObject root = sfObjectMap.Get().GetSFObject(gameObject);
            if (root == null)
            {
                return;
            }
            if (!sfUnityUtils.IsPrefabAssetRoot(gameObject))
            {
                List<GameObject> toDetach = new List<GameObject>();
                sfUnityUtils.ForEachDescendant(gameObject, (GameObject child) =>
                {
                    sfObject obj = sfObjectMap.Get().GetSFObject(child);
                    // Detach unsynced descendants and reattach them when the game object and its descendants are
                    // recreated.
                    if (obj == null || !obj.IsSyncing)
                    {
                        if (!sfLockManager.Get().IsLockObject(child))
                        {
                            m_reattachList.Add(new AttachmentInfo(child));
                            toDetach.Add(child);
                        }
                        return false;
                    }
                    // Detach descendants with a different parent on the server, and put the parent in the server
                    // hierarchy changed set to restore its children.
                    sfObject parent = sfObjectMap.Get().GetSFObject(child.transform.parent);
                    if (obj.Parent != parent && obj.Parent != root && !obj.Parent.IsDescendantOf(root))
                    {
                        m_serverHierarchyChangedSet.Add(obj.Parent);
                        toDetach.Add(child);
                    }
                    return true;
                });
                // Detach descendants we don't want destroyed before destroying the game object.
                for (int i = 0; i < toDetach.Count; i++)
                {
                    sfComponentUtils.SetParent(toDetach[i], null);
                }
            }
            sfObject obj = RemoveMapping(gameObject);
            if (obj != null)
            {
                obj.ForEachDescendant((sfObject child) =>
                {
                    RemoveMapping(child);
                    return true;
                });
            }
            DestroyGameObject(gameObject);
            // Recreate it in PreUpdate after we receive all property changes
            m_recreateList.Add(root);
        }

        /// <summary>Recreates the game objects for sfObjects in the recreate list.</summary>
        private void RecreateGameObjects()
        {
            if (m_recreateList.Count == 0)
            {
                return;
            }

            foreach (sfObject obj in m_recreateList)
            {
                if (obj.IsSyncing && !sfObjectMap.Get().Contains(obj))
                {
                    InitializeGameObjectHierarchy(obj);
                    GameObject gameObject = sfObjectMap.Get().Get<GameObject>(obj);
                    if (obj.IsLockRequested)
                    {
                        // If we have requested the lock on this object, the old game object was selected before it was
                        // destroyed. Select the new game object.
                        if (gameObject == null)
                        {
                            obj.ReleaseLock();
                        }
                        else
                        {
                            List<UObject> selection = new List<UObject>(Selection.objects);
                            selection.Add(gameObject);
                            Selection.objects = selection.ToArray();
                        }
                    }
                    if (!obj.IsLocked && gameObject != null && (gameObject.hideFlags & HideFlags.NotEditable) != 0)
                    {
                        // Sometimes the prefabs aren't editable which causes the prefab instances to be not editable,
                        // so we need make them editable.
                        foreach (GameObject go in sfUnityUtils.IterateSelfAndDescendants(gameObject))
                        {
                            sfUnityUtils.RemoveFlags(go, HideFlags.NotEditable);
                        }
                    }
                }
            }
            m_recreateList.Clear();

            // Reattach unsynced objects that were detached from recreated objects.
            foreach (AttachmentInfo attachment in m_reattachList)
            {
                attachment.Restore();
            }
            m_reattachList.Clear();
        }

        /// <summary>
        /// Creates prefab variants that could not be created earlier because their source prefab wasn't created yet.
        /// </summary>
        private void CreateDependentPrefabVariants()
        {
            if (m_prefabDependencies.Count == 0)
            {
                return;
            }
            // The iteratator iterates all of a prefab variant's source prefabs that need to be created before
            // iterating the variant, so we will create all the source prefabs before creating the variant.
            foreach (string path in m_prefabDependencies)
            {
                List<sfObject> prefabObjects;
                if (m_prefabObjectCreateMap.Remove(path, out prefabObjects))
                {
                    for (int i = 0; i < prefabObjects.Count; i++)
                    {
                        sfObject obj = prefabObjects[i];
                        InitializeGameObjectHierarchy(obj, true);
                    }
                }
            }
            m_prefabDependencies.Clear();
        }

        /// <summary>Temporarily unlocks a game object, and relocks it on the next update.</summary>
        /// <param name="gameObject">gameObject to unlock temporarily.</param>
        public void TempUnlock(GameObject gameObject)
        {
            if ((gameObject.hideFlags & HideFlags.NotEditable) != HideFlags.None)
            {
                m_relockObjects = true;// Relock objects on the next update
                sfUnityUtils.RemoveFlags(gameObject, HideFlags.NotEditable);
                m_tempUnlockedObjects.Add(gameObject);
            }
        }

        /// <summary>Relocks all game objects in a prefab instance on the next PreUpdate.</summary>
        /// <param name="gameObject">gameObject in prefab to relock.</param>
        public void RelockPrefabNextPreUpdate(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }
            sfUnityUtils.ForEachInPrefabInstance(gameObject, (GameObject go) =>
            {
                m_tempUnlockedObjects.Add(go);
                return true;
            });
            m_relockObjects = true;
        }

        /// <summary>
        /// Sends property changes for a game object and its components to the server. Reverts them to the server state
        /// if the object is locked.
        /// </summary>
        /// <param name="gameObject">gameObject to sync properties for.</param>
        /// <param name="recursive">if true, will recursively sync properties for child game objects.</param>
        public void SyncProperties(GameObject gameObject, bool recursive = false)
        {
            if (gameObject == null)
            {
                return;
            }
            sfObject obj = sfObjectMap.Get().GetSFObject(gameObject);
            if (obj == null || !obj.IsSyncing)
            {
                return;
            }
            if (obj.IsLocked)
            {
                sfPropertyManager.Get().ApplyProperties(gameObject, (sfDictionaryProperty)obj.Property);
            }
            else
            {
                sfPropertyManager.Get().SendPropertyChanges(gameObject, (sfDictionaryProperty)obj.Property);
            }

            foreach (Component component in gameObject.GetComponents<Component>())
            {
                obj = sfObjectMap.Get().GetSFObject(component);
                if (obj == null || !obj.IsSyncing)
                {
                    continue;
                }
                if (obj.IsLocked)
                {
                    sfPropertyManager.Get().ApplyProperties(component, (sfDictionaryProperty)obj.Property);
                }
                else
                {
                    sfPropertyManager.Get().SendPropertyChanges(component, (sfDictionaryProperty)obj.Property);
                }
            }

            if (recursive)
            {
                for (int i = 0; i < gameObject.transform.childCount; i++)
                {
                    SyncProperties(gameObject.transform.GetChild(i).gameObject);
                }
            }
        }

        /// <summary>
        /// Destroys server objects for destroyed children of a game object. Recreates the game object if the objects
        /// are locked.
        /// </summary>
        /// <param name="gameObject">gameObject to sync destroyed children for.</param>
        public void SyncDestroyedChildren(GameObject gameObject)
        {
            sfObject obj = sfObjectMap.Get().GetSFObject(gameObject.transform);
            if (obj == null || !obj.IsSyncing)
            {
                return;
            }
            foreach (sfObject childObj in obj.Children)
            {
                if (childObj.Type == sfType.GameObject && sfObjectMap.Get().Get<GameObject>(childObj).IsDestroyed())
                {
                    // Sync changed hierarchies before deleting the object in case descendants of the object were
                    // reparented.
                    SyncChangedHierarchies();
                    SyncDeletedObject(childObj);
                }
            }
        }

        /// <summary>Applies the server state to a game object and its components.</summary>
        /// <param name="gameObject">gameObject to apply server state for.</param>
        /// <param name="recursive">if true, will also apply server state to descendants of the game object.</param>
        public void ApplyServerState(GameObject gameObject, bool recursive = false)
        {
            sfObject obj = sfObjectMap.Get().GetSFObject(gameObject);
            if (obj != null && obj.IsSyncing)
            {
                ApplyServerState(obj, recursive);
            }
            else if (IsSyncable(gameObject))
            {
                DestroyGameObject(gameObject);
            }
        }

        /// <summary>Applies the server state to a game object and its components.</summary>
        /// <param name="obj">obj for the game object to apply server state for.</param>
        /// <param name="recursive">if true, will also apply server state to descendants of the game object.</param>
        public void ApplyServerState(sfObject obj, bool recursive = false)
        {
            if (obj.Type != sfType.GameObject)
            {
                return;
            }
            GameObject gameObject = sfObjectMap.Get().Get<GameObject>(obj);
            if (gameObject == null)
            {
                InitializeGameObjectHierarchy(obj);
                return;
            }
            m_serverHierarchyChangedSet.Add(obj.Parent);
            sfPropertyManager.Get().ApplyProperties(gameObject, (sfDictionaryProperty)obj.Property);
            sfComponentTranslator translator = sfObjectEventDispatcher.Get().GetTranslator<sfComponentTranslator>(
                sfType.Component);
            int index = -1;
            foreach (sfObject child in obj.Children)
            {
                index++;
                if (child.Type != sfType.Component)
                {
                    continue;
                }
                Component component = sfObjectMap.Get().Get<Component>(child);
                if (component == null)
                {
                    translator.OnCreate(child, index);
                }
                else
                {
                    sfPropertyManager.Get().ApplyProperties(component, (sfDictionaryProperty)child.Property);
                    if (recursive && component is Transform)
                    {
                        foreach (sfObject grandChild in child.Children)
                        {
                            if (grandChild.Type == sfType.GameObject)
                            {
                                ApplyServerState(grandChild, true);
                            }
                        }
                    }
                }
            }
            // Destroy unsynced components
            foreach (Component component in gameObject.GetComponents<Component>())
            {
                if (!sfObjectMap.Get().Contains(component) && translator.IsSyncable(component))
                {
                    translator.DestroyComponent(component);
                }
            }
            translator.ApplyComponentOrder(gameObject);
            if (recursive)
            {
                // Destroy unsynced children and reparent children with different server parents.
                sfObject transformObj = sfObjectMap.Get().GetSFObject(gameObject.transform);
                for (int i = gameObject.transform.childCount - 1; i >= 0; i--)
                {
                    GameObject child = gameObject.transform.GetChild(i).gameObject;
                    sfObject childObj = sfObjectMap.Get().GetSFObject(child);
                    if (childObj == null)
                    {
                        if (IsSyncable(child))
                        {
                            DestroyGameObject(child);
                        }
                    }
                    else if (childObj.Parent != transformObj)
                    {
                        m_serverHierarchyChangedSet.Add(childObj.Parent);
                    }
                }
            }
        }

        /// <summary>
        /// Called when the user completes dragging objects in the hierarchy. Re-adds HideFlags.NotEditable on the next
        /// pre update to objects that were temporarily made editable to make dragging work. If the target is null, adds
        /// the scene's hierarchy sfObject to the local hierarchy changed set to sync root game object order changes in
        /// the next pre update.
        /// </summary>
        /// <param name="target">target the objects were dragged onto.</param>
        /// <param name="scene">scene the objects were dragged onto.</param>
        private void OnHierarchyDragComplete(GameObject target, Scene scene)
        {
            // Relock objects on the next update that were temporarily unlocked to make dragging work. If we try to
            // relock them now, Unity will cancel reparenting if the parent is locked.
            m_relockObjects = true;

            if (target == null)
            {
                // Unity doesn't have an event for root object order changes, so we detect it by detecting a drag
                // without a target game object. This won't detect root order changes made programmatically by plugins.
                // Unity does fire a parent change event when undoing a root order change, so we don't need to do
                // anything extra to detect that.
                //TODO: In Unity 6 there is a new ObjectChangeKind.ChangeRootOrder event we can use.
                sfSceneTranslator translator = sfObjectEventDispatcher.Get().GetTranslator<sfSceneTranslator>(
                    sfType.Scene);
                sfObject obj = translator.GetHierarchyObject(scene);
                if (obj != null)
                {
                    m_localHierarchyChangedSet.Add(obj);
                }
            }
#if !UNITY_2022_2_OR_NEWER
            else
            {
                // Unity 2022.1 and below do not have child reorder events, so we detect it by checking if any of the
                // dragged objects are already a parent of the target (their parent hasn't updated yet).
                foreach (UObject uobj in DragAndDrop.objectReferences)
                {
                    GameObject gameObject = uobj as GameObject;
                    if (gameObject != null && gameObject.transform.parent == target.transform)
                    {
                        sfUndoManager.Get().Record(new sfUndoReorderChildrenOperation(target));
                        sfUnityEventDispatcher.Get().InvokeOnReorderChildren(target);
                    }
                }
            }
#endif
        }

        /// <summary>
        /// Validates a hierarchy drag operation. A drag operation is allowed if the target is not fully locked and all
        /// dragged objects are unlocked.
        /// </summary>
        /// <param name="target">target parent for the dragged objects.</param>
        /// <param name="childIndex">childIndex the dragged objects will be inserted at.</param>
        /// <returns>true if the drag should be allowed.</returns>
        private bool ValidateHierarchyDrag(GameObject target, int childIndex)
        {
            sfObject targetObj = sfObjectMap.Get().GetSFObject(target);
            // If the target is locked, temporarily unlock it. We need to unlock partially locked objects to allow
            // children to be added to them. We need to unlock fully locked objects as well because keeping them
            // locked interferes with drag target detection and causes flickering.
            if (targetObj != null && targetObj.IsLocked &&
                (target.hideFlags & HideFlags.NotEditable) != HideFlags.None)
            {
                sfUnityUtils.RemoveFlags(target, HideFlags.NotEditable);
                m_tempUnlockedObjects.Add(target);
            }

            if (targetObj != null && targetObj.IsFullyLocked)
            {
                return false;
            }
            // Don't allow the drag if any of the dragged objects are locked, unless they are assets.
            foreach (UObject uobj in DragAndDrop.objectReferences)
            {
                if (sfLoader.Get().IsAsset(uobj))
                {
                    continue;
                }
                sfObject obj = sfObjectMap.Get().GetSFObject(uobj);
                if (obj != null && obj.IsLocked)
                {
                    return false;
                }
                GameObject gameObject = uobj as GameObject;
                if (gameObject == null)
                {
                    continue;
                }
                // Disallow dragging a missing prefab that is not the root of the prefab.
                sfMissingPrefab missingPrefab = gameObject.GetComponent<sfMissingPrefab>();
                if (missingPrefab != null && !missingPrefab.IsRoot)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Adds a game object's parent's sfObject to the set of objects with child game objects to upload.
        /// </summary>
        /// <param name="gameObject"></param>
        private void AddParentToUploadSet(GameObject gameObject)
        {
            if (!IsSyncable(gameObject))
            {
                return;
            }
            sfObject parent = GetParentObject(gameObject);
            if (parent != null && parent.IsSyncing)
            {
                m_parentsWithNewChildren.Add(parent);
                IncrementPrefabRevision(parent);
            }
        }

        /// <summary>
        /// Gets the sfObject for a game object's parent. This is either a hierarchy object if the game object is a root
        /// object, or a transform component object.
        /// </summary>
        /// <param name="gameObject">gameObject to get parent object for.</param>
        /// <returns>parent object.</returns>
        private sfObject GetParentObject(GameObject gameObject)
        {
            if (gameObject.transform.parent == null)
            {
                // The parent object is a hierarchy object
                sfSceneTranslator sceneTranslator = sfObjectEventDispatcher.Get()
                    .GetTranslator<sfSceneTranslator>(sfType.Hierarchy);
                return sceneTranslator.GetHierarchyObject(gameObject.scene);
            }
            else
            {
                // The parent object is a transform
                return sfObjectMap.Get().GetSFObject(gameObject.transform.parent);
            }
        }

        /// <summary>
        /// Uploads new child game objects of objects in the parents-with-new-children set to the server.
        /// </summary>
        private void UploadGameObjects()
        {
            if (m_parentsWithNewChildren.Count == 0)
            {
                return;
            }
            sfSession session = SceneFusion.Get().Service.Session;
            List<sfObject> uploadList = new List<sfObject>();
            foreach (sfObject parent in m_parentsWithNewChildren)
            {
                if (!parent.IsSyncing)
                {
                    continue;
                }

                // User an enumerator to iterate the chilren since they are stored in a linked list and index iteration
                // is slow.
                IEnumerator<sfObject> childIter = parent.Children.GetEnumerator();
                bool childIterHasValue = childIter.MoveNext();
                int index = 0; // Child index of first uploaded object
                // Check for new child game objects to upload
                foreach (GameObject gameObject in IterateChildGameObjects(parent))
                {
                    sfObject obj = sfObjectMap.Get().GetSFObject(gameObject);
                    if (obj != null && obj.IsSyncing)
                    {
                        if (!childIterHasValue)
                        {
                            continue;
                        }
                        // Objects uploaded together must be in a continuous sequence, so when we find an object that
                        // is already uploaded, upload the upload list if it's non-empty.
                        if (uploadList.Count > 0)
                        {
                            session.Create(uploadList, parent, index);
                            index += uploadList.Count;
                            uploadList.Clear();
                        }

                        // Advance the child iterator to the next child after obj.
                        while (childIterHasValue && childIter.Current != obj)
                        {
                            index++;
                            childIterHasValue = childIter.MoveNext();
                        }
                        if (childIterHasValue)
                        {
                            index++;
                            childIterHasValue = childIter.MoveNext();
                        }
                    }
                    else if ((obj == null || !obj.IsDeletePending) && IsSyncable(gameObject))
                    {
                        // Sometimes Unity destroys and recreates a game object without firing an event for the
                        // destroyed object. This happens when a new prefab is created from a game object and there
                        // were broken prefab instances for that prefab. We check for this by checking if there was a
                        // deleted game object at the same index as the new game object.
                        bool isReplacement = childIterHasValue && !childIter.Current.IsDeletePending &&
                                sfObjectMap.Get().Get<GameObject>(childIter.Current).IsDestroyed();

                        // If the parent or replaced object is locked, delete the new game object.
                        if (parent.IsFullyLocked || (isReplacement && childIter.Current.IsLocked))
                        {
                            DestroyGameObject(gameObject);

                            if (isReplacement)
                            {
                                // Recreate the replaced object.
                                InitializeGameObjectHierarchy(childIter.Current, index);
                            }
                        }
                        else
                        {
                            // When Unity replaces a broken prefab instance, the transform is not overriden so it gets
                            // moved to default prefab location. We detect this and reapply the transform values from
                            // the object it replaced.
                            if (isReplacement && PrefabUtility.IsPartOfPrefabInstance(gameObject))
                            {
                                SerializedObject so = sfPropertyManager.Get()
                                    .GetSerializedObject(gameObject.transform);
                                SerializedProperty sprop = so.FindProperty(sfProp.Position);
                                if (sprop != null && !sprop.prefabOverride)
                                {
                                    sfObject transformObj = GetTransformObj(childIter.Current);
                                    if (transformObj != null)
                                    {
                                        sfPropertyManager.Get().ApplyProperties(gameObject.transform, 
                                            (sfDictionaryProperty)transformObj.Property);
                                        sfPropertyManager.Get().ApplySerializedProperties(gameObject.transform);
                                    }
                                }
                            }

                            // Found an object to upload. Create an sfObject and add it to the upload list.
                            obj = CreateObject(gameObject);
                            if (obj != null)
                            {
                                uploadList.Add(obj);
                            }

                            if (isReplacement)
                            {
                                // Delete the replaced object.
                                SyncDeletedObject(childIter.Current);
                            }
                        }
                        if (isReplacement)
                        {
                            childIterHasValue = childIter.MoveNext();
                        }
                    }
                }
                // Upload the objects
                if (uploadList.Count > 0)
                {
                    session.Create(uploadList, parent, index);
                    uploadList.Clear();
                }
            }
            m_parentsWithNewChildren.Clear();
        }

        /// <summary>
        /// Gets the transform object from a game object sfObject by returning the first child component sfObject, which
        /// is always the transform.
        /// </summary>
        /// <param name="obj">game object sfObject to get transform sfObject for.</param>
        /// <returns>transform object.</returns>
        private sfObject GetTransformObj(sfObject obj)
        {
            foreach (sfObject child in obj.Children)
            {
                if (child.Type == sfType.Component)
                {
                    return child;
                }
            }
            return null;
        }

        /// <summary>Gets the child game objects of an sfObject's scene or transform.</summary>
        /// <param name="parent">parent for the scene or transform to get child game objects from.</param>
        /// <returns>children of the object. Null if the transform could not be found.</returns>
        public List<GameObject> GetChildGameObjects(sfObject parent)
        {
            if (parent == null)
            {
                return null;
            }
            if (parent.Type == sfType.Hierarchy)
            {
                sfSceneTranslator sceneTranslator = sfObjectEventDispatcher.Get()
                    .GetTranslator<sfSceneTranslator>(sfType.Hierarchy);
                Scene scene = sceneTranslator.GetScene(parent);
                if (scene.isLoaded)
                {
                    List<GameObject> children = new List<GameObject>();
                    scene.GetRootGameObjects(children);
                    return children;
                }
            }
            else if (parent.Type == sfType.Component)
            {
                Transform transform = sfObjectMap.Get().Get<Transform>(parent);
                if (transform != null)
                {
                    List<GameObject> children = new List<GameObject>();
                    foreach (Transform child in transform)
                    {
                        children.Add(child.gameObject);
                    }
                    return children;
                }
            }
            return null;
        }

        /// <summary>Iterates the child game objects of an sfObject's scene or transform.</summary>
        /// <param name="parent">parent for the scene or transform to iterate.</param>
        /// <returns></returns>
        public IEnumerable<GameObject> IterateChildGameObjects(sfObject parent)
        {
            if (parent.Type == sfType.Hierarchy)
            {
                sfSceneTranslator sceneTranslator = sfObjectEventDispatcher.Get()
                    .GetTranslator<sfSceneTranslator>(sfType.Hierarchy);
                Scene scene = sceneTranslator.GetScene(parent);
                if (scene.isLoaded)
                {
                    List<GameObject> roots = new List<GameObject>();
                    scene.GetRootGameObjects(roots);
                    foreach (GameObject gameObject in roots)
                    {
                        yield return gameObject;
                    }
                }
            }
            else if (parent.Type == sfType.Component)
            {
                Transform transform = sfObjectMap.Get().Get<Transform>(parent);
                if (transform != null)
                {
                    foreach (Transform child in transform)
                    {
                        yield return child.gameObject;
                    }
                }
            }
        }

        /// <summary>Called when a game object's parent is changed by another user.</summary>
        /// <param name="obj">obj whose parent changed.</param>
        /// <param name="childIndex">childIndex of the object. -1 if the object is a root.</param>
        public override void OnParentChange(sfObject obj, int childIndex)
        {
            if (obj.Parent != null)
            {
                // Apply the change at the end of the frame.
                m_serverHierarchyChangedSet.Add(obj.Parent);
            }
        }

        /// <summary>
        /// If uobj is a syncable game object, creates an empty sfObject without uploading it and maps it to the game
        /// object in the sfObjectMap. If the game object is the root of a prefab, also creates the properties for it and
        /// uploads it.
        /// </summary>
        /// <param name="uobj">uobj to create sfObject for.</param>
        /// <param name="outObj">outObj created for the uobject.</param>
        /// <returns>true if the uobject was handled by this translator.</returns>
        public override bool TryCreate(UObject uobj, out sfObject outObj)
        {
            outObj = null;
            GameObject gameObject = uobj as GameObject;
            if (gameObject == null)
            {
                return false;
            }
            if (IsSyncable(gameObject) && !sfObjectMap.Get().Contains(gameObject))
            {
                outObj = new sfObject(sfType.GameObject, new sfDictionaryProperty());
                AddMapping(outObj, gameObject);

                // If the object is the root of a prefab, upload it now, since it isn't part of a hierarchy and won't
                // get uploaded later.
                if (sfUnityUtils.IsPrefabAssetRoot(gameObject) && CreateObject(gameObject) != null)
                {
                    SceneFusion.Get().Service.Session.Create(outObj);
                }
            }
            return true;
        }

        /// <summary>
        /// Recusively creates sfObjects for a game object and its children if they don't already exist. Does not modify
        /// existing objects.
        /// </summary>
        /// <param name="gameObject">gameObject to create sfObject for.</param>
        /// <returns>created for the gameObject. Null if the game object already had an sfObject.</returns>
        public sfObject CreateObject(GameObject gameObject)
        {
            sfObject obj = sfObjectMap.Get().GetOrCreateSFObject(gameObject, sfType.GameObject);
            if (obj.IsSyncing)
            {
                return null;
            }
            m_instanceIdToSFObjectMap[gameObject.GetInstanceID()] = obj;

            sfDictionaryProperty properties = (sfDictionaryProperty)obj.Property;
            bool isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(gameObject);
            bool isPrefabAssetRoot = isPrefabAsset && gameObject.transform.parent == null;
            Guid guid = Guid.Empty;
            if (isPrefabAsset)
            {
                if (isPrefabAssetRoot)
                {
                    properties[sfProp.Path] = AssetDatabase.GetAssetPath(gameObject);
                }

                if (!PrefabUtility.IsPartOfPrefabInstance(gameObject))
                {
                    properties[sfProp.FileId] = sfLoader.Get().GetLocalFileId(gameObject);
                }
                else if (PrefabUtility.IsOutermostPrefabInstanceRoot(gameObject))
                {
                    UObject handle = PrefabUtility.GetPrefabInstanceHandle(gameObject);
                    properties[sfProp.FileId] = sfLoader.Get().GetLocalFileId(handle);
                }
            }
            else
            {
                guid = sfGuidManager.Get().GetGuid(gameObject);
                if (sfGuidManager.Get().GetGameObject(guid) != gameObject)
                {
                    // If the game object's guid is mapped to a different game object, this is a duplicate object.
                    // Duplicate objects can be created when a user deletes a locked object which is recreated because it
                    // was locked, and then undoes the delete.
                    List<GameObject> toDetach = new List<GameObject>();
                    sfUnityUtils.ForEachDescendant(gameObject, (GameObject child) =>
                    {
                        sfObject childObj = sfObjectMap.Get().GetSFObject(child);
                        if (childObj != null && childObj.IsSyncing && sfObjectMap.Get().Get<GameObject>(childObj) == child)
                        {
                            // This descendant is a synced object and not a duplicate. Detach it and put the parent in the
                            // server hierarchy changed set to restore its children.
                            toDetach.Add(child);
                            m_serverHierarchyChangedSet.Add(childObj.Parent);
                            return false;
                        }
                        RemoveMapping(child);
                        return true;
                    });
                    for (int i = 0; i < toDetach.Count; i++)
                    {
                        sfComponentUtils.SetParent(toDetach[i], null);
                    }
                    // Destroy the duplicate object.
                    RemoveMapping(gameObject);
                    DestroyGameObject(gameObject);
                    return null;
                }

                properties[sfProp.Guid] = guid.ToByteArray();
            }

            // If a user duplicates a locked object, the duplicate will be locked, so we need to unlock it.
            // We detect this by checking the hideflags, except for prefab instances which will have their prefab's hide
            // flags, so we always try to find a lock object to destroy on prefab instances.
            if ((gameObject.hideFlags & HideFlags.NotEditable) != 0 ||
                PrefabUtility.IsPartOfPrefabInstance(gameObject))
            {
                Unlock(gameObject, true);
            }

            string prefabPath;
            long fileId;
            bool needsPrefabReconnect = RemoveInvalidMissingPrefab(gameObject);
            GetPrefabInfo(gameObject, out prefabPath, out fileId);
            if (!string.IsNullOrEmpty(prefabPath))
            {
                properties[sfProp.PrefabSourcePath] = prefabPath;
                if (fileId != 0)
                {
                    properties[sfProp.PrefabSourceFileId] = fileId;
                }

                // Upload the prefab if it isn't already uploaded.
                List<uint> revisions = GetPrefabSourceRevisions(gameObject, true);
                if (revisions != null)
                {
                    properties[sfProp.InstanceRevisions] = revisions.ToArray();
                }
            }
            else if (PrefabUtility.GetPrefabInstanceStatus(gameObject) == PrefabInstanceStatus.MissingAsset)
            {
                ksLog.Warning(this, "Found prefab game object instance " + gameObject.name +
                    " with a missing prefab asset. The game object will be a non-prefab for other users.",
                    gameObject);
                if (OnMissingPrefab != null)
                {
                    OnMissingPrefab(gameObject);
                }
            }

            if (Selection.Contains(gameObject))
            {
                obj.RequestLock();
            }

            sfPropertyManager.Get().CreateProperties(gameObject, properties);

            // Create component child objects
            sfComponentTranslator translator = sfObjectEventDispatcher.Get().GetTranslator<sfComponentTranslator>(
                sfType.Component);
            bool isFirst = true;
            foreach (Component component in gameObject.GetComponents<Component>())
            {
                if (translator.IsSyncable(component))
                {
                    sfObject child = translator.CreateObject(component, isFirst);
                    isFirst = false;
                    if (child != null)
                    {
                        obj.AddChild(child);
                    }
                }
            }

            InvokeOnLockStateChange(obj, gameObject);

            if (needsPrefabReconnect)
            {
                // The game object had a sfMissingPrefab component for a prefab that exists. Recreate the game object
                // as a prefab instance.
                DestroyAndRecreate(gameObject);
            }
            return obj;
        }

        /// <summary>
        /// Gets the revision numbers for a prefab instance's source prefabs and optionally uploads the source prefab if
        /// it is the root of the prefab and isn't already uploaded. A prefab instance has a revision number for each
        /// level of prefab nesting where the source prefab is the root of the prefab asset. The revision numbers are
        /// returned as a list where the first index is for the inner-most prefab root and the last is for the outer-
        /// most prefab root. Each root prefab also has an asset revision number. If any of the instance revision
        /// numbers do not match the asset revision number of the corresponding source prefab root, the sfObjects for
        /// the prefab instance need to be updated to reflect updates from the source prefab. Does nothing if prefab
        /// syncing is disabled.
        /// </summary>
        /// <param name="gameObject">gameObject to get the instance revision numbers for.</param>
        /// <param name="uploadPrefabSource">
        /// if true, will upload the prefab instance's source prefab if it is the root
        /// of the prefab and is not already uploaded.
        /// </param>
        /// <returns>
        /// prefab source revision numbers. Null if the game object is not the root of any prefab
        /// instance (including nested prefabs) or if the revision numbers are all zeroes.
        /// </returns>
        private List<uint> GetPrefabSourceRevisions(GameObject gameObject, bool uploadPrefabSource = false)
        {
            if (sfConfig.Get().SyncPrefabs != sfConfig.PrefabSyncMode.FULL)
            {
                return null;
            }
            GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
            if (prefab == null)
            {
                return null;
            }

            if (uploadPrefabSource && prefab.transform.parent == null && IsSyncable(prefab))
            {
                // Upload the prefab if it isn't already uploaded.
                sfObject prefabObj = sfObjectMap.Get().GetSFObject(prefab);
                if (prefabObj == null || !prefabObj.IsSyncing)
                {
                    prefabObj = CreateObject(prefab);
                    if (prefabObj != null)
                    {
                        SceneFusion.Get().Service.Session.Create(prefabObj);
                    }
                }
            }
            return GetPrefabRevisions(prefab);
        }

        /// <summary>
        /// Gets the revision numbers for a prefab game object and, if it is nested, its source prefabs. There is one
        /// revision number for each level of nesting where the game object is the root of its prefab. The revision
        /// numbers are returned in a list where the first being the inner-most prefab root and the last being the
        /// outer-most.
        /// </summary>
        /// <returns>
        /// prefab revision numbers. Null if the game object is not the root of its prefab and none
        /// of its sources are either, or if the revision numbers are all zeroes.
        /// </returns>
        private List<uint> GetPrefabRevisions(GameObject prefab)
        {
            List<uint> revisions = null;
            bool isAllZeroes = true;

            // Iterate the prefab and its sources.
            while (prefab != null)
            {
                // Get the revision number if its the root of the prefab.
                if (prefab.transform.parent == null)
                {
                    // If the prefab isn't synced, stop.
                    sfObject prefabObj = sfObjectMap.Get().GetSFObject(prefab);
                    if (prefabObj == null || !prefabObj.IsSyncing)
                    {
                        break;
                    }

                    if (revisions == null)
                    {
                        revisions = new List<uint>();
                    }
                    uint revision = 0;
                    sfBaseProperty prop;
                    if (((sfDictionaryProperty)prefabObj.Property).TryGetField(sfProp.PrefabRevision, out prop))
                    {
                        revision = (uint)prop;
                        if (revision != 0)
                        {
                            isAllZeroes = false;
                        }
                    }
                    revisions.Add(revision);
                }
                prefab = PrefabUtility.GetCorrespondingObjectFromSource(prefab);
            }
            return isAllZeroes ? null : revisions;
        }

        /// <summary>Checks if a prefab instance has different revision numbers than its source prefabs.</summary>
        /// <param name="gameObject">gameObject to check revision numbers for.</param>
        /// <returns>
        /// true if the game object is a prefab instance whose revision numbers do not match its sources.
        /// </returns>
        private bool IsPrefabInstanceRevisionStale(GameObject gameObject)
        {
            sfObject obj = sfObjectMap.Get().GetSFObject(gameObject);
            GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
            if (obj == null || prefab == null)
            {
                return false;
            }

            // Get the prefab instance's revision numbers.
            uint[] instanceRevisions = null;
            sfDictionaryProperty properties = (sfDictionaryProperty)obj.Property;
            sfBaseProperty prop;
            if (properties.TryGetField(sfProp.InstanceRevisions, out prop))
            {
                instanceRevisions = (uint[])prop;
            }

            // Get the prefab source's revision numbers.
            List<uint> prefabRevisions = GetPrefabRevisions(prefab);

            return (instanceRevisions != null || prefabRevisions != null) && (instanceRevisions == null ||
                prefabRevisions == null || !instanceRevisions.SequenceEqual(prefabRevisions));
        }
        
        /// <summary>
        /// Removes invalid missing prefab components from a game object. A missing component is invalid if it has
        /// child indexes and the parent is not part of the prefab the missing prefab component is for.
        /// </summary>
        /// <param name="gameObject">gameObject to check for and remove missing prefab components from.</param>
        /// <returns>true if a sfMissingPrefab component was removed because the prefab exists.</returns>
        private bool RemoveInvalidMissingPrefab(GameObject gameObject)
        {
            sfMissingPrefab missingPrefab = gameObject.GetComponent<sfMissingPrefab>();
            if (missingPrefab == null)
            {
                return false;
            }
            // If the missing prefab is the root of the prefab, check if the prefab exists.
            if (missingPrefab.IsRoot)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(missingPrefab.PrefabPath) != null)
                {
                    return true;
                }
                CreateMissingPrefabNotification(missingPrefab);
                return false;
            }
            // If the game object has no parent or the parent is not part of the same prefab path, destroy the missing
            // prefab component.
            if (gameObject.transform.parent == null || 
                GetPrefabPath(gameObject.transform.parent.gameObject) != missingPrefab.PrefabPath)
            {
                UObject.DestroyImmediate(missingPrefab);
                return false;
            }
            CreateMissingPrefabNotification(missingPrefab);
            return false;
        }

        /// <summary>Creates a notification for a missing prefab.</summary>
        /// <param name="missingPrefab">missingPrefab to create notification for.</param>
        private void CreateMissingPrefabNotification(sfMissingPrefab missingPrefab)
        {
            if (!missingPrefab.IsRoot && AssetDatabase.LoadAssetAtPath<GameObject>(missingPrefab.PrefabPath) != null)
            {
                sfNotification.Create(sfNotificationCategory.MissingPrefab,
                    "Unable to find child prefab in '" + missingPrefab.PrefabPath + "'.", missingPrefab.gameObject);
            }
            else
            {
                sfNotification notification = sfNotification.Create(sfNotificationCategory.MissingPrefab,
                    "Unable to load prefab '" + missingPrefab.PrefabPath + "'.", missingPrefab.gameObject);
                // If there's only 1 object this is a new notification. Add it to the map.
                if (notification.Objects.Count == 1)
                {
                    m_missingPrefabNotificationMap[missingPrefab.PrefabPath] = notification;
                }
            }
        }

        /// <summary>
        /// Called when assets are imported. If any of the assets were previously missing prefabs, replaces missing
        /// prefabs with the new prefab.
        /// </summary>
        /// <param name="assets">assets that were imported.</param>
        private void HandleImportAssets(string[] assets)
        {
            foreach (string path in assets)
            {
                // Prefab path could end in ".prefab" or ".fbx".
                if (m_missingPrefabNotificationMap.ContainsKey(path))
                {
                    // Replacing missing prefabs will crash Unity if we do it now and one of the new prefabs was
                    // created from a missing prefab stand-in, so we do it from delayCall.
                    EditorApplication.delayCall += () =>
                    {
                        ReplaceMissingPrefabs(path);
                    };
                }
            }
        }

        /// <summary>
        /// Destroys and recreates missing prefab stand-ins as prefabs instances for the prefab at the given path.
        /// </summary>
        /// <param name="path">path to prefab to replace missing prefab stand-ins for.</param>
        private void ReplaceMissingPrefabs(string path)
        {
            sfNotification notification;
            if (m_missingPrefabNotificationMap.Remove(path, out notification))
            {
                int count = 0;
                foreach (GameObject gameObject in notification.Objects)
                {
                    if (gameObject != null)
                    {
                        DestroyAndRecreate(gameObject);
                        count++;
                    }
                }
                notification.Clear();
                if (count > 0)
                {
                    ksLog.Info(this, "Replaced " + count + " missing prefab(s) with '" + path + "'.");
                }
            }
        }

        /// <summary>Called when a game object is created by another user.</summary>
        /// <param name="obj">obj that was created.</param>
        /// <param name="childIndex">childIndex of the new object. -1 if the object is a root.</param>
        public override void OnCreate(sfObject obj, int childIndex)
        {
            InitializeGameObjectHierarchy(obj, childIndex);   
        }

        /// <summary>
        /// Creates or finds a game object for an sfObject and initializes it with server values. Recursively
        /// initializes children. Unlike InitializeGameObject, this will also place the game object in the correct
        /// hierarchy location. If the game object is located in the wrong child index, the child order will be synced
        /// in the next update.
        /// </summary>
        /// <param name="obj">obj to initialize game object for.</param>
        /// <param name="createVariantsOfMissingPrefabs">
        /// if true, will create a stand-in prefab variant with an
        /// sfMissingPrefab component for prefab variants of missing prefabs. If false, delays creating game
        /// objects for variants of missing prefabs until the next update, which prevents missing prefab
        /// notifications in cases where prefab B is a prefab variant of prefab A and prefab B syncs before
        /// prefab A.
        /// </param>
        /// <returns>gameObject for the sfObject. Null if the game object could not be initialized.</returns>
        public GameObject InitializeGameObjectHierarchy(sfObject obj, bool createVariantsOfMissingPrefabs = false)
        {
            return InitializeGameObjectHierarchy(obj, obj.GetChildIndex(), createVariantsOfMissingPrefabs);
        }

        /// <summary>
        /// Creates or finds a game object for an sfObject and initializes it with server values. Recursively
        /// initializes children. Unlike InitializeGameObject, this will also place the game object in the correct
        /// hierarchy location. If the game object is located in the wrong child index, the child order will be synced
        /// in the next update.
        /// </summary>
        /// <param name="obj">obj to initialize game object for.</param>
        /// <param name="childIndex">childIndex of the sfObject. -1 if it is a root.</param>
        /// <param name="createVariantsOfMissingPrefabs">
        /// if true, will create a stand-in prefab variant with an
        /// sfMissingPrefab component for prefab variants of missing prefabs. If false, delays creating game
        /// objects for variants of missing prefabs until the next update, which prevents missing prefab
        /// notifications in cases where prefab B is a prefab variant of prefab A and prefab B syncs before
        /// prefab A.
        /// </param>
        /// <returns>gameObject for the sfObject. Null if the game object could not be initialized.</returns>
        public GameObject InitializeGameObjectHierarchy(
            sfObject obj,
            int childIndex,
            bool createVariantsOfMissingPrefabs = false)
        {
            GameObject gameObject = sfObjectMap.Get().Get<GameObject>(obj);
            if (gameObject != null)
            {
                ksLog.Warning(this, "CreateGameObject called for sfObject that already has a game object '" +
                    gameObject.name + "'.");
                return null;
            }
            if (obj.Parent == null)
            {
                // If the object has no parent it's the root of a prefab asset.
                gameObject = InitializeGameObject(obj, new Scene(), createVariantsOfMissingPrefabs);
            }
            else if (obj.Parent.Type == sfType.Hierarchy)
            {
                sfSceneTranslator sceneTranslator = sfObjectEventDispatcher.Get()
                    .GetTranslator<sfSceneTranslator>(sfType.Hierarchy);
                Scene scene = sceneTranslator.GetScene(obj.Parent);
                if (scene.isLoaded)
                {
                    gameObject = InitializeGameObject(obj, scene, createVariantsOfMissingPrefabs);
                }
            }
            else if (obj.Parent.Type == sfType.Component)
            {
                Transform transform = sfObjectMap.Get().Get<Transform>(obj.Parent);
                if (transform != null)
                {
                    gameObject = InitializeGameObject(obj, transform.gameObject.scene, createVariantsOfMissingPrefabs);
                }
            }
            else
            {
                ksLog.Warning(this, "GameObject sfObject has invalid parent type: " + obj.Parent.Type);
                return null;
            }

            if (gameObject == null)
            {
                return null;
            }

            // If the game object is the last child it will be in the correct location, unless it is a root prefab.
            if (obj.Parent != null && (childIndex != obj.Parent.Children.Count - 1 ||
                (PrefabUtility.IsPartOfPrefabInstance(gameObject) && gameObject.transform.parent == null)))
            {
                m_serverHierarchyChangedSet.Add(obj.Parent);
            }

            // If the game object is a prefab asset, update the file ids in the prefab file.
            if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
            {
                sfFileIdUpdater updater = sfFileIdUpdater.Get(gameObject, false);
                if (updater != null)
                {
                    sfPrefabSaver.Get().SavePrefabIfDirty(gameObject);
                    updater.UpdateFileIds();
                }
            }
            return gameObject;
        }

        /// <summary>
        /// Creates or finds a game object for an sfObject and initializes it with server values. Recursively
        /// initializes children.
        /// </summary>
        /// <param name="obj">obj to initialize game object for.</param>
        /// <param name="scene">scene the game object belongs to.</param>
        /// <param name="createVariantsOfMissingPrefabs">
        /// if true, will create a stand-in prefab variant with an
        /// sfMissingPrefab component for prefab variants of missing prefabs. If false, delays creating game
        /// objects for variants of missing prefabs until the next update, which prevents missing prefab
        /// notifications in cases where prefab B is a prefab variant of prefab A and prefab B syncs before
        /// prefab A.
        /// </param>
        /// <returns>gameObject for the sfObject. Null if the game object could not be initialized.</returns>
        public GameObject InitializeGameObject(sfObject obj, Scene scene, bool createVariantsOfMissingPrefabs = false)
        {
            sfDictionaryProperty properties = (sfDictionaryProperty)obj.Property;
            // Try get the prefab path and child index properties
            string prefabPath = null;
            long sourceFileId = 0;
            sfBaseProperty property;
            if (properties.TryGetField(sfProp.PrefabSourcePath, out property))
            {
                prefabPath = (string)property;
                if (properties.TryGetField(sfProp.PrefabSourceFileId, out property))
                {
                    sourceFileId = (long)property;
                    if (CheckDuplicatePrefabChild(obj, sourceFileId))
                    {
                        return null;
                    }
                }
            }

            string assetPath = null;
            Guid guid = Guid.Empty;
            GameObject gameObject = null;
            bool isPrefabAsset = false;
            long fileId = 0;
            if (obj.Parent == null)
            {
                // Try load the prefab by path.
                isPrefabAsset = true;
                assetPath = (string)properties[sfProp.Path];
                fileId = (long)properties[sfProp.FileId];
                gameObject = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (gameObject != null)
                {
                    sfObject current = sfObjectMap.Get().GetSFObject(gameObject);
                    if (current != null)
                    {
                        // The prefab was created twice, which can happen if two users try to create the prefab at the
                        // same time. Keep the version that was created first and delete the second one.
                        ksLog.Warning(this, "Prefab " + assetPath + " was uploaded by multiple users. The second " +
                            "version will be ignored.");
                        sfObjectUtils.DeleteDuplicate(current, obj);
                    }
                    else
                    {
                        foreach (Transform child in gameObject.transform)
                        {
                            CreateGuids(child.gameObject);
                        }
                    }
                }
            }
            else
            {
                sfBaseProperty prop;
                if (properties.TryGetField(sfProp.Guid, out prop))
                {
                    // Try get the game object by its guid
                    guid = new Guid((byte[])prop);
                    gameObject = sfGuidManager.Get().GetGameObject(guid);
                }
                else
                {
                    // The game object is a child in a prefab asset. Try get the prefab by its local file id.
                    isPrefabAsset = true;
                    if (properties.TryGetField(sfProp.FileId, out prop))
                    {
                        fileId = (long)prop;
                        gameObject = FindPrefabChild(obj.Parent, fileId, string.IsNullOrEmpty(prefabPath) ? 
                            FileIdTarget.PREFAB_ASSET : FileIdTarget.PREFAB_INSTANCE_HANDLE);
                    }
                }
            }

            if (gameObject != null && !ValidateGameObject(gameObject, prefabPath, sourceFileId, obj.Parent))
            {
                // The game object is not the correct prefab. Remove it from the guid manager and don't use it.
                // If the game object is a prefab child asset, we need to destroy it before creating a new one so we
                // don't get a duplicate file id.
                sfGuidManager.Get().Remove(gameObject);
                if (PrefabUtility.IsPartOfPrefabAsset(gameObject) && gameObject.transform.parent != null)
                {
                    DestroyGameObject(gameObject);
                }
                gameObject = null;
            }
            if (gameObject == null && sourceFileId != 0)
            {
                // Try find the child from the prefab
                gameObject = FindPrefabChild(obj.Parent, sourceFileId, FileIdTarget.PREFAB_SOURCE);
                if (gameObject != null)
                {
                    if (sfObjectMap.Get().Contains(gameObject))
                    {
                        gameObject = null;
                    }
                    else if (!isPrefabAsset)
                    {
                        sfGuidManager.Get().SetGuid(gameObject, guid);
                    }
                }
            }

            // Create the game object if we couldn't find it by its guid or asset path.
            sfMissingPrefab missingPrefab = null;
            bool isNewPrefabInstance = false;
            bool isNewObject = gameObject == null;
            if (isNewObject)
            {
                bool isMissingPrefab = false;
                if (prefabPath != null)
                {
                    if (sourceFileId == 0)
                    {
                        gameObject = sfUnityUtils.InstantiatePrefab(scene, prefabPath);
                        isNewPrefabInstance = gameObject != null;
                    }
                    // Starting in 2022.2 it is possible to delete a prefab child instance.
#if UNITY_2022_2_OR_NEWER
                    else
                    {
                        Transform parentTransform = sfObjectMap.Get().Get<Transform>(obj.Parent);
                        if (parentTransform != null)
                        {
                            gameObject = RestoreChildPrefab(parentTransform.gameObject, sourceFileId);
                            // Restoring a deleted prefab child resets the hideflags, so we need to relock the game
                            // objects in the prefab instance.
                            if (obj.IsLocked && gameObject != null)
                            {
                                RelockPrefabNextPreUpdate(gameObject);
                            }
                        }
                    }
#endif
                    if (gameObject == null)
                    {
                        if (sfConfig.Get().SyncPrefabs == sfConfig.PrefabSyncMode.FULL && isPrefabAsset &&
                            !createVariantsOfMissingPrefabs)
                        {
                            // The sfObject is for a prefab variant whose source prefab isn't created yet. We need to
                            // the source prefab before we can create the prefab variant.

                            // Get the prefab variant's asset path.
                            if (string.IsNullOrEmpty(assetPath))
                            {
                                assetPath = (string)((sfDictionaryProperty)obj.Root.Property)[sfProp.Path];
                            }

                            // Track a dependency between the prefab variant and the source prefab.
                            if (m_prefabDependencies.Add(assetPath, prefabPath))
                            {
                                // Add the sfObject to the prefab object create map that maps prefab asset paths to a
                                // list of prefab variant sfObjects that still need to be created. We will create
                                // these prefab variants the next Update after their source prefab is created. If the
                                // source prefab is still missing, a stand-in will be created then.
                                List<sfObject> prefabObjects;
                                if (!m_prefabObjectCreateMap.TryGetValue(assetPath, out prefabObjects))
                                {
                                    prefabObjects = new List<sfObject>();
                                    m_prefabObjectCreateMap[assetPath] = prefabObjects;
                                }
                                prefabObjects.Add(obj);
                                return null;
                            }
                            else
                            {
                                ksLog.Warning(this, "Circular dependency detected between prefabs " + assetPath +
                                    " and " + prefabPath);
                            }
                        }
                        gameObject = new GameObject();
                        if (scene.isLoaded)
                        {
                            SceneManager.MoveGameObjectToScene(gameObject, scene);
                        }
                        isMissingPrefab = true;
                    }
                }
                else
                {
                    gameObject = new GameObject();
                    if (scene.isLoaded)
                    {
                        SceneManager.MoveGameObjectToScene(gameObject, scene);
                    }
                }

                if (isMissingPrefab)
                {
                    missingPrefab = gameObject.AddComponent<sfMissingPrefab>();
                    missingPrefab.PrefabPath = prefabPath;
                    missingPrefab.FileId = sourceFileId;
                }
                if (!string.IsNullOrEmpty(assetPath))
                {
                    // Save the object as a prefab and delete the object from the scene.
                    ksPathUtils.Create(assetPath);
                    GameObject prefab = PrefabUtility.SaveAsPrefabAsset(gameObject, assetPath);
                    UObject.DestroyImmediate(gameObject);
                    gameObject = prefab;
                }
                else if (!isPrefabAsset)
                {
                    sfGuidManager.Get().SetGuid(gameObject, guid);
                    sfUI.Get().MarkSceneViewStale();
                }
            }
            else
            {
                // Send a lock request if we have the game object selected
                if (Selection.Contains(gameObject))
                {
                    obj.RequestLock();
                }

                if (prefabPath != null)
                {
                    missingPrefab = gameObject.GetComponent<sfMissingPrefab>();
                }
            }

            // Set the parent
            if (obj.Parent != null)
            {
                if (obj.Parent.Type == sfType.Hierarchy)
                {
                    if (gameObject.transform.parent != null)
                    {
                        sfComponentUtils.SetParent(gameObject, null);
                    }
                    if (gameObject.scene != scene)
                    {
                        SceneManager.MoveGameObjectToScene(gameObject, scene);
                    }
                }
                else
                {
                    Transform parent = sfObjectMap.Get().Get<Transform>(obj.Parent);
                    if (parent != null && gameObject.transform.parent != parent)
                    {
                        if (PrefabUtility.IsPartOfPrefabAsset(parent) &&
                            !PrefabUtility.IsPartOfPrefabAsset(gameObject))
                        {
                            // Unity doesn't let us add a new child game object to a prefab, so instead we create a
                            // prefab instance, add the child to it and apply the prefab override child to the prefab.
                            assetPath = AssetDatabase.GetAssetPath(parent);
                            parent = (Transform)PrefabUtility.InstantiatePrefab(parent);
                            gameObject.transform.SetParent(parent, false);
                            PrefabUtility.ApplyAddedGameObject(gameObject, assetPath, InteractionMode.AutomatedAction);

                            // Get the new child (and missing prefab component) from the prefab and destroy the prefab
                            // instance.
                            gameObject = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
                            if (missingPrefab != null)
                            {
                                missingPrefab = gameObject.GetComponent<sfMissingPrefab>();
                            }
                            UObject.DestroyImmediate(parent.root.gameObject);
                        }
                        else
                        {
                            sfComponentUtils.SetParent(gameObject.transform, parent);
                        }
                    }
                }
            }

            AddMapping(obj, gameObject);
            if (missingPrefab != null)
            {
                CreateMissingPrefabNotification(missingPrefab);
            }

            sfPropertyManager.Get().ApplyProperties(gameObject, properties);

            // Set the local file id if the game object is a new object in a prefab asset or the root of a prefab
            // asset.
            if ((isNewObject || obj.Parent == null) && isPrefabAsset && fileId != 0)
            {
                sfFileIdUpdater updater = sfFileIdUpdater.Get(gameObject);
                if (updater != null)
                {
                    // If the game object is a prefab variant, it has a prefab instance handle that stores all the
                    // prefab overrides. Set the file id of the prefab instance handle, as it determines the ids of all
                    // other uobjects in the prefab variant, which are not written in the prefab file.
                    UObject prefabHandle = PrefabUtility.GetPrefabInstanceHandle(gameObject);
                    if (prefabHandle != null)
                    {
                        updater.SetFileId(prefabHandle, fileId,
                            (UObject oldUObj, UObject newUObj) => ReplacePrefabInstanceHandle(obj, newUObj)
                        );
                    }
                    else
                    {
                        updater.SetFileId(gameObject, fileId);
                    }
                }
            }

            // Set references to this game object
            sfReferenceProperty[] references = SceneFusion.Get().Service.Session.GetReferences(obj);
            sfPropertyManager.Get().SetReferences(gameObject, references);

            // Link child sfObjects to components.
            sfComponentTranslator translator = sfObjectEventDispatcher.Get().GetTranslator<sfComponentTranslator>(
                sfType.Component);
            sfComponentFinder finder = new sfComponentFinder(gameObject, translator);
            foreach (sfObject child in obj.Children)
            {
                if (child.Type == sfType.Component)
                {
                    bool fileIdMismatch;
                    Component component = finder.Find(child, out fileIdMismatch);
                    if (component != null)
                    {
                        sfObjectMap.Get().Add(child, component);
                        if (fileIdMismatch)
                        {
                            sfFileIdUpdater updater = sfFileIdUpdater.Get(component);
                            if (updater != null)
                            {
                                long componentFileId = (long)((sfDictionaryProperty)child.Property)[sfProp.FileId];
                                updater.SetFileId(component, componentFileId);
                            }
                        }
                    }
                }
            }
            // Destroy unsynced components
            foreach (Component component in gameObject.GetComponents<Component>())
            {
                if (!sfObjectMap.Get().Contains(component) && translator.IsSyncable(component))
                {
                    translator.DestroyComponent(component);
                }
            }
            // Create new components and sync component properties. This must be done after destroying unsynced
            // components so we don't end up in a case where component A and component B cannot both exist on the same
            // object and we try to create A before we destroy B.
            int index = 0;
            foreach (sfObject child in obj.Children)
            {
                if (child.Type == sfType.Component)
                {
                    translator.InitializeComponent(gameObject, child);
                }
                else
                {
                    sfObjectEventDispatcher.Get().OnCreate(child, index);
                }
                index++;
            }
            // Sync component order
            if (!finder.InOrder)
            {
                translator.ApplyComponentOrder(gameObject);
            }

            // Unity has a bug where if you instantiate a prefab and set the rotation on the transform to not override
            // the prefab, setting the position and scale through the serialized object won't work and they will have
            // the prefab values. We fix it by setting the position and scale directly on the transform.
            if (isNewPrefabInstance)
            {
                sfObject transformObj = sfObjectMap.Get().GetSFObject(gameObject.transform);
                if (transformObj != null)
                {
                    sfDictionaryProperty transformProperties = (sfDictionaryProperty)transformObj.Property;
                    if (!transformProperties.HasField(sfProp.Rotation))
                    {
                        sfBaseProperty prop;
                        if (transformProperties.TryGetField(sfProp.Position, out prop))
                        {
                            gameObject.transform.localPosition = prop.As<Vector3>();
                        }
                        if (transformProperties.TryGetField(sfProp.Scale, out prop))
                        {
                            gameObject.transform.localScale = prop.As<Vector3>();
                        }
                    }
                }
            }

            if (obj.IsLocked)
            {
                OnLock(obj);
            }
            InvokeOnLockStateChange(obj, gameObject);
            return gameObject;
        }

        /// <summary>
        /// Gets prefab path and child index data for a game object. If the game object is a missing prefab stand-in,
        /// gets the path and child index from the sfMissingPrefab component.
        /// </summary>
        /// <param name="gameObject">gameObject to get prefab info for.</param>
        /// <param name="prefabPath,">
        /// prefabPath, or null if the game object is not a prefab game object instance or missing
        /// prefab stand-in.
        /// </param>
        /// <param name="fileId">
        /// fileId of the prefab source object, or 0 if the object is a prefab root instance, or
        /// not a prefab game object instance or missing prefab stand-in.
        /// </param>
        /// <returns>
        /// true if the gameObject is a prefab game object instance. Missing prefab stand-ins return false.
        /// </returns>
        private bool GetPrefabInfo(GameObject gameObject, out string prefabPath, out long fileId)
        {
            GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
            if (prefab != null)
            {
                prefabPath = AssetDatabase.GetAssetPath(prefab);
                fileId = prefab.transform.parent == null ? 0 : sfLoader.Get().GetLocalFileId(prefab);
                return true;
            }
            sfMissingPrefab missingPrefab = gameObject.GetComponent<sfMissingPrefab>();
            if (missingPrefab != null)
            {
                prefabPath = missingPrefab.PrefabPath;
                fileId = missingPrefab.FileId;
            }
            else
            {
                prefabPath = null;
                fileId = 0;
            }
            return false;
        }

        /// <summary>
        /// Gets the prefab path for a game object. If the game object has a sfMissingPrefab component, gets the path
        /// from that.
        /// </summary>
        /// <param name="gameObject">gameObject to get prefab info for.</param>
        /// <returns>prefab path, or null if the game object is not a prefab.</returns>
        public string GetPrefabPath(GameObject gameObject)
        {
            GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
            if (prefab != null)
            {
                return AssetDatabase.GetAssetPath(prefab);
            }
            else
            {
                sfMissingPrefab missingPrefab = gameObject.GetComponent<sfMissingPrefab>();
                if (missingPrefab != null)
                {
                    return missingPrefab.PrefabPath;
                }
            }
            return null;
        }

        /// <summary>
        /// Increments the prefab revision number stored in the sfObject for the root of a prefab, if the given sfObject
        /// is part of a prefab asset and the revision number hasn't been incremented this frame. The revision number is
        /// used to determine if the sfObjects for prefab instances need to be updated when a prefab is updated.
        /// </summary>
        /// <param name="obj">
        /// if this obj if for a uobject in a prefab asset, increments the prefab revision number
        /// in the prefab root's sfObject.
        /// </param>
        internal void IncrementPrefabRevision(sfObject obj)
        {
            if (sfConfig.Get().SyncPrefabs != sfConfig.PrefabSyncMode.FULL || obj == null || !obj.IsSyncing)
            {
                return;
            }
            // If the root type is not sfType.GameObject, this is not a prefab.
            obj = obj.Root;
            if (obj.Type != sfType.GameObject || !m_revisedPrefabs.Add(obj))
            {
                return;
            }
            sfDictionaryProperty properties = (sfDictionaryProperty)obj.Property;
            sfBaseProperty revisionProp;
            if (properties.TryGetField(sfProp.PrefabRevision, out revisionProp))
            {
                ((sfValueProperty)revisionProp).Value++;
            }
            else
            {
                properties[sfProp.PrefabRevision] = 1u;
            }
        }

#if UNITY_2022_2_OR_NEWER
        /// <summary>Restores a deleted prefab child instance.</summary>
        /// <param name="prefab">prefab parent instance with deleted child.</param>
        /// <param name="fileId">fileId of source prefab of deleted prefab child instance to restore.</param>
        /// <returns>restored prefab child instance, or null if it could not be restored.</returns>
        private GameObject RestoreChildPrefab(GameObject parent, long fileId)
        {
            if (parent == null || fileId == 0)
            {
                return null;
            }
            GameObject prefabParent = PrefabUtility.GetCorrespondingObjectFromSource(parent);
            if (prefabParent == null)
            {
                return null;
            }
            GameObject prefab = null;
            foreach (Transform child in prefabParent.transform)
            {
                if (sfLoader.Get().GetLocalFileId(child.gameObject) == fileId)
                {
                    prefab = child.gameObject;
                    break;
                }
            }
            if (prefab == null)
            {
                return null;
            }
            PrefabUtility.RevertRemovedGameObject(parent, prefab, InteractionMode.AutomatedAction);

            // If the prefab instance is a prefab variant, we have to save it to make the restored child appear.
            if (PrefabUtility.IsPartOfPrefabAsset(parent))
            {
                PrefabUtility.SavePrefabAsset(parent.transform.root.gameObject);
            }

            // Unity seems to always add the child at the end, so we start looking at the last child.
            for (int i = parent.transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.transform.GetChild(i).gameObject;
                if (PrefabUtility.GetCorrespondingObjectFromSource(child) == prefab)
                {
                    return child;
                }
            }
            return null;
        }
#endif

        /// <summary>
        /// Validates that a game object has the correct prefab source and is not already in the sfObjectMap.
        /// </summary>
        /// <param name="gameObject">gameObject to validate.</param>
        /// <param name="path">
        /// path to prefab the game object should be an instance of. Null if the game object should not
        /// be a prefab game object instance.
        /// </param>
        /// <param name="fileId">
        /// fileId the game object's prefab source should have. 0 if it should not be a prefab child
        /// instance.
        /// </param>
        /// <param name="parent">
        /// parent sfObject the game object's parent should have if the game object is a prefab child
        /// instance. Not checked if null or if the game object is not a prefab child instance.
        /// </param>
        /// <returns>
        /// true if the game object has the correct prefab source and/or is part of the correct prefab
        /// asset and is not in the sfObjectMap.
        /// </returns>
        private bool ValidateGameObject(
            GameObject gameObject,
            string path,
            long fileId, 
            sfObject parent = null)
        {
            if (sfObjectMap.Get().Contains(gameObject))
            {
                return false;
            }
            string currentPath;
            long currentFileId;
            bool isPrefabGameObjectInstance = GetPrefabInfo(gameObject, out currentPath, out currentFileId);
            if (currentPath != path || currentFileId != fileId)
            {
                return false;
            }
            Transform parentTransform = null;
            if (parent != null && fileId != 0)
            {
                parentTransform = sfObjectMap.Get().Get<Transform>(parent);
                if (gameObject.transform.parent != parentTransform)
                {
                    return false;
                }
            }
            // If it has a path but is not a prefab game object instance, it's a missing prefab stand-in. Stand-ins are
            // not valid if the prefab exists.
            if (!string.IsNullOrEmpty(path) && !isPrefabGameObjectInstance &&
                AssetDatabase.LoadAssetAtPath<GameObject>(path) != null &&
                (fileId == 0 || AssetDatabase.GetTypeFromPathAndFileID(path, fileId) != null))
            {
                return false;
            }
            return true;
        }

        /// <summary>Returns the prefab child instance or asset with the given parent sfObject and file id.</summary>
        /// <param name="parentObj">parentObj to get child from.</param>
        /// <param name="fileId">fileId of the prefab source, prefab asset, or prefab instance handle.</param>
        /// <param name="fileIdTarget">
        /// determines what the target of the file id is and what kind of object we
        /// are looking for:
        /// - prefab source: we are looking for a prefab child instance whose prefab source has the given id.
        /// - prefab asset: we are looking for a prefab child asset with the given id.
        /// - prefab instance handle: we are looking for a prefab variant whose prefab instance handle has the
        /// given id.
        /// </param>
        /// <returns>prefab child for the given file id, or null if not found.</returns>
        private GameObject FindPrefabChild(sfObject parentObj, long fileId, FileIdTarget fileIdTarget)
        {
            if (parentObj == null)
            {
                return null;
            }
            Transform parent = sfObjectMap.Get().Get<Transform>(parentObj);
            if (parent == null || (fileIdTarget == FileIdTarget.PREFAB_SOURCE ?
                !PrefabUtility.IsPartOfPrefabInstance(parent) : !PrefabUtility.IsPartOfPrefabAsset(parent)))
            {
                return null;
            }
            foreach (Transform child in parent)
            {
                UObject prefab = null;
                switch (fileIdTarget)
                {
                    case FileIdTarget.PREFAB_SOURCE: prefab =
                        prefab = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject); break;
                    case FileIdTarget.PREFAB_INSTANCE_HANDLE:
                        prefab = PrefabUtility.GetPrefabInstanceHandle(child.gameObject); break;
                    case FileIdTarget.PREFAB_ASSET:
                        prefab = child.gameObject; break;
                }
                if (prefab != null && sfLoader.Get().GetLocalFileId(prefab) == fileId)
                {
                    return child.gameObject;
                }
            }
            return null;
        }

        /// <summary>
        /// Checks if an sfObject has a sibling with the same file id, meaning it is a duplicate prefab child instance.
        /// If a duplicate is detected, the one that was created second on the server is deleted.
        /// </summary>
        /// <param name="obj">obj to check for duplicate of.</param>
        /// <param name="fileId">fileId of the object.</param>
        /// <returns>true if the obj was deleted because an older duplicate was detected.</returns>
        private bool CheckDuplicatePrefabChild(sfObject obj, long fileId)
        {
            sfBaseProperty prop;
            foreach (sfObject current in obj.Parent.Children)
            {
                if (current != obj && ((sfDictionaryProperty)current.Property).TryGetField(sfProp.PrefabSourceFileId, out prop) &&
                    (long)prop == fileId)
                {
                    return sfObjectUtils.DeleteDuplicate(current, obj);
                }
            }
            return false;
        }

        /// <summary>Called when a locally created object is confirmed as created.</summary>
        /// <param name="obj">obj that whose creation was confirmed.</param>
        public override void OnConfirmCreate(sfObject obj)
        {
            sfUI.Get().MarkIconWindowsStale(sfObjectMap.Get().GetUObject(obj));
        }

        /// <summary>Called when a game object is deleted by another user.</summary>
        /// <param name="obj">obj that was deleted.</param>
        public override void OnDelete(sfObject obj)
        {
            GameObject gameObject = sfObjectMap.Get().Get<GameObject>(obj);
            // Destroy the game object
            if (gameObject != null)
            {
                // Apply server hierarchy changes before deleting the game object in case the game object has children
                // that were reparented.
                ApplyHierarchyChanges();
                sfUI.Get().MarkSceneViewStale();
                sfUI.Get().MarkInspectorStale(gameObject, true);
                DestroyGameObject(gameObject);
                sfHierarchyWatcher.Get().MarkHierarchyStale();
                if (gameObject != null)
                {
                    // Clears the properties and parent/child connections for the object and its descendants, then
                    // reuploads the game object, reusing the sfObjects to preserve ids.
                    OnConfirmDelete(obj, false);
                    return;
                }
            }
            // Remove the game object and its descendants from the guid manager.
            obj.ForSelfAndDescendants((sfObject child) =>
            {
                m_lockedPrefabInstancesWithUpdates.Remove(child);
                GameObject go = RemoveMapping(child);
                if (go.IsDestroyed())
                {
                    sfGuidManager.Get().Remove(go);
                }
                return true;
            });
        }

        /// <summary>
        /// Called when a locally-deleted game object is confirmed as deleted. If unsubscribed is true, removes the
        /// object and its descendants from the sfObjectMap. Otherwise clears properties on the object and its
        /// descendants, but keeps them in the sfObjectMap so they can be reused if the game object gets recreated so
        /// references to the objects will work.
        /// </summary>
        /// <param name="obj">obj that was confirmed as deleted.</param>
        /// <param name="unsubscribed">
        /// true if the deletion occurred because we unsubscribed from the object's parent.
        /// </param>
        public override void OnConfirmDelete(sfObject obj, bool unsubscribed)
        {
            if (unsubscribed)
            {
                // Remove the object and its descendants from the sfObjectMap.
                foreach (sfObject child in obj.SelfAndDescendants)
                {
                    sfObjectMap.Get().Remove(child);
                }
            }
            else
            {
                // Clear the properties and children recursively, but keep the objects around so they can be resused to
                // preserve ids if the game object is recreated.
                obj.ForSelfAndDescendants((sfObject child) =>
                {
                    child.Property = new sfDictionaryProperty();
                    if (child.Parent != null)
                    {
                        child.Parent.RemoveChild(child);
                    }
                    GameObject gameObject = sfObjectMap.Get().Get<GameObject>(child);
                    // If the game object still exists, reupload it.
                    if (gameObject != null)
                    {
                        sfUI.Get().MarkIconWindowsStale(gameObject);
                        AddParentToUploadSet(gameObject);
                    }
                    return true;
                });
            }
        }

        /// <summary>
        /// Destroys a game object and removes notifications from it and its descendants. Logs a warning if the game
        /// object could not be destroyed, which occurs in Unity 2022.1 and below if the game object is a prefab child
        /// instance. If the game object is the root of a prefab asset, does not destroy it or log a warning.
        /// </summary>
        /// <param name="gameObject">gameObject to destroy.</param>
        public void DestroyGameObject(GameObject gameObject)
        {
            // Remove all notifications for the game object and its descendants.
            sfUnityUtils.ForSelfAndDescendants(gameObject, (GameObject child) =>
            {
                sfNotificationManager.Get().RemoveNotificationsFor(child);
                foreach (Component component in child.GetComponents<Component>())
                {
                    if (component != null)
                    {
                        sfNotificationManager.Get().RemoveNotificationsFor(component);
                    }
                }
                return true;
            });
            if (sfUnityUtils.IsPrefabAssetRoot(gameObject))
            {
                // The root of a prefab asset cannot be destroyed.
                return;
            }
            sfPrefabSaver.Get().MarkPrefabDirty(gameObject);
            EditorUtility.SetDirty(gameObject);
            try
            {
                UObject.DestroyImmediate(gameObject, sfConfig.Get().SyncPrefabs == sfConfig.PrefabSyncMode.FULL);
            }
            catch (Exception e)
            {
                if (gameObject != null)
                {
                    ksLog.Warning(this, "Unable to destroy game object '" + gameObject.name + "': " + e.Message);
                    // If the object was locked, we want to unlock it.
                    sfUnityUtils.RemoveFlags(gameObject, HideFlags.NotEditable);
                }
                else
                {
                    ksLog.LogException(this, e);
                }
            }
        }

        /// <summary>
        /// Called when a game object is deleted locally. Deletes the game object on the server, or recreates it if the
        /// game object is locked.
        /// </summary>
        /// <param name="instanceId">instanceId of game object that was deleted</param>
        private void OnDeleteGameObject(int instanceId)
        {
            // Do not remove the sfObject so it will be reused if the game object is recreated and references to it
            // will be preserved.
            sfObject obj;
            if (!m_instanceIdToSFObjectMap.TryGetValue(instanceId, out obj) || !obj.IsSyncing)
            {
                return;
            }
            // Sync changed hierarchies before deleting the object in case descendants of the object were
            // reparented.
            SyncChangedHierarchies();
            SyncDeletedObject(obj);
        }

        /// <summary>
        /// Deletes an sfObject for a game object. Recreates the game object if it the sfObject is locked.
        /// </summary>
        /// <param name="obj">obj to delete.</param>
        private void SyncDeletedObject(sfObject obj)
        {
            if (obj == null)
            {
                return;
            }
            if (obj.Type != sfType.GameObject)
            {
                ksLog.Error(this, "DeleteObject was given a " + obj.Type + " sfObject instead of " +
                    sfType.GameObject + ".");
                return;
            }
            // Remove the notifications for the deleted objects.
            obj.ForSelfAndDescendants((sfObject child) =>
            {
                UObject uobj = sfObjectMap.Get().GetUObject(child);
                if ((object)uobj != null)
                {
                    sfNotificationManager.Get().RemoveNotificationsFor(uobj);
                }
                return true;
            });
            if (obj.IsLocked)
            {
                // The object is locked. Recreate it.
                obj.ForSelfAndDescendants((sfObject child) =>
                {
                    if (child.IsLockPending)
                    {
                        child.ReleaseLock();
                    }
                    RemoveMapping(child);
                    return true;
                });
                InitializeGameObjectHierarchy(obj);
            }
            else
            {
                IncrementPrefabRevision(obj.Parent);
                SceneFusion.Get().Service.Session.Delete(obj);
            }
        }

        /// <summary>
        /// Called when a game object is created locally. Adds the game object's parent sfObject to the set of objects
        /// with new children to upload.
        /// </summary>
        /// <param name="gameObject">gameObject that was created.</param>
        private void OnCreateGameObject(GameObject gameObject)
        {
            sfObject obj = sfObjectMap.Get().GetSFObject(gameObject);
            if (obj == null || !obj.IsSyncing)
            {
                AddParentToUploadSet(gameObject);
            }
        }

        /// <summary>
        /// Called when one or more child game objects of an object in a prefab asset are created. Adds the parent game
        /// object's sfObject to the set of objects with new children to upload.
        /// </summary>
        /// <param name="prefabParent">prefabParent with new children.</param>
        private void OnCreatePrefabChild(GameObject prefabParent)
        {
            if (prefabParent == null)
            {
                return;
            }
            sfObject obj = sfObjectMap.Get().GetSFObject(prefabParent.transform);
            if (obj != null && obj.IsSyncing)
            {
                m_parentsWithNewChildren.Add(obj);
                IncrementPrefabRevision(obj);
            }
        }

        /// <summary>Called when a field is removed from a dictionary property.</summary>
        /// <param name="dict">dict the field was removed from.</param>
        /// <param name="name">name of the removed field.</param>
        public override void OnRemoveField(sfDictionaryProperty dict, string name)
        {
            base.OnRemoveField(dict, name);
            sfObject obj = dict.GetContainerObject();
            if (!obj.IsLocked)
            {
                return;
            }
            // Gameobjects become unlocked when you set a prefab property to the default value, so we relock it.
            GameObject gameObject = sfObjectMap.Get().Get<GameObject>(obj);
            if (gameObject != null && PrefabUtility.GetPrefabInstanceHandle(gameObject) != null && 
                !PrefabUtility.IsPartOfPrefabAsset(gameObject))
            {
                sfUnityUtils.AddFlags(gameObject, HideFlags.NotEditable);
            }
        }

        /// <summary>Called when a game object is locked by another user.</summary>
        /// <param name="obj">obj that was locked.</param>
        public override void OnLock(sfObject obj)
        {
            GameObject gameObject = sfObjectMap.Get().Get<GameObject>(obj);
            if (gameObject != null)
            {
                Lock(gameObject, obj);
                InvokeOnLockStateChange(obj, gameObject);
            }
        }

        /// <summary>Called when a game object is unlocked by another user.</summary>
        /// <param name="obj">obj that was unlocked.</param>
        public override void OnUnlock(sfObject obj)
        {
            GameObject gameObject = sfObjectMap.Get().Get<GameObject>(obj);
            if (gameObject != null)
            {
                Unlock(gameObject);
                InvokeOnLockStateChange(obj, gameObject);

                // If the prefab instance's source prefab was updated while the prefab instance was locked, add the game
                // object to the update list to send changes for if it wasn't already updated by someone else (revision
                // number doesn't match the prefab). We can't send the changes now because modifying sfObjects is
                // disabled during RPC processing when this function gets called.
                if (m_lockedPrefabInstancesWithUpdates.Remove(obj))
                {
                    m_prefabInstanceUpdateList.Add(gameObject);
                }
            }
        }

        /// <summary>Called when a game object's lock owner changes.</summary>
        /// <param name="obj">obj whose lock owner changed.</param>
        public override void OnLockOwnerChange(sfObject obj)
        {
            GameObject gameObject = sfObjectMap.Get().Get<GameObject>(obj);
            if (gameObject != null)
            {
                InvokeOnLockStateChange(obj, gameObject);
                sfLockManager.Get().UpdateLockMaterial(gameObject, obj);
            }
        }

        /// <summary>
        /// Locks a game object, preventing it from being edited and creating a lock object to render the lock shader.
        /// </summary>
        /// <param name="gameObject">gameObject to lock.</param>
        /// <param name="obj">obj for the game object.</param>
        private void Lock(GameObject gameObject, sfObject obj)
        {
            sfUI.Get().MarkInspectorStale(gameObject, true);
            // Don't change hide flags or create a lock object for prefab assets, as that would apply to the prefab
            // instances. The sfGameObjectEditor handles locking for prefab assets.
            if (!PrefabUtility.IsPartOfPrefabAsset(gameObject))
            {
                sfUnityUtils.AddFlags(gameObject, HideFlags.NotEditable);
                sfLockManager.Get().CreateLockObject(gameObject, obj);
            }
        }

        /// <summary>
        /// Unlocks a game object, allowing it to be edited and destroying the lock object that renders the lock shader.
        /// </summary>
        /// <param name="gameObject">gameObject to unlock.</param>
        /// <param name="forceCheckLockObject">
        /// if true, will check for a lock object to destroy even when lock shaders
        /// are disabled.
        /// </param>
        private void Unlock(GameObject gameObject, bool forceCheckLockObject = false)
        {
            sfUI.Get().MarkInspectorStale(gameObject, true);
            if (!PrefabUtility.IsPartOfPrefabAsset(gameObject))
            {
                sfUnityUtils.RemoveFlags(gameObject, HideFlags.NotEditable);
                GameObject lockObject = sfLockManager.Get().FindLockObject(gameObject, forceCheckLockObject);
                if (lockObject != null)
                {
                    UObject.DestroyImmediate(lockObject);
                    sfUI.Get().MarkSceneViewStale();
                }
            }
        }

        /// <summary>
        /// Called before saving a scene. Temporarily unlocks locked game objects in the scene so they are not saved as
        /// not editable.
        /// </summary>
        /// <param name="scene">scene that will be saved.</param>
        private void PreSave(Scene scene)
        {
            foreach (GameObject gameObject in sfUnityUtils.IterateGameObjects(scene))
            {
                sfObject obj = sfObjectMap.Get().GetSFObject(gameObject);
                if (obj != null && obj.IsLocked)
                {
                    sfUnityUtils.RemoveFlags(gameObject, HideFlags.NotEditable);
                    m_tempUnlockedObjects.Add(gameObject);
                }
            }
        }

        /// <summary>Relocks all game objects that were temporarily unlocked.</summary>
        private void RelockObjects()
        {
            foreach (GameObject gameObject in m_tempUnlockedObjects)
            {
                if (!PrefabUtility.IsPartOfPrefabAsset(gameObject))
                {
                    sfObject obj = sfObjectMap.Get().GetSFObject(gameObject);
                    if (obj != null && obj.IsLocked)
                    {
                        sfUnityUtils.AddFlags(gameObject, HideFlags.NotEditable);
                    }
                }
            }
            m_tempUnlockedObjects.Clear();
        }

        /// <summary>Called when a uobject is selected. Syncs the object if it is an unsynced game object.</summary>
        /// <param name="uobj">uobj that was selected.</param>
        private void OnSelect(UObject uobj)
        {
            GameObject gameObject = uobj as GameObject;
            if (gameObject == null)
            {
                return;
            }
            sfObject obj = sfObjectMap.Get().GetSFObject(gameObject);
            if (obj == null || !obj.IsSyncing)
            {
                AddParentToUploadSet(gameObject);
            }
        }

        /// <summary>Called when a prefab stage is opened. Sends a lock request for the prefab being edited.</summary>
        /// <param name="stage">stage that opened.</param>
        private void HandleOpenPrefabStage(PrefabStage stage)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(stage.assetPath);
            sfObject obj = sfObjectMap.Get().GetSFObject(prefab);
            if (obj != null)
            {
                obj.RequestLock();
            }
        }

        /// <summary>
        /// Called when a prefab stage is closed. Releases the lock on the edited prefab if it is not selected.
        /// </summary>
        /// <param name="stage">stage that was closed.</param>
        private void HandleClosePrefabStage(PrefabStage stage)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(stage.assetPath);
            if (sfSelectionWatcher.Get().IsSelected(prefab))
            {
                return;
            }
            sfObject obj = sfObjectMap.Get().GetSFObject(prefab);
            if (obj != null)
            {
                obj.ReleaseLock();
            }
        }

        /// <summary>
        /// Called when an game object is deselected. Releases the lock on the object if it is not being edited in a
        /// prefab stage.
        /// </summary>
        /// <param name="obj">obj for the deselected game object.</param>
        /// <param name="uobj">uobj that was deselected.</param>
        public override void OnDeselect(sfObject obj, UObject uobj)
        {
            if (sfConfig.Get().SyncPrefabs == sfConfig.PrefabSyncMode.FULL)
            {
                GameObject gameObject = uobj as GameObject;
                if (gameObject != null && sfUnityUtils.IsPrefabAssetRoot(gameObject))
                {
                    string path = AssetDatabase.GetAssetPath(gameObject);
                    if (sfUnityUtils.FindPrefabStage(path) != null)
                    {
                        return;
                    }
                }
            }
            base.OnDeselect(obj, uobj);
        }

        /// <summary>Invokes the OnLockStateChange event.</summary>
        /// <param name="obj">obj whose lock state changed.</param>
        /// <param name="gameObject">gameObject whose lock state changed.</param>
        private void InvokeOnLockStateChange(sfObject obj, GameObject gameObject)
        {
            sfUI.Get().MarkIconWindowsStale(gameObject);
            sfUI.Get().MarkInspectorStale(gameObject);
            if (OnLockStateChange == null)
            {
                return;
            }
            LockType lockType = LockType.UNLOCKED;
            if (obj.IsFullyLocked)
            {
                lockType = LockType.FULLY_LOCKED;
            }
            else if (obj.IsPartiallyLocked)
            {
                lockType = LockType.PARTIALLY_LOCKED;
            }
            OnLockStateChange(gameObject, lockType, obj.LockOwner);
        }

        /// <summary>Checks if a game object has a component that will prevent it from syncing.</summary>
        /// <param name="gameObject">gameObject to check for components to prevent syncing.</param>
        /// <returns>true if the gameObject has a component that prevents syncing.</returns>
        private bool HasComponentThatPreventsSync(GameObject gameObject)
        {
            if (m_blacklist.Count == 0)
            {
                return false;
            }
            foreach (Component component in gameObject.GetComponents<Component>())
            {
                if (!CanSyncObjectsWith(component))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
