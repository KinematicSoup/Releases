/*
KINEMATICSOUP CONFIDENTIAL
 Copyright(c) 2014-2025 KinematicSoup Technologies Incorporated 
 All Rights Reserved.

NOTICE:  All information contained herein is, and remains the property of 
KinematicSoup Technologies Incorporated and its suppliers, if any. The 
intellectual and technical concepts contained herein are proprietary to 
KinematicSoup Technologies Incorporated and its suppliers and may be covered by
U.S. and Foreign Patents, patents in process, and are protected by trade secret
or copyright law. Dissemination of this information or reproduction of this
material is strictly forbidden unless prior written permission is obtained from
KinematicSoup Technologies Incorporated.
*/

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using KS.Unity.Editor;
using KS.Unity;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Track scene and asset modification times.</summary>
    public class ksSceneTracker : ScriptableObject
    {
        // Mapping of scene paths to the last time the file was modified.
        [SerializeField]
        private ksSerializableDictionary<string, long> m_sceneTimes = new ksSerializableDictionary<string, long>();

        // Mapping of asset paths to the last time the file was modified.
        [SerializeField]
        private ksSerializableDictionary<string, long> m_assetTimes = new ksSerializableDictionary<string, long>();

        // Mapping of asset paths to a list of scenes that reference the asset.
        [SerializeField]
        private ksSerializableDictionary<string, List<string>> m_assetSceneReferences = new ksSerializableDictionary<string, List<string>>();

        /// <summary>
        /// Return a list of paths for scenes that have been modified since they were added to this tracker.
        /// </summary>
        /// <returns>List of paths for scenes that were modified.</returns>
        public List<string> GetModifiedScenes()
        {
            List<string> modifiedScenes = GetScenesReferencingModifiedAssets();
            string[] assetGUIDs = AssetDatabase.FindAssets("t:SceneAsset");
            for (int i = 0; i < assetGUIDs.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(assetGUIDs[i]);

                // Do not include scenes from packages
                if (modifiedScenes.Contains(path) || !path.StartsWith("Assets/"))
                {
                    continue;
                }

                // If the scene is untracked, then add it to the list of modified scenes that needs to be processed.
                long publishTime = 0;
                if (!m_sceneTimes.TryGetValue(path, out publishTime))
                {
                    modifiedScenes.Add(path);
                    continue;
                }

                // Check if the tracked scene modified time has changed.
                // Negative time indicates the scene is not suppose to have a config file.
                bool expectConfig = publishTime >= 0;
                publishTime = Math.Abs(publishTime);
                long fileTime = File.GetLastWriteTimeUtc(ksPaths.ProjectRoot + "/" + path).Ticks;
                if (publishTime != fileTime)
                {
                    modifiedScenes.Add(path);
                    continue;
                }

                if (!expectConfig)
                {
                    continue;
                }
                // Check if the scene config file exists
                string name = ksPathUtils.GetName(path);
                int index = name.LastIndexOf('.');
                if (index >= 0)
                {
                    name = name.Substring(0, index);
                }
                try
                {
                    if (!File.Exists(ksPaths.SceneConfigFile(name)))
                    {
                        modifiedScenes.Add(path);
                    }
                }
                catch (Exception e)
                {
                    ksLog.LogException(this, e);
                    modifiedScenes.Add(path);
                }
            }

            return modifiedScenes;
        }

        /// <summary>
        /// Get a list of scenes which reference modified assets files.
        /// </summary>
        /// <returns>List of scene referenced by modified assets.</returns>
        public List<string> GetScenesReferencingModifiedAssets()
        {
            List<string> scenes = new List<string>();
            List<string> deletedAssets = new List<string>();
            foreach (KeyValuePair<string, long> pair in m_assetTimes)
            {
                string assetPath = pair.Key;
                long modifiedTime = pair.Value;

                if (!File.Exists(ksPaths.ProjectRoot + "/" + assetPath))
                {
                    deletedAssets.Add(assetPath);
                }
                else if (File.GetLastWriteTimeUtc(ksPaths.ProjectRoot + "/" + assetPath).Ticks == modifiedTime)
                {
                    // The asset was not modified since the last time it was tracked.
                    continue;
                }

                List<string> referencedScenePaths;
                if (!m_assetSceneReferences.TryGetValue(assetPath, out referencedScenePaths) || referencedScenePaths == null)
                {
                    continue;
                }

                foreach (string scenePath in referencedScenePaths)
                {
                    if (!scenes.Contains(scenePath) && File.Exists(ksPaths.ProjectRoot + "/" + scenePath))
                    {
                        scenes.Add(scenePath);
                    }
                }
            }

            // Clear deleted scene assets from the tracking.
            foreach (string sceneAsset in deletedAssets)
            {
                m_assetTimes.Remove(sceneAsset);
                m_assetSceneReferences.Remove(sceneAsset);
            }
            return scenes;
        }

        /// <summary>Track a scene modification time.</summary>
        /// <param name="path">Scene path</param>
        /// <param name="expectConfig">Do we expect the scene to have a config file?</param>
        public void TrackScene(string path, bool expectConfig)
        {
            long fileTime = File.GetLastWriteTimeUtc(ksPaths.ProjectRoot + "/" + path).Ticks;
            // Time is negative for scenes that are not suppose to have config files.
            if (!expectConfig)
            {
                fileTime = -fileTime;
            }

            if (m_sceneTimes.ContainsKey(path))
            {
                m_sceneTimes[path] = fileTime;
            }
            else
            {
                m_sceneTimes.Add(path, fileTime);
            }
        }

        /// <summary>
        /// Track when an asset was last modified and associate it with a scene.
        /// </summary>
        /// <param name="path">Path to an asset file.</param>
        /// <param name="scenePath">Path to a scene asset file.</param>
        public void TrackAsset(string path, string scenePath)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(scenePath))
            {
                return;
            }
            long fileTime = File.GetLastWriteTimeUtc(ksPaths.ProjectRoot + "/" + path).Ticks;
            if (m_assetTimes.ContainsKey(path))
            {
                m_assetTimes[path] = fileTime;
            }
            else
            {
                m_assetTimes.Add(path, fileTime);
            }

            List<string> m_scenes;
            if (!m_assetSceneReferences.TryGetValue(path, out m_scenes))
            {
                m_scenes = new List<string>();
                m_assetSceneReferences.Add(path, m_scenes);
            }
            if (!m_scenes.Contains(scenePath))
            {
                m_scenes.Add(scenePath);
            }
        }

        /// <summary>
        /// Remove scene references from all tracked assets
        /// </summary>
        /// <param name="scenePath"></param>
        public void RemoveAssetSceneReferences(string scenePath)
        {
            foreach (KeyValuePair<string, List<string>> pair in m_assetSceneReferences)
            {
                pair.Value.Remove(scenePath);
            }
        }

        /// <summary>Clear all tracking data.</summary>
        public void Clear()
        {
            m_sceneTimes.Clear();
            m_assetTimes.Clear();
            m_assetSceneReferences.Clear();
        }

        /// <summary>
        /// Update the config writer when loaded after Unity serialization (or when the object is created).
        /// </summary>
        public void OnEnable()
        {
            hideFlags = HideFlags.HideAndDontSave;
            ksConfigWriter.SceneTracker = this;
        }
    }
}