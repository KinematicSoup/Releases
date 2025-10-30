using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AnimatedValues;
using KS.SF.Reactor;
using KS.SceneFusion;
using KS.SceneFusion2.Client;
using KS.SF.Unity.Editor;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>Manages avatars for other users and sends local camera and controllers info to server.</summary>
    class sfAvatarTranslator : sfBaseTranslator
    {
        /// <summary>Mesh id.</summary>
        enum MeshId
        {
            CAMERA,
            HEAD,
            HMD,
            BODY,
            OCULUS_LEFT,
            OCULUS_RIGHT,
            VIVE
        };

        /// <summary>This class holds the scene view camera info.</summary>
        private class CameraInfo
        {
            public Vector3 Position = new Vector3();
            public Quaternion Rotation = Quaternion.identity;
            public Vector3 Pivot = new Vector3();
            public float SceneViewSize = 10f;
            public bool Orthographic = false;
            public bool In2DMode = false;
        }

        private const string CAMERA_PREFAB_NAME = "ksCam.prefab";
        private const float DISAPEAR_DISTANCE = 1.8f;
        private const float NAME_OFFSET_Y = .75f;
        private const float INTERPOLATION_FRAME_NUM = 35f;
        private const float MAX_INTERPOLATION_DISTANCE = 50f;
        private const float MAX_INTERPOLATION_ANGLE = 180f;

        /// <summary>Delegate for stop following camera.</summary>
        public delegate void OnUnfollowDelegate();

        /// <summary>sfProperty change event handler.</summary>
        /// <param name="id">id of the user whose avatar property changed</param>
        /// <param name="avatar">avatar to apply property change to.</param>
        /// <param name="property">property that changed.</param>
        public delegate void PropertyChangeHandler(uint userId, GameObject avatar, sfBaseProperty property);

        // Map of property names to custom sfProperty change event handlers
        protected Dictionary<string, PropertyChangeHandler> m_propertyChangeHandlers =
            new Dictionary<string, PropertyChangeHandler>();

        private static GameObject m_cameraPrefab = null;
        private sfCameraManager m_cameraManager = sfCameraManager.Get();

        private sfSession m_session = null;
        private sfObject m_cameraObject = null;
        private CameraInfo m_localCameraInfo = new CameraInfo();
        private Dictionary<uint, GameObject> m_userIdToCamera = new Dictionary<uint, GameObject>();
        private Dictionary<uint, GameObject> m_sfObjectIdToGameObject = new Dictionary<uint, GameObject>();
        private Dictionary<uint, Material> m_userIdToMaterial = new Dictionary<uint, Material>();
        private Dictionary<uint, CameraInfo> m_userIdToCameraInfo = new Dictionary<uint, CameraInfo>();

        public OnUnfollowDelegate OnUnfollow = null;
        private uint m_followedUserId = 0;
        private float m_interpolatingFrame = INTERPOLATION_FRAME_NUM;
        private ksVector3 m_renderPivot;
        private Quaternion m_renderRotation;
        private float m_renderSize = 0f;
        private ksVector3 m_oldPivot;
        private ksQuaternion m_oldRotation;
        private float m_oldSize = 0f;

        private ksReflectionObject m_2dModeField = null;
        private ksReflectionObject m_orthoField = null;

        /// <summary>Initialization</summary>
        public override void Initialize()
        {
            RegisterPropertyChangeHandler();
        }

        /// <summary>Registers avatar property change handler.</summary>
        private void RegisterPropertyChangeHandler()
        {
            m_propertyChangeHandlers[sfProp.Mesh] = OnMeshChange;
            m_propertyChangeHandlers[sfProp.Position] = OnPositionChange;
            m_propertyChangeHandlers[sfProp.Rotation] = OnRotationChange;
            m_propertyChangeHandlers[sfProp.Pivot] = OnPivotChange;
            m_propertyChangeHandlers[sfProp.SceneViewSize] = OnSceneViewSizeChange;
            m_propertyChangeHandlers[sfProp.Orthographic] = OnOrthographicChange;
            m_propertyChangeHandlers[sfProp.In2DMode] = OnIs2DChange;
        }

        /// <summary>Called after connecting to a session.</summary>
        public override void OnSessionConnect()
        {
            if (m_2dModeField == null || m_orthoField == null)
            {
                ksReflectionObject sceneViewType = new ksReflectionObject(typeof(SceneView));
                m_2dModeField = sceneViewType.GetField("m_2DMode");
                m_orthoField = sceneViewType.GetField("m_Ortho");
            }

            if (m_cameraPrefab == null)
            {
                m_cameraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    sfPaths.Prefabs + CAMERA_PREFAB_NAME);
            }

            m_session = SceneFusion.Get().Service.Session;
            m_session.OnUserJoin += OnUserColorChange;
            m_session.OnUserLeave += OnUserLeave;
            m_session.OnUserColorChange += OnUserColorChange;
            foreach (sfUser user in m_session.GetUsers())
            {
                OnUserColorChange(user);
            }

            sfUnityEventDispatcher.Get().OnUpdate += Update;

            if (m_cameraManager.LastActiveCamera != null)
            {
                m_localCameraInfo.Position = m_cameraManager.LastActiveCamera.transform.position;
                m_localCameraInfo.Rotation = m_cameraManager.LastActiveCamera.transform.rotation;
                sfDictionaryProperty cameraProperties = CreateAvatarProperty(
                    (int)MeshId.CAMERA,
                    m_localCameraInfo.Position,
                    m_localCameraInfo.Rotation);
                cameraProperties[sfProp.UserId] = new sfValueProperty(m_session.LocalUserId);
                SceneView view = SceneView.lastActiveSceneView;
                if (view != null)
                {
                    cameraProperties[sfProp.Pivot] = sfValueProperty.From(view.pivot);
                    cameraProperties[sfProp.SceneViewSize] = new sfValueProperty(view.size);
                    cameraProperties[sfProp.Orthographic] = new sfValueProperty(view.orthographic);
                    cameraProperties[sfProp.In2DMode] = new sfValueProperty(view.in2DMode);

                    m_localCameraInfo.Pivot = view.pivot;
                    m_localCameraInfo.SceneViewSize = view.size;
                    m_localCameraInfo.Orthographic = view.orthographic;
                    m_localCameraInfo.In2DMode = view.in2DMode;
                }
                m_cameraObject = new sfObject(sfType.Avatar, cameraProperties, sfObject.ObjectFlags.TRANSIENT);
                m_session.Create(m_cameraObject);
            }
        }

        /// <summary>Called after disconnecting from a session.</summary>
        public override void OnSessionDisconnect()
        {
            sfUnityEventDispatcher.Get().OnUpdate -= Update;
            m_session.OnUserJoin -= OnUserColorChange;
            m_session.OnUserLeave -= OnUserLeave;
            m_session.OnUserColorChange -= OnUserColorChange;
            DestroyAvatars();
            m_followedUserId = 0;
        }

        /// <summary>Called every update.</summary>
        /// <param name="deltaTime">deltaTime in seconds since the last update.</param>
        private void Update(float deltaTime)
        {
            UpdateAvatars();
            SendChange();
            MoveViewportTowardsFollowedCamera();
        }

        /// <summary>Creates a dictionary property and adds mesh, location and rotation properties.</summary>
        /// <param name="meshId"></param>
        /// <param name="location"></param>
        /// <param name="rotation"></param>
        /// <returns></returns>
        private sfDictionaryProperty CreateAvatarProperty(int meshId, Vector3 location, Quaternion rotation)
        {
            sfDictionaryProperty properties = new sfDictionaryProperty();
            properties[sfProp.Mesh] = new sfValueProperty(meshId);
            properties[sfProp.Position] = sfValueProperty.From(location);
            properties[sfProp.Rotation] = sfValueProperty.From(rotation);
            return properties;
        }

        //TODO: Need to call this before recompile.
        /// <summary>Destroys all avatars and their materials.</summary>
        private void DestroyAvatars()
        {
            foreach (GameObject avatar in m_sfObjectIdToGameObject.Values)
            {
                UObject.DestroyImmediate(avatar);
            }

            m_sfObjectIdToGameObject.Clear();
            m_userIdToCamera.Clear();
            m_userIdToCameraInfo.Clear();

            foreach (Material material in m_userIdToMaterial.Values)
            {
                UObject.DestroyImmediate(material);
            }
            m_userIdToMaterial.Clear();
            sfUI.Get().MarkSceneViewStale();
        }

        /// <summary>
        /// Hides avatars that are too close to the camera and reposition the names to face the camera.
        /// </summary>
        public void UpdateAvatars()
        {
            Camera camera = sfCameraManager.Get().LastActiveCamera;
            if (camera == null)
            {
                return;
            }
            Vector3 sceneCameraPosition = camera.transform.position;
            foreach (GameObject avatar in m_sfObjectIdToGameObject.Values)
            {
                avatar.SetActive(sfConfig.Get().UI.ShowUserCameras && 
                    (avatar.transform.position - sceneCameraPosition).magnitude > DISAPEAR_DISTANCE);
                if (avatar.activeSelf)
                {
                    TextMesh text = avatar.GetComponentInChildren<TextMesh>();
                    if (text != null)
                    {
                        text.transform.forward = (text.transform.position - camera.transform.position).normalized;
                        text.transform.position = avatar.transform.position + Vector3.up * NAME_OFFSET_Y;
                    }
                }
            }
        }

        /// <summary>Sends avatar changed to the server.</summary>
        public void SendChange()
        {
            if (m_cameraObject == null || sfCameraManager.Get().LastActiveCamera == null)
            {
                return;
            }

            sfDictionaryProperty cameraProperties = m_cameraObject.Property as sfDictionaryProperty;
            
            Vector3 position = sfCameraManager.Get().LastActiveCamera.transform.position;
            if (m_localCameraInfo.Position != position)
            {
                m_localCameraInfo.Position = position;
                cameraProperties[sfProp.Position] = sfValueProperty.From(position);
            }
            
            Quaternion rotation = sfCameraManager.Get().LastActiveCamera.transform.rotation;
            if (m_localCameraInfo.Rotation != rotation)
            {
                m_localCameraInfo.Rotation = rotation;
                cameraProperties[sfProp.Rotation] = sfValueProperty.From(rotation);
            }

            SceneView view = SceneView.lastActiveSceneView;
            if (view == null)
            {
                return;
            }
            if (m_localCameraInfo.Pivot != view.pivot)
            {
                m_localCameraInfo.Pivot = view.pivot;
                cameraProperties[sfProp.Pivot] = sfValueProperty.From(view.pivot);
            }
            if (m_localCameraInfo.SceneViewSize != view.size)
            {
                m_localCameraInfo.SceneViewSize = view.size;
                cameraProperties[sfProp.SceneViewSize] = view.size;
            }
            if (m_localCameraInfo.Orthographic != view.orthographic)
            {
                m_localCameraInfo.Orthographic = view.orthographic;
                cameraProperties[sfProp.Orthographic] = sfValueProperty.From(view.orthographic);
            }
            if (m_localCameraInfo.In2DMode != view.in2DMode)
            {
                m_localCameraInfo.In2DMode = view.in2DMode;
                cameraProperties[sfProp.In2DMode] = sfValueProperty.From(view.in2DMode);
            }
        }

        /// <summary>Called when an object is created by another user.</summary>
        /// <param name="obj">obj that was created.</param>
        /// <param name="childIndex">childIndex of new object. -1 if object is a root.</param>
        public override void OnCreate(sfObject obj, int childIndex)
        {
            obj.ForSelfAndDescendants(delegate (sfObject currentObject)
            {
                // Create avatar game object
                GameObject avatar = null;
                uint userId = GetOwnerId(currentObject);
                sfUser user = m_session.GetUser(userId);
                if (user == null)
                {
                    return false;
                }
                sfDictionaryProperty properties = currentObject.Property as sfDictionaryProperty;
                MeshId meshId = (MeshId)(int)properties[sfProp.Mesh];
                Vector3 position = properties[sfProp.Position].Cast<Vector3>();
                Quaternion rotation = properties[sfProp.Rotation].Cast<Quaternion>();
                if (meshId == MeshId.HEAD)
                {
                    //TODO: Create XR avatars
                }
                else
                {
                    avatar = CreateCameraAvatar(user.Name, position, rotation, m_userIdToMaterial[userId]);
                }
                if (avatar != null)
                {
                    m_sfObjectIdToGameObject[currentObject.Id] = avatar;
                    if (meshId == MeshId.HEAD || meshId == MeshId.CAMERA)
                    {
                        m_userIdToCamera.Add(userId, avatar);
                        CameraInfo info = new CameraInfo();
                        info.Position = properties[sfProp.Position].Cast<Vector3>();
                        info.Rotation = properties[sfProp.Rotation].Cast<Quaternion>();
                        info.Pivot = properties[sfProp.Pivot].Cast<Vector3>();
                        info.SceneViewSize = (float)properties[sfProp.SceneViewSize];
                        info.Orthographic = (bool)properties[sfProp.Orthographic];
                        info.In2DMode = (bool)properties[sfProp.In2DMode];
                        m_userIdToCameraInfo.Add(userId, info);
                    }
                }
                return true;
            });
        }

        /// <summary>
        /// Called when an object is deleted by another user.
        /// Destroys the avatar game object and removes it from the avatar dictionary.
        /// </summary>
        /// <param name="obj">obj that was deleted.</param>
        public override void OnDelete(sfObject obj)
        {
            GameObject avatar = null;
            obj.ForSelfAndDescendants(delegate (sfObject currentObject)
            {
                if (m_sfObjectIdToGameObject.TryGetValue(currentObject.Id, out avatar))
                {
                    UObject.DestroyImmediate(avatar);
                    m_sfObjectIdToGameObject.Remove(currentObject.Id);
                    sfUI.Get().MarkSceneViewStale();
                }
                return true;
            });
        }

        /// <summary>Called when an object property changes.</summary>
        /// <param name="property">property that changed.</param>
        public override void OnPropertyChange(sfBaseProperty property)
        {
            if (m_session == null)
            {
                return;
            }
            if (property.Type == sfBaseProperty.Types.VALUE)
            {
                PropertyChangeHandler handler = null;
                if (m_propertyChangeHandlers.TryGetValue(property.Name, out handler))
                {
                    GameObject avatar = null;
                    sfObject container = property.GetContainerObject();
                    if (m_sfObjectIdToGameObject.TryGetValue(container.Id, out avatar))
                    {
                        handler(GetOwnerId(container), avatar, property);
                    }
                }
                else
                {
                    ksLog.Warning(this, "No property change handler for " + property.Name);
                }
            }
        }

        /// <summary>
        /// Called when a user leaves the session.
        /// Destroys the user material and remove it from the material dictionary.
        /// Removes the user id from the camera avatar dictionary.
        /// </summary>
        /// <param name="user"></param>
        private void OnUserLeave(sfUser user)
        {
            Material material = null;
            if (m_userIdToMaterial.TryGetValue(user.Id, out material))
            {
                UObject.DestroyImmediate(material);
                m_userIdToMaterial.Remove(user.Id);
            }
            m_userIdToCamera.Remove(user.Id);
            m_userIdToCameraInfo.Remove(user.Id);
        }

        /// <summary>Called when a user's color changes. Updates the avatars' color.</summary>
        /// <param name="user"></param>
        private void OnUserColorChange(sfUser user)
        {
            Material material = null;
            if (!m_userIdToMaterial.TryGetValue(user.Id, out material))
            {
                material = UObject.Instantiate(sfUserMaterials.CameraMaterial);
                material.hideFlags = HideFlags.HideAndDontSave;
                material.name = "User" + user.Id + "CameraMaterial";
                m_userIdToMaterial[user.Id] = material;
            }
            if (material != null)
            {
                material.color = user.Color;
                sfUI.Get().MarkSceneViewStale();
            }
        }

        /// <summary>Called when an avatar's mesh changes. Recreates the avatars with the new mesh.</summary>
        /// <param name="id">id of the user whose avatar property changed</param>
        /// <param name="avatar">avatar to apply property change.</param>
        /// <param name="property"></param>
        private void OnMeshChange(uint userId, GameObject avatar, sfBaseProperty property)
        {
            sfObject container = property.GetContainerObject();
            int newMeshId = (int)property;
            switch ((MeshId)newMeshId)
            {
                case MeshId.CAMERA:
                {
                    string username = avatar.GetComponentInChildren<TextMesh>().text;
                    Vector3 position = avatar.transform.position;
                    Quaternion rotation = avatar.transform.rotation;
                    UObject.DestroyImmediate(avatar);
                    avatar = CreateCameraAvatar(username, position, rotation, m_userIdToMaterial[userId]);
                    m_sfObjectIdToGameObject[container.Id] = avatar;
                    m_userIdToCamera[userId] = avatar;
                    sfUI.Get().MarkSceneViewStale();
                    break;
                }
                case MeshId.HEAD:
                {
                    // TODO
                    break;
                }
                default:
                {
                    // TODO
                    break;
                }
            }
        }

        /// <summary>Called when an avatar's position changes. Moves the avatar game object.</summary>
        /// <param name="id">id of the user whose avatar property changed</param>
        /// <param name="avatar">avatar to apply property change.</param>
        /// <param name="property"></param>
        private void OnPositionChange(uint userId, GameObject avatar, sfBaseProperty property)
        {
            avatar.transform.position = property.Cast<Vector3>();
            m_userIdToCameraInfo[userId].Position = avatar.transform.position;
            if (m_followedUserId == userId)
            {
                StartFollowing();
            }
            sfUI.Get().MarkSceneViewStale();
        }

        /// <summary>Called when an avatar's rotation changes. Rotates the avatar game object.</summary>
        /// <param name="id">id of the user whose avatar property changed</param>
        /// <param name="avatar">avatar to apply property change.</param>
        /// <param name="property"></param>
        private void OnRotationChange(uint userId, GameObject avatar, sfBaseProperty property)
        {
            avatar.transform.rotation = property.Cast<Quaternion>();
            m_userIdToCameraInfo[userId].Rotation = avatar.transform.rotation;
            if (m_followedUserId == userId)
            {
                StartFollowing();
            }
            sfUI.Get().MarkSceneViewStale();
        }

        /// <summary>Called when the scene view pivot changes.</summary>
        /// <param name="id">id of the user whose avatar property changed</param>
        /// <param name="avatar">avatar to apply property change.</param>
        /// <param name="property"></param>
        private void OnPivotChange(uint userId, GameObject avatar, sfBaseProperty property)
        {
            m_userIdToCameraInfo[userId].Pivot = property.Cast<Vector3>();
            if (m_followedUserId == userId)
            {
                StartFollowing();
            }
        }

        /// <summary>Called when the scene view size changes.</summary>
        /// <param name="id">id of the user whose avatar property changed</param>
        /// <param name="avatar">avatar to apply property change.</param>
        /// <param name="property"></param>
        private void OnSceneViewSizeChange(uint userId, GameObject avatar, sfBaseProperty property)
        {
            m_userIdToCameraInfo[userId].SceneViewSize = (float)property;
            if (m_followedUserId == userId)
            {
                StartFollowing();
            }
        }

        /// <summary>Called when another user's scene view enters/leaves the ortheographic mode.</summary>
        /// <param name="id">id of the user whose avatar property changed</param>
        /// <param name="avatar">avatar to apply property change.</param>
        /// <param name="property"></param>
        private void OnOrthographicChange(uint userId, GameObject avatar, sfBaseProperty property)
        {
            m_userIdToCameraInfo[userId].Orthographic = (bool)property;
            if (m_followedUserId == userId && SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.orthographic = (bool)property;
            }
        }

        /// <summary>Called when another user's scene view enters/leaves the 2D mode.</summary>
        /// <param name="id">id of the user whose avatar property changed</param>
        /// <param name="avatar">avatar to apply property change.</param>
        /// <param name="property"></param>
        private void OnIs2DChange(uint userId, GameObject avatar, sfBaseProperty property)
        {
            m_userIdToCameraInfo[userId].In2DMode = (bool)property;
            if (m_followedUserId == userId && SceneView.lastActiveSceneView != null)
            {
                m_2dModeField.SetValue(SceneView.lastActiveSceneView, (bool)property);
            }
        }

        /// <summary>Gets owner's id of the given object.</summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        private uint GetOwnerId(sfObject obj)
        {
            while (obj.Parent != null)
            {
                obj = obj.Parent;
            }
            sfDictionaryProperty properties = obj.Property as sfDictionaryProperty;
            return (uint)properties[sfProp.UserId];
        }

        /// <summary>Creates and returns the camera avatar game object.</summary>
        /// <param name="username"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="material"></param>
        /// <returns></returns>
        private GameObject CreateCameraAvatar(
            string username,
            Vector3 position,
            Quaternion rotation,
            Material material)
        {
            if (m_cameraPrefab == null)
            {
                return null;
            }
            GameObject avatar = UObject.Instantiate(m_cameraPrefab);
            avatar.name = "PlayerCamera";
            // Each object needs hideflags.HideAndDontSave to prevent them from being selectable.
            foreach (GameObject gameObject in sfUnityUtils.IterateSelfAndDescendants(avatar))
            {
                gameObject.hideFlags = HideFlags.HideAndDontSave;
            }
            MeshRenderer playerCameraMeshRenderer = avatar.GetComponent<MeshRenderer>();
            if (playerCameraMeshRenderer == null)
            {
                playerCameraMeshRenderer = avatar.AddComponent<MeshRenderer>();
            }
            playerCameraMeshRenderer.sharedMaterial = material;
            avatar.transform.position = position;
            avatar.transform.rotation = rotation;
            TextMesh nameTextMesh = avatar.GetComponentInChildren<TextMesh>();
            if (nameTextMesh != null)
            {
                nameTextMesh.text = username;
                sfTextMeshOutline outline = nameTextMesh.GetComponent<sfTextMeshOutline>();
                if (outline != null)
                {
                    outline.CreateOutline();
                }
            }
            return avatar;
        }

        /// <summary>Called when the follow checkbox is checked or unchecked.</summary>
        /// <param name="id">id of the user to follow or unfollow</param>
        /// <param name="isFollowing"></param>
        /// <returns>followed user id. 0 means not following any user.</returns>
        public uint OnFollow(uint userId, bool isFollowing)
        {
            if (m_followedUserId != userId && m_userIdToCameraInfo.ContainsKey(userId))
            {
                m_followedUserId = userId;
                CameraInfo info = null;
                if (m_userIdToCameraInfo.TryGetValue(m_followedUserId, out info) &&
                    SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.orthographic = info.Orthographic;
                    SceneView.lastActiveSceneView.in2DMode = info.In2DMode;
                }
                StartFollowing();
                return userId;
            }
            else
            {
                OnGoTo(userId);
                return 0;
            }
        }

        /// <summary>Called when the go to button is clicked.</summary>
        /// <param name="id">id of the user to go to</param>
        public void OnGoTo(uint userId)
        {
            m_followedUserId = 0;
            m_interpolatingFrame = -1;

            CameraInfo info = null;
            if (m_userIdToCameraInfo.TryGetValue(userId, out info))
            {
                m_cameraManager.SceneViewLookAt(
                    info.Pivot,
                    info.Rotation,
                    info.SceneViewSize,
                    info.Orthographic,
                    false);
            }
        }

        /// <summary>Starts camera following.</summary>
        private void StartFollowing()
        {
            m_interpolatingFrame = 0;
            CameraInfo info = null;
            if(m_userIdToCameraInfo.TryGetValue(m_followedUserId, out info))
            {
                m_oldPivot = info.Pivot;
                m_oldRotation = info.Rotation;
                m_oldSize = info.SceneViewSize;
            }
        }

        /// <summary>Stops camera following.</summary>
        private void StopFollowing()
        {
            m_followedUserId = 0;
            if (OnUnfollow != null)
            {
                OnUnfollow();
            }
        }

        /// <summary>Moves the scene view camera towards the followed camera.</summary>
        private void MoveViewportTowardsFollowedCamera()
        {
            SceneView view = SceneView.lastActiveSceneView;
            CameraInfo info = null;
            if (view == null || !m_userIdToCameraInfo.TryGetValue(m_followedUserId, out info))
            {
                return;
            }

            // Hide followed camera
            GameObject cameraToFollow = null;
            if (m_userIdToCamera.TryGetValue(m_followedUserId, out cameraToFollow))
            {
                cameraToFollow.SetActive(false);
            }

            // Stop following if the user changed the local camera
            if (m_interpolatingFrame > 0f &&
                (view.pivot != m_renderPivot ||
                    view.rotation != m_renderRotation ||
                    view.size != m_renderSize ||
                    view.in2DMode != info.In2DMode ||
                    ((AnimBool)m_orthoField.GetValue(view)).target != info.Orthographic))
            {
                StopFollowing();
                return;
            }

            Transform sceneViewCameraTransform = view.camera.transform;
            if (m_interpolatingFrame == 0f)
            {
                bool snap = false;
                if (Vector3.Distance(m_oldPivot, info.Pivot) > MAX_INTERPOLATION_DISTANCE)
                {
                    snap = true;
                }
                else
                {
                    float angle = ksQuaternion.DeltaDegrees(m_oldRotation, info.Rotation);
                    if (angle > MAX_INTERPOLATION_ANGLE)
                    {
                        snap = true;
                    }
                }

                if (snap)
                {
                    m_cameraManager.SceneViewLookAt(
                        info.Pivot,
                        info.Rotation,
                        info.SceneViewSize,
                        info.Orthographic,
                        true);
                    m_interpolatingFrame = INTERPOLATION_FRAME_NUM + 1f;
                    return;
                }
            }

            if (m_interpolatingFrame <= INTERPOLATION_FRAME_NUM)
            {
                m_interpolatingFrame++;
                float t = m_interpolatingFrame / INTERPOLATION_FRAME_NUM;
                m_renderPivot = Vector3.Slerp(
                    m_oldPivot,
                    info.Pivot,
                    t);
                m_renderRotation = Quaternion.Slerp(
                    m_oldRotation,
                    info.Rotation,
                    t);
                m_renderSize = Mathf.Lerp(
                    m_oldSize,
                    SceneView.lastActiveSceneView.size,
                    t);

                m_cameraManager.SceneViewLookAt(
                    m_renderPivot,
                    m_renderRotation,
                    m_renderSize,
                    SceneView.lastActiveSceneView.orthographic,
                    true);
                SceneView.RepaintAll();
            }
            else if (sceneViewCameraTransform.position != info.Position
                    || sceneViewCameraTransform.rotation != info.Rotation
                    || view.orthographic != info.Orthographic
                    || view.in2DMode != info.In2DMode
                    )
            {
                m_cameraManager.SceneViewLookAt(
                    info.Pivot,
                    info.Rotation,
                    info.SceneViewSize,
                    info.Orthographic,
                    true);
                SceneView.RepaintAll();
            }
        }
    }
}
