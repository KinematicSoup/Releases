using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using KS.SceneFusion2.Client;
using KS.SF.Reactor;
using KS.SF.Unity.Editor;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>Manages scene syncing.</summary>
    public class sfSceneTranslator : sfBaseTranslator
    {
        /// <summary>Preupload scene handler.</summary>
        /// <param name="scene">scene being uploaded.</param>
        /// <param name="sceneObj"></param>
        public delegate void PreUploadSceneHandler(Scene scene, sfObject sceneObj);

        /// <summary>
        /// Invoked before a scene is uploaded. Use this event to sync custom scene data by adding child sfObjects to
        /// the scene sfObject.
        /// </summary>
        public event PreUploadSceneHandler PreUploadScene;

        private sfObject m_lockObject;
        private sfListProperty m_localSubscriptionsProperty = new sfListProperty();
        private sfSession m_session;
        private ksLinkedList<Scene> m_uploadList = new ksLinkedList<Scene>();
        private List<Scene> m_removedScenes = new List<Scene>();
        private List<sfObject> m_loadList = new List<sfObject>();
        private Dictionary<Scene, sfObject> m_sceneToObjectMap = new Dictionary<Scene, sfObject>();
        private Dictionary<sfObject, Scene> m_objectToSceneMap = new Dictionary<sfObject, Scene>();
        private int m_missingObjectCount = 0;
        private int m_missingPrefabCount = 0;
        private bool m_loadedInitialScenes = false;

        /// <summary>Called after connecting to a session.</summary>
        public override void OnSessionConnect()
        {
            sfUnityEventDispatcher.Get().OnUpdate += Update;
            sfUnityEventDispatcher.Get().OnOpenScene += OnOpenScene;
            sfUnityEventDispatcher.Get().OnCloseScene += OnCloseScene;
            m_session = SceneFusion.Get().Service.Session;

            // Create a scene subscriptions object to track the local user's scene subscriptions.
            sfDictionaryProperty properties = new sfDictionaryProperty();
            sfObject obj = new sfObject(sfType.SceneSubscriptions, properties,
                sfObject.ObjectFlags.TRANSIENT); // Transient means the object is destroyed when we disconnect.
            properties[sfProp.UserId] = SceneFusion.Get().Service.Session.LocalUserId;
            m_localSubscriptionsProperty = new sfListProperty();
            properties[sfProp.Subscriptions] = m_localSubscriptionsProperty;
            m_session.Create(obj);

            if (SceneFusion.Get().Service.IsSessionCreator)
            {
                m_loadedInitialScenes = true;
                // Upload scenes. Upload the active scene first so it will be the one other users load if there are multiple scenes.
                Scene activeScene = SceneManager.GetActiveScene();
                RequestLock();
                m_uploadList.Add(activeScene);
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (scene.isLoaded && scene != activeScene)
                    {
                        m_uploadList.Add(scene);
                    }
                }
            } 
        }

        /// <summary>Called after disconnecting from a session.</summary>
        public override void OnSessionDisconnect()
        {
            m_loadedInitialScenes = false;
            m_lockObject = null;
            sfUnityEventDispatcher.Get().OnUpdate -= Update;
            sfUnityEventDispatcher.Get().OnOpenScene -= OnOpenScene;
            sfUnityEventDispatcher.Get().OnCloseScene -= OnCloseScene;
            m_sceneToObjectMap.Clear();
            m_objectToSceneMap.Clear();
            m_removedScenes.Clear();
            m_uploadList.Clear();
            m_loadList.Clear();
        }

        /// <summary>Gets the sfObject for a scene.</summary>
        /// <param name="scene">scene to get sfObject for.</param>
        /// <returns>for the scene, or null if the scene has no sfObject.</returns>
        public sfObject GetSceneObject(Scene scene)
        {
            sfObject obj;
            m_sceneToObjectMap.TryGetValue(scene, out obj);
            return obj;
        }

        /// <summary>Gets the hierarchy sfObject for a scene.</summary>
        /// <param name="scene">scene to get hierarchy sfObject for.</param>
        /// <returns>for the scene hierarchy, or null if the scene has no sfObject.</returns>
        public sfObject GetHierarchyObject(Scene scene)
        {
            sfObject obj = GetSceneObject(scene);
            if (obj != null)
            {
                foreach (sfObject child in obj.Children)
                {
                    if (child.Type == sfType.Hierarchy)
                    {
                        return child;
                    }
                }
            }
            return null;
        }

        /// <summary>Gets the scene for an sfObject.</summary>
        /// <param name="obj">obj to get scene for. Can be a scene object or a hierarchy object.</param>
        /// <returns>scene for the sfObject. Invalid scene if the sfObject has no scene.</returns>
        public Scene GetScene(sfObject obj)
        {
            if (obj == null)
            {
                return new Scene();
            }
            if (obj.Type == sfType.Hierarchy)
            {
                obj = obj.Parent;
            }
            Scene scene;
            m_objectToSceneMap.TryGetValue(obj, out scene);
            return scene;
        }

        /// <summary>Checks if we have a subscription to all scenes.</summary>
        /// <returns>true if we are subscribed to all scenes.</returns>
        public bool IsSubscribedToAllScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    return false;
                }
                sfObject obj = GetSceneObject(scene);
                if (obj == null || !obj.IsSubscribed)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Prompts the user to save the untitled scene if there is one.</summary>
        /// <returns>
        /// true if the user saved the untitled scene or there was no untitled scene. False if the user
        /// hit cancel.
        /// </returns>
        public bool PromptSaveUntitledScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (string.IsNullOrEmpty(scene.path))
                {
                    return PromptSaveUntitledScene(scene);
                }
            }
            return true;
        }

        /// <summary>Prompts the user to save the untitled scene.</summary>
        /// <param name="untitledScene">untitledScene to save.</param>
        /// <returns>true if the user saved the scene. False if they hit cancel.</returns>
        private bool PromptSaveUntitledScene(Scene untitledScene)
        {
            return EditorUtility.DisplayDialog("Save Scene", "Untitled scenes must be saved to use Scene Fusion.",
                "Save", "Cancel") && EditorSceneManager.SaveScene(untitledScene);
        }

        /// <summary>Handles the creation of a scene, scene lock, or hierarchy sfObject.</summary>
        /// <param name="obj">obj that was created.</param>
        /// <param name="childIndex">childIndex of the object. -1 if the object is a root.</param>
        public override void OnCreate(sfObject obj, int childIndex)
        {
            switch (obj.Type)
            {
                case sfType.SceneLock:
                {
                    m_lockObject = obj;
                    // If we have levels to upload, request a lock and upload the levels once we have the lock.
                    if (m_uploadList.Count > 0)
                    {
                        m_lockObject.RequestLock();
                    }
                    break;
                }
                case sfType.Scene:
                {
                    if (!m_loadedInitialScenes)
                    {
                        // Delay loading the initial scenes until we have all the scene objects, so we know if we have
                        // any of the synced scenes open or if we need to load and subscribed to the first one.
                        m_loadList.Add(obj);
                    }
                    else
                    {
                        OnCreateScene(obj);
                    }
                    break;
                }
                case sfType.Hierarchy:
                {
                    EditorUtility.DisplayProgressBar("Scene Fusion", "Syncing scene data.", 0.0f);
                    try
                    {
                        OnCreateHierarchy(obj);
                    }
                    finally
                    {
                        EditorUtility.ClearProgressBar();
                    }
                    break;
                }
            }
        }

        /// <summary>Called when a scene is closed by another user. Closes the scene locally.</summary>
        /// <param name="obj">obj that was deleted.</param>
        public override void OnDelete(sfObject obj)
        {
            Scene scene;
            if (!m_objectToSceneMap.TryGetValue(obj, out scene))
            {
                return;
            }
            if (obj.IsSubscriptionRequested)
            {
                RemoveSubscription(obj);
            }
            m_objectToSceneMap.Remove(obj);
            m_sceneToObjectMap.Remove(scene);
            // We don't allow other users to remove the scene if someone else has it open (is subscribed to it),
            // however it is possible we may have it open when it is removed due to a race condition if we open it
            // right as it is being removed, so we need to handle this case.
            if (scene.isLoaded)
            {
                if (scene.isDirty)
                {
                    // Prompt the user to save the scene before closing it.
                    string username = obj.LockOwner == null ? "Another user" : obj.LockOwner.Name;
                    int choice = EditorUtility.DisplayDialogComplex("Save Scene", username + " removed " + scene.path +
                        ". Do you want to save the scene before removing it?", "Save and Close",
                        "Close Without Saving", "Cancel");
                    switch (choice)
                    {
                        case 0: // Save and close
                        {
                            EditorSceneManager.SaveScene(scene);
                            break;
                        }
                        case 2: // Cancel and reupload the scene
                        {
                            RequestLock();
                            m_uploadList.Add(scene);
                            return;
                        }
                    }
                }
                foreach (sfObject child in obj.Descendants)
                {
                    sfObjectMap.Get().Remove(child);
                }
            }
            EditorSceneManager.CloseScene(scene, true);
        }

        /// <summary>
        /// Called when a scene object is confirmed as deleted. Removes the scene and scene object from the scene to
        /// object and object to scene maps. If the object is a hierarchy object, calls OnConfirmDelete on the child
        /// child objects.
        /// </summary>
        /// <param name="obj">obj whose deletion was confirmed.</param>
        /// <param name="unsubscribed">
        /// true if the deletion occurred because we unsubscribed from the object's parent.
        /// </param>
        public override void OnConfirmDelete(sfObject obj, bool unsubscribed)
        {
            if (obj.Type == sfType.Hierarchy)
            {
                foreach (sfObject child in obj.Children)
                {
                    sfObjectEventDispatcher.Get().OnConfirmDelete(child, unsubscribed);
                }
            }
            else
            {
                Scene scene;
                if (m_objectToSceneMap.TryGetValue(obj, out scene))
                {
                    m_sceneToObjectMap.Remove(scene);
                    m_objectToSceneMap.Remove(obj);
                }
            }
        }

        /// <summary>Called every frame.</summary>
        /// <param name="deltaTime">deltaTime in seconds since the last frame.</param>
        private void Update(float deltaTime)
        {
            if (m_loadList.Count > 0)
            {
                LoadInitialScenes();
                m_loadedInitialScenes = true;
            }
            UploadNewScenes();
            ProcessRemovedScenes();
        }

        /// <summary>
        /// Loads the initial scenes synced for the session. If the user has none of the session scenes open, the first
        /// scene will be loaded and subscribed to, and the rest will be added to the hierarchy without being loaded.
        /// Otherwise, any session scenes the user has loaded will be subscribed to and the rest will be added to the
        /// hierarchy without being loaded. The user will be prompted to save scenes that are not in the session before
        /// they are removed.
        /// </summary>
        private void LoadInitialScenes()
        {
            bool removeScenes = false;
            // The first scene from the session is loaded as a single scene, unless we already have one of the scenes
            // from the session loaded.
            OpenSceneMode firstLoadMode = OpenSceneMode.Single;
            Scene untitledScene = new Scene();
            // Iterate the current open scenes.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (removeScenes && firstLoadMode == OpenSceneMode.AdditiveWithoutLoading && untitledScene.isLoaded)
                {
                    break;
                }
                Scene scene = SceneManager.GetSceneAt(i);
                // Check if this is an untitled scene.
                if (string.IsNullOrEmpty(scene.path))
                {
                    untitledScene = scene;
                    removeScenes = true;
                    continue;
                }
                // Check if there is an sfObject for this scene.
                bool found = false;
                for (int j = 0; j < m_loadList.Count; j++)
                {
                    sfDictionaryProperty properties = (sfDictionaryProperty)m_loadList[j].Property;
                    string path = "Assets/" + (string)properties[sfProp.Path];
                    if (ksPathUtils.Clean(scene.path) == ksPathUtils.Clean(path))
                    {
                        found = true;
                        if (scene.isLoaded)
                        {
                            // We have a scene open that has an sfObject, so the first scene should be added without
                            // being loaded.
                            firstLoadMode = OpenSceneMode.AdditiveWithoutLoading;
                        }
                        break;
                    }
                }
                if (!found)
                {
                    removeScenes = true;
                }
            }
            if (removeScenes)
            {
                // Prompt the user to save modified scenes before we remove them.
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    ksLog.Info(this, "User cancelled saving the current scene(s). Disconnecting.");
                    SceneFusion.Get().Service.LeaveSession();
                    return;
                }
                // We have to remove the untitled scene before we load scenes, because otherwise we cannot create a new
                // scene if we don't have one of the scenes from the server.
                if (untitledScene.isLoaded && string.IsNullOrEmpty(untitledScene.path) && 
                    firstLoadMode == OpenSceneMode.AdditiveWithoutLoading)
                {
                    EditorSceneManager.CloseScene(untitledScene, true);
                }
            }
            // Add the scenes from the session to the hierarchy and subscribe if they are loaded.
            for (int i = 0; i < m_loadList.Count; i++)
            {
                OnCreateScene(m_loadList[i], i == 0 ? firstLoadMode : OpenSceneMode.AdditiveWithoutLoading);
            }
            m_loadList.Clear();
            if (removeScenes && firstLoadMode == OpenSceneMode.AdditiveWithoutLoading)
            {
                // Remove scenes that don't have sfObjects.
                for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (GetSceneObject(scene) == null)
                    {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }
        }

        /// <summary>Uploads scenes in the upload list once we acquire the scene lock.</summary>
        private void UploadNewScenes()
        {
            // Wait until we acquire the lock on the lock object before uploading scenes to ensure two users don't try
            // to upload the same scene at the same time.
            if (m_lockObject != null && !m_lockObject.IsLockPending &&
                m_lockObject.LockOwner == m_session.LocalUser)
            {
                m_missingObjectCount = 0;
                m_missingPrefabCount = 0;
                foreach (Scene scene in m_uploadList)
                {
                    sfObject obj;
                    if (m_sceneToObjectMap.TryGetValue(scene, out obj))
                    {
                        if (obj.IsDeletePending)
                        {
                            // We have to wait until the delete is confirmed to reupload the scene.
                            continue;
                        }
                        if (obj.IsSyncing)
                        {
                            m_uploadList.RemoveCurrent();
                            if (scene.isLoaded)
                            {
                                Subscribe(obj);
                            }
                            continue;
                        }
                        m_sceneToObjectMap.Remove(scene);
                        m_objectToSceneMap.Remove(obj);
                    }
                    m_uploadList.RemoveCurrent();
                    UploadScene(scene);
                }
                if (m_uploadList.Count == 0)
                {
                    m_lockObject.ReleaseLock();
                }
                ShowMissingReferencesDialog();
            }
        }

        /// <summary>
        /// Re-adds removed scenes without loading them if other users have them open, otherwise deletes them from the
        /// server.
        /// </summary>
        private void ProcessRemovedScenes()
        {
            if (m_removedScenes.Count > 0 && SceneFusion.Get().Service.Session != null)
            {
                foreach (Scene scene in m_removedScenes)
                {
                    sfObject obj = GetSceneObject(scene);
                    if (obj == null)
                    {
                        return;
                    }
                    // Re-add the scene if any other users are subscribed to it.
                    if (HasSubscriptions(obj))
                    {
                        ksLog.Info(this, "Cannot remove scene because other users have it open.");
                        OnCreateScene(obj);
                    }
                    else
                    {
                        m_session.Delete(obj);
                    }
                }
                m_removedScenes.Clear();
            }
        }

        /// <summary>
        /// Called when a scene is opened. If it is opened additively, queues the scene to be uploaded. If it is opened
        /// as a single scene, disconnects from the session.
        /// </summary>
        /// <param name="scene">scene that was opened.</param>
        /// <param name="mode">mode the scene was opened with.</param>
        private void OnOpenScene(Scene scene, OpenSceneMode mode)
        {
            // Note: new scenes will have a null/empty name
            if (!scene.IsValid() || scene.isSubScene)
            {
                return;
            }
            switch (mode)
            {
                case OpenSceneMode.Additive:
                {
                    sfObject obj = GetSceneObject(scene);
                    if (obj != null)
                    {
                        Subscribe(obj);
                    }
                    // Remove the untitled scene if the user doesn't save it.
                    else if (string.IsNullOrEmpty(scene.path) && !PromptSaveUntitledScene(scene))
                    {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                    else
                    {
                        RequestLock();
                        m_uploadList.Add(scene);
                    }
                    break;
                }
                case OpenSceneMode.Single:
                {
                    ksLog.Info(this, "User opened a new scene. Disconnecting from server.");
                    SceneFusion.Get().Service.LeaveSession();
                    break;
                }
            }
        }

        /// <summary>
        /// Called when a scene is closed. Unsubscribes from the scene. If the scene was removed, adds the scene to the
        /// removed scenes list.
        /// </summary>
        /// <param name="scene">scene that was closed.</param>
        /// <param name="removed">was the scene removed or just unloaded?</param>
        private void OnCloseScene(Scene scene, bool removed)
        {
            sfObject obj = GetSceneObject(scene);
            if (obj == null)
            {
                return;
            }
            Unsubscribe(obj);
            if (removed)
            {
                m_removedScenes.Add(scene);
            }
        }

        /// <summary>
        /// Called when a scene is created by another user. Opens the scene using the given open scene mode if it is
        /// not already open. Subcribes to the scene if it is loaded. Creates the scene if it does not exist.
        /// </summary>
        /// <param name="obj">obj that was created.</param>
        /// <param name="mode">
        /// mode specifying how to open the scene -- single, additive, or additive without
        /// loading.
        /// </param>
        private void OnCreateScene(sfObject obj, OpenSceneMode mode = OpenSceneMode.AdditiveWithoutLoading)
        {
            sfDictionaryProperty properties = (sfDictionaryProperty)obj.Property;
            bool loaded = true;
            string path = "Assets/" + (string)properties[sfProp.Path];
            Scene scene = SceneManager.GetSceneByPath(path);
            if (!scene.IsValid())
            {
                try
                {
                    scene = EditorSceneManager.OpenScene(path, mode);
                }
                catch (Exception)
                {
                    loaded = false;
                }
            }
            if (!loaded || !scene.IsValid())
            {
                ksLog.Info(this, "Could not find scene '" + path + "'. Creating new scene.");
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode == OpenSceneMode.Single ?
                    NewSceneMode.Single : NewSceneMode.Additive);
                ksPathUtils.Create(path);
                EditorSceneManager.SaveScene(scene, path);
                if (mode == OpenSceneMode.AdditiveWithoutLoading)
                {
                    EditorSceneManager.CloseScene(scene, false);
                }
            }

            m_sceneToObjectMap[scene] = obj;
            m_objectToSceneMap[obj] = scene;

            if (!scene.isLoaded)
            {
                return;
            }
            Subscribe(obj);

            // Load guids for game objects
            sfGuidManager.Get().LoadGuids(scene);
            sfGameObjectTranslator translator = sfObjectEventDispatcher.Get().GetTranslator<sfGameObjectTranslator>(
                sfType.GameObject);
            // Create deterministic guids for game objects that don't already have guids
            foreach (GameObject gameObject in scene.GetRootGameObjects())
            {
                translator.CreateGuids(gameObject);
            }
        }

        /// <summary>Called when a scene hierarchy is created. Syncs the scene with the server data.</summary>
        /// <param name="obj">obj that was created.</param>
        private void OnCreateHierarchy(sfObject obj)
        {
            Scene scene = GetScene(obj);
            if (!scene.isLoaded)
            {
                return;
            }
            sfGameObjectTranslator translator = sfObjectEventDispatcher.Get().GetTranslator<sfGameObjectTranslator>(
                sfType.GameObject);

            // Sync the game objects
            foreach (sfObject childObj in obj.Children)
            {
                translator.InitializeGameObject(childObj, scene);
            }
            // Destroy unsynced game objects
            foreach (GameObject gameObject in scene.GetRootGameObjects())
            {
                if (!sfObjectMap.Get().Contains(gameObject) && translator.IsSyncable(gameObject))
                {
                    UObject.DestroyImmediate(gameObject, true);
                }
            }
            // Sync game object order
            translator.ApplyHierarchyChanges(obj);
        }

        /// <summary>Uploads a scene to the server.</summary>
        /// <param name="scene">scene to upload.</param>
        private void UploadScene(Scene scene)
        {
            if (!scene.isLoaded)
            {
                return;
            }
            if (string.IsNullOrEmpty(scene.path))
            {
                // We should never get here, so log an error if we do somehow. We require untitled scenes to be saved
                // before they can be upload to avoid problems from Unity's restriction that only one untitled scene
                // can exist at a time, which arise when a user saves an untitled scene during a session and then
                // creates a new untitled scene, but the other users didn't save the first untitled scene and now try
                // to create a second one.
                ksLog.Error(this, "Cannot upload untitled scene.");
                return;
            }
            sfDictionaryProperty properties = new sfDictionaryProperty();
            sfObject obj = new sfObject(sfType.Scene, properties, sfObject.ObjectFlags.REQUIRES_SUBSCRIPTION);
            m_sceneToObjectMap[scene] = obj;
            m_objectToSceneMap[obj] = scene;
            properties[sfProp.Path] = scene.path.Substring("Assets/".Length);
            m_localSubscriptionsProperty.Add(new sfReferenceProperty(obj.Id));

            // Create a hierarchy child object. The game objects will be children of the hierarchy object. Other
            // scene-objects such as lighting will be children of the scene object.
            sfObject hierarchyObj = new sfObject(sfType.Hierarchy);
            obj.AddChild(hierarchyObj);

            sfGuidManager.Get().LoadGuids(scene);
            sfGameObjectTranslator translator = sfObjectEventDispatcher.Get().GetTranslator<sfGameObjectTranslator>(
                sfType.GameObject);

            EditorUtility.DisplayProgressBar("Scene Fusion", "Syncing scene data.", 0.0f);
            try
            {
                sfPropertyManager.Get().OnMissingObject += IncrementMissingObjectCount;
                translator.OnMissingPrefab += IncrementMissingPrefabCount;

                if (PreUploadScene != null)
                {
                    PreUploadScene(scene, obj);
                }

                GameObject[] gameObjects = scene.GetRootGameObjects();
                for (int i = 0; i < gameObjects.Length; ++i)
                {
                    float progress = (float)i / gameObjects.Length;
                    GameObject gameObject = gameObjects[i];
                    EditorUtility.DisplayProgressBar("Scene Fusion", "Syncing scene data.", progress);
                    if (translator.IsSyncable(gameObject))
                    {
                        translator.CreateGuids(gameObject);
                        sfObject childObj = translator.CreateObject(gameObject);
                        if (childObj != null)
                        {
                            hierarchyObj.AddChild(childObj);
                        }
                    }
                }

                m_session.Create(obj);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                sfPropertyManager.Get().OnMissingObject -= IncrementMissingObjectCount;
                translator.OnMissingPrefab -= IncrementMissingPrefabCount;
            }
        }

        /// <summary>
        /// Subscribes to receive an sfObject's children, and tracks that the local user is subscribed to that object in
        /// a property on their scene subscriptions sfObject.
        /// </summary>
        /// <param name="obj">obj to subscribe to.</param>
        private void Subscribe(sfObject obj)
        {
            if (obj.IsSubscriptionRequested)
            {
                return;
            }
            obj.Subscribe();
            m_localSubscriptionsProperty.Add(new sfReferenceProperty(obj.Id));
        }

        /// <summary>
        /// Unsubscribes from receiving an sfObject's children, and removes the object from the subscriptions property
        /// on the local user's scene subscriptions sfObject.
        /// </summary>
        /// <param name="obj">obj to unsubscribe from.</param>
        private void Unsubscribe(sfObject obj)
        {
            if (!obj.IsSubscriptionRequested)
            {
                return;
            }
            obj.Unsubscribe();
            RemoveSubscription(obj);
        }

        /// <summary>
        /// Removes a reference to an object from the subscriptions list property on the local user's scene
        /// subscriptions sfObject.
        /// </summary>
        /// <param name="obj">obj to remove from subscriptions property.</param>
        private void RemoveSubscription(sfObject obj)
        {
            for (int i = 0; i < m_localSubscriptionsProperty.Count; i++)
            {
                sfReferenceProperty refProp = (sfReferenceProperty)m_localSubscriptionsProperty[i];
                if (refProp.ObjectId == obj.Id)
                {
                    m_localSubscriptionsProperty.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>
        /// Checks if any users are subscribed to the given object by checking if there are references to the object
        /// from a scene subscriptions object.
        /// </summary>
        /// <param name="obj">obj to check if any users are subscribed to.</param>
        /// <returns>if any users are subscribed to the object.</returns>
        private bool HasSubscriptions(sfObject obj)
        {
            foreach (sfReferenceProperty reference in m_session.GetReferences(obj))
            {
                if (reference.GetContainerObject().Type == sfType.SceneSubscriptions)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Requests the lock for uploading levels.</summary>
        private void RequestLock()
        {
            // If the lock object does not exist, create and lock it.
            if (m_lockObject == null && SceneFusion.Get().Service.IsSessionCreator)
            {
                m_lockObject = new sfObject(sfType.SceneLock);
                m_lockObject.RequestLock();
                m_session.Create(m_lockObject);
            }
            else if (m_lockObject != null && m_lockObject.LockOwner != m_session.LocalUser)
            {
                // This will send a lock request as soon as the lock object becomes unlocked
                m_lockObject.RequestLock();
            }
        }

        /// <summary>Increments the missing object count.</summary>
        /// <param name="uobj">uobj that references a missing object. Unused.</param>
        private void IncrementMissingObjectCount(UObject uobj)
        {
            m_missingObjectCount++;
        }

        /// <summary>Increments the missing prefab count.</summary>
        /// <param name="gameObj">prefab game object instance that is missing its prefab asset. Unused.</param>
        private void IncrementMissingPrefabCount(GameObject gameObj)
        {
            m_missingPrefabCount++;
        }

        /// <summary>
        /// If the use missing object or missing prefab counts are non-zero, shows a dialog informing the user that
        /// these references will not sync properly and asks them if they want to continue. If they say no, disconnects
        /// from the session.
        /// </summary>
        private void ShowMissingReferencesDialog()
        {
            string message = "";
            if (m_missingObjectCount > 0)
            {
                message = "Found " + m_missingObjectCount + " missing object reference" +
                    (m_missingObjectCount == 1 ? "" : "s") + ". These references cannot sync properly and will " +
                    "be synced to other users as null if you continue.\n\n";
                m_missingObjectCount = 0;
            }
            if (m_missingPrefabCount > 0)
            {
                if (m_missingPrefabCount == 1)
                {
                    message += "Found 1 prefab instance with a missing prefab asset. ";
                }
                else
                {
                    message += "Found " + m_missingPrefabCount + " prefab instances with missing prefab assets. ";
                }
                message += "These prefab instances cannot sync properly and will be synced to other users as non-" +
                    "prefabs if you continue.\n\n";
                m_missingPrefabCount = 0;
            }
            if (!string.IsNullOrEmpty(message))
            {
                if (!EditorUtility.DisplayDialog("Missing Object References",
                    message + "See logs for details. Do you want to continue anyway?", "Continue", "Cancel"))
                {
                    SceneFusion.Get().Service.LeaveSession();
                }
            }
        }
    }
}
