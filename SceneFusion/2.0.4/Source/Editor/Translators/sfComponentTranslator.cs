using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using KS.SF.Reactor;
using KS.SceneFusion2.Client;
using KS.SceneFusion;
using KS.SceneFusionCommon;
using KS.SF.Unity.Editor;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>Manages syncing of components</summary>
    public class sfComponentTranslator : sfBaseUObjectTranslator
    {
        /// <summary>Callback to initialize a component or sfObject.</summary>
        /// <param name="obj"></param>
        /// <param name="component"></param>
        public delegate void Initializer(sfObject obj, Component component);

        /// <summary>Callback for when a component is deleted by another user.</summary>
        /// <param name="obj">obj for the component that was deleted.</param>
        /// <param name="component">component that was deleted.</param>
        public delegate void DeleteHandler(sfObject obj, Component component);

        /// <summary>Callback for when a component is deleted locally.</summary>
        /// <param name="obj">obj for the component that was deleted locally.</param>
        public delegate void LocalDeleteHandler(sfObject obj);

        /// <summary>
        /// Maps component types to initializers to call after initializing a component with server values, but
        /// before creating the component's children.
        /// </summary>
        public sfTypeEventMap<Initializer> ComponentInitializers
        {
            get { return m_componentInitializers; }
        }
        private sfTypeEventMap<Initializer> m_componentInitializers = new sfTypeEventMap<Initializer>();

        /// <summary>
        /// Maps component types to initializers to call after create the sfObject for a component, but before creating
        /// the child objects.
        /// </summary>
        public sfTypeEventMap<Initializer> ObjectInitializers
        {
            get { return m_objectInitializers; }
        }
        private sfTypeEventMap<Initializer> m_objectInitializers = new sfTypeEventMap<Initializer>();

        /// <summary>
        /// Maps component types to delete handlers to call when components are deleted by the server.
        /// </summary>
        public sfTypeEventMap<DeleteHandler> DeleteHandlers
        {
            get { return m_deleteHandlers; }
        }
        private sfTypeEventMap<DeleteHandler> m_deleteHandlers = new sfTypeEventMap<DeleteHandler>();

        /// <summary>Maps component types to delete handlers to call when components are deleted locally.</summary>
        public sfTypeEventMap<LocalDeleteHandler> LocalDeleteHandlers
        {
            get { return m_localDeleteHandlers; }
        }
        private sfTypeEventMap<LocalDeleteHandler> m_localDeleteHandlers = new sfTypeEventMap<LocalDeleteHandler>();

        // Don't sync these component types
        private HashSet<Type> m_blacklist = new HashSet<Type>();
        private HashSet<GameObject> m_componentOrderChangedSet = new HashSet<GameObject>();
        private List<KeyValuePair<sfMissingComponent, Component>> m_replacedComponents = 
            new List<KeyValuePair<sfMissingComponent, Component>>();
        private int m_replacementCount = 0;

        private ksReflectionObject m_roGetCoupledComponent;

        /// <summary>Initialization</summary>
        public override void Initialize()
        {
            m_roGetCoupledComponent = new ksReflectionObject(typeof(Component)).GetMethod("GetCoupledComponent");
            DontSync<sfMissingPrefab>();

            // This property is not visible in the inspector and is automatically set to the MeshRenderer on the same
            // object so it does not need to be synced. If it is synced and one user hasn't imported TMP and they
            // create a TMP_Text, it will sync as null (it gets set after you import TMP) which causes errors for other
            // users, so we don't sync it.
            sfPropertyManager.Get().Blacklist.Add("TMPro.TMP_Text", "m_renderer");

            // Directly set transform position/rotation/scale to avoid SceneView rendering delays common when using serialized properties.
            m_propertyChangeHandlers.Add<Transform>(sfProp.Position, (UObject uobj, sfBaseProperty prop) =>
            {
                RedrawSceneView(null);
                if (prop == null)
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(uobj))
                    {
                        return false;
                    }
                    (uobj as Transform).localPosition = Vector3.zero;
                }
                else
                {
                    (uobj as Transform).localPosition = prop.Cast<Vector3>();
                }
                sfPrefabSaver.Get().MarkPrefabDirty(uobj);
                return true;
            });
            m_propertyChangeHandlers.Add<Transform>(sfProp.Rotation, (UObject uobj, sfBaseProperty prop) =>
            {
                RedrawSceneView(null);
                if (prop == null)
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(uobj))
                    {
                        return false;
                    }
                    (uobj as Transform).localRotation = Quaternion.identity;
                }
                else
                {
                    (uobj as Transform).localRotation = prop.Cast<Quaternion>();
                }
                sfPrefabSaver.Get().MarkPrefabDirty(uobj);
                return true;
            });
            m_propertyChangeHandlers.Add<Transform>(sfProp.Scale, (UObject uobj, sfBaseProperty prop) =>
            {
                RedrawSceneView(null);
                if (prop == null)
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(uobj))
                    {
                        return false;
                    }
                    (uobj as Transform).localScale = Vector3.one;
                }
                else
                {
                    (uobj as Transform).localScale = prop.Cast<Vector3>();
                }
                sfPrefabSaver.Get().MarkPrefabDirty(uobj);
                return true;
            });

            PostPropertyChange.Add<MeshFilter>("m_Mesh", MarkLockObjectStale);
            PostPropertyChange.Add<SpriteRenderer>("m_Sprite", MarkLockObjectStale);
            PostPropertyChange.Add<LineRenderer>("m_Parameters", MarkLockObjectStale);
            PostPropertyChange.Add<Component>("m_Enabled", (UObject uobj, sfBaseProperty prop) =>
                sfUI.Get().MarkSceneViewStale());

            PostUObjectChange.Add<LODGroup>(MarkLockLODStale);
            PostUObjectChange.Add<Renderer>((UObject uobj) => sfUI.Get().MarkSceneViewStale());
        }

        /// <summary>Called after connecting to a session.</summary>
        public override void OnSessionConnect()
        {
            sfUnityEventDispatcher.Get().OnUpdate += Update;
            sfUnityEventDispatcher.Get().OnAddOrRemoveComponents += OnAddOrRemoveComponents;
        }

        /// <summary>Called after disconnecting from a session.</summary>
        public override void OnSessionDisconnect()
        {
            sfUnityEventDispatcher.Get().OnUpdate -= Update;
            sfUnityEventDispatcher.Get().OnAddOrRemoveComponents -= OnAddOrRemoveComponents;
        }

        /// <summary>Don't sync components of type T.</summary>
        public void DontSync<T>() where T : Component
        {
            m_blacklist.Add(typeof(T));
        }

        /// <summary>Don't sync components of the given type.</summary>
        /// <param name="type">type to not sync.</param>
        public void DontSync(Type type)
        {
            m_blacklist.Add(type);
        }

        /// <summary>
        /// Checks if a component can be synced. Components are syncable if the following conditions are met:
        /// - the component is not null
        /// - it is not hidden
        /// - it can be saved in the editor
        /// - it is not blacklisted (DontSync wasn't called on its type).
        /// </summary>
        public bool IsSyncable(Component component)
        {
            return component != null &&
                (component.hideFlags & (HideFlags.HideInInspector | HideFlags.DontSaveInEditor)) == HideFlags.None &&
                !IsBlacklisted(component);
        }

        /// <summary>Called every frame.</summary>
        /// <param name="deltaTime">deltaTime in seconds since the last update.</param>
        private void Update(float deltaTime)
        {
            // Destroy replaced missing components and update references to the replacement component. Apply properties
            // to the replacement component.
            foreach (KeyValuePair<sfMissingComponent, Component> replacement in m_replacedComponents)
            {
                sfObjectMap.Get().Remove(replacement.Key);
                DestroyComponent(replacement.Key);
                sfObject obj = sfObjectMap.Get().GetSFObject(replacement.Value);
                if (obj != null)
                {
                    sfPropertyManager.Get().ApplyProperties(replacement.Value, (sfDictionaryProperty)obj.Property);
                    sfReferenceProperty[] references = SceneFusion.Get().Service.Session.GetReferences(obj);
                    sfPropertyManager.Get().SetReferences(replacement.Value, references);
                }
            }
            m_replacedComponents.Clear();

            if (m_replacementCount > 0)
            {
                ksLog.Info(this, "Replaced " + m_replacementCount + " missing component(s).");
                m_replacementCount = 0;
            }

            // Apply component order changes from the server
            if (m_componentOrderChangedSet.Count > 0)
            {
                foreach (GameObject gameObject in m_componentOrderChangedSet)
                {
                    ApplyComponentOrder(gameObject);
                }
                m_componentOrderChangedSet.Clear();
            }
        }

        /// <summary>
        /// Called when components are added or removed from a game object. Checks for new or deleted components on a
        /// game object and sends changes to the server, or reverts to the server state if the game object is locked.
        /// </summary>
        /// <param name="gameObject">gameObject with added or removed components.</param>
        private void OnAddOrRemoveComponents(GameObject gameObject)
        {
            SyncComponents(gameObject);
        }

        /// <summary>
        /// Checks for new or deleted components on a game object and sends changes to the server, or reverts to the
        /// server state if the game object is locked.
        /// </summary>
        /// <param name="gameObject">gameObject to sync components on.</param>
        /// <returns>true if components changed.</returns>
        public bool SyncComponents(GameObject gameObject)
        {
            sfObject obj = sfObjectMap.Get().GetSFObject(gameObject);
            if (obj == null || !obj.IsSyncing)
            {
                return false;
            }
            bool changed = false;
            bool replacedTransform = false;
            sfObject destroyedTransformObj = null;
            if (obj.IsLocked)
            {
                replacedTransform = RestoreDeletedComponents(obj);
            }
            else if (DeleteObjectsForDestroyedComponents(obj, out destroyedTransformObj))
            {
                changed = true;
            }
            sfGameObjectTranslator translator = sfObjectEventDispatcher.Get().GetTranslator<sfGameObjectTranslator>(
                sfType.GameObject);
            int index = 0;
            foreach (Component component in gameObject.GetComponents<Component>())
            {
                if (!IsSyncable(component))
                {
                    continue;
                }
                sfObject componentObj = sfObjectMap.Get().GetSFObject(component);
                // Check if the component is new
                if (componentObj == null || !componentObj.IsSyncing)
                {
                    if (obj.IsLocked)
                    {
                        DestroyComponent(component);
                        continue;
                    }
                    if (!translator.CanSyncObjectsWith(component))
                    {
                        EditorUtility.DisplayDialog(Product.NAME + " - Cannot Add Component", "Cannot add '" +
                            component.GetType().Name + "' to a synced game object because objects with that " +
                            "component will not sync.", "OK");
                        DestroyComponent(component);
                        continue;
                    }
                    componentObj = CreateObject(component);
                    if (componentObj != null)
                    {
                        SceneFusion.Get().Service.Session.Create(componentObj, obj, index);
                        changed = true;
                        if (component is Transform && destroyedTransformObj != null)
                        {
                            replacedTransform = true;
                            // Move children from the old transform object to the new transform object.
                            while (destroyedTransformObj.Children.Count > 0)
                            {
                                componentObj.AddChild(destroyedTransformObj.Children[0]);
                            }
                        }
                    }
                }
                index++;
            }

            if (sfUndoManager.Get().IsHandlingUndoRedo)
            {
                // Undoing component addition/removal can change the hideflags, so reset the hideflags.
                if (obj.IsLocked)
                {
                    sfUnityUtils.AddFlags(gameObject, HideFlags.NotEditable);
                }
                else
                {
                    sfUnityUtils.RemoveFlags(gameObject, HideFlags.NotEditable);
                }

                if (replacedTransform)
                {
                    // Undoing relpacement of a Transform with a RectTransorm or vice versa restores the child
                    // state, so we need to sync that.
                    translator.SyncDestroyedChildren(gameObject);
                    translator.SyncHierarchyNextUpdate(sfObjectMap.Get().GetSFObject(gameObject.transform));

                    if (obj.IsLocked)
                    {
                        // If the user undoes a transform replacement on a locked object, then performs a redo, this
                        // can crash Unity, so we clear the redo stack to prevent this.
                        sfUndoManager.Get().ClearRedo();
                    }
                }
            }
            if (destroyedTransformObj != null)
            {
                // Delete the old transform.
                SceneFusion.Get().Service.Session.Delete(destroyedTransformObj);
            }

            if (changed && translator != null)
            {
                translator.IncrementPrefabRevision(obj);
            }
            return changed;
        }

        /// <summary>Applies component order changes from the server to a game object.</summary>
        /// <param name="gameObject">gameObject to apply component order to.</param>
        public void ApplyComponentOrder(GameObject gameObject)
        {
            sfObject obj = sfObjectMap.Get().GetSFObject(gameObject);
            if (obj == null)
            {
                return;
            }
            bool changed = false;
            sfPropertyManager.Get().ApplySerializedProperties(gameObject);
            SerializedObject so = new SerializedObject(gameObject);
            SerializedProperty serializedComponents = so.FindProperty("m_Component");
            int index = 0;
            foreach (sfObject child in obj.Children)
            {
                if (child.Type != sfType.Component)
                {
                    continue;
                }
                Component serverComponent = sfObjectMap.Get().Get<Component>(child);
                if (serverComponent == null)
                {
                    continue;
                }
                // Get the next syncable component from the serialized components
                Component clientComponent = null;
                SerializedProperty sprop = null;
                while (index < serializedComponents.arraySize)
                {
                    sprop = serializedComponents.GetArrayElementAtIndex(index).FindPropertyRelative("component");
                    clientComponent = sprop.objectReferenceValue as Component;
                    index++;
                    if (clientComponent == serverComponent || IsSyncable(clientComponent))
                    {
                        break;
                    }
                }
                // Check if the client component matched the expected component from the server.
                if (clientComponent != serverComponent && clientComponent != null)
                {
                    // Components don't match. Overwrite the client reference.
                    sprop.objectReferenceValue = serverComponent;
                    changed = true;
                }
            }
            if (changed)
            {
                sfPropertyUtils.ApplyProperties(so, true);
            }
        }

        /// <summary>
        /// Sends component order changes for a game object to the server. If the object is locked, reverts the
        /// components to the server order.
        /// </summary>
        /// <param name="gameObject">gameObject to sync component order for.</param>
        public void SyncComponentOrder(GameObject gameObject)
        {
            sfObject obj = sfObjectMap.Get().GetSFObject(gameObject);
            if (obj == null)
            {
                return;
            }
            if (obj.IsLocked || gameObject.GetComponent<sfMissingPrefab>() != null)
            {
                // Apply the server order at the end of the frame.
                m_componentOrderChangedSet.Add(gameObject);
                return;
            }
            Component[] components = gameObject.GetComponents<Component>();
            sfObject clientChild = null;
            int serverIndex = -1;
            int clientIndex = 0;
            bool changed = false;
            foreach (sfObject serverChild in obj.Children)
            {
                serverIndex++;
                if (serverChild.Type != sfType.Component)
                {
                    continue;
                }
                while (clientChild != serverChild)
                {
                    // Get the next child from the local components
                    clientChild = null;
                    while (clientIndex < components.Length && clientChild == null)
                    {
                        clientChild = sfObjectMap.Get().GetSFObject(components[clientIndex]);
                        clientIndex++;
                    }
                    if (clientChild == null)
                    {
                        return;
                    }
                    // Check if the local component matches the server component
                    if (clientChild != serverChild)
                    {
                        // Components don't match. Move the server component.
                        clientChild.SetChildIndex(serverIndex);
                        serverIndex++;
                        changed = true;
                    }
                }
            }
            if (changed)
            {
                sfGameObjectTranslator translator = 
                    sfObjectEventDispatcher.Get().GetTranslator<sfGameObjectTranslator>(sfType.GameObject);
                if (translator != null)
                {
                    translator.IncrementPrefabRevision(obj);
                }
            }
        }

        /// <summary>Iterates the component children of an object and recreates destroyed components.</summary>
        /// <param name="obj">obj to restore deleted component children for.</param>
        /// <returns>true if a destroyed transform was restored.</returns>
        private bool RestoreDeletedComponents(sfObject obj)
        {
            int index = -1;
            bool restoredTransform = false;
            foreach (sfObject child in obj.Children)
            {
                index++;
                if (child.Type != sfType.Component)
                {
                    continue;
                }
                Component component = sfObjectMap.Get().Get<Component>(child);
                if (component.IsDestroyed())
                {
                    sfObjectMap.Get().Remove(component);
                    if (component is Transform)
                    {
                        GameObject gameObject = sfObjectMap.Get().Get<GameObject>(obj);
                        if (gameObject != null && gameObject.transform.GetType() == component.GetType())
                        {
                            // The transform was replaced with a transform of the same type. Link the old transform's
                            // sfObject to the new transform and apply the server properties.
                            sfObjectMap.Get().Add(child, gameObject.transform);
                            sfPropertyManager.Get().ApplyProperties(gameObject.transform, 
                                (sfDictionaryProperty)child.Property);
                            continue;
                        }
                        restoredTransform = true;
                    }
                    OnCreate(child, index);
                }
            }
            return restoredTransform;
        }

        /// <summary>
        /// Iterates the component children of an object, looking for destroyed components and deletes their
        /// corresponding sfObjects.
        /// </summary>
        /// <param name="obj">obj to check for deleted child components.</param>
        /// <param name="destroyedTransformObj">
        /// Set to the sfObject for the destroyed transform if the
        /// transform was destroyed (and replaced with a different type of transform), or set to null.
        /// </param>
        /// <returns>true if components were deleted.</returns>
        private bool DeleteObjectsForDestroyedComponents(sfObject obj, out sfObject destroyedTransformObj)
        {
            destroyedTransformObj = null;
            GameObject gameObject = sfObjectMap.Get().Get<GameObject>(obj);
            if (gameObject == null)
            {
                return false;
            }
            bool deleted = false;
            foreach (sfObject child in obj.Children)
            {
                if (child.Type != sfType.Component)
                {
                    continue;
                }
                Component component = sfObjectMap.Get().Get<Component>(child);
                if (component.IsDestroyed())
                {
                    deleted = true;
                    LocalDeleteHandler handlers = m_localDeleteHandlers.GetHandlers(component.GetType());
                    if (handlers != null)
                    {
                        handlers(child);
                    }
                    sfNotificationManager.Get().RemoveNotificationsFor(component);
                    sfObjectMap.Get().Remove(child);
                    if (component is Transform)
                    {
                        Transform transform = gameObject.transform;
                        if (transform.GetType() == component.GetType())
                        {
                            // The transform was replaced with another transform of the same type. Link the old
                            // transform's sfObject to the new transform and send property changes.
                            sfObjectMap.Get().Add(child, transform);
                            sfPropertyManager.Get().SendPropertyChanges(transform, (sfDictionaryProperty)child.Property);
                        }
                        else
                        {
                            destroyedTransformObj = child;
                        }
                        continue;
                    }
                    SceneFusion.Get().Service.Session.Delete(child);
                }
            }
            return deleted;
        }

        /// <summary>Creates an sfObject for a uobject. Does not upload or create properties for the object.</summary>
        /// <param name="uobj">uobj to create sfObject for.</param>
        /// <param name="outObj">outObj created for the uobject.</param>
        /// <returns>true if the uobject was handled by this translator.</returns>
        public override bool TryCreate(UObject uobj, out sfObject outObj)
        {
            outObj = null;
            Component component = uobj as Component;
            if (component == null)
            {
                return false;
            }
            if (IsSyncable(component))
            {
                outObj = new sfObject(sfType.Component, new sfDictionaryProperty());
                sfObjectMap.Get().Add(outObj, component);
            }
            return true;
        }

        /// <summary>Recursively creates sfObjects for a component and its children.</summary>
        /// <param name="component">component to create sfObject for.</param>
        /// <param name="isTransform">true if the component is a transform.</param>
        public sfObject CreateObject(Component component, bool isTransform = false)
        {
            sfObject obj = sfObjectMap.Get().GetOrCreateSFObject(component, sfType.Component);
            if (obj.IsSyncing)
            {
                return null;
            }
            sfDictionaryProperty properties = (sfDictionaryProperty)obj.Property;
            properties[sfProp.Class] = sfComponentUtils.GetName(component);

            if (PrefabUtility.IsPartOfPrefabAsset(component) && !PrefabUtility.IsPartOfPrefabInstance(component))
            {
                properties[sfProp.FileId] = sfLoader.Get().GetLocalFileId(component);
            }

            sfMissingComponent missingComponent = component as sfMissingComponent;
            if (missingComponent != null)
            {
                sfMissingScriptSerializer.Get().DeserializeProperties(missingComponent, properties);

                Component reloadedComponent = GetOrAddComponent(missingComponent.gameObject,
                    missingComponent.Name, PrefabUtility.IsPartOfPrefabInstance(missingComponent));
                if (reloadedComponent == null)
                {
                    CreateMissingComponentNotification(missingComponent);
                }
                else
                {
                    component = reloadedComponent;
                    m_replacementCount++;
                    sfObjectMap.Get().Add(obj, component);

                    // Reapply the server component order at the end of the frame.
                    m_componentOrderChangedSet.Add(component.gameObject);

                    // Keep the missing component around until the end of the frame to be sure we've created reference
                    // properties for all references to it, then update the references and destroy the component.
                    m_replacedComponents.Add(new KeyValuePair<sfMissingComponent, Component>(missingComponent,
                        component));
                }
            }
            else
            {
                sfPropertyManager.Get().CreateProperties(component, properties);
            }

            // Get the file id from the source prefab if the component is a non-transform prefab component instance
            // and set the file id property.
            if (!isTransform)
            {
                Component prefabComponent = PrefabUtility.GetCorrespondingObjectFromSource(component);
                if (prefabComponent != null)
                {
                    long fileId = sfLoader.Get().GetLocalFileId(prefabComponent);
                    if (fileId != 0)
                    {
                        properties[sfProp.PrefabSourceFileId] = fileId;
                    }
                }
            }

            // Call the initializers
            Initializer handlers = m_objectInitializers.GetHandlers(component.GetType());
            if (handlers != null)
            {
                handlers(obj, component);
            }

            // Create gameobject child objects if this is a transform
            if (isTransform)
            {
                Transform transform = component as Transform;
                if (transform != null)
                {
                    sfGameObjectTranslator translator = sfObjectEventDispatcher.Get()
                        .GetTranslator<sfGameObjectTranslator>(sfType.GameObject);
                    foreach (Transform childTransform in transform)
                    {
                        if (translator.IsSyncable(childTransform.gameObject))
                        {
                            sfObject child = translator.CreateObject(childTransform.gameObject);
                            if (child != null)
                            {
                                obj.AddChild(child);
                            }
                            else
                            {
                                child = sfObjectMap.Get().GetSFObject(childTransform.gameObject);
                                if (child != null && child.IsSyncing)
                                {
                                    translator.SyncHierarchyNextUpdate(obj);
                                }
                            }
                        }
                    }
                }
            }
            return obj;
        }

        /// <summary>Called when a component is created by another user.</summary>
        /// <param name="obj">obj that was created.</param>
        /// <param name="childIndex">childIndex of the new object. -1 if the object is a root.</param>
        public override void OnCreate(sfObject obj, int childIndex)
        {
            if (obj.Parent == null || obj.Parent.Type != sfType.GameObject)
            {
                ksLog.Warning(this, "Component object cannot be created without a game object parent.");
                return;
            }
            GameObject gameObject = sfObjectMap.Get().Get<GameObject>(obj.Parent);
            if (gameObject == null)
            {
                return;
            }
            InitializeComponent(gameObject, obj);
            sfUI.Get().MarkSceneViewStale();
            sfUI.Get().MarkInspectorStale(gameObject, true);
            // If this isn't the last component, we need to reapply the server component order.
            if (childIndex != obj.Parent.Children.Count - 1)
            {
                m_componentOrderChangedSet.Add(gameObject);
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
        }

        /// <summary>
        /// Initializes the component for an sfObject from the sfObjectMap with server values, or creates one if there
        /// isn't one in the sfObjectMap. Recursively initializes children.
        /// </summary>
        /// <param name="gameObject">gameObject to attach the component to.</param>
        /// <param name="obj">obj to initialize component for.</param>
        public void InitializeComponent(GameObject gameObject, sfObject obj)
        {
            sfDictionaryProperty properties = (sfDictionaryProperty)obj.Property;
            Component component = sfObjectMap.Get().Get<Component>(obj);
            sfMissingComponent missingComponent = component as sfMissingComponent;
            sfGameObjectTranslator translator = sfObjectEventDispatcher.Get().GetTranslator<sfGameObjectTranslator>(
                sfType.GameObject);
            if (component == null)
            {
                string name = (string)properties[sfProp.Class];
                sfBaseProperty prop;
                long sourceFileId = properties.TryGetField(sfProp.PrefabSourceFileId, out prop) ? (long)prop : 0;

                // We could not find the component. If it's a prefab component instance, try find a removed prefab
                // component instance for this component we can restore.
                if (PrefabUtility.IsPartOfPrefabInstance(gameObject) && sourceFileId != 0)
                {
                    if (CheckDuplicateComponent(obj, sourceFileId))
                    {
                        return;
                    }

                    RemovedComponent removedComponent = FindRemovedComponent(gameObject, name, sourceFileId);
                    component = RestoreRemovedComponent(removedComponent);
                    // Restoring a deleted prefab component resets the hideflags, so we need to relock the game
                    // objects in the prefab instance.
                    if (component != null && obj.Parent.IsLocked && translator != null)
                    {
                        translator.RelockPrefabNextPreUpdate(gameObject);
                    }
                }

                // Create the component if we could not find it.
                if (component == null)
                {
                    component = GetOrAddComponent(gameObject, name, sourceFileId != 0);
                    if (component == null)
                    {
                        missingComponent = gameObject.AddComponent<sfMissingComponent>();
                        missingComponent.Name = name;
                        component = missingComponent;
                    }
                }

                // Set the local file id if the game object is a non-variant prefab.
                if (PrefabUtility.IsPartOfPrefabAsset(gameObject) && !PrefabUtility.IsPartOfPrefabInstance(gameObject))
                {
                    sfFileIdUpdater updater = sfFileIdUpdater.Get(gameObject);
                    if (updater != null)
                    {
                        long fileId = properties.TryGetField(sfProp.FileId, out prop) ? (long)prop : 0;
                        updater.SetFileId(component, fileId);
                    }
                }

                sfLockManager.Get().MarkLockObjectStale(gameObject);
            }
            else if (missingComponent != null)
            {
                sfBaseProperty prop;
                long sourceFileId = properties.TryGetField(sfProp.PrefabSourceFileId, out prop) ? (long)prop : 0;

                Component reloadedComponent = GetOrAddComponent(gameObject, missingComponent.Name, sourceFileId != 0);
                if (reloadedComponent != null)
                {
                    component = reloadedComponent;
                    m_replacementCount++;
                    DestroyComponent(missingComponent);
                    // Reapply the server component order at the end of the frame.
                    m_componentOrderChangedSet.Add(gameObject);
                }
            }

            sfObjectMap.Get().Add(obj, component);

            if (missingComponent != null)
            {
                CreateMissingComponentNotification(missingComponent);
                sfMissingScriptSerializer.Get().SerializeProperties(missingComponent, properties);
            }
            else
            {
                sfPropertyManager.Get().ApplyProperties(component, properties);
            }

            // Set references to this component
            sfReferenceProperty[] references = SceneFusion.Get().Service.Session.GetReferences(obj);
            sfPropertyManager.Get().SetReferences(component, references);

            // Call the component initializers
            Initializer handlers = m_componentInitializers.GetHandlers(component.GetType());
            if (handlers != null)
            {
                handlers(obj, component);
            }

            // Initialize children
            int index = 0;
            foreach (sfObject child in obj.Children)
            {
                if (child.Type == sfType.GameObject && translator != null)
                {
                    if (component != gameObject.transform)
                    {
                        ksLog.Warning(this, "Ignoring game object sfObject with non-transform parent.");
                    }
                    else
                    {
                        GameObject childGameObject = sfObjectMap.Get().Get<GameObject>(child);
                        if (childGameObject == null)
                        {
                            translator.InitializeGameObject(child, gameObject.scene);
                        }
                        else if (childGameObject.transform.parent != gameObject.transform)
                        {
                            sfComponentUtils.SetParent(childGameObject, gameObject);
                        }
                    }
                }
                else
                {
                    sfObjectEventDispatcher.Get().OnCreate(child, index);
                }
                index++;
            }
            if (component == gameObject.transform && translator != null)
            {
                // Destroy unsynced children
                for (int i = gameObject.transform.childCount - 1; i >= 0; i--)
                {
                    GameObject child = gameObject.transform.GetChild(i).gameObject;
                    if (!sfObjectMap.Get().Contains(child) && translator.IsSyncable(child))
                    {
                        translator.DestroyGameObject(child);
                    }
                }
                // Sync child order
                if (obj.Children.Count > 0)
                {
                    translator.ApplyHierarchyChanges(obj);
                }
            }
        }

        /// <summary>
        /// Gets a component by name that is not mapped to an sfObject, or creates one if none is found. You can get the
        /// name using sfComponentUtils.GetName. We check for an existing component instead of always adding one because
        /// sometimes adding one component will cause another component to be added, and if we then added the second
        /// component it would be added twice.
        /// </summary>
        /// <param name="gameObject">gameObject to get component from or add component to.</param>
        /// <param name="-">
        /// if true, will look for a prefab instance component. If false, will look
        /// for a non-prefab instance component.
        /// </param>
        /// <returns>name of component to get or add.</returns>
        public Component GetOrAddComponent(GameObject gameObject, string name, bool isPrefabInstanceComponent)
        {
            // If the source prefab has unsaved changes, save it to make any newly added components appear on the
            // prefab instances.
            sfPrefabSaver.Get().SavePrefabSourceIfDirty(gameObject);
            Component[] components = gameObject.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (sfComponentUtils.GetName(component) == name && component.GetType() != typeof(sfMissingComponent) &&
                    !sfObjectMap.Get().Contains(component) &&
                    isPrefabInstanceComponent == PrefabUtility.IsPartOfPrefabInstance(component))
                {
                    // If this isn't the last component, we need to reapply the server component order.
                    if (i != components.Length - 1)
                    {
                        m_componentOrderChangedSet.Add(gameObject);
                    }
                    return component;
                }
            }
            // Apply pending serialized property modifications to the game object before adding a component to prevent
            // the game object's component list being reverted and losing the new component when property modifications
            // are applied.
            sfPropertyManager.Get().ApplySerializedProperties(gameObject);
            sfPrefabSaver.Get().MarkPrefabDirty(gameObject);
            return sfComponentUtils.AddComponent(gameObject, name);
        }

        /// <summary>
        /// Checks if an sfObject has a sibling with the same prefab source file id, meaning it is a duplicate prefab
        /// component instance that shares the same prefab source component as another component sfObject. If a
        /// duplicate is detected, the one that was created second on the server is deleted.
        /// </summary>
        /// <param name="obj">obj to check for duplicate of.</param>
        /// <param name="fileId">fileId to check for duplicate of.</param>
        /// <returns>true if the obj was destroyed because an older duplicate was detected.</returns>
        private bool CheckDuplicateComponent(sfObject obj, long fileId)
        {
            sfBaseProperty prop;
            foreach (sfObject current in obj.Parent.Children)
            {
                if (current != obj &&
                    ((sfDictionaryProperty)current.Property).TryGetField(sfProp.PrefabSourceFileId, out prop) &&
                    (long)prop == fileId)
                {
                    return sfObjectUtils.DeleteDuplicate(current, obj);
                }
            }
            return false;
        }

        /// <summary>Finds the removed prefab component instance with the given type name and local file id.</summary>
        /// <param name="gameObject">gameObject to get removed prefab component instance from.</param>
        /// <param name="name">type name of the removed component to find.</param>
        /// <param name="fileId">local file id in the prefab asset of the removed component to find.</param>
        /// <returns>from the prefab instance, or null if none was found.</returns>
        private RemovedComponent FindRemovedComponent(GameObject gameObject, string name, long fileId)
        {
            List<RemovedComponent> removedComponents = PrefabUtility.GetRemovedComponents(gameObject);
            if (removedComponents.Count == 0)
            {
                return null;
            }
            foreach (RemovedComponent removed in removedComponents)
            {
                if (sfLoader.Get().GetLocalFileId(removed.assetComponent) == fileId)
                {
                    return sfComponentUtils.GetName(removed.assetComponent) == name ? removed : null;
                }
            }
            return null;
        }

        /// <summary>Restores a removed prefab component instance to a prefab game object instance.</summary>
        /// <param name="removedComponent">removedComponent to restore.</param>
        private Component RestoreRemovedComponent(RemovedComponent removedComponent)
        {
            if (removedComponent == null)
            {
                return null;
            }
            // Restore the component. This code is taken from the decompiled Unity code for RemovedComponent.Revert(),
            // except the InteractionMode is changed to prevent registering an operation on the undo stack.
            PrefabUtility.RevertRemovedComponent(removedComponent.containingInstanceGameObject,
                removedComponent.assetComponent, InteractionMode.AutomatedAction);
            // Not sure what this does but it's in Unity's code...
            Component coupledComponent = m_roGetCoupledComponent.InstanceInvoke(
                removedComponent.assetComponent) as Component;
            if (coupledComponent != null || coupledComponent.IsDestroyed())
            {
                PrefabUtility.RevertRemovedComponent(removedComponent.containingInstanceGameObject, coupledComponent,
                    InteractionMode.AutomatedAction);
            }

            // If the prefab instance is a prefab variant, we have to save it to make the restored component appear in
            // components list.
            if (PrefabUtility.IsPartOfPrefabAsset(removedComponent.containingInstanceGameObject))
            {
                PrefabUtility.SavePrefabAsset(removedComponent.containingInstanceGameObject.transform.root.gameObject);
            }

            // Find the instance component we restored
            foreach (Component component in removedComponent.containingInstanceGameObject.GetComponents<Component>())
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(component) == removedComponent.assetComponent)
                {
                    return component;
                }
            }
            return null;
        }

        /// <summary>Creates the notification for a missing component.</summary>
        /// <param name="missingComponent">missingComponent to create notification for.</param>
        private void CreateMissingComponentNotification(sfMissingComponent missingComponent)
        {
            sfNotification.Create(sfNotificationCategory.MissingComponent, "Unable to load component '" +
                sfComponentUtils.GetDisplayName(missingComponent.Name) + "'.", missingComponent);
        }

        /// <summary>Called when a component is deleted by another user.</summary>
        /// <param name="obj">obj that was deleted.</param>
        public override void OnDelete(sfObject obj)
        {
            Component component = sfObjectMap.Get().Remove(obj) as Component;
            if (component != null)
            {
                DeleteHandler handlers = m_deleteHandlers.GetHandlers(component.GetType());
                if (handlers != null)
                {
                    handlers(obj, component);
                }
                
                MarkLockObjectStale(component);
                sfUI.Get().MarkSceneViewStale();
                sfUI.Get().MarkInspectorStale(component);
                sfNotificationManager.Get().RemoveNotificationsFor(component);
                DestroyComponent(component);
            }
        }

        /// <summary>Called when a locally-deleted component is confirmed as deleted.</summary>
        /// <param name="obj">obj that was confirmed as deleted.</param>
        /// <param name="unsubscribed">
        /// true if the deletion occurred because we unsubscribed from the object's parent.
        /// </param>
        public override void OnConfirmDelete(sfObject obj, bool unsubscribed)
        {
            // Clear the properties and children, but keep the object around so it can be resused to preserve ids if
            // the component is recreated.
            obj.Property = new sfDictionaryProperty();
            while (obj.Children.Count > 0)
            {
                obj.RemoveChild(obj.Children[0]);
            }
        }

        /// <summary>Called when a component's parent or child index is changed by another user.</summary>
        /// <param name="obj">obj whose parent changed.</param>
        /// <param name="childIndex">childIndex of the object. -1 if the object is a root.</param>
        public override void OnParentChange(sfObject obj, int childIndex)
        {
            GameObject gameObject = sfObjectMap.Get().Get<GameObject>(obj.Parent);
            if (gameObject != null)
            {
                m_componentOrderChangedSet.Add(gameObject);
            }
        }

        /// <summary>
        /// Destroys a component. First destroys any components that depend on the component being destroyed.
        /// </summary>
        /// <param name="component">component to destroy.</param>
        public void DestroyComponent(Component component)
        {
            if (component is Transform)
            {
                // Transform components cannot be removed, however they can be replaced by adding a RectTransform,
                // and RectTransforms can be replaced by adding a Transform. The component will be destroyed when the
                // replacement Transform is added...
                return;
            }
            // Apply pending serialized property modifications before destroying the component to prevent the component
            // being readded and causing a component cannot be loaded warning when serialized properties are applied.
            sfPropertyManager.Get().ApplySerializedProperties(component.gameObject);
            RemoveDependentComponents(component);
            EditorUtility.SetDirty(component);
            sfPrefabSaver.Get().MarkPrefabDirty(component);
            UObject.DestroyImmediate(component, sfConfig.Get().SyncPrefabs == sfConfig.PrefabSyncMode.FULL);
        }

        /// <summary>
        /// Removes all components on a game object that depend on the given component recursively, so if A depends on B
        /// depends on C and we call this with A, first C is removed and then B.
        /// </summary>
        /// <param name="component">component to remove dependent components for.</param>
        /// <param name="previousComponents">
        /// previousComponents already being removed. Contains one component for each level of
        /// recursion. Used to detect circular dependencies.
        /// </param>
        private void RemoveDependentComponents(Component component, Stack<Component> previousComponents = null)
        {
            List<Component> dependents = new List<Component>();
            Type type = component.GetType();
            foreach (Component comp in component.GetComponents<Component>())
            {
                if (comp == component || comp == null)
                {
                    continue;
                }
                Type currentType = comp.GetType();
                if (currentType == type)
                {
                    // There's another component of the same type so we can delete this one without breaking any
                    // dependencies
                    return;
                }
                Type requiredType;
                if (sfDependencyTracker.Get().DependsOn(currentType, type, out requiredType) && 
                    (requiredType == type ||
                    // make sure there isn't another component that shares the same required base class
                    component.GetComponents(requiredType).Length <= 1))
                {
                    dependents.Add(comp);
                }
            }
            if (dependents.Count <= 0)
            {
                return;
            }
            if (previousComponents == null)
            {
                previousComponents = new Stack<Component>();
            }
            previousComponents.Push(component);
            foreach (Component comp in dependents)
            {
                if (previousComponents.Contains(comp))
                {
                    ksLog.Error(this, "Detected circular dependency while trying to remove components.");
                }
                else
                {
                    RemoveDependentComponents(comp, previousComponents);
                    EditorUtility.SetDirty(comp);
                    UObject.DestroyImmediate(comp);
                }
            }
            previousComponents.Pop();
        }

        /// <summary>Called when a field is removed from a dictionary property.</summary>
        /// <param name="dict">dict the field was removed from.</param>
        /// <param name="name">name of the removed field.</param>
        public override void OnRemoveField(sfDictionaryProperty dict, string name)
        {
            base.OnRemoveField(dict, name);
            sfObject obj = dict.GetContainerObject().Parent;
            if (!obj.IsLocked)
            {
                return;
            }
            // Gameobjects become unlocked when you set a prefab property to the default value, so we relock it.
            GameObject gameObject = sfObjectMap.Get().Get<GameObject>(obj);
            if (gameObject != null && PrefabUtility.GetPrefabInstanceHandle(gameObject) != null)
            {
                sfUnityUtils.AddFlags(gameObject, HideFlags.NotEditable);
            }
        }

        /// <summary>Redraws the scene view.</summary>
        /// <param name="uobj.">uobj. Does nothing.</param>
        private void RedrawSceneView(UObject uobj)
        {
            sfUI.Get().MarkSceneViewStale();
        }

        /// <summary>Marks the given UObject's lock object stale.</summary>
        /// <param name="uobj"></param>
        /// <param name="property">
        /// unused. This parameter is here so the function can be used as a post
        /// property change handler.
        /// </param>
        private void MarkLockObjectStale(UObject uobj, sfBaseProperty property = null)
        {
            sfLockManager.Get().MarkLockObjectStale(((Component)uobj).gameObject);
        }

        /// <summary>Marks the given LOD group's lock LODs stale.</summary>
        /// <param name="lodGroup"></param>
        private void MarkLockLODStale(UObject lodGroup)
        {
            sfLockManager.Get().MarkLockLODStale(((LODGroup)lodGroup).gameObject);
        }

        /// <summary>Checks if a component is black listed.</summary>
        /// <param name="component">component to check.</param>
        /// <returns>true if the component is black listed.</returns>
        private bool IsBlacklisted(Component component)
        {
            if (m_blacklist.Count == 0)
            {
                return false;
            }
            foreach (Type type in m_blacklist)
            {
                if (type.IsAssignableFrom(component.GetType()))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
