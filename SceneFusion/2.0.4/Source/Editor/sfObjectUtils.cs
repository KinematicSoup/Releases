using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using KS.SceneFusion2.Client;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>sfObject utility functions.</summary>
    public static class sfObjectUtils
    {
        /// <summary>
        /// Deletes <paramref name="obj"/> and updates references to it to instead reference
        /// <paramref name="replacementObj"/>. If session edits are disabled because SF is proccessing RPCs, does the
        /// deletion and reference updating in delayCall.
        /// </summary>
        /// <param name="obj">Object to delete and replace references for.</param>
        /// <param name="replacementObj">Object references should refence instead of <paramref name="obj"/>.</param>
        public static void DeleteAndReplaceReferences(sfObject obj, sfObject replacementObj)
        {
            if (obj == null || replacementObj == null || !SceneFusion.Get().Service.IsConnected)
            {
                return;
            }
            Action callback = () =>
            {
                sfPropertyUtils.ReplaceReferences(obj, replacementObj);
                if (obj.IsSyncing && !obj.IsLocked)
                {
                    SceneFusion.Get().Service.Session.Delete(obj);
                }
            };

            // If edits are disabled (because SF is processing RPCs), delete the object and update references in
            // delayCall, otherwise do it now.
            if (SceneFusion.Get().Service.Session.EditsDisabled)
            {
                EditorApplication.delayCall += () =>
                {
                    if (SceneFusion.Get().Service.IsConnected)
                    {
                        callback();
                    }
                };
            }
            else
            {
                callback();
            }
        }

        /// <summary>
        /// Deletes <paramref name="newObj"/> if <paramref name="currentObj"/> was already created on the server,
        /// otherwise deletes <paramref name="newObj"/>. Updates references to the deleted object to instead reference
        /// the other object. This is used to delete the sfObject that was creates 2nd on the server when multiple
        /// sfObjects are created for the same uobject. The deletion/reference updating happens in delayCall if edits
        /// are disabled because SF is still processing RPCs.
        /// </summary>
        /// <param name="currentObj">Current <see cref="sfObject"/> for the uobject.</param>
        /// <param name="newObj">New <see cref="sfObject"/> for the uobject.</param>
        /// <returns>
        /// True if <paramref name="newObj"/> was or will be deleted because <paramref name="currentObj"/> was created 
        /// first.
        /// </returns>
        public static bool DeleteDuplicate(sfObject currentObj, sfObject newObj)
        {
            if (currentObj.IsCreated)
            {
                DeleteAndReplaceReferences(newObj, currentObj);
                return true;
            }
            DeleteAndReplaceReferences(currentObj, newObj);
            sfObjectMap.Get().Remove(currentObj);
            return false;
        }
    }
}
