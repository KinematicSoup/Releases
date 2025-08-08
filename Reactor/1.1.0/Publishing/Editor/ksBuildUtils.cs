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
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Build utility for iterating prefab and scene objects.</summary>
    public class ksBuildUtils
    {
        /// <summary>Stores a prefab with its path and asset bundle.</summary>
        public struct PrefabInfo
        {
            /// <summary>Path to the prefab</summary>
            public string Path;
            /// <summary>Path to asset bundle the prefab is in. Null if not in an asset bundle.</summary>
            public string AssetBundle;
            /// <summary>The prefab game object.</summary>
            public GameObject GameObject;

            /// <summary>Constructor</summary>
            /// <param name="gameObject">Prefab game object</param>
            /// <param name="path">Path to the prefab.</param>
            /// <param name="assetBundle">Asset bundle path. Null if the prefab is not in an asset bundle.</param>
            public PrefabInfo(GameObject gameObject, string path, string assetBundle = null)
            {
                GameObject = gameObject;
                Path = path;
                AssetBundle = assetBundle;
            }
        }

        /// <summary>Iterates all prefabs.</summary>
        /// <returns>Pefab iterator</returns>
        public static IEnumerable<GameObject> IteratePrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:prefab");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    yield return prefab;
                }
            }
        }

        /// <summary>Iterates all prefabs in resources and asset bundles.</summary>
        /// <returns>Prefab iterator</returns>
        public static IEnumerable<PrefabInfo> IterateResourceAndAssetBundlePrefabs()
        {
            // Iterate all resource prefabs
            foreach (GameObject prefabObject in Resources.LoadAll<GameObject>(""))
            {
                string assetPath = AssetDatabase.GetAssetPath(prefabObject);
                if (assetPath.EndsWith(".prefab") && !AssetIsInAssetBundle(assetPath))
                {
                    yield return new PrefabInfo(prefabObject, assetPath);
                }
            }
            // Iterate all asset bundle prefabs
            foreach (string assetBundle in AssetDatabase.GetAllAssetBundleNames())
            {
                foreach (string assetPath in AssetDatabase.GetAssetPathsFromAssetBundle(assetBundle))
                {
                    if (assetPath.EndsWith(".prefab"))
                    {
                        GameObject prefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                        if (prefabObject != null)
                        {
                            yield return new PrefabInfo(prefabObject, assetPath, assetBundle);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Check if the asset at the given path is in an asset bundle.
        /// </summary>
        /// <param name="assetPath">Path of asset</param>
        /// <returns>True if the asset is in an asset bundle</returns>
        public static bool AssetIsInAssetBundle(string assetPath)
        {
            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            return importer != null && !string.IsNullOrEmpty(importer.assetBundleName);
        }

        /// <summary>Iterate all components of the type <typeparamref name="T"/>in all scenes.</summary>
        /// <typeparam name="T">Type to iterate.</typeparam>
        /// <returns>Iterator</returns>
        public static IEnumerable<T> IterateSceneObjects<T>() where T : Component
        {
            foreach (Component component in IterateSceneObjects(typeof(T)))
            {
                yield return (T)component;
            }
        }

        /// <summary>Iterate all components of the given <paramref name="types"/> in all scenes.</summary>
        /// <param name="types">Types to iterate.</param>
        /// <returns>Iterator</returns>
        public static IEnumerable<Component> IterateSceneObjects(params Type[] types)
        {
            EditorSceneManager.SaveOpenScenes();
            SceneSetup[] sceneSetup = EditorSceneManager.GetSceneManagerSetup();

            string[] assetGUIDs = AssetDatabase.FindAssets("t:SceneAsset");
            for (int i = 0; i < assetGUIDs.Length; i++)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(assetGUIDs[i]);
                // Do not include scenes from packages
                if (!scenePath.StartsWith("Assets/"))
                {
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (!scene.IsValid())
                {
                    continue;
                }

                foreach (Type type in types)
                {
                    UnityEngine.Object[] components = UnityEngine.Object.FindObjectsByType(type,
                        FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (Component component in components)
                    {
                        yield return component;
                    }
                }
                EditorSceneManager.SaveScene(scene);
            }
            EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
        }

        /// <summary>Get a full hierarchy name for a game object</summary>
        /// <param name="gameObject">Game object to get name for.</param>
        /// <returns>The name of the game object prefixed by the names of its parents seperated by '+'</returns>
        public static string FullGameObjectName(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return "null";
            }

            string path = gameObject.name;
            Transform parent = gameObject.transform.parent;
            while (parent != null)
            {
                path = parent.name + "+" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}
