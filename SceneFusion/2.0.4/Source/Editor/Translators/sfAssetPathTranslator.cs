using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using KS.SceneFusion2.Client;
using KS.SceneFusion;
using KS.SF.Reactor;
using KS.SF.Unity.Editor;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>
    /// Manages syncing of asset paths and handles notifications for missing assets. Each asset referenced that isn't
    /// synced by another translator gets an asset path sfObject containing the asset path, guid, and for subassets, the
    /// file id. The guid and file id are used for offline missing asset replacement. References to the asset are synced
    /// as sfReferenceProperties referencing the asset path object. This allows us to find all synced references to an
    /// asset, as well as track when new references are added or removed which we use for missing asset notifications.
    /// </summary>
    public class sfAssetPathTranslator : sfBaseTranslator
    {
        private Dictionary<sfAssetInfo, sfObject> m_infoToObjectMap = new Dictionary<sfAssetInfo, sfObject>();
        // Keys are the sfObject id of the asset path object for the missing asset.
        private Dictionary<uint, sfNotification> m_notificationMap = new Dictionary<uint, sfNotification>();
        private List<KeyValuePair<sfObject, sfNotification>> m_notificationsToAdd =
            new List<KeyValuePair<sfObject, sfNotification>>();
        private HashSet<sfObject> m_missingObjects = new HashSet<sfObject>();
        private bool m_saved = false;

        /// <summary>Called after connecting to a session. Registers event handlers.</summary>
        public override void OnSessionConnect()
        {
            sfUnityEventDispatcher.Get().OnUpdate += Update;
            sfLoader.Get().OnLoadError += HandleLoadError;
            sfLoader.Get().OnFindMissingAsset += HandleFindMissingAsset;
            SceneFusion.Get().Service.Session.OnAddReference += HandleAddReference;
            SceneFusion.Get().Service.Session.OnRemoveReference += HandleRemoveReference;
            sfSceneSaveWatcher.Get().PreSave += PreSave;
            sfSceneSaveWatcher.Get().PostSave += PostSave;
        }

        /// <summary>Called after disconnecting from a session. Unregisters event handlers.</summary>
        public override void OnSessionDisconnect()
        {
            ConvertStandInsToGuids();
            sfUnityEventDispatcher.Get().OnUpdate -= Update;
            sfLoader.Get().OnLoadError -= HandleLoadError;
            sfLoader.Get().OnFindMissingAsset -= HandleFindMissingAsset;
            SceneFusion.Get().Service.Session.OnAddReference -= HandleAddReference;
            SceneFusion.Get().Service.Session.OnRemoveReference -= HandleRemoveReference;
            sfSceneSaveWatcher.Get().PreSave -= PreSave;
            sfSceneSaveWatcher.Get().PostSave -= PostSave;
            m_infoToObjectMap.Clear();
            m_notificationMap.Clear();
            m_missingObjects.Clear();
        }

        /// <summary>Gets the UObject for an sfObject. Loads the UObject by asset path.</summary>
        /// <param name="obj">obj to get UObject for.</param>
        /// <param name="current">
        /// current value of the serialized property we are getting the UObject reference for.
        /// </param>
        /// <returns>for the sfObject.</returns>
        public override UObject GetUObject(sfObject obj, UObject current = null)
        {
            sfDictionaryProperty properties = (sfDictionaryProperty)obj.Property;
            sfAssetInfo assetInfo = sfPropertyUtils.GetAssetInfo(properties);
            return sfLoader.Get().Load(assetInfo);
        }

        /// <summary>
        /// Called when an object is created by another user. Adds the object to the path to object map and sets
        /// references to the asset the object represents.
        /// </summary>
        /// <param name="obj">obj that was created.</param>
        /// <param name="childIndex">childIndex of new object. -1 if object is a root.</param>
        public override void OnCreate(sfObject obj, int childIndex)
        {
            sfDictionaryProperty properties = (sfDictionaryProperty)obj.Property;
            sfAssetInfo info = sfPropertyUtils.GetAssetInfo(properties);
            sfObject current;
            if (m_infoToObjectMap.TryGetValue(info, out current))
            {
                // The asset path was created twice, which can happen if two users try to create the asset path at the
                // same time. Keep the version that was created first and delete the second one.
                if (sfObjectUtils.DeleteDuplicate(current, obj))
                {
                    return;
                }
            }
            m_infoToObjectMap[info] = obj;

            // Set references to this asset.
            sfReferenceProperty[] references = SceneFusion.Get().Service.Session.GetReferences(obj);
            if (references.Length > 0)
            {
                UObject asset = sfLoader.Get().Load(info);
                if (asset != null)
                {
                    sfPropertyManager.Get().SetReferences(asset, references);
                }
            }
        }

        /// <summary>Gets the path object for an asset info.</summary>
        /// <param name="info">info to get path object for.</param>
        /// <returns>path object.</returns>
        public sfObject GetPathObject(sfAssetInfo info)
        {
            sfObject obj;
            m_infoToObjectMap.TryGetValue(info, out obj);
            return obj;
        }

        /// <summary>Gets the path object for an asset info. Creates one if it does not already exist.</summary>
        /// <param name="info">info to get path object for.</param>
        /// <param name="asset">
        /// asset for the path object. Used to get the guid and file id if the path object needs to be
        /// created.
        /// </param>
        /// <returns>path object.</returns>
        public sfObject GetOrCreatePathObject(sfAssetInfo info, UObject asset)
        {
            if (!info.IsValid)
            {
                return null;
            }
            sfObject obj;
            if (!m_infoToObjectMap.TryGetValue(info, out obj) )
            {
                sfDictionaryProperty properties = new sfDictionaryProperty();
                sfPropertyUtils.SetAssetInfoProperties(properties, info);
                string guid;
                long fileId;
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out guid, out fileId))
                {
                    properties[sfProp.Guid] = guid;
                    // We only sync file id for non-library subassets and prefabs.
                    if (sfLoader.Get().IsLibraryAsset(asset))
                    {
                        properties[sfProp.IsLibraryAsset] = true;
                        if (asset is GameObject)
                        {
                            properties[sfProp.FileId] = fileId;
                        }
                    }
                    else if (AssetDatabase.IsSubAsset(asset))
                    {
                        properties[sfProp.FileId] = fileId;
                    }
                }

                obj = new sfObject(sfType.AssetPath, properties);
                SceneFusion.Get().Service.Session.Create(obj);
                m_infoToObjectMap[info] = obj;
            }
            return obj;
        }

        /// <summary>Called every frame. Adds queued notifications to uobjects.</summary>
        /// <param name="deltaTime">deltaTime in seconds since the last update.</param>
        private void Update(float deltaTime)
        {
            // Add queued notifications to uobjects.
            foreach (KeyValuePair<sfObject, sfNotification> pair in m_notificationsToAdd)
            {
                UObject uobj = sfObjectMap.Get().GetUObject(pair.Key);
                if (uobj != null)
                {
                    pair.Value.AddToObject(uobj);
                }
            }
            m_notificationsToAdd.Clear();
        }

        /// <summary>Called when the sfLoader fails to load an asset. Creates a missing asset notification.</summary>
        /// <param name="info">info for asset that failed to load.</param>
        /// <param name="message">error message.</param>
        private void HandleLoadError(sfAssetInfo info, string message)
        {
            sfObject obj = GetPathObject(info);
            if (obj != null && m_missingObjects.Add(obj))
            {
                sfNotification notification = sfNotification.Create(sfNotificationCategory.MissingAsset, message);
                m_notificationMap[obj.Id] = notification;
                sfReferenceProperty[] references = SceneFusion.Get().Service.Session.GetReferences(obj);
                foreach (sfReferenceProperty reference in references)
                {
                    // Add the notification on the next update in case the sfObject isn't linked to a UObject yet.
                    m_notificationsToAdd.Add(new KeyValuePair<sfObject, sfNotification>(
                        reference.GetContainerObject(), notification));
                }
            }
        }

        /// <summary>
        /// Called when a missing asset is found. Clears the missing asset notification and updates references to the
        /// asset.
        /// </summary>
        /// <param name="info">info for the asset that was previously missing.</param>
        /// <param name="asset">asset that was previously missing.</param>
        private void HandleFindMissingAsset(sfAssetInfo info, UObject asset)
        {
            sfObject obj = GetPathObject(info);
            if (obj != null)
            {
                m_missingObjects.Remove(obj);
                sfNotification notification;
                if (m_notificationMap.Remove(obj.Id, out notification))
                {
                    notification.Clear();
                }

                sfReferenceProperty[] references = SceneFusion.Get().Service.Session.GetReferences(obj);
                if (references.Length > 0)
                {
                    sfPropertyManager.Get().SetReferences(asset, references);
                    ksLog.Info(this, "Replaced " + references.Length + " stand-in reference(s) for " + info + ".");
                }
            }
        }

        /// <summary>
        /// Called when a reference is to an object is synced. If the reference is for a missing asset, adds a
        /// notification to the uobject with the reference.
        /// </summary>
        /// <param name="reference."></param>
        private void HandleAddReference(sfReferenceProperty reference)
        {
            sfNotification notification;
            if (m_notificationMap.TryGetValue(reference.ObjectId, out notification))
            {
                // Add the notification on the next update in case the sfObject isn't linked to a UObject yet.
                m_notificationsToAdd.Add(new KeyValuePair<sfObject, sfNotification>(
                    reference.GetContainerObject(), notification));
            }
        }

        /// <summary>
        /// Called when a synced reference is removed. If the reference was for a missing asset, removes the
        /// notification from the uobject with the reference.
        /// </summary>
        /// <param name="reference"></param>
        /// <param name="objectId">objectId of the object that was referenced.</param>
        private void HandleRemoveReference(sfReferenceProperty reference, uint objectId)
        {
            sfNotification notification;
            if (m_notificationMap.TryGetValue(objectId, out notification))
            {
                UObject uobj = sfObjectMap.Get().GetUObject(reference.GetContainerObject());
                if (uobj != null)
                {
                    notification.RemoveFromObject(uobj);
                }
            }
        }

        /// <summary>
        /// Creates assets for missing asset stand-ins with the guid and file id of the missing assets and updates
        /// references to reference the new assets, then deletes the new assets so stand-in references become missing
        /// object references with the correct guid and file id that Unity can automatically update when the asset with
        /// that guid and file id becomes available.
        /// </summary>
        private void ConvertStandInsToGuids()
        {
            foreach (sfObject obj in m_missingObjects)
            {
                sfDictionaryProperty properties = (sfDictionaryProperty)obj.Property;
                sfAssetInfo info = sfPropertyUtils.GetAssetInfo(properties);
                UObject standIn = sfLoader.Get().Load(info);
                if (standIn == null)
                {
                    continue;
                }
                string guid = (string)properties[sfProp.Guid];
                sfBaseProperty prop;
                bool isLibraryAsset = false;
                if (properties.TryGetField(sfProp.IsLibraryAsset, out prop))
                {
                    isLibraryAsset = (bool)prop;
                }
                long fileId = 0;
                // We only sync file id for non-library subassets and prefabs.
                if (properties.TryGetField(sfProp.FileId, out prop))
                {
                    fileId = (long)prop;
                }
                UObject asset = sfLoader.Get().CreateStandInAssetWithGuid(standIn, guid, isLibraryAsset, fileId);
                if (asset == null)
                {
                    ksLog.Warning(this, "Unable to create stand-in " + (isLibraryAsset ? "library " : "") +
                        "asset for " + info + ". Offline missing asset replacement will not work for this asset.");
                }
                sfReferenceProperty[] references = SceneFusion.Get().Service.Session.GetReferences(obj);
                sfPropertyManager.Get().SetReferences(asset, references);
            }
            // Delete the temporary folder containing the new assets.
            ksPathUtils.Delete(sfPaths.Temp, true);
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Called before saving a scene. Converts stand-ins to missing objects with the correct guid and file id.
        /// </summary>
        /// <param name="scene">scene being saved.</param>
        private void PreSave(Scene scene)
        {
            if (!m_saved)
            {
                // Set saved flag to avoid saving multiple times if we are saving multiple scenes at once.
                m_saved = true;
                ConvertStandInsToGuids();
            }
        }

        /// <summary>
        /// Called after scenes are saved. Sets references that were changed to missing objects guid references before
        /// saving back to stand-in references.
        /// </summary>
        private void PostSave()
        {
            m_saved = false;
            foreach (sfObject obj in m_missingObjects)
            {
                sfDictionaryProperty properties = (sfDictionaryProperty)obj.Property;
                sfAssetInfo info = sfPropertyUtils.GetAssetInfo(properties);
                UObject standIn = sfLoader.Get().Load(info);
                if (standIn == null)
                {
                    continue;
                }
                sfReferenceProperty[] references = SceneFusion.Get().Service.Session.GetReferences(obj);
                sfPropertyManager.Get().SetReferences(standIn, references, false);
            }
        }
    }
}
