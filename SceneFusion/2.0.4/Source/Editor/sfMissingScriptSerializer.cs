using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine.SceneManagement;
using KS.SceneFusion2.Client;
using KS.SceneFusion;
using KS.SF.Reactor;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>Handles serialization and deserialization of missing scripts.</summary>
    public class sfMissingScriptSerializer
    {
        /// <summary>Singleton instance</summary>
        public static sfMissingScriptSerializer Get()
        {
            return m_instance;
        }
        private static sfMissingScriptSerializer m_instance = new sfMissingScriptSerializer();

        private bool m_saved = false;

        /// <summary>
        /// Starts listening for scene save events so reference data can be serialized before scenes are saved.
        /// </summary>
        public void Start()
        {
            sfSceneSaveWatcher.Get().PreSave += PreSave;
            sfSceneSaveWatcher.Get().PostSave += PostSave;
        }

        /// <summary>Stops listening for scene save events and serializes reference data in missing scripts.</summary>
        public void Stop()
        {
            sfSceneSaveWatcher.Get().PreSave -= PreSave;
            sfSceneSaveWatcher.Get().PostSave -= PostSave;
            SaveAllReferences();
        }

        /// <summary>Serializes a dictionary property to a missing script's serialized property data.</summary>
        /// <param name="missingScript">missingScript to serialize properties to.</param>
        /// <param name="properties">properties to serialize.</param>
        public void SerializeProperties(sfIMissingScript missingScript, sfDictionaryProperty properties)
        {
            foreach (KeyValuePair<string, sfBaseProperty> field in properties)
            {
                if (!field.Key.StartsWith('#'))
                {
                    missingScript.SerializedProperties[field.Key] = sfBaseProperty.Serialize(field.Value);
                }
            }
        }

        /// <summary>
        /// Serializes a property to a missing script's serialized property data. Does nothing if the root property is
        /// not an sfDictionaryProperty or if the property has no parent. If the property is a nested subproperty, the
        /// entire ancestor property at depth 1 will be serialized.
        /// </summary>
        /// <param name="missingScript">missingScript to serialize to.</param>
        /// <param name="property">property to serialize.</param>
        public void SerializeProperty(sfIMissingScript missingScript, sfBaseProperty property)
        {
            // Get property at depth 1.
            while (property.GetDepth() > 1)
            {
                property = property.ParentProperty;
            }
            if (!string.IsNullOrEmpty(property.Name))
            {
                missingScript.SerializedProperties[property.Name] = sfBaseProperty.Serialize(property);
            }
        }

        /// <summary>Deserializes missing script properties into an sfDictionaryProperty.</summary>
        /// <param name="missingScript">missingScript to deserialize properties from.</param>
        /// <param name="properties">properties to add deserialized properties to.</param>
        public void DeserializeProperties(sfIMissingScript missingScript, sfDictionaryProperty properties)
        {
            List<sfBaseProperty> changedProperties = new List<sfBaseProperty>();
            foreach (KeyValuePair<string, byte[]> field in missingScript.SerializedProperties)
            {
                sfBaseProperty property = sfBaseProperty.Deserialize(field.Value);
                if (property != null)
                {
                    if (UpdateReferences(missingScript, ref property))
                    {
                        changedProperties.Add(property);
                    }
                    properties[field.Key] = property;
                }
            }
            // Reserialize the properties with the updated references.
            foreach (sfBaseProperty property in changedProperties)
            {
                missingScript.SerializedProperties[property.Name] = sfBaseProperty.Serialize(property);
            }
        }

        /// <summary>
        /// Updates the missing script's map of sfObject ids to uobjects for reference properties in an
        /// sfDictionaryProperty.
        /// </summary>
        /// <param name="missingScript">missingScript to update reference map for.</param>
        /// <param name="properties">properties to look for references in.</param>
        private void StoreReferences(sfIMissingScript missingScript, sfDictionaryProperty properties)
        {
            sfSession session = SceneFusion.Get().Service.Session;
            if (session == null)
            {
                return;
            }
            // Save the id of the session these ids are from. If we deserialize the properties in a different session,
            // we will need to use the reference map to find the correct objects since sfObject ids can change between
            // sessions.
            missingScript.SessionId = session.Info.RoomInfo.Id;
            missingScript.ReferenceMap.Clear();
            // Iterate the properties and look for reference properties.
            foreach (sfBaseProperty prop in properties.Iterate())
            {
                if (prop.Type == sfBaseProperty.Types.REFERENCE)
                {
                    sfObject obj = session.GetObject(((sfReferenceProperty)prop).ObjectId);
                    UObject uobj = sfObjectEventDispatcher.Get().GetUObject(obj);
                    if (uobj != null)
                    {
                        missingScript.ReferenceMap[obj.Id] = uobj;
                    }
                }
            }
        }

        /// <summary>
        /// Updates the ids of reference properties in a property if the missing script's properties were serialized in
        /// a different session. If the referenced object cannot be found, replaces the reference property with a null
        /// property.
        /// </summary>
        /// <param name="missingScript">missingScript the property belongs to.</param>
        /// <param name="property">
        /// property to update references in. If this is a reference property whose object
        /// cannot be found, it will be set to a null property.
        /// </param>
        /// <param name="changed">set to true if any references were updated.</param>
        /// <returns></returns>
        private bool UpdateReferences(sfIMissingScript missingScript, ref sfBaseProperty property)
        {
            // Nothing to update if our reference map is empty.
            if (missingScript.ReferenceMap.Count == 0)
            {
                return false;
            }
            // Nothing to update if the properties were serialized in the current session since the ids will not have
            // changed.
            sfSession session = SceneFusion.Get().Service.Session;
            if (session == null || session.Info.RoomInfo.Id == missingScript.SessionId)
            {
                return false;
            }
            bool changed = false;
            List<sfReferenceProperty> nullReferences = new List<sfReferenceProperty>();
            foreach (sfBaseProperty prop in property.Iterate())
            {
                if (prop.Type == sfBaseProperty.Types.REFERENCE)
                {
                    sfReferenceProperty reference = (sfReferenceProperty)prop;
                    UObject uobj;
                    // Get the referenced uobject from the reference map
                    if (missingScript.ReferenceMap.TryGetValue(reference.ObjectId, out uobj))
                    {
                        // Create a reference property from the uobject.
                        sfReferenceProperty newReference = sfPropertyUtils.FromReference(uobj) as sfReferenceProperty;
                        if (newReference != null)
                        {
                            // Update the reference to the id of the sfObject.
                            if (reference.ObjectId != newReference.ObjectId)
                            {
                                changed = true;
                                reference.ObjectId = newReference.ObjectId;
                            }
                            continue;
                        }
                    }
                    if (prop.ParentProperty == null)
                    {
                        property = new sfNullProperty();
                        return true;
                    }
                    nullReferences.Add(reference);
                }
            }
            // Replace properties that reference null with null properties.
            foreach (sfReferenceProperty reference in nullReferences)
            {
                switch (reference.ParentProperty.Type)
                {
                    case sfBaseProperty.Types.DICTIONARY:
                    {
                        changed = true;
                        ((sfDictionaryProperty)reference.ParentProperty)[reference.Name] = new sfNullProperty();
                        break;
                    }
                    case sfBaseProperty.Types.LIST:
                    {
                        changed = true;
                        ((sfListProperty)reference.ParentProperty)[reference.Index] = new sfNullProperty();
                        break;
                    }
                }
            }
            return changed;
        }

        /// <summary>
        /// Called before saving a scene. If in a session, updates reference maps for all missing scripts with
        /// notifications.
        /// </summary>
        private void PreSave(Scene scene)
        {
            if (m_saved)
            {
                return;
            }
            // Set saved flag to avoid saving multiple times if we are saving multiple scenes at once.
            m_saved = true;
            SaveAllReferences();
        }

        /// <summary>Called after scenes are saved.</summary>
        private void PostSave()
        {
            m_saved = false;
        }

        /// <summary>If in a session, updates reference maps for all missing scripts with notifications.</summary>
        private void SaveAllReferences()
        {
            if (SceneFusion.Get().Service.Session == null)
            {
                return;
            }
            // Find missing scripts by iterating all objects with missing component notifications.
            ksLinkedList<sfNotification> notifications =
                sfNotificationManager.Get().GetNotifications(sfNotificationCategory.MissingComponent);
            if (notifications == null)
            {
                return;
            }
            foreach (sfNotification notification in notifications)
            {
                foreach (UObject uobj in notification.Objects)
                {
                    sfIMissingScript missingScript = uobj as sfIMissingScript;
                    if (missingScript == null)
                    {
                        continue;
                    }
                    sfObject obj = sfObjectMap.Get().GetSFObject(uobj);
                    if (obj == null)
                    {
                        continue;
                    }
                    sfDictionaryProperty properties = obj.Property as sfDictionaryProperty;
                    if (properties != null)
                    {
                        StoreReferences(missingScript, properties);
                    }
                }
            }
        }
    }
}
