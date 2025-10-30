using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using KS.SceneFusion2.Client;
using KS.SceneFusion;
using KS.SF.Unity.Editor;
using KS.SF.Reactor;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>
    /// Overrides the game object inspector to disable prefab editing for locked prefabs or when
    /// <see cref="sfPrefabLocker.AllowSavingPrefabs"/> is false and show a message in the inspector saying prefab
    /// editing is disabled. Shows notifications for the target gameobject and its components.
    /// </summary>
    [CustomEditor(typeof(GameObject))]
    [CanEditMultipleObjects]
    class sfGameObjectEditor : ksOverrideEditor
    {
        /// <summary>Holds state that persists through Unity serializations.</summary>
        private class State : ksSingleton<State>
        {
            /// <summary>Is the notification list expanded?</summary>
            public bool NotificationsExpanded = true;
        }

        private ksReflectionObject m_roOnSceneDrag;
        private static List<GameObject> m_lockedPrefabs = new List<GameObject>();
        // Set of components on locked prefabs that were already non-editable. We don't want to remove the non-editable
        // hideflag from these components.
        private static HashSet<Component> m_nonEditableComponents = new HashSet<Component>();

        /// <summary>Unlocks locked prefabs.</summary>
        private static void UnlockPrefabs()
        {
            foreach (GameObject prefab in m_lockedPrefabs)
            {
                if (prefab != null)
                {
                    sfUnityUtils.RemoveFlags(prefab, HideFlags.NotEditable, m_nonEditableComponents);
                }
            }
            m_lockedPrefabs.Clear();
            m_nonEditableComponents.Clear();
        }

        /// <summary>Initialization</summary>
        protected override void OnEnable()
        {
            LoadBaseEditor("GameObjectInspector");
            m_roOnSceneDrag = ReflectionEditor.GetMethod("OnSceneDrag");
        }

        /// <summary>Calls the overridden inspector's OnDisable.</summary>
        protected override void OnDisable()
        {
            // Sometimes the name property's serialized object is invalid and when we destroy the base editor it will
            // log an uncatchable null reference exception, so we check if it is valid first.
            ksReflectionObject roName = ReflectionEditor.GetField("m_Name");
            GameObject temp = null;
            if (!ValidateSerializedPropertyPtrs(roName))
            {
                // If the property is invalid, we create a temporary game object and set the property to the temporary
                // object's name property so we won't get an error when we destroy the base editor.
                temp = new GameObject("#sfTemp");
                temp.hideFlags = HideFlags.HideAndDontSave;
                roName.SetValue(new SerializedObject(temp).FindProperty("m_Name"));
            }
            base.OnDisable();
            if (temp != null)
            {
                DestroyImmediate(temp);
            }
        }

        /// <summary>
        /// Checks that a reflection object holds a serialized property with a non-zero native pointer, and the
        /// property's serialized object is not null and has a non-zero native pointer.
        /// </summary>
        /// <param name="roSerializedProperty">Reflection object for the serialized property to validate.</param>
        /// <returns>True if the serialized property contained in the reflection object has valid pointers.</returns>
        private bool ValidateSerializedPropertyPtrs(ksReflectionObject roSerializedProperty)
        {
            try
            {
                SerializedObject so = ((SerializedProperty)roSerializedProperty.GetValue()).serializedObject;
                if (so == null)
                {
                    return false;
                }
                if (((IntPtr)roSerializedProperty.GetField("m_NativePropertyPtr").GetValue()) == IntPtr.Zero)
                {
                    return false;
                }
                if (((IntPtr)new ksReflectionObject(so).GetField("m_NativeObjectPtr").GetValue()) == IntPtr.Zero)
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Draws the game object header GUI. Locks prefab targets if <see cref="sfPrefabLocker.AllowSavingPrefabs"/>
        /// is false or if their sfObject's are locked.
        /// </summary>
        protected override void OnHeaderGUI()
        {
            // If there are multiple inspectors open and one is viewing a locked prefab and the other is viewing an
            // instance of that prefab and the user reverts the prefab instance overrides, the prefab instance will
            // also become locked unless we unlock the prefabs locked by the other inspector, which we do here.
            if (m_lockedPrefabs.Count > 0)
            {
                EditorApplication.delayCall -= UnlockPrefabs;
                UnlockPrefabs();
            }
            bool lockedPrefabs = GUI.enabled && LockPrefabs();
            base.OnHeaderGUI();
            if (lockedPrefabs && !sfPrefabLocker.Get().AllowSavingPrefabs)
            {
                if (ksStyle.HelpBox(MessageType.Info,
                    "Prefab editing during a Scene Fusion session is not allowed when prefab syncing is disabled. " +
                    "To enable prefab syncing, end the session, open the ", null,
                    "Scene Fusion settings", "",
                    ", check the 'Sync Prefabs' checkbox and start a new session.", null) >= 0)
                {
                    sfInitializer.OpenSettings();
                }
                EditorGUILayout.Space(2f);
            }
            DrawNotifications();
        }

        /// <summary>
        /// Iterates the targets looking for prefabs and locks them if their sfObject is locked or if 
        /// <see cref="sfPrefabLocker.AllowSavingPrefabs"/> is false. Prefabs assets will be unlocked after the
        /// inspector is drawn.
        /// </summary>
        /// <returns>True if any of the targets were locked prefabs</returns>
        private bool LockPrefabs()
        {
            // If we are syncing prefabs, we don't track which components were already non-editable as we control the
            // not-editable hideflags on synced objects and their components.
            HashSet<Component> nonEditableComponents = 
                sfPrefabLocker.Get().AllowSavingPrefabs ? null : m_nonEditableComponents;
            bool locked = false;
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            foreach (GameObject gameObject in targets)
            {
                if (ShouldLock(gameObject))
                {
                    locked = true;
                    sfUnityUtils.AddFlags(gameObject, HideFlags.NotEditable, nonEditableComponents);
                    m_lockedPrefabs.Add(gameObject);
                    if (m_lockedPrefabs.Count == 1)
                    {
                        // Unlock the prefabs after inspectors are drawn. This is needed to prevent prefab instances
                        // from being locked when they are created or reverted.
                        EditorApplication.delayCall += UnlockPrefabs;
                    }
                }
                else if (!sfPrefabLocker.Get().AllowSavingPrefabs && stage != null &&
                    stage.IsPartOfPrefabContents(gameObject))
                {
                    locked = true;
                    if ((gameObject.hideFlags & HideFlags.NotEditable) == 0)
                    {
                        // Lock all game objects in the prefab stage. These are temporary objects that get destroyed
                        // when the prefab stage is closed. They do not need to be unlocked.
                        foreach (GameObject prefab in sfUnityUtils.IterateSelfAndDescendants(stage.prefabContentsRoot))
                        {
                            sfUnityUtils.AddFlags(prefab, HideFlags.NotEditable);
                        }
                    }
                }
            }
            return locked;
        }

        /// <summary>
        /// Checks if a game object should be locked (edits disabled). It should be locked if:
        ///  - <see cref="sfPrefabLocker.AllowSavingPrefabs"/> is false and the game object is a prefab asset.
        ///  - The game object is a prefab asset (or stage object for a prefab asset) and it's sfObject is locked.
        /// </summary>
        /// <param name="gameObject">Game object to check.</param>
        /// <returns>True if the game object should be locked.</returns>
        private bool ShouldLock(GameObject gameObject)
        {
            if (!sfPrefabLocker.Get().AllowSavingPrefabs)
            {
                return PrefabUtility.IsPartOfPrefabAsset(gameObject) && 
                    (gameObject.hideFlags & HideFlags.NotEditable) == 0;
            }
            if (!PrefabUtility.IsPartOfPrefabAsset(gameObject))
            {
                gameObject = sfPrefabStageMap.Get().GetPrefabObject(gameObject);
                if (gameObject == null)
                {
                    return false;
                }
            }
            sfObject obj = sfObjectMap.Get().GetSFObject(gameObject);
            return obj != null && obj.IsLocked;
        }

        /// <summary>
        /// Draws notifications for the game object and its components. Does nothing if multiple objects are selected.
        /// </summary>
        private void DrawNotifications()
        {
            if (targets.Length != 1)
            {
                return;
            }
            GameObject gameObject = target as GameObject;
            if (gameObject == null)
            {
                return;
            }
            // If the game object is a stage object, get the prefab object instead.
            GameObject prefab = sfPrefabStageMap.Get().GetPrefabObject(gameObject);
            if (prefab != null)
            {
                gameObject = prefab;
            }
            ksLinkedList<sfNotification> notifications =
                        sfNotificationManager.Get().GetNotifications(gameObject, true);
            if (notifications == null || notifications.Count == 0)
            {
                return;
            }
            Rect rect = EditorGUILayout.GetControlRect();
            // This makes the foldout arrow line up with the component foldout arrows.
            rect.x += 1f;
            rect.width -= 1f;
            // The spaces makes the label line up with the component labels.
            GUIContent content = new GUIContent("       Scene Fusion Notifications (" + notifications.Count + ")", 
                ksStyle.GetHelpBoxIcon(MessageType.Warning));
            State state = State.Get();
            state.NotificationsExpanded = EditorGUI.BeginFoldoutHeaderGroup(rect, state.NotificationsExpanded, content);
            if (state.NotificationsExpanded)
            {
                EditorGUI.indentLevel++;
                foreach (sfNotification notification in notifications)
                {
                    EditorGUILayout.HelpBox(notification.Message, MessageType.None);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space(2f);
        }

        /// <summary>
        /// This is an undocumented Unity message function that is needed for dragging objects into the scene to work.
        /// </summary>
        /// <param name="sceneView"></param>
        /// <param name="index"></param>
        public void OnSceneDrag(SceneView sceneView, int index)
        {
            m_roOnSceneDrag.Invoke(sceneView, index);
        }
    }
}
