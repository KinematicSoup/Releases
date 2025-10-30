using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>Provides pre and post scene save events.</summary>
    public class sfSceneSaveWatcher : AssetModificationProcessor
    {
        /// <summary>Pre save event handler.</summary>
        /// <param name="scene">scene that is being saved.</param>
        public delegate void PreSaveHandler(Scene scene);

        /// <summary>Post save event handler.</summary>
        public delegate void PostSaveHandler();

        /// <summary>Invoked before a scene is saved.</summary>
        public event PreSaveHandler PreSave;

        /// <summary>Invoked after scenes are saved.</summary>
        public event PostSaveHandler PostSave;

        /// <summary></summary>
        /// <returns>singleton instance</returns>
        public static sfSceneSaveWatcher Get()
        {
            return m_instance;
        }
        private static sfSceneSaveWatcher m_instance = new sfSceneSaveWatcher();

        /// <summary>Singleton constructor</summary>
        private sfSceneSaveWatcher()
        {

        }

        /// <summary>
        /// Called before assets are saved. Dispatches events for scenes that are about to be saved, then if any scenes
        /// were saved, dispatches a PostSave event on the next pre update.
        /// </summary>
        /// <param name="paths">paths to assets that will be saved.</param>
        /// <returns>paths to assets that will be saved.</returns>
        public static string[] OnWillSaveAssets(string[] paths)
        {
            if (m_instance.PreSave != null || m_instance.PostSave != null)
            {
                bool savingScene = false;
                foreach (string path in paths)
                {
                    if (path.EndsWith(".unity"))
                    {
                        Scene scene = SceneManager.GetSceneByPath(path);
                        if (scene.IsValid() && scene.isLoaded)
                        {
                            savingScene = true;
                            if (m_instance.PreSave != null)
                            {
                                m_instance.PreSave(scene);
                            }
                        }
                    }
                }
                if (savingScene && m_instance.PostSave != null)
                {
                    sfUnityEventDispatcher.Get().PreUpdate += m_instance.InvokePostSave;
                }
            }
            return paths;
        }

        /// <summary>Invokes the PostSave event.</summary>
        /// <param name="deltaTime">deltaTime in seconds since the last update.</param>
        private void InvokePostSave(float deltaTime)
        {
            sfUnityEventDispatcher.Get().PreUpdate -= InvokePostSave;
            if (PostSave != null)
            {
                PostSave();
            }
        }
    }
}
