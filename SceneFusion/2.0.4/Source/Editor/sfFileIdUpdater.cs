using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using KS.SF.Unity.Editor;
using KS.SceneFusion2.Client;
using UObject = UnityEngine.Object;
using KS.SF.Reactor;
using System.IO;
using KS.SceneFusion;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>
    /// Overwrites Unity local file ids in a file. Can only be used when serialization mode is ForceText.
    /// </summary>
    public class sfFileIdUpdater
    {
        /// <summary>Replace handler.</summary>
        /// <param name="oldUObj">Old object</param>
        /// <param name="newUObj">New object</param>
        public delegate void ReplaceHandler(UObject oldUObj, UObject newUObj);

        private static Dictionary<string, sfFileIdUpdater> m_updaters = new Dictionary<string, sfFileIdUpdater>();

        private string m_path;
        private Dictionary<long, UObject> m_fileIdToUObject = new Dictionary<long, UObject>();
        private Dictionary<UObject, long> m_uobjectToFileId = new Dictionary<UObject, long>();
        private Dictionary<UObject, ReplaceHandler> m_replaceHandlers;

        private ksScriptUpdater m_updater;

        /// <summary>Gets the file id updater for an asset, optionally creating one if it does not exist.</summary>
        /// <param name="asset">Asset to get file id updater for.</param>
        /// <param name="create">
        /// True to create an updater for the asset if one does not exist. Will not create if <paramref name="asset"/>
        /// is not an asset.
        /// </param>
        /// <returns>File id updater for the asset, or null if none was found.</returns>
        public static sfFileIdUpdater Get(UObject asset, bool create = true)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }
            sfFileIdUpdater updater;
            if (!m_updaters.TryGetValue(path, out updater) && create)
            {
                updater = new sfFileIdUpdater(path);
                m_updaters[path] = updater;
            }
            return updater;
        }

        /// <summary>Constructor</summary>
        /// <param name="path">Path to file to update local file ids in.</param>
        private sfFileIdUpdater(string path)
        {
            m_path = path;
        }

        /// <summary>
        /// Sets the local file id for a uobject in the asset. The file id isn't applied until 
        /// <see cref="UpdateFileIds"/> is called.
        /// </summary>
        /// <param name="uobj">UObject to set file id for.</param>
        /// <param name="newFileId">File id to set.</param>
        /// <param name="replaceHandler">
        /// Callback to call when the uobject is replaced after the file ids are updated. If null,
        /// <see cref="sfBaseUObjectTranslator.OnReplace(sfObject, UObject, UObject)"/>. Use this to get a callback for
        /// unsynced uobjects.
        /// </param>
        public void SetFileId(UObject uobj, long newFileId, ReplaceHandler replaceHandler = null)
        {
            if (newFileId == 0)
            {
                return;
            }
            // Get the current local file id for the uobject. First check if there is an unapplied file id change in the
            // uobjectToFileId map.
            long currentFileId;
            if (!m_uobjectToFileId.TryGetValue(uobj, out currentFileId))
            {
                currentFileId = sfLoader.Get().GetLocalFileId(uobj);
            }
            else
            {
                m_fileIdToUObject.Remove(currentFileId);
            }
            if (currentFileId == 0 || currentFileId == newFileId)
            {
                return;
            }

            if (m_updater == null)
            {
                m_updater = new ksScriptUpdater();
            }
            // Add regex to replace the current file id with the new file id.
            m_updater.AddReplacement("\\{fileID: " + currentFileId + "\\}", "{fileID: " + newFileId + "}");
            m_updater.AddReplacement("&" + currentFileId + "(\r\n|\r|\n)", "&" + newFileId + Environment.NewLine);

            // Store the mapping between the uobject and the new file id.
            m_fileIdToUObject[newFileId] = uobj;
            m_uobjectToFileId[uobj] = newFileId;

            if (replaceHandler != null)
            {
                if (m_replaceHandlers == null)
                {
                    m_replaceHandlers = new Dictionary<UObject, ReplaceHandler>();
                }
                m_replaceHandlers[uobj] = replaceHandler;
            }
        }

        /// <summary>
        /// Updates local file ids set by <see cref="SetFileId(UObject, long)"/> by overwriting the old ids in the asset
        /// file and clears the updater. Changing an assets file id will destroy the old uobject instance of that asset
        /// and create a new one. Calls the old uobject's replace handler if there is one, or  calls 
        /// <see cref="sfBaseUObjectTranslator.OnReplace(sfObject, UObject, UObject)"/> to inform translators of the 
        /// replacements. Once this is called, <see cref="Get(UObject, bool)"/> will return null or optionally create a 
        /// new instance.
        /// </summary>
        public void UpdateFileIds()
        {
            m_updaters.Remove(m_path);
            if (m_updater == null)
            {
                return;
            }
            if (EditorSettings.serializationMode != SerializationMode.ForceText)
            {
                ksLog.Warning(this, "Cannot set file ids for '" + m_path +
                    "' when serialization mode is not ForceText.");
                return;
            }

            // Overwrite the file ids in the file.
            m_updater.ReplaceInFile(m_path);
            AssetDatabase.Refresh();

            // Load all subassets and find the ones with new ids by seeing if there is a different uobject for that id
            // in the fileIdToUObject map.
            foreach (UObject uobj in AssetDatabase.LoadAllAssetsAtPath(m_path))
            {
                long fileId = sfLoader.Get().GetLocalFileId(uobj);
                UObject oldUObj;
                if (m_fileIdToUObject.TryGetValue(fileId, out oldUObj) && oldUObj != uobj)
                {
                    // Call the replace handler for the old object if there is one.
                    if (m_replaceHandlers != null)
                    {
                        ReplaceHandler handler;
                        if (m_replaceHandlers.TryGetValue(oldUObj, out handler))
                        {
                            handler(oldUObj, uobj);
                            continue;
                        }
                    }

                    sfObject obj = sfObjectMap.Get().GetSFObject(oldUObj);
                    if (obj == null)
                    {
                        continue;
                    }
                    // Call OnReplace to inform the translator of the replacement uobject.
                    sfBaseUObjectTranslator translator = sfObjectEventDispatcher.Get()
                        .GetTranslator<sfBaseUObjectTranslator>(obj);
                    if (translator != null)
                    {
                        translator.OnReplace(obj, oldUObj, uobj);
                    }
                }
            }
            m_fileIdToUObject.Clear();
            m_uobjectToFileId.Clear();
            m_updater = null;
        }
    }
}
