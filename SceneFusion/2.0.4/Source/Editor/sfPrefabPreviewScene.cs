using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using KS.SF.Unity.Editor;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>
    /// Loads a copy of a prefab into a preview scene which allows you to perform edits on the copy that aren't allowed
    /// directly on the prefab such as reparenting or unpacking prefab variants, and then save the changes back to the
    /// prefab.
    /// </summary>
    public class sfPrefabPreviewScene
    {
        private static ksReflectionObject m_roLoadPrefabIntoPreviewScene;

        /// <summary>
        /// The root object in the preview scene that is a copy of the prefab asset root.
        /// </summary>
        public GameObject RootObject
        {
            get { return m_rootObject; }
        }
        private GameObject m_rootObject;

        /// <summary>The preview scene.</summary>
        public Scene Scene
        {
            get { return m_scene; }
        }
        private Scene m_scene;

        /// <summary>Path to the prefab asset.</summary>
        public string PrefabPath
        {
            get { return m_path; }
        }
        private string m_path;

        /// <summary>Static initialization</summary>
        static sfPrefabPreviewScene()
        {
            m_roLoadPrefabIntoPreviewScene = new ksReflectionObject(typeof(PrefabStageUtility))
                .GetMethod("LoadPrefabIntoPreviewScene");
        }

        /// <summary>Constructor</summary>
        /// <param name="prefab">
        /// Prefab to load into a preview scene. If this is not a prefab, the preview scene won't be loaded.
        /// </param>
        public sfPrefabPreviewScene(GameObject prefab) : this(AssetDatabase.GetAssetPath(prefab))
        {

        }

        /// <summary>Constructor</summary>
        /// <param name="path">
        /// Path to prefab to load into a preview scene. If the prefab isn't found, the preview scene won't be loaded.
        /// </param>
        public sfPrefabPreviewScene(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            m_path = path;
            m_scene = EditorSceneManager.NewPreviewScene();
            m_rootObject = m_roLoadPrefabIntoPreviewScene.Invoke(path, m_scene) as GameObject;
        }

        /// <summary>Saves the contents of the preview scene to the prefab.</summary>
        public void Save()
        {
            if (m_rootObject != null)
            {
                PrefabUtility.SaveAsPrefabAsset(m_rootObject, m_path);
            }
        }

        /// <summary>Closes the preview scene.</summary>
        public void Close()
        {
            if (m_scene.isLoaded)
            {
                EditorSceneManager.ClosePreviewScene(m_scene);
            }
        }

        /// <summary>
        /// Finds the transform at the equivalent hierarchy location to <paramref name="targetTransform"/> under the
        /// <see cref="RootObject"/>'s transform. Eg. if <paramref name="targetTransform"/> is the first child of the 
        /// second child of a root object, gets the first child of the second child of root object's transform.
        /// </summary>
        /// <param name="targetTransform">Transform to find equivalent transform for.</param>
        /// <returns>
        /// Transform located at the equivalent hierarchy location under the root transform as 
        /// <paramref name="targetTransform"/>, or null if none was found.
        /// </returns>
        public Transform FindEquivalentTransform(Transform targetTransform)
        {
            if (RootObject == null)
            {
                return null;
            }
            Transform rootTransform = RootObject.transform;
            if (targetTransform.parent == null)
            {
                return rootTransform;
            }
            Stack<int> indexes = new Stack<int>();
            while (targetTransform.parent != null)
            {
                indexes.Push(targetTransform.GetSiblingIndex());
                targetTransform = targetTransform.parent;
            }
            Transform equivalentTransform = rootTransform;
            while (indexes.Count > 0)
            {
                int index = indexes.Pop();
                if (index >= equivalentTransform.childCount)
                {
                    return null;
                }
                equivalentTransform = equivalentTransform.GetChild(index);
            }
            return equivalentTransform;
        }

        /// <summary>
        /// Finds the game object at the equivalent hierarchy location to <paramref name="target"/> under the
        /// <see cref="RootObject"/>. Eg. if <paramref name="transform"/> is the first child of the second child of a
        /// root object, gets the first child of the second child of root object.
        /// </summary>
        /// <param name="target">Game object to find equivalent game object for.</param>
        /// <returns>
        /// Game object located at the equivalent hierarchy location under the root as <paramref name="target"/>, or
        /// null if none was found.
        /// </returns>
        public GameObject FindEquivalentGameObject(GameObject target)
        {
            if (target == null)
            {
                return null;
            }
            Transform equivalentTranform = FindEquivalentTransform(target.transform);
            return equivalentTranform == null ? null : equivalentTranform.gameObject;
        }
    }
}
