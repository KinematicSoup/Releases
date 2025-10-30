using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using KS.SceneFusion2.Client;
using KS.SceneFusion;
using KS.SF.Reactor;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>Base class for translators that sync UObject properties using Unity's serialization system.</summary>
    public class sfBaseUObjectTranslator : sfBaseTranslator
    {
        /// <summary>sfProperty change event handler.</summary>
        /// <param name="uobj">uobj whose property changed.</param>
        /// <param name="property">property that changed. Null if the property was removed.</param>
        /// <returns>
        /// if false, the default property hander that sets the property using Unity serialization will be
        /// called.
        /// </returns>
        public delegate bool PropertyChangeHandler(UObject uobj, sfBaseProperty property);

        /// <summary>Handler for a post property change event.</summary>
        /// <param name="uobj">uobj whose property changed.</param>
        /// <param name="property">property that changed.</param>
        public delegate void PostPropertyChangeHandler(UObject uobj, sfBaseProperty property);

        /// <summary>Handler for a post uobject change event.</summary>
        /// <param name="uobj">uobj whose property changed.</param>
        public delegate void PostUObjectChangeHandler(UObject uobj);

        protected sfTypedPropertyEventMap<PropertyChangeHandler> m_propertyChangeHandlers =
            new sfTypedPropertyEventMap<PropertyChangeHandler>();

        /// <summary>Post property change event map.</summary>
        /// <returns></returns>
        public sfTypedPropertyEventMap<PostPropertyChangeHandler> PostPropertyChange
        {
            get { return m_postPropertyChangeHandlers; }
        }
        private sfTypedPropertyEventMap<PostPropertyChangeHandler> m_postPropertyChangeHandlers =
            new sfTypedPropertyEventMap<PostPropertyChangeHandler>();

        /// <summary>Post UObject change event map.</summary>
        /// <returns></returns>
        public sfTypeEventMap<PostUObjectChangeHandler> PostUObjectChange
        {
            get { return m_postUObjectChangeHandlers; }
        }
        private sfTypeEventMap<PostUObjectChangeHandler> m_postUObjectChangeHandlers =
            new sfTypeEventMap<PostUObjectChangeHandler>();

        /// <summary>Called when an sfObject property changes.</summary>
        /// <param name="property">property that changed.</param>
        public override void OnPropertyChange(sfBaseProperty property)
        {
            UObject uobj = sfObjectMap.Get().GetUObject(property.GetContainerObject());
            if (uobj == null || CallPropertyChangeHandlers(uobj, property) || 
                sfPropertyUtils.IsCustomProperty(property))
            {
                return;
            }

            sfIMissingScript missingScript = uobj as sfIMissingScript;
            if (missingScript != null)
            {
                sfMissingScriptSerializer.Get().SerializeProperty(missingScript, property);
                return;
            }

            SerializedObject so = sfPropertyManager.Get().GetSerializedObject(uobj);
            SerializedProperty sprop = sfPropertyManager.Get().GetSerializedProperty(so, property);
            if (sprop != null)
            {
                sfPropertyManager.Get().SetValue(sprop, property);
                sfPropertyManager.Get().QueuePropertyChangeEvent(uobj, property);
            }
        }

        /// <summary>Called when a field is removed from a dictionary property.</summary>
        /// <param name="dict">dict the field was removed from.</param>
        /// <param name="name">name of the removed field.</param>
        public override void OnRemoveField(sfDictionaryProperty dict, string name)
        {
            UObject uobj = sfObjectMap.Get().GetUObject(dict.GetContainerObject());
            if (uobj == null || CallPropertyChangeHandlers(uobj, dict, name) || name.StartsWith('#') ||
                sfPropertyUtils.IsCustomProperty(dict))
            {
                return;
            }

            sfIMissingScript missingScript = uobj as sfIMissingScript;
            if (missingScript != null)
            {
                if (dict.GetDepth() == 0)
                {
                    missingScript.SerializedProperties.Remove(name);
                }
                else
                {
                    sfMissingScriptSerializer.Get().SerializeProperty(missingScript, dict);
                }
                return;
            }

            // TODO: If we add remove elements handlers which we needed in Unreal but may not need here, if this is an
            // array property and the default value is an empty array, call the remove elements handler for all elements
            SerializedObject so = sfPropertyManager.Get().GetSerializedObject(uobj);
            SerializedProperty sprop = sfPropertyManager.Get().GetSerializedProperty(so, dict, name);
            if (sprop != null)
            {
                sfPropertyManager.Get().SetToDefaultValue(sprop);
                sfPropertyManager.Get().QueuePropertyChangeEvent(uobj, dict, name);
            }
        }

        /// <summary>Called when one or more elements are added to a list property.</summary>
        /// <param name="list">list that elements were added to.</param>
        /// <param name="index">index elements were inserted at.</param>
        /// <param name="count">number of elements added.</param>
        public override void OnListAdd(sfListProperty list, int index, int count)
        {
            UObject uobj = sfObjectMap.Get().GetUObject(list.GetContainerObject());
            if (uobj == null || CallPropertyChangeHandlers(uobj, list) || sfPropertyUtils.IsCustomProperty(list))
            {
                return;
            }

            sfIMissingScript missingScript = uobj as sfIMissingScript;
            if (missingScript != null)
            {
                sfMissingScriptSerializer.Get().SerializeProperty(missingScript, list);
                return;
            }

            SerializedObject so = sfPropertyManager.Get().GetSerializedObject(uobj);
            SerializedProperty sprop = sfPropertyManager.Get().GetSerializedProperty(so, list);
            if (!sprop.isArray)
            {
                return;
            }
            for (int i = index; i < index + count; i++)
            {
                sprop.InsertArrayElementAtIndex(i);
                sfPropertyManager.Get().SetValue(sprop.GetArrayElementAtIndex(i), list[i]);
            }
            sfPropertyManager.Get().QueuePropertyChangeEvent(uobj, list);
        }

        /// <summary>Called when one or more elements are removed from a list property.</summary>
        /// <param name="list">list that elements were removed from.</param>
        /// <param name="index">index elements were removed from.</param>
        /// <param name="count">number of elements removed.</param>
        public override void OnListRemove(sfListProperty list, int index, int count)
        {
            UObject uobj = sfObjectMap.Get().GetUObject(list.GetContainerObject());
            if (uobj == null || CallPropertyChangeHandlers(uobj, list) || sfPropertyUtils.IsCustomProperty(list))
            {
                return;
            }

            sfIMissingScript missingScript = uobj as sfIMissingScript;
            if (missingScript != null)
            {
                sfMissingScriptSerializer.Get().SerializeProperty(missingScript, list);
                return;
            }

            SerializedObject so = sfPropertyManager.Get().GetSerializedObject(uobj);
            SerializedProperty sprop = sfPropertyManager.Get().GetSerializedProperty(so, list);
            if (!sprop.isArray)
            {
                return;
            }

            for (int i = index + count - 1; i >= index; i--)
            {
#if !UNITY_2021_2_OR_NEWER
                int oldLength = sprop.arraySize;
#endif
                sprop.DeleteArrayElementAtIndex(i);
#if !UNITY_2021_2_OR_NEWER
                // Unity has a weird behaviour where it sets non-null object reference elements to null instead of
                // actually deleting them, so you may have to call this twice to delete it.
                if (oldLength == sprop.arraySize)
                {
                    sprop.DeleteArrayElementAtIndex(i);
                }
#endif
            }
            sfPropertyManager.Get().QueuePropertyChangeEvent(uobj, list);
        }

        /// <summary>Creates an sfObject for a UObject. Does nothing if one already exists.</summary>
        /// <param name="uobj">uobj to create sfObject for.</param>
        /// <param name="type">type of sfObject to create.</param>
        /// <returns>for the UObject, or null if an sfObject already existed.</returns>
        public sfObject CreateObject(UObject uobj, string type)
        {
            sfObject obj = sfObjectMap.Get().GetOrCreateSFObject(uobj, type);
            if (obj == null)
            {
                return null;
            }
            sfDictionaryProperty properties = (sfDictionaryProperty)obj.Property;
            sfPropertyManager.Get().CreateProperties(uobj, properties);
            return obj;
        }

        /// <summary>Calls the property change handlers for an sfProperty.</summary>
        /// <param name="uobj">uobj whose property changed.</param>
        /// <param name="property">property that changed.</param>
        /// <returns>true if the change event was handled.</returns>
        public bool CallPropertyChangeHandlers(UObject uobj, sfBaseProperty property, string name = null)
        {
            PropertyChangeHandler handlers;
            if (!string.IsNullOrEmpty(name) && property.ParentProperty == null)
            {
                // Non-empty name with a root property means a field in the root dictionary was removed (set to the
                // default value). Call the handler with the property name and with null as the property.
                handlers = m_propertyChangeHandlers.GetHandlers(uobj.GetType(), name);
                property = null;
            }
            else
            {
                handlers = m_propertyChangeHandlers.GetHandlers(uobj.GetType(), property);
            }

            if (handlers != null)
            {
                foreach (bool result in handlers.GetInvocationList().Select(
                    (Delegate handler) => ((PropertyChangeHandler)handler)(uobj, property)))
                {
                    if (result)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>Calls post property change handlers for a property and uobject.</summary>
        /// <param name="uobj">uobj with the property that changed.</param>
        /// <param name="property">property that changed.</param>
        /// <param name="name">
        /// if non-empty, the name of the sub-property that was removed from a dictionary
        /// property.
        /// </param>
        public void CallPostPropertyChangeHandlers(UObject uobj, sfBaseProperty property, string name = null)
        {
            PostPropertyChangeHandler handlers;
            if (!string.IsNullOrEmpty(name) && (property == null || property.ParentProperty == null))
            {
                // Non-empty name with a root property means a field in the root dictionary was removed (set to the
                // default value). Call the handler with the property name and with null as the property.
                handlers = m_postPropertyChangeHandlers.GetHandlers(uobj.GetType(), name);
                property = null;
            }
            else
            {
                handlers = m_postPropertyChangeHandlers.GetHandlers(uobj.GetType(), property);
            }
            if (handlers != null)
            {
                handlers(uobj, property);
            }
        }

        /// <summary>Calls post UObject change handlers for a uobject.</summary>
        /// <param name="uobj">uobj with the property that changed.</param>
        public void CallPostUObjectChangeHandlers(UObject uobj)
        {
            PostUObjectChangeHandler handlers = m_postUObjectChangeHandlers.GetHandlers(uobj.GetType());
            if (handlers != null)
            {
                handlers(uobj);
            }
        }

        /// <summary>Called when a uobject is selected. Sends a lock request for the object.</summary>
        /// <param name="obj">obj for the selected uobject.</param>
        /// <param name="uobj">uobj that was selected.</param>
        public override void OnSelect(sfObject obj, UObject uobj)
        {
            obj.RequestLock();
        }

        /// <summary>Called when a uobject is deselected. Releases the lock on the object.</summary>
        /// <param name="obj">obj for the deselected uobject.</param>
        /// <param name="uobj">uobj that was deselected.</param>
        public override void OnDeselect(sfObject obj, UObject uobj)
        {
            obj.ReleaseLock();
        }

        /// <summary>
        /// Called when a uobject is replaced with another uobject, which happens when we change the local file id of an
        /// asset. Removes the mapping for the sfObject and old uobject and maps the sfObject to the new uobject, and
        /// updates references from the old uobject to reference the new uobject.
        /// </summary>
        /// <param name="obj">obj whose uobject was replaced.</param>
        /// <param name="old">old uobject.</param>
        /// <param name="new">new uobject.</param>
        public virtual void OnReplace(sfObject obj, UObject oldUObj, UObject newUObj)
        {
            sfObjectMap.Get().Remove(oldUObj);
            sfObjectMap.Get().Add(obj, newUObj);
            sfReferenceProperty[] references = SceneFusion.Get().Service.Session.GetReferences(obj);
            sfPropertyManager.Get().SetReferences(newUObj, references);
        }
    }
}
