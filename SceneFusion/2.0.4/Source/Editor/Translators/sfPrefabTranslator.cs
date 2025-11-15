using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using UnityEditor;
using KS.SceneFusion2.Client;
using KS.SF.Unity.Editor;
using KS.SF.Reactor;
using KS.SF.LZMA;
using KS.SceneFusion;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>
    /// Manages syncing of prefabs created during a session by syncing the prefab file and metafile if
    /// <see cref="sfConfig.SyncPrefabs"/> is <see cref="sfConfig.PrefabSyncMode.CREATE_ONLY"/>.
    /// </summary>
    public class sfPrefabTranslator : sfBaseTranslator
    {
        private Dictionary<string, sfObject> m_pathToObjectMap = new Dictionary<string, sfObject>();

        /// <summary>
        /// Initialization. Registers an event handler to start the translator after the config settings for the
        /// session are synced.
        /// </summary>
        public override void Initialize()
        {
            sfConfigTranslator translator = sfObjectEventDispatcher.Get().GetTranslator<sfConfigTranslator>(
                sfType.Config);
            translator.OnConfigSynced += Start;
        }

        /// <summary>Registers an on new prefab event handler if prefab syncing is disabled.</summary>
        private void Start()
        {
            if (sfConfig.Get().SyncPrefabs == sfConfig.PrefabSyncMode.CREATE_ONLY)
            {
                sfNewPrefabWatcher.Get().OnNewPrefab += OnNewPrefab;
            }
        }

        /// <summary>
        /// Called when disconnected from a session. Unregisters the on new prefab event handler and clears the path to
        /// object map.
        /// </summary>
        public override void OnSessionDisconnect()
        {
            m_pathToObjectMap.Clear();
            sfNewPrefabWatcher.Get().OnNewPrefab -= OnNewPrefab;
        }

        /// <summary>
        /// Called when a prefab object is created by another user. Creates the prefab file and metafile.
        /// </summary>
        /// <param name="obj">Object that was created.</param>
        /// <param name="childIndex">Child index of the object. -1 if the object is a root object.</param>
        public override void OnCreate(sfObject obj, int childIndex)
        {
            sfDictionaryProperty properties = (sfDictionaryProperty)obj.Property;
            string path = (string)properties[sfProp.Path];

            sfObject current;
            if (m_pathToObjectMap.TryGetValue(path, out current))
            {
                // The prefab was created twice, which can happen if 2 users create the prefab at the same time. Keep
                // the one that was created first.
                if (sfObjectUtils.DeleteDuplicate(current, obj))
                {
                    return;
                }
            }
            m_pathToObjectMap[path] = obj;

            // Decompress and write the prefab file. The metafile is not compressed because compressing it actually
            // increased the file size in testing.
            if (WriteFile(path, (byte[])properties[sfProp.Data], true) &&
                WriteFile(path + ".meta", (byte[])properties[sfProp.Meta]))
            {
                ksLog.Info(this, "Created file '" + path + "'.");
                AssetDatabase.Refresh();
            }
        }

        /// <summary>Called when a new prefab is created. Uploads the prefab if it isn't already uploaded.</summary>
        /// <param name="path">Path to new prefab.</param>
        private void OnNewPrefab(string path)
        {
            if (m_pathToObjectMap.ContainsKey(path))
            {
                return;
            }
            // Read and compress the prefab file.
            byte[] data = ReadFile(path, true);
            if (data == null)
            {
                return;
            }
            // Do not compress the metafile because it actually increased the file size in testing.
            byte[] metadata = ReadFile(path + ".meta");
            if (metadata == null)
            {
                return;
            }
            sfDictionaryProperty properties = new sfDictionaryProperty();
            properties[sfProp.Path] = path;
            properties[sfProp.Data] = data;
            properties[sfProp.Meta] = metadata;
            sfObject obj = new sfObject(sfType.Prefab, properties);
            m_pathToObjectMap[path] = obj;
            SceneFusion.Get().Service.Session.Create(obj);
        }

        /// <summary>Reads and optionally compresses all bytes from a file and logs errors that occur.</summary>
        /// <param name="path">Path to file to read.</param>
        /// <param name="compress">If true, compresses the file data.</param>
        /// <returns>Binary file data (optionally compressed), or null if an error occurred.</returns>
        private byte[] ReadFile(string path, bool compress = false)
        {
            byte[] data;
            try
            {
                data = File.ReadAllBytes(path);
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Error reading file '" + path + "'.", e);
                return null;
            }
            if (!compress)
            {
                return data;
            }
            try
            {
                return ksLZW.Compress(data);
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Error compressing file '" + path + "'.", e);
                return null;
            }
        }

        /// <summary>
        /// Writes a byte array to a file if it does not already exist. Optionally decompresses the byte array first.
        /// Logs errors that occur.
        /// </summary>
        /// <param name="path">Path to write to.</param>
        /// <param name="data">Data to write.</param>
        /// <param name="decompress">If true, decompresses the byte array before writing the file.</param>
        /// <returns>True if the file was written.</returns>
        private bool WriteFile(string path, byte[] data, bool decompress = false)
        {
            if (File.Exists(path))
            {
                ksLog.Info(this, "File '" + path + "' will not be written because it already exists.");
                return false;
            }
            if (decompress)
            {
                try
                {
                    data = ksLZW.Decompress(data);
                }
                catch (Exception e)
                {
                    ksLog.Error(this, "Error decompressing data for '" + path + "'.", e);
                    return false;
                }
            }
            ksPathUtils.Create(path);
            try
            {
                File.WriteAllBytes(path, data);
                return true;
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Error writing file '" + path + "'.", e);
                return false;
            }
        }
    }
}
