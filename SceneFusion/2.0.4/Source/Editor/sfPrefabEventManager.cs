using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using Unity.Collections;
using KS.SF.Reactor;
using KS.SF.Unity.Editor;
using KS.SceneFusion;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>
    /// Builds an <see cref="ObjectChangeEventStream"/> of and an array of <see cref="UndoPropertyModification"/> for
    /// prefabs by listening for change events for prefab stage objects and creating an equivalent event for the prefab
    /// object. Invokes the prefab events when the prefab stage is saved. Invokes 
    /// <see cref="sfUnityEventDispatcher.OnHierarchyStructureChange"/> when prefabs are imported and we do not know
    /// how they were modified, which happens when prefabs files are modified externally or when undoing a prefab stage
    /// edit after the prefab stage is closed.
    /// </summary>
    public class sfPrefabEventManager
    {
        /// <summary></summary>
        /// <returns>Singleton instance</returns>
        public static sfPrefabEventManager Get()
        {
            return m_instance;
        }
        private static sfPrefabEventManager m_instance = new sfPrefabEventManager();

        /// <summary>The number of stored events.</summary>
        public int EventCount
        {
            get { return m_eventBuilder.eventCount; }
        }

        /// <summary>The number of stored property modifications.</summary>
        public int PropertyCount
        {
            get { return m_propertyModifications == null ? 0 : m_propertyModifications.Length; }
        }

        /// <summary>
        /// Does the <see cref="sfPrefabStageMap"/> need to be rebuilt after stored prefab events are invoked? True if
        /// there are stored events which may have created new stage objects.
        /// </summary>
        public bool StageMapDirty
        {
            get { return m_stageMapDirty; }
        }

        private ObjectChangeEventStream.Builder m_eventBuilder =
            new ObjectChangeEventStream.Builder(Allocator.Persistent);
        private UndoPropertyModification[] m_propertyModifications;

        // Does the sfPrefabStageMap need to be rebuilt after prefab events are invoked?
        private bool m_stageMapDirty = false;

        private HashSet<string> m_importedPrefabs = new HashSet<string>();
        private HashSet<string> m_modifiedPrefabs = new HashSet<string>();

        private sfUnityEventDispatcher m_dispatcher;
        private sfPrefabStageMap m_prefabStageMap;

        private sfPrefabEventManager()
        {
            m_dispatcher = sfUnityEventDispatcher.Get();
            m_prefabStageMap = sfPrefabStageMap.Get();
        }

        internal sfPrefabEventManager(sfUnityEventDispatcher dispatcher, sfPrefabStageMap prefabStageMap)
        {
            m_dispatcher = dispatcher;
            m_prefabStageMap = prefabStageMap;
        }

        /// <summary>Deconstructor</summary>
        ~sfPrefabEventManager()
        {
            m_eventBuilder.Dispose();
        }

        /// <summary>Registers event listeners.</summary>
        public void Start()
        {
            m_dispatcher.OnCreate += TryAddCreateEvent;
            m_dispatcher.OnDelete += TryAddDeleteEvent;
            m_dispatcher.OnPropertiesChanged += TryAddPropertiesChangeEvent;
            m_dispatcher.OnParentChange += TryAddParentChangeEvent;
            m_dispatcher.OnReorderChildren += TryAddChildrenOrderChangeEvent;
            m_dispatcher.OnAddOrRemoveComponents += TryAddStructureChangeEvent;
            m_dispatcher.OnHierarchyStructureChange += TryAddHierarchyStructureChangeEvent;
            m_dispatcher.OnUpdatePrefabInstances += InvokeEventsForImportedPrefabs;
            m_dispatcher.PostModifyProperties += TryAddPropertyModifications;
            m_dispatcher.OnModifyObject += TryAddModifiedPrefab;
            m_dispatcher.OnOpenPrefabStage += ClearEvents;
            m_dispatcher.OnClosePrefabStage += ClearEvents;
            m_dispatcher.OnSavePrefabStage += InvokePrefabEvents;
            ksEditorEvents.OnImportAssets += TryAddImportedPrefabs;
        }

        /// <summary>Unregisters event listeners and clears state.</summary>
        public void Stop()
        {
            m_dispatcher.OnCreate -= TryAddCreateEvent;
            m_dispatcher.OnDelete -= TryAddDeleteEvent;
            m_dispatcher.OnPropertiesChanged -= TryAddPropertiesChangeEvent;
            m_dispatcher.OnParentChange -= TryAddParentChangeEvent;
            m_dispatcher.OnReorderChildren -= TryAddChildrenOrderChangeEvent;
            m_dispatcher.OnAddOrRemoveComponents -= TryAddStructureChangeEvent;
            m_dispatcher.OnHierarchyStructureChange -= TryAddHierarchyStructureChangeEvent;
            m_dispatcher.OnUpdatePrefabInstances -= InvokeEventsForImportedPrefabs;
            m_dispatcher.PostModifyProperties -= TryAddPropertyModifications;
            m_dispatcher.OnModifyObject -= TryAddModifiedPrefab;
            m_dispatcher.OnOpenPrefabStage -= ClearEvents;
            m_dispatcher.OnClosePrefabStage -= ClearEvents;
            m_dispatcher.OnSavePrefabStage -= InvokePrefabEvents;
            m_dispatcher.PreUpdate -= InvokeEventsForImportedPrefabs;
            ksEditorEvents.OnImportAssets -= TryAddImportedPrefabs;
            ClearEvents();
            m_modifiedPrefabs.Clear();
            m_importedPrefabs.Clear();
        }

        internal ObjectChangeEventStream GetEvents()
        {
            // Temp allocator means the object is disposed at the end of the frame.
            return m_eventBuilder.ToStream(Allocator.Temp);
        }

        /// <summary>
        /// Adds a create event if the <paramref name="gameObject"/>'s parent is a stage object in the
        /// <see cref="sfPrefabStageMap"/>, with the id changed to the id of the prefab object.
        /// </summary>
        /// <param name="gameObject">Created object</param>
        private void TryAddCreateEvent(GameObject gameObject)
        {
            if (gameObject.transform.parent == null)
            {
                return;
            }
            // If the game object is a new stage object, its prefab object won't exist yet, so instead we get the
            // prefab object for the new object's parent. The prefab event invoked will be a create prefab child event
            // which tells us one or more child objects were created but doesn't tell us which ones.
            GameObject prefab = m_prefabStageMap.GetPrefabObject(gameObject.transform.parent.gameObject);
            if (prefab == null)
            {
                return;
            }
            CreateGameObjectHierarchyEventArgs data = 
                new CreateGameObjectHierarchyEventArgs(prefab.GetInstanceID(), prefab.scene);
            m_eventBuilder.PushCreateGameObjectHierarchyEvent(ref data);
            m_stageMapDirty = true;
        }

        /// <summary>
        /// Adds a delete event if the <paramref name="instanceId"/> is for a stage object in the
        /// <see cref="sfPrefabStageMap"/>, with the id changed to the id of the prefab object.
        /// </summary>
        /// <param name="instanceId">Instance id of deleted object</param>
        private void TryAddDeleteEvent(int instanceId)
        {
            GameObject prefab = m_prefabStageMap.GetPrefabObject<GameObject>(instanceId);
            if (prefab == null)
            {
                return;
            }
            DestroyGameObjectHierarchyEventArgs data =
                new DestroyGameObjectHierarchyEventArgs(prefab.GetInstanceID(), prefab.scene);
            m_eventBuilder.PushDestroyGameObjectHierarchyEvent(ref data);
        }

        /// <summary>
        /// Adds a properties change event if the <paramref name="uobj"/> is a stage object in the
        /// <see cref="sfPrefabStageMap"/>, with the id changed to the id of the prefab object.
        /// </summary>
        /// <param name="UObject">Object with changed properties</param>
        private void TryAddPropertiesChangeEvent(UObject uobj)
        {
            UObject prefab = m_prefabStageMap.GetPrefabObject(uobj);
            if (prefab == null)
            {
                return;
            }
            ChangeGameObjectOrComponentPropertiesEventArgs data = 
                new ChangeGameObjectOrComponentPropertiesEventArgs(prefab.GetInstanceID(), new Scene());
            m_eventBuilder.PushChangeGameObjectOrComponentPropertiesEvent(ref data);
        }

        /// <summary>
        /// Adds a parent change event if the <paramref name="gameObject"/> is a stage object in the
        /// <see cref="sfPrefabStageMap"/>, with the id changed to the id of the prefab object.
        /// </summary>
        /// <param name="gameObject">Game object whose parent changed.</param>
        private void TryAddParentChangeEvent(GameObject gameObject)
        {
            GameObject prefab = m_prefabStageMap.GetPrefabObject(gameObject);
            if (prefab == null)
            {
                return;
            }
            // We don't care about the previous and new parent ids so we pass 0 for both of them.
            ChangeGameObjectParentEventArgs data =
                new ChangeGameObjectParentEventArgs(prefab.GetInstanceID(), prefab.scene, 0, prefab.scene, 0);
            m_eventBuilder.PushChangeGameObjectParentEvent(ref data);
        }

        /// <summary>
        /// Adds a child order change event if the <paramref name="gameObject"/> is a stage object in the
        /// <see cref="sfPrefabStageMap"/>, with the id changed to the id of the prefab object.
        /// </summary>
        /// <param name="gameObject">Game object whose children order changed.</param>
        private void TryAddChildrenOrderChangeEvent(GameObject gameObject)
        {
            GameObject prefab = m_prefabStageMap.GetPrefabObject(gameObject);
            if (prefab == null)
            {
                return;
            }
            // Unity's API is missing a function to push a change children order event, so instead we use a parent
            // change event with 0 for the child id and use the prefab's id as the new and old parent id to represent
            // a child order change event.
            ChangeGameObjectParentEventArgs data = new ChangeGameObjectParentEventArgs(0, prefab.scene, 
                prefab.GetInstanceID(), prefab.scene, prefab.GetInstanceID());
            m_eventBuilder.PushChangeGameObjectParentEvent(ref data);
        }

        /// <summary>
        /// Adds a structure change event if the <paramref name="gameObject"/> is a stage object in the
        /// <see cref="sfPrefabStageMap"/>, with the id changed to the id of the prefab object. A structure change
        /// event means components were added or removed.
        /// </summary>
        /// <param name="gameObject">Game object with added or removed components.</param>
        private void TryAddStructureChangeEvent(GameObject gameObject)
        {
            GameObject prefab = m_prefabStageMap.GetPrefabObject(gameObject);
            if (prefab == null)
            {
                return;
            }
            ChangeGameObjectStructureEventArgs data =
                new ChangeGameObjectStructureEventArgs(prefab.GetInstanceID(), prefab.scene);
            m_eventBuilder.PushChangeGameObjectStructureEvent(ref data);
            m_stageMapDirty = true;
        }

        /// <summary>
        /// Adds a hierarchy structure change event if the <paramref name="gameObject"/> is a stage object in the
        /// <see cref="sfPrefabStageMap"/>, with the id changed to the id of the prefab object. A hierarchy structure
        /// change event means anything may have changed on a game object and/or any of its descendants.
        /// </summary>
        /// <param name="gameObject">Game object with changes, including possibly descendant changes.</param>
        private void TryAddHierarchyStructureChangeEvent(GameObject gameObject)
        {
            GameObject prefab = m_prefabStageMap.GetPrefabObject(gameObject);
            if (prefab == null)
            {
                return;
            }
            if (prefab.transform.parent != null)
            {
                // Making a prefab child into a nested prefab will destroy the child and create a new one with a
                // different instance id, so we write the id of the parent into the event instead since that will not
                // change and sync the entire hierarchy under the parent. 
                prefab = prefab.transform.parent.gameObject;
            }
            ChangeGameObjectStructureHierarchyEventArgs data =
                new ChangeGameObjectStructureHierarchyEventArgs(prefab.GetInstanceID(), prefab.scene);
            m_eventBuilder.PushChangeGameObjectStructureHierarchyEvent(ref data);
            m_stageMapDirty = true;
        }

        /// <summary>
        /// Adds property modifications for stage objects in the <see cref="sfPrefabStageMap"/>, with the target
        /// changed to the prefab object. If a modification already exists for a property/object, it will be replaced
        /// with the new modification.
        /// </summary>
        /// <param name="modifications">Modifications</param>
        private void TryAddPropertyModifications(UndoPropertyModification[] modifications)
        {
            if (modifications == null || modifications.Length == 0)
            {
                return;
            }
            // Build a list of prefab modifications.
            List<UndoPropertyModification> prefabModifications = new List<UndoPropertyModification>();
            for (int i = 0; i < modifications.Length; i++)
            {
                UndoPropertyModification modification = modifications[i];
                if (modification.currentValue.target == null)
                {
                    continue;
                }
                UObject prefab = m_prefabStageMap.GetPrefabObject(modification.currentValue.target);
                if (prefab != null)
                {
                    // Create a new modification with the target changed to the prefab object.
                    PropertyModification prefabValue = new PropertyModification();
                    prefabValue.target = prefab;
                    prefabValue.propertyPath = modification.currentValue.propertyPath;
                    modification.currentValue = prefabValue;
                    prefabModifications.Add(modification);
                }
                else
                {
                    // If the target is a prefab, add it to the modified prefab set.
                    TryAddModifiedPrefab(modification.currentValue.target.GetInstanceID());
                }
            }
            if (prefabModifications.Count == 0)
            {
                return;
            }
            m_propertyModifications = sfUndoManager.Get().AddModifications(m_propertyModifications,
                prefabModifications.ToArray());
        }

        /// <summary>
        /// Called when assets are imported. Adds imported prefabs to the imported prefab list, and invokes hierarchy
        /// structure change events for these prefabs in the next pre update or when prefab instances are updated if we
        /// haven't detected what changed by then. Update prefab instance events are invoked instead of hierarchy
        /// structure change events for prefab variants if one of their source prefabs was also imported, even if we
        /// detected changes to the prefab variant.
        /// </summary>
        /// <param name="paths">Imported asset paths</param>
        internal void TryAddImportedPrefabs(string[] paths)
        {
            foreach (string path in paths)
            {
                if (path.EndsWith(".prefab"))
                {
                    if (m_importedPrefabs.Count == 0)
                    {
                        // Normally we invoke the events when prefab instances are updated, but if there are no prefab
                        // instances in the scene, on update prefab instances won't fire so we do it in pre update.
                        m_dispatcher.PreUpdate += InvokeEventsForImportedPrefabs;
                    }
                    m_importedPrefabs.Add(path);

                    // If the dispatcher is disabled, SF modified this prefab. We still need to track is so we can fire
                    // update prefab instance events for updated prefab variants.
                    if (!m_dispatcher.Enabled)
                    {
                        m_modifiedPrefabs.Add(path);
                    }
                }
            }
        }

        /// <summary>
        /// If the <paramref name="instanceId"/> is for a prefab, adds the prefab to the set of prefabs with known 
        /// modifications. These prefabs will not have hierarchy structure change events fired when they are imported.
        /// </summary>
        /// <param name="instanceId">Id of modified object.</param>
        private void TryAddModifiedPrefab(int instanceId)
        {
            string path = AssetDatabase.GetAssetPath(instanceId);
            if (path != null && path.EndsWith(".prefab"))
            {
                m_modifiedPrefabs.Add(path);
            }
        }

        /// <summary>Clears the events and property modifications.</summary>
        private void ClearEvents()
        {
            m_eventBuilder.Dispose();
            m_eventBuilder = new ObjectChangeEventStream.Builder(Allocator.Persistent);
            m_propertyModifications = null;
            m_stageMapDirty = false;
        }

        /// <summary>Clears the events and property modifications.</summary>
        /// <param name="stage">Unused</param>
        private void ClearEvents(PrefabStage stage)
        {
            ClearEvents();
        }

        /// <summary>Invokes and clears stored prefab events.</summary>
        /// <param name="gameObject">Unused</param>
        private void InvokePrefabEvents(GameObject gameObject)
        {
            if (m_eventBuilder.eventCount == 0 && m_propertyModifications == null)
            {
                return;
            }
            try
            {
                if (m_propertyModifications != null)
                {
                    m_dispatcher.InvokeOnModifyProperties(m_propertyModifications);
                    m_dispatcher.InvokePostModifyProperties(m_propertyModifications);
                }
                ObjectChangeEventStream stream = GetEvents();
                m_dispatcher.InvokeChangeEvents(ref stream, true);
                if (m_stageMapDirty)
                {
                    m_prefabStageMap.Rebuild();
                }
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Error invoking prefab events.", e);
            }
            ClearEvents();
        }

        /// <summary>
        /// Invokes event for prefabs in the imported prefab list. If the prefab is a variant of another imported
        /// prefab, invokes <see cref="sfUnityEventDispatcher.OnUpdatePrefabInstance"/>. Otherwise if the prefab is not
        /// in the modified set, invokes <see cref="sfUnityEventDispatcher.OnHierarchyStructureChange"/>. Events for
        /// source prefabs are invoked before events for variants of those prefabs. Clears the imported prefabs list
        /// and modified prefabs set.
        /// </summary>
        /// <param name="deltaTime">Unused</param>
        private void InvokeEventsForImportedPrefabs(float deltaTime)
        {
            InvokeEventsForImportedPrefabs();
        }

        /// <summary>
        /// Invokes event for prefabs in the imported prefab list. If the prefab is a variant of another imported
        /// prefab, invokes <see cref="sfUnityEventDispatcher.OnUpdatePrefabInstance"/>. Otherwise if the prefab is not
        /// in the modified set, invokes <see cref="sfUnityEventDispatcher.OnHierarchyStructureChange"/>. Events for
        /// source prefabs are invoked before events for variants of those prefabs. Clears the imported prefabs list
        /// and modified prefabs set.
        /// </summary>
        private void InvokeEventsForImportedPrefabs()
        {
            m_dispatcher.PreUpdate -= InvokeEventsForImportedPrefabs;
            HashSet<GameObject> invokedSet = new HashSet<GameObject>();
            foreach (string path in m_importedPrefabs)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    InvokeEventsForPrefabAndSources(prefab, path, invokedSet);
                }
            }
            m_importedPrefabs.Clear();
            m_modifiedPrefabs.Clear();
        }


        /// <summary>
        /// Invokes update prefab instance and/or hierarchy structure change events for a prefab and its imported
        /// source prefabs that aren't in the <paramref name="invokedSet"/>, and adds them to the
        /// <paramref name="invokedSet"/>. Events for source prefabs are invoked first, so if this is called on prefab
        /// C which is a variant of B which is a variant of A, events will be invoked first for A, then B, then C. If B
        /// is in the invoked set, then only the event for C will be invoked. See 
        /// <see cref="InvokeEventsForImportedPrefabs()"/> for rules about which events are invoked.
        /// </summary>
        /// <param name="prefab">Prefab to invoke events for.</param>
        /// <param name="path">Path to the prefab.</param>
        /// <param name="invokedSet">Set of prefabs that already had events invoked.</param>
        private void InvokeEventsForPrefabAndSources(GameObject prefab, string path, HashSet<GameObject> invokedSet)
        {
            if (!invokedSet.Add(prefab))
            {
                return;
            }
            // Iterate the source prefabs to see if they were imported and invoke events for imported source prefabs
            // first.
            bool sourceImported = false;
            sfUnityUtils.ForSelfAndDescendants(prefab, (GameObject gameObj) =>
            {
                GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(gameObj);
                if (source != null && source.transform.parent == null)
                {
                    string sourcePath = AssetDatabase.GetAssetPath(source);
                    if (m_importedPrefabs.Contains(sourcePath))
                    {
                        sourceImported = true;
                        InvokeEventsForPrefabAndSources(source, sourcePath, invokedSet);
                        m_dispatcher.InvokeOnUpdatePrefabInstance(gameObj);
                    }
                }
                return true;
            });

            if (!sourceImported && !m_modifiedPrefabs.Contains(path))
            {
                m_dispatcher.InvokeOnHierarchyStructureChange(prefab);
            }
        }
    }
}