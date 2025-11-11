using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using KS.SceneFusion;
using KS.SceneFusion2.Client;
using KS.SF.Reactor;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>
    /// The object event dispatcher listens for object events and calls the corresponding functions on the translator
    /// registered for the object's type.
    /// </summary>
    public partial class sfObjectEventDispatcher
    {
        /// <summary></summary>
        /// <returns>singleton instance.</returns>
        public static sfObjectEventDispatcher Get()
        {
            return m_instance;
        }
        private static sfObjectEventDispatcher m_instance = new sfObjectEventDispatcher();

        /// <summary>Initialize handler</summary>
        public delegate void InitializeHandler();

        /// <summary>
        /// Invoked after translators are initialized. Once this is fired, it is safe to access translators using
        /// sfObjectEventDispatcher.Get().GetTranslator calls.
        /// </summary>
        public event InitializeHandler OnInitialize;

        /// <summary>Is the object event dispatcher running?</summary>
        public bool IsActive
        {
            get { return m_active; }
        }

        /// <summary>Gets the config.</summary>
        private sfConfig Config
        {
            get { return m_config == null ? sfConfig.Get() : m_config; }
        }

        private sfConfig m_config;
        private Dictionary<string, sfBaseTranslator> m_translatorMap = new Dictionary<string, sfBaseTranslator>();
        private List<sfBaseTranslator> m_translators = new List<sfBaseTranslator>();
        private bool m_active = false;
        private sfObjectMap m_objectMap;

        /// <summary>Constructor</summary>
        private sfObjectEventDispatcher()
        {
            m_objectMap = sfObjectMap.Get();
        }

        /// <summary>Registers a translator to handle events for a given object type.</summary>
        /// <param name="objectType">objectType the translator should handle events for.</param>
        /// <param name="translator">translator to register.</param>
        public void Register(string objectType, sfBaseTranslator translator)
        {
            if (m_translatorMap.ContainsKey(objectType))
            {
                ksLog.Error(this, "Cannot register translator for '" + objectType +
                    "' because another translator is already registered for that type");
                return;
            }
            m_translatorMap[objectType] = translator;
            if (!m_translators.Contains(translator))
            {
                m_translators.Add(translator);
            }
        }

        /// <summary>
        /// Calls Initialize on all translators. Invokes OnInitialize and removes all OnInitialize handlers.
        /// </summary>
        public void InitializeTranslators()
        {
            foreach (sfBaseTranslator translator in m_translators)
            {
                translator.Initialize();
            }
            if (OnInitialize != null)
            {
                OnInitialize();
                OnInitialize = null;
            }
        }

        /// <summary>Starts listening for events and calls OnSessionConnect on all registered translators.</summary>
        /// <param name="session">session to listen to events on.</param>
        public void Start(sfSession session)
        {
            if (m_active)
            {
                return;
            }
            m_active = true;
            if (session != null)
            {
                session.OnCreate += OnCreate;
                session.OnConfirmCreate += OnConfirmCreate;
                session.OnDelete += OnDelete;
                session.OnConfirmDelete += OnConfirmDelete;
                session.OnLock += OnLock;
                session.OnUnlock += OnUnlock;
                session.OnLockOwnerChange += OnLockOwnerChange;
                session.OnDirectLockChange += OnDirectLockChange;
                session.OnParentChange += OnParentChange;
                session.OnPropertyChange += OnPropertyChange;
                session.OnRemoveField += OnRemoveField;
                session.OnListAdd += OnListAdd;
                session.OnListRemove += OnListRemove;
            }
            sfSelectionWatcher.Get().OnSelect += OnSelect;
            sfSelectionWatcher.Get().OnDeselect += OnDeselect;
            foreach (sfBaseTranslator translator in m_translators)
            {
                try
                {
                    translator.OnSessionConnect();
                }
                catch (Exception e)
                {
                    ksLog.Error(this, "Error calling " + translator.GetType().Name + ".OnSessionConnect.", e);
                }
            }
        }

        /// <summary>Stops listening for events and calls OnSessionDisconnect on all registered translators.</summary>
        /// <param name="session">session to stop listening to events on.</param>
        public void Stop(sfSession session)
        {
            if (!m_active)
            {
                return;
            }
            m_active = false;
            if (session != null)
            {
                session.OnCreate -= OnCreate;
                session.OnConfirmCreate -= OnConfirmCreate;
                session.OnDelete -= OnDelete;
                session.OnConfirmDelete -= OnConfirmDelete;
                session.OnLock -= OnLock;
                session.OnUnlock -= OnUnlock;
                session.OnLockOwnerChange -= OnLockOwnerChange;
                session.OnDirectLockChange -= OnDirectLockChange;
                session.OnParentChange -= OnParentChange;
                session.OnPropertyChange -= OnPropertyChange;
                session.OnRemoveField -= OnRemoveField;
                session.OnListAdd -= OnListAdd;
                session.OnListRemove -= OnListRemove;
            }
            sfSelectionWatcher.Get().OnSelect -= OnSelect;
            sfSelectionWatcher.Get().OnDeselect -= OnDeselect;
            foreach (sfBaseTranslator translator in m_translators)
            {
                try
                {
                    translator.OnSessionDisconnect();
                }
                catch (Exception e)
                {
                    ksLog.Error(this, "Error calling " + translator.GetType().Name + ".OnSessionDisconnect.", e);
                }
            }
        }

        /// <summary>
        /// Creates an sfObject for a uobject if the uobject is not already synced and is a syncable asset type or not
        /// an asset by calling TryCreate on each translator until one of them handles the request.
        /// </summary>
        /// <param name="uobj">uobj to create sfObject for.</param>
        /// <returns>true if an sfObject was creaed for the uobj.</returns>
        public bool TryCreateSFObject(UObject uobj)
        {
            sfObject obj;
            return TryCreateSFObject(uobj, out obj);
        }

        /// <summary>
        /// Creates an sfObject for a uobject if the uobject is not already synced and is a syncable asset type or not
        /// an asset by calling TryCreate on each translator until one of them handles the request.
        /// </summary>
        /// <param name="uobj">uobj to create sfObject for.</param>
        /// <param name="obj">obj for the uobject. May be null.</param>
        /// <returns>true if an sfObject was creaed for the uobj.</returns>
        public bool TryCreateSFObject(UObject uobj, out sfObject obj)
        {
            obj = null;
            if (uobj == null)
            {
                return false;
            }
            sfObject current = m_objectMap.GetSFObject(uobj);
            if ((current != null && current.IsSyncing) ||
                (sfLoader.Get().IsAsset(uobj) && !sfLoader.Get().IsSyncableAssetType(uobj) &&
                (sfConfig.Get().SyncPrefabs != sfConfig.PrefabSyncMode.FULL ||
                !PrefabUtility.IsPartOfPrefabAsset(uobj))))
            {
                return false;
            }
            obj = Create(uobj);
            return obj != null;
        }

        /// <summary>
        /// Creates an sfObject for a uobject by calling TryCreate on each translator until one of them handles the request.
        /// </summary>
        /// <param name="uobj">uobj to create sfObject for.</param>
        /// <returns>for the uobject. May be null.</returns>
        public sfObject Create(UObject uobj)
        {
            if (uobj == null)
            {
                return null;
            }
            sfObject obj = null;
            foreach (sfBaseTranslator translator in m_translators)
            {
                if (translator.TryCreate(uobj, out obj))
                {
                    break;
                }
            }
            return obj;
        }

        /// <summary>Gets the translator for an object.</summary>
        /// <param name="obj">obj to get translator for.</param>
        /// <returns>
        /// translator for the object, or null if there is no translator for the object's
        /// type.
        /// </returns>
        public sfBaseTranslator GetTranslator(sfObject obj)
        {
            return obj == null ? null : GetTranslator(obj.Type);
        }

        /// <summary>Gets the translator for the given type.</summary>
        /// <param name="type"></param>
        /// <returns>translator for the type, or null if there is no translator for the given type.</returns>
        public sfBaseTranslator GetTranslator(string type)
        {
            sfBaseTranslator translator;
            if (!m_translatorMap.TryGetValue(type, out translator))
            {
                ksLog.Error(this, "Unknown object type '" + type + "'.");
            }
            return translator;
        }

        /// <summary>Gets the translator for an object.</summary>
        /// <param name="obj">obj to get translator for.</param>
        /// <returns>translator for the object, or null if there is no translator for the object's type.</returns>
        public T GetTranslator<T>(sfObject obj) where T : sfBaseTranslator
        {
            return GetTranslator(obj) as T;
        }

        /// <summary>Gets the translator for the given type.</summary>
        /// <param name="type"></param>
        /// <returns>translator for the type, or null if there is no translator for the given type.</returns>
        public T GetTranslator<T>(string type) where T : sfBaseTranslator
        {
            return GetTranslator(type) as T;
        }

        /// <summary>Calls GetUObject on the translator for an sfObject.</summary>
        /// <param name="obj">obj to get UObject for.</param>
        /// <param name="current">
        /// current value of the serialized property we are getting the UObject reference for.
        /// </param>
        /// <returns>for the sfObject.</returns>
        public UObject GetUObject(sfObject obj, UObject current = null)
        {
            sfBaseTranslator translator = GetTranslator(obj);
            return translator == null ? null : translator.GetUObject(obj, current);
        }

        /// <summary>Calls OnCreate on the translator for an object.</summary>
        /// <param name="obj">obj that was created.</param>
        /// <param name="childIndex">childIndex the object was created at.</param>
        public void OnCreate(sfObject obj, int childIndex)
        {
            if (Config.Logging.HasFlag(sfConfig.EventLoggingFlags.CREATE))
            {
                ksLog.Debug(this, "Create " + obj + ", child index: " + childIndex);
            }
            sfBaseTranslator translator = GetTranslator(obj);
            if (translator != null)
            {
                translator.OnCreate(obj, childIndex);
            }
        }

        /// <summary>Calls OnPropertyChange on the translator for an object.</summary>
        /// <param name="property">property that changed.</param>
        public void OnPropertyChange(sfBaseProperty property)
        {
            if (Config.Logging.HasFlag(sfConfig.EventLoggingFlags.PROPERTY_CHANGE))
            {
                ksLog.Debug(this, "Set " + property.GetPath() + " = " + property.Print() + " on " +
                    property.GetContainerObject());
            }
            sfBaseTranslator translator = GetTranslator(property.GetContainerObject());
            if (translator != null)
            {
                translator.OnPropertyChange(property);
            }
        }

        /// <summary>Calls OnConfirmCreate on the translator for an object.</summary>
        /// <param name="obj">obj that whose creation was confirmed.</param>
        public void OnConfirmCreate(sfObject obj)
        {
            if (Config.Logging.HasFlag(sfConfig.EventLoggingFlags.CONFIRM_CREATE))
            {
                ksLog.Debug(this, "Confirm create " + obj);
            }
            sfBaseTranslator translator = GetTranslator(obj);
            if (translator != null)
            {
                translator.OnConfirmCreate(obj);
            }
        }

        /// <summary>Calls OnDelete on the translator for an object.</summary>
        /// <param name="obj">obj that was deleted.</param>
        public void OnDelete(sfObject obj)
        {
            if (Config.Logging.HasFlag(sfConfig.EventLoggingFlags.DELETE))
            {
                ksLog.Debug(this, "Delete " + obj);
            }
            sfBaseTranslator translator = GetTranslator(obj);
            if (translator != null)
            {
                translator.OnDelete(obj);
            }
        }

        /// <summary>Calls OnConfirmDelete on the translator for an object.</summary>
        /// <param name="obj">obj that whose deletion was confirmed.</param>
        /// <param name="unsubscribed">
        /// true if the deletion occurred because we unsubscribed from the object's parent.
        /// </param>
        public void OnConfirmDelete(sfObject obj, bool unsubscribed)
        {
            if (Config.Logging.HasFlag(sfConfig.EventLoggingFlags.CONFIRM_DELETE))
            {
                ksLog.Debug(this, "Confirm delete " + obj + ", unsubscribed: " + unsubscribed);
            }
            sfBaseTranslator translator = GetTranslator(obj);
            if (translator != null)
            {
                translator.OnConfirmDelete(obj, unsubscribed);
            }
        }

        /// <summary>Calls OnLock on the translator for an object.</summary>
        /// <param name="obj">obj that was locked.</param>
        public void OnLock(sfObject obj)
        {
            if (Config.Logging.HasFlag(sfConfig.EventLoggingFlags.LOCK))
            {
                ksLog.Debug(this, "Lock " + obj);
            }
            sfBaseTranslator translator = GetTranslator(obj);
            if (translator != null)
            {
                translator.OnLock(obj);
            }
        }

        /// <summary>Calls OnUnlock on the translator for an object.</summary>
        /// <param name="obj">obj that was unlocked.</param>
        public void OnUnlock(sfObject obj)
        {
            if (Config.Logging.HasFlag(sfConfig.EventLoggingFlags.UNLOCK))
            {
                ksLog.Debug(this, "Unlock " + obj);
            }
            sfBaseTranslator translator = GetTranslator(obj);
            if (translator != null)
            {
                translator.OnUnlock(obj);
            }
        }

        /// <summary>Calls OnLockOwnerChange on the translator for an object.</summary>
        /// <param name="obj">obj whose lock owner changed.</param>
        public void OnLockOwnerChange(sfObject obj)
        {
            if (Config.Logging.HasFlag(sfConfig.EventLoggingFlags.LOCK_OWNER_CHANGE))
            {
                ksLog.Debug(this, "Lock owner change " + obj);
            }
            sfBaseTranslator translator = GetTranslator(obj);
            if (translator != null)
            {
                translator.OnLockOwnerChange(obj);
            }
        }

        /// <summary>Calls OnDirectLockChange on the translator for an object.</summary>
        /// <param name="obj">obj whose direct lock state changed.</param>
        public void OnDirectLockChange(sfObject obj)
        {
            if (Config.Logging.HasFlag(sfConfig.EventLoggingFlags.DIRECT_LOCK_CHANGE))
            {
                ksLog.Debug(this, "Direct lock change " + obj);
            }
            sfBaseTranslator translator = GetTranslator(obj);
            if (translator != null)
            {
                translator.OnDirectLockChange(obj);
            }
        }

        /// <summary>Calls OnParentChange on the translator for an object.</summary>
        /// <param name="obj">obj whose parent changed.</param>
        /// <param name="childIndex">childIndex of the object. -1 if the object is a root.</param>
        public void OnParentChange(sfObject obj, int childIndex)
        {
            if (Config.Logging.HasFlag(sfConfig.EventLoggingFlags.PARENT_CHANGE))
            {
                ksLog.Debug(this, "Parent change " + obj + ", child index: " + childIndex);
            }
            sfBaseTranslator translator = GetTranslator(obj);
            if (translator != null)
            {
                translator.OnParentChange(obj, childIndex);
            }
        }

        /// <summary>Calls OnRemoveField on the translator for an object.</summary>
        /// <param name="dict">dict the field was removed from.</param>
        /// <param name="name">name of removed field.</param>
        public void OnRemoveField(sfDictionaryProperty dict, string name)
        {
            if (Config.Logging.HasFlag(sfConfig.EventLoggingFlags.REMOVE_FIELD))
            {
                ksLog.Debug(this, "Remove " + (dict.GetDepth() == 0 ? name : (dict.GetPath() + "." + name)) +
                    " from " + dict.GetContainerObject());
            }
            sfBaseTranslator translator = GetTranslator(dict.GetContainerObject());
            if (translator != null)
            {
                translator.OnRemoveField(dict, name);
            }
        }

        /// <summary>Calls OnListAdd on the translator for an object.</summary>
        /// <param name="list">list that elements were added to.</param>
        /// <param name="index">index elements were inserted at.</param>
        /// <param name="count">number of elements added.</param>
        public void OnListAdd(sfListProperty list, int index, int count)
        {
            if (Config.Logging.HasFlag(sfConfig.EventLoggingFlags.LIST_ADD))
            {
                ksLog.Debug(this, "Add " + (count == 1 ? list[index].Print() : (count + " elements")) + " at " +
                    list.GetPath() + "[" + index + "] on " + list.GetContainerObject());
            }
            sfBaseTranslator translator = GetTranslator(list.GetContainerObject());
            if (translator != null)
            {
                translator.OnListAdd(list, index, count);
            }
        }

        /// <summary>Calls OnListRemove on the translator for an object.</summary>
        /// <param name="list">list that elements were removed from.</param>
        /// <param name="index">index elements were removed from.</param>
        /// <param name="count">number of elements removed.</param>
        public void OnListRemove(sfListProperty list, int index, int count)
        {
            if (Config.Logging.HasFlag(sfConfig.EventLoggingFlags.LIST_REMOVE))
            {
                ksLog.Debug(this, "Remove " + count + " element(s) at " + list.GetPath() + "[" + index + "] on " +
                    list.GetContainerObject());
            }
            sfBaseTranslator translator = GetTranslator(list.GetContainerObject());
            if (translator != null)
            {
                translator.OnListRemove(list, index, count);
            }
        }

        /// <summary>Calls OnSelect on the translator for a uobject.</summary>
        /// <param name="uobj">uobj that was selected.</param>
        public void OnSelect(UObject uobj)
        {
            sfObject obj = m_objectMap.GetSFObject(uobj);
            if (obj == null || !obj.IsSyncing)
            {
                return;
            }
            sfBaseTranslator translator = GetTranslator(obj);
            if (translator != null)
            {
                translator.OnSelect(obj, uobj);
            }
        }

        /// <summary>Calls OnDeselect on the translator for a uobject.</summary>
        /// <param name="uobj">uobj that was deselected.</param>
        public void OnDeselect(UObject uobj)
        {
            sfObject obj = m_objectMap.GetSFObject(uobj);
            if (obj == null || !obj.IsSyncing)
            {
                return;
            }
            sfBaseTranslator translator = GetTranslator(obj);
            if (translator != null)
            {
                translator.OnDeselect(obj, uobj);
            }
        }
    }
}
