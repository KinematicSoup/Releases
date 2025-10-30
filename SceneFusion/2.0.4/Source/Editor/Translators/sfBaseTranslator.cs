using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using KS.SceneFusion2.Client;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>Base class for handling object events.</summary>
    public class sfBaseTranslator
    {
        /// <summary>
        /// Called after all translators are registered. Do one time initialization here that depends on other
        /// translators.
        /// </summary>
        public virtual void Initialize() { }

        /// <summary>Called after connecting to a session.</summary>
        public virtual void OnSessionConnect() { }

        /// <summary>Called after disconnecting from a session.</summary>
        public virtual void OnSessionDisconnect() { }

        /// <summary>Creates an sfObject for a uobject.</summary>
        /// <param name="uobj">uobj to create sfObject for.</param>
        /// <param name="outObj">outObj created for the uobject.</param>
        /// <returns>true if the uobject was handled by this translator.</returns>
        public virtual bool TryCreate(UObject uobj, out sfObject outObj)
        {
            outObj = null;
            return false;
        }

        /// <summary>Gets the UObject for an sfObject.</summary>
        /// <param name="obj">obj to get UObject for.</param>
        /// <param name="current">
        /// current value of the serialized property we are getting the UObject reference for.
        /// </param>
        /// <returns>for the sfObject.</returns>
        public virtual UObject GetUObject(sfObject obj, UObject current = null)
        {
            return sfObjectMap.Get().GetUObject(obj);
        }

        /// <summary>Called when an object is created by another user.</summary>
        /// <param name="obj">obj that was created.</param>
        /// <param name="childIndex">childIndex of new object. -1 if object is a root.</param>
        public virtual void OnCreate(sfObject obj, int childIndex) { }

        /// <summary>Called when a locally created object is confirmed as created.</summary>
        /// <param name="obj">obj that whose creation was confirmed.</param>
        public virtual void OnConfirmCreate(sfObject obj) { }

        /// <summary>Called when an object is deleted by another user.</summary>
        /// <param name="obj">obj that was deleted.</param>
        public virtual void OnDelete(sfObject obj) { }

        /// <summary>Called when a locally deleted object is confirmed as deleted.</summary>
        /// <param name="obj">obj that whose deletion was confirmed.</param>
        /// <param name="unsubscribed">
        /// true if the deletion occurred because we unsubscribed from the object's parent.
        /// </param>
        public virtual void OnConfirmDelete(sfObject obj, bool unsubscribed) { }

        /// <summary>Called when an object is locked by another user.</summary>
        /// <param name="obj">obj that was locked.</param>
        public virtual void OnLock(sfObject obj) { }

        /// <summary>Called when an object is unlocked by another user.</summary>
        /// <param name="obj">obj that was unlocked.</param>
        public virtual void OnUnlock(sfObject obj) { }

        /// <summary>Called when an object's lock owner changes.</summary>
        /// <param name="obj">obj whose lock owner changed.</param>
        public virtual void OnLockOwnerChange(sfObject obj) { }

        /// <summary>Called when an object's direct lock owner changes.</summary>
        /// <param name="obj">obj whose direct lock owner changed.</param>
        public virtual void OnDirectLockChange(sfObject obj) { }

        /// <summary>Called when an object's parent is changed by another user.</summary>
        /// <param name="obj">obj whose parent changed.</param>
        /// <param name="childIndex">childIndex of the object. -1 if the object is a root.</param>
        public virtual void OnParentChange(sfObject obj, int childIndex) { }

        /// <summary>Called when an object property changes.</summary>
        /// <param name="property">property that changed.</param>
        public virtual void OnPropertyChange(sfBaseProperty property) { }

        /// <summary>Called when a field is removed from a dictionary property.</summary>
        /// <param name="dict">dict the field was removed from.</param>
        /// <param name="name">name of removed field.</param>
        public virtual void OnRemoveField(sfDictionaryProperty dict, string name) { }

        /// <summary>Called when one or more elements are added to a list property.</summary>
        /// <param name="list">list that elements were added to.</param>
        /// <param name="index">index elements were inserted at.</param>
        /// <param name="count">number of elements added.</param>
        public virtual void OnListAdd(sfListProperty list, int index, int count) { }

        /// <summary>Called when one or more elements are removed from a list property.</summary>
        /// <param name="list">list that elements were removed from.</param>
        /// <param name="index">index elements were removed from.</param>
        /// <param name="count">number of elements removed.</param>
        public virtual void OnListRemove(sfListProperty list, int index, int count) { }

        /// <summary>Called when a Unity serialized property changes.</summary>
        /// <param name="obj">obj whose property changed.</param>
        /// <param name="sprop">sprop that changed.</param>
        /// <returns>false if the property change event should be handled by the default handler.</returns>
        public virtual bool OnSPropertyChange(sfObject obj, SerializedProperty sprop) { return false; }

        /// <summary>Called when a uobject is selected. The default implementation does nothing.</summary>
        /// <param name="obj">obj for the selected uobject.</param>
        /// <param name="uobj">uobj that was selected.</param>
        public virtual void OnSelect(sfObject obj, UObject uobj) { }

        /// <summary>Called when a uobject is deselected. The default implementation does nothing.</summary>
        /// <param name="obj">obj for the deselected uobject.</param>
        /// <param name="uobj">uobj that was deselected.</param>
        public virtual void OnDeselect(sfObject obj, UObject uobj) { }
    }
}
