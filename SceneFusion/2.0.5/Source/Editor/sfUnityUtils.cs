/*
KINEMATICSOUP CONFIDENTIAL
 Copyright(c) 2014-2026 KinematicSoup Technologies Incorporated 
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
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using KS.SF.Reactor;
using KS.SF.Unity.Editor;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>Unity utility functions.</summary>
    public static class sfUnityUtils
    {
        /// <summary>Callback to use with game object iteration methods.</summary>
        /// <param name="gameObject">gameObject being iterated.</param>
        /// <returns>true to iterate the children of the game object.</returns>
        public delegate bool ForEachCallback(GameObject gameObject);

        /// <summary>Callback to use with WithActiveScene.</summary>
        public delegate void WithActiveSceneCallback();

        private static readonly string LOG_CHANNEL = typeof(sfUnityUtils).ToString();

        private static List<PrefabStage> m_prefabStages;

        /// <summary>
        /// Extension method to check if a UObject is destroyed (is it fake null instead of real null?).
        /// </summary>
        public static bool IsDestroyed(this UObject uobj)
        {
            return uobj == null && (object)uobj != null;
        }

        /// <summary>
        /// Attempts to get a game object from a uobject by casting it first to a game object and then to a component
        /// and returning the component's game object.
        /// </summary>
        /// <param name="uobj">uobj to get game object from.</param>
        /// <returns>game object, or null if uobj was not a game object or component.</returns>
        public static GameObject GetGameObject(UObject uobj)
        {
            GameObject gameObject = uobj as GameObject;
            if (gameObject == null)
            {
                Component component = uobj as Component;
                if (component != null)
                {
                    gameObject = component.gameObject;
                }
            }
            return gameObject;
        }

        /// <summary>Iterates all game objects in all loaded scenes.</summary>
        public static IEnumerable<GameObject> IterateGameObjects()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                {
                    foreach (GameObject gameObject in IterateGameObjects(scene))
                    {
                        yield return gameObject;
                    }
                }
            }
        }

        /// <summary>Iterates all game objects in a scene if the scene is loaded.</summary>
        /// <param name="scene">scene to iterate game objects for.</param>
        /// <returns></returns>
        public static IEnumerable<GameObject> IterateGameObjects(Scene scene)
        {
            if (scene.isLoaded)
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (GameObject go in IterateSelfAndDescendants(root))
                    {
                        yield return go;
                    }
                }
            }
        }

        /// <summary>Iterates self and all descendants.</summary>
        /// <param name="root"></param>
        /// <returns></returns>
        public static IEnumerable<GameObject> IterateSelfAndDescendants(GameObject root)
        {
            if (root == null)
            {
                yield break;
            }
            yield return root;
            if (root == null)
            {
                yield break;
            }
            foreach (Transform childTransform in root.transform)
            {
                foreach (GameObject go in IterateSelfAndDescendants(childTransform.gameObject))
                {
                    yield return go;
                    if (childTransform == null)
                    {
                        break;
                    }
                }
                if (root == null)
                {
                    yield break;
                }
            }
        }

        /// <summary>
        /// Calls a callback on all game objects that are part of the same prefab instance. If the game object is not a
        /// prefab instance, the callback is called only on the game object this is called with.
        /// </summary>
        /// <param name="gameObject">
        /// the callback is called on all game objects belonging to the same prefab
        /// instance as this object.
        /// </param>
        /// <param name="callback">
        /// callback to call on each game object in the prefab instance. If it returns false
        /// for an object, it will not be called on descendants of that object.
        /// </param>
        public static void ForEachInPrefabInstance(GameObject gameObject, ForEachCallback callback)
        {
            if (gameObject == null)
            {
                return;
            }
            GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject);
            if (root == null)
            {
                callback(root);
                return;
            }
            UObject prefab = PrefabUtility.GetPrefabInstanceHandle(root);
            ForSelfAndDescendants(root, (GameObject child) =>
            {
                return PrefabUtility.GetPrefabInstanceHandle(child) == prefab && callback(child);
            });
        }

        /// <summary>Calls a callback on all game objects in all loaded scenes.</summary>
        /// <param name="callback">
        /// callback to call. If the callback returns false for an object, the callback will
        /// not be called on descendants of that object.
        /// </param>
        public static void ForEachGameObject(ForEachCallback callback)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                {
                    GameObject[] rootObjects = scene.GetRootGameObjects();
                    for (int j = 0; j < rootObjects.Length; ++j)
                    {
                        if (callback(rootObjects[j]))
                        {
                            ForEachDescendant(rootObjects[j], callback);
                        }
                    }
                }
            }
        }

        /// <summary>Calls a callback on all descendants of a game object.</summary>
        /// <param name="gameObject">gameObject with descendants to call the callback on.</param>
        /// <param name="callback">
        /// callback to call. If the callback returns false for an object, the callback will
        /// not be called on descendants of that object.
        /// </param>
        public static void ForEachDescendant(GameObject gameObject, ForEachCallback callback)
        {
            if (gameObject == null)
            {
                return;
            }
            foreach (Transform child in gameObject.transform)
            {
                if (callback(child.gameObject))
                {
                    ForEachDescendant(child.gameObject, callback);
                }
            }
        }

        /// <summary>Calls a function on a game object and all its descendants.</summary>
        /// <param name="gameObject">gameObject to iterate.</param>
        /// <param name="callback">
        /// callback to call on objects. If it returns false for a game object, that object's
        /// descendants will not be iterated.
        /// </param>
        public static void ForSelfAndDescendants(GameObject gameObject, ForEachCallback callback)
        {
            if (gameObject != null && callback(gameObject))
            {
                foreach (Transform child in gameObject.transform)
                {
                    ForSelfAndDescendants(child.gameObject, callback);
                }
            }
        }

        /// <summary>Checks if a game object is the root of a prefab asset.</summary>
        /// <param name="gameObject">gameObject to check.</param>
        /// <returns>true if the game object is the root of a prefab.</returns>
        public static bool IsPrefabAssetRoot(GameObject gameObject)
        {
            return gameObject != null && gameObject.transform.parent == null &&
                PrefabUtility.IsPartOfPrefabAsset(gameObject);
        }

        /// <summary>
        /// Checks if there is a prefab stage open for the prefab at the given path and returns the prefab stage if
        /// found.
        /// </summary>
        /// <param name="prefabPath">prefabPath to find prefab stage.</param>
        /// <returns>prefab stage for the given prefab path, or null if none was found.</returns>
        public static PrefabStage FindPrefabStage(string prefabPath)
        {
            if (m_prefabStages == null)
            {
                m_prefabStages = new ksReflectionObject(typeof(PrefabStage)).GetField("m_AllPrefabStages")
                    .GetValue() as List<PrefabStage>;
                if (m_prefabStages == null)
                {
                    return null;
                }
            }
            foreach (PrefabStage stage in m_prefabStages)
            {
                if (stage.assetPath == prefabPath)
                {
                    return stage;
                }
            }
            return null;
        }

        /// <summary>Checks if a game object is part of a prefab that is open in the current prefab stage.</summary>
        /// <param name="gameObject">
        /// gameObject to check if is part of a prefab opened in the current prefab stage.
        /// </param>
        /// <returns>true if the game object is part of a prefab that is open in the current prefab stage.</returns>
        public static bool IsOpenInPrefabStage(GameObject gameObject)
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            return stage != null && stage.assetPath == AssetDatabase.GetAssetPath(gameObject);
        }

        /// <summary>
        /// Gets the prefab root instance from a prefab instance handle (the object containing the overrides).
        /// </summary>
        /// <param name="prefabInstanceHandle">
        /// prefabInstanceHandle to get prefab root instance from. This object does not have a C# class
        /// so the class is UObject.
        /// </param>
        /// <returns>prefab root instance, or null if it couldn't be found.</returns>
        public static GameObject GetPrefabRootInstanceFromHandle(UObject prefabInstanceHandle)
        {
            if (prefabInstanceHandle == null)
            {
                return null;
            }
            SerializedObject so = new SerializedObject(prefabInstanceHandle);
            SerializedProperty sprop = so.FindProperty("m_RootGameObject");
            return sprop == null ? null : sprop.objectReferenceValue as GameObject;
        }

        /// <summary>Instantiates a prefab.</summary>
        /// <param name="scene">scene to instantiate the prefab in.</param>
        /// <param name="prefabPath"></param>
        /// <returns>instantiated prefab, or null if the prefab could not be instantiated.</returns>
        public static GameObject InstantiatePrefab(Scene scene, string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                return null;
            }
            return (GameObject)(scene.isLoaded ? 
                PrefabUtility.InstantiatePrefab(prefab, scene) : PrefabUtility.InstantiatePrefab(prefab));
        }

        /// <summary>Adds hide flags to a game object and its components.</summary>
        /// <param name="gameObject">gameObject to set hide flags on.</param>
        /// <param name="flags">flags to add.</param>
        /// <param name="alreadyHasFlagSet">
        /// if not null, components that already have the flags set will
        /// be added to this set.
        /// </param>
        public static void AddFlags(
            GameObject gameObject,
            HideFlags flags,
            HashSet<Component> alreadyHasFlagSet = null)
        {
            // Setting hide flags on a game object sets all component flags to the game object's, so we have to store
            // the components' hide flags before setting the game object's, then set them back to what they were with
            // the new flags added.
            Component[] components = gameObject.GetComponents<Component>();
            HideFlags[] componentFlags = new HideFlags[components.Length];
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component != null)
                {
                    if (alreadyHasFlagSet != null && (component.hideFlags & flags) == flags)
                    {
                        alreadyHasFlagSet.Add(component);
                    }
                    componentFlags[i] = component.hideFlags | flags;
                }
            }
            gameObject.hideFlags |= flags;
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    components[i].hideFlags = componentFlags[i];
                }
            }
        }

        /// <summary>Removes hide flags from a game object and its components.</summary>
        /// <param name="gameObject">gameObject to clear hide flags on.</param>
        /// <param name="flags">flags to remove.</param>
        /// <param name="blacklist">if not null, components in this set will not have the flag removed.</param>
        public static void RemoveFlags(GameObject gameObject, HideFlags flags, HashSet<Component> blacklist = null)
        {
            // Setting hide flags on a game object sets all component flags to the game object's, so we have to store
            // the components' hide flags before setting the game object's, then set them back to what they were with
            // the new flags removed.
            Component[] components = gameObject.GetComponents<Component>();
            HideFlags[] componentFlags = new HideFlags[components.Length];
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component != null)
                {
                    componentFlags[i] = blacklist != null && blacklist.Contains(component) ? 
                        component.hideFlags : component.hideFlags & ~flags;
                }
            }
            gameObject.hideFlags &= ~flags;
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    components[i].hideFlags = componentFlags[i];
                }
            }
        }

        /// <summary>
        /// Sets the active scene to the given scene if it isn't the active scene before calling the callback, then
        /// restores the active scene to what it was before. The callback is not called if the scene is not loaded.
        /// </summary>
        /// <param name="scene">scene to set as the active scene while calling the callback.</param>
        /// <param name="callback">callback to call.</param>
        public static void WithActiveScene(Scene scene, WithActiveSceneCallback callback)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene != scene)
            {
                SceneManager.SetActiveScene(scene);
            }
            try
            {
                callback();
            }
            finally
            {
                // Restore the active scene.
                SceneManager.SetActiveScene(activeScene);
            }
        }

        /// <summary>
        /// Gets a uobject by its ulong id. In Unity 6.3+ the ulong is converted to an entity id, and in older versions
        /// it is converted to an int instance id.
        /// </summary>
        /// <typeparam name="T">Type to cast uobject to. Returns null if the uobject is a different type.</typeparam>
        /// <param name="id">Entity or instance id</param>
        /// <returns>
        /// The uobject with the given id, or null if none was found or it was not a type <typeparamref name="T"/>.
        /// </returns>
        public static T GetUObject<T>(ulong id) where T : UObject
        {
            return GetUObject(id) as T;
        }

        /// <summary>
        /// Gets a uobject by its ulong id. In Unity 6.3+ the ulong is converted to an entity id, and in older versions
        /// it is converted to an int instance id.
        /// </summary>
        /// <param name="id">Entity or instance id</param>
        /// <returns>The uobject with the given id, or null if none was found.</returns>
        public static UObject GetUObject(ulong id)
        {
#if UNITY_6000_4_OR_NEWER
            return EditorUtility.EntityIdToObject(EntityId.FromULong(id));
#elif UNITY_6000_3_OR_NEWER
            unchecked
            {
                return EditorUtility.EntityIdToObject((EntityId)(int)id);
            }
#else
            unchecked
            {
                return EditorUtility.InstanceIDToObject((int)id);
            }
#endif
        }

        /// <summary>
        /// Gets a uobject's id as a ulong. In Unity 6.3+ the id is the entity id, and in older versions it is the
        /// instance id.
        /// </summary>
        /// <param name="uobj">UObject to get id for.</param>
        /// <returns>Entity or instance id.</returns>
        public static ulong GetUObjectId(UObject uobj)
        {
#if UNITY_6000_4_OR_NEWER
            return EntityId.ToULong(uobj.GetEntityId());
#elif UNITY_6000_3_OR_NEWER
            unchecked
            {
                return (ulong)(int)uobj.GetEntityId();
            }
#else
            unchecked
            {
                return (ulong)uobj.GetInstanceID();
            }
#endif
        }

#if UNITY_6000_3_OR_NEWER
        /// <summary>
        /// Zero entity id constant. In Unity versions older than 6.3, this constant is instead a zero int. Use this in
        /// functions that take an entity id in 6.3+ and an int instance id in older version.
        /// </summary>
        public static readonly EntityId NO_ID = EntityId.None;


        /// <summary>
        /// Gets a uobject by its entity id. In Unity versions older than 6.3, this function instead takes an int
        /// instance id.
        /// </summary>
        /// <typeparam name = "T" > Type to cast uobject to.Returns null if the uobject is a different type.</typeparam>
        /// <param name="id">Entity id</param>
        /// <returns>
        /// The uobject with the given id, or null if none was found or it was not a type <typeparamref name="T"/>.
        /// </returns>
        public static T GetUObject<T>(EntityId id) where T : UObject
        {
            return GetUObject(id) as T;
        }

        /// <summary>
        /// Gets a uobject by its entity id. In Unity versions older than 6.3, this function instead takes an int
        /// instance id.
        /// </summary>
        /// <param name="id">Entity id</param>
        /// <returns>The uobject with the given id, or null if none was found.</returns>
        public static UObject GetUObject(EntityId id)
        {
            return EditorUtility.EntityIdToObject(id);
        }

        /// <summary
        /// Gets a uobject's entity id. In Unity versions older than 6.3 this function returns the instance id instead.
        /// </summary>
        /// <param name="uobj">Object to get id for.</param>
        /// <returns>Entity id</returns>
        public static EntityId GetUnityId(UObject uobj)
        {
            return uobj.GetEntityId();
        }
#else
        /// <summary>
        /// Zero intance id int constant. In Unity 6.3+, this constant is instead a zero entity id. Use this in
        /// functions that take an entity id in 6.3+ and an int instance id in older version.
        /// </summary>
        public const int NO_ID = 0;

        /// <summary>
        /// Gets a uobject by its instance id. In Unity 6.3+, this function instead takes an entity id.
        /// </summary>
        /// <typeparam name = "T" > Type to cast uobject to.Returns null if the uobject is a different type.</typeparam>
        /// <param name="id">Instance id</param>
        /// <returns>
        /// The uobject with the given id, or null if none was found or it was not a type <typeparamref name="T"/>.
        /// </returns>
        public static T GetUObject<T>(int id) where T : UObject
        {
            return GetUObject(id) as T;
        }

        /// <summary>
        /// Gets a uobject by its instance id. In Unity 6.3+, this function instead takes an entity id.
        /// </summary>
        /// <param name="id">Instance id</param>
        /// <returns>The uobject with the given id, or null if none was found.</returns>
        public static UObject GetUObject(int id)
        {
            return EditorUtility.InstanceIDToObject(id);
        }

        /// <summary>Gets a uobject's instance id. In Unity 6.3+ this function returns the entity id instead.</summary>
        /// <param name="uobj">Object to get id for.</param>
        /// <returns>Instance id</returns>
        public static int GetUnityId(UObject uobj)
        {
            return uobj.GetInstanceID();
        }
#endif

        /// <summary>
        /// Gets the path of an asset by its id as a ulong. The is is the entity id in Unity 6.3+, and the instance id
        /// in older versions.
        /// </summary>
        /// <param name="uobjectId">Entity or instance id.</param>
        /// <returns>Asset path, or empty string if none was found.</returns>
        public static string GetAssetPath(ulong uobjectId)
        {
#if UNITY_6000_4_OR_NEWER
            return AssetDatabase.GetAssetPath(EntityId.FromULong(uobjectId));
#elif UNITY_6000_3_OR_NEWER
            unchecked
            {
                return AssetDatabase.GetAssetPath((EntityId)(int)uobjectId);
            }
#else
            unchecked
            {
                return AssetDatabase.GetAssetPath((int)uobjectId);
            }
#endif
        }
    }
}
