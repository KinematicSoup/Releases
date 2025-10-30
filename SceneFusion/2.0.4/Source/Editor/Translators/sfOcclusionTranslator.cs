using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using KS.SceneFusion2.Client;
using KS.SceneFusion;
using KS.SF.Reactor;
using KS.SF.Unity.Editor;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>
    /// Manages syncing of occlusion settings. Each scene <see cref="sfObject"/> gets a child <see cref="sfObject"/>
    /// containing the occlusion settings for the scene.
    /// </summary>
    public class sfOcclusionTranslator : sfBaseTranslator
    {
        /// <summary>
        /// How often in seconds to check for changes to occlusion settings. We only check for changes when the most
        /// recent operation on the undo stack modified occlusion settings as needed to detect changes from clicking
        /// and dragging the field labels.
        /// </summary>
        private const float POLL_CHANGE_INTERVAL = .1f;

        // Maps scenes to occlusion settings sfObjects.
        private Dictionary<Scene, sfObject> m_sceneToObjectMap = new Dictionary<Scene, sfObject>();
        private string m_pollingPropertyName;
        private Scene m_pollingScene;
        private float m_pollChangeTimer;
        private ksReflectionObject m_roOcclusionWindow;
        private EditorWindow m_occlusionWindow;
        private bool m_occlusionWindowStale = false;

        /// <summary>Initialization</summary>
        public override void Initialize()
        {
            sfSceneTranslator sceneTranslator = sfObjectEventDispatcher.Get().GetTranslator<sfSceneTranslator>(
                sfType.Scene);
            sceneTranslator.PreUploadScene += CreateOcclusionObject;

            m_roOcclusionWindow = new ksReflectionObject(typeof(EditorWindow).Assembly,
                "UnityEditor.OcclusionCullingWindow").GetField("ms_OcclusionCullingWindow");
        }

        /// <summary>Called after connecting to a session. Registers event handlers.</summary>
        public override void OnSessionConnect()
        {
            sfUndoManager.Get().OnRegisterUndo += OnRegisterUndo;
            EditorSceneManager.sceneClosed += OnCloseScene;
        }

        /// <summary>
        /// Called after disconnecting from a session. Unregsiters event handlers and clears the scene to object map.
        /// </summary>
        public override void OnSessionDisconnect()
        {
            sfUndoManager.Get().OnRegisterUndo -= OnRegisterUndo;
            EditorSceneManager.sceneClosed -= OnCloseScene;
            m_sceneToObjectMap.Clear();
        }

        /// <summary>
        /// Creates the occlusion settings <see cref="sfObject"/> for a scene and adds it as a child of the scene's
        /// <see cref="sfObject"/>.
        /// </summary>
        /// <param name="scene">Scene to create occlusion settings <see cref="sfObject"/> for.</param>
        /// <param name="sceneObj"><see cref="sfObject"/> for the scene.</param>
        private void CreateOcclusionObject(Scene scene, sfObject sceneObj)
        {
            // We an only access the occlusion settings for the active scene.
            sfUnityUtils.WithActiveScene(scene, () =>
            {
                sfDictionaryProperty properties = new sfDictionaryProperty();
                properties[sfProp.SmallestOccluder] = StaticOcclusionCulling.smallestOccluder;
                properties[sfProp.SmallestHole] = StaticOcclusionCulling.smallestHole;
                properties[sfProp.BackfaceThreshold] = StaticOcclusionCulling.backfaceThreshold;
                sfObject obj = new sfObject(sfType.OcclusionSettings, properties);
                m_sceneToObjectMap[scene] = obj;
                sceneObj.AddChild(obj);
            });
        }


        /// <summary>
        /// Called when an occlusion settings object is created by another user. Applies the occlusion settings to the
        /// scene it is for.
        /// </summary>
        /// <param name="obj">Object that was created.</param>
        /// <param name="childIndex">Child index of the new object. -1 if the object is a root.</param>
        public override void OnCreate(sfObject obj, int childIndex)
        {
            if (obj.Parent == null)
            {
                ksLog.Warning(this, obj.Type + " object has no parent.");
                return;
            }
            sfSceneTranslator translator = sfObjectEventDispatcher.Get()
               .GetTranslator<sfSceneTranslator>(sfType.Scene);
            Scene scene = translator.GetScene(obj.Parent);
            m_sceneToObjectMap[scene] = obj;
            sfUnityUtils.WithActiveScene(scene, () =>
            {
                sfDictionaryProperty properties = (sfDictionaryProperty)obj.Property;
                StaticOcclusionCulling.smallestOccluder = (float)properties[sfProp.SmallestOccluder];
                StaticOcclusionCulling.smallestHole = (float)properties[sfProp.SmallestHole];
                StaticOcclusionCulling.backfaceThreshold = (float)properties[sfProp.BackfaceThreshold];
            });
            MarkOcclusionWindowStale(scene);
        }

        /// <summary>
        /// Called when an occlusion property is changed by another user. Applies the property change to the occlusion
        /// settings.
        /// </summary>
        /// <param name="property">Property that changed</param>
        public override void OnPropertyChange(sfBaseProperty property)
        {
            sfSceneTranslator translator = sfObjectEventDispatcher.Get()
               .GetTranslator<sfSceneTranslator>(sfType.Scene);
            Scene scene = translator.GetScene(property.GetContainerObject().Parent);
            sfUnityUtils.WithActiveScene(scene, () =>
            {
                switch (property.Name)
                {
                    case sfProp.SmallestOccluder: StaticOcclusionCulling.smallestOccluder = (float)property; break;
                    case sfProp.SmallestHole: StaticOcclusionCulling.smallestHole = (float)property; break;
                    case sfProp.BackfaceThreshold: StaticOcclusionCulling.backfaceThreshold = (float)property; break;
                }
            });
            MarkOcclusionWindowStale(scene);
        }

        /// <summary>
        /// Sends an occlusion property update for the active scene to the server if the property value changed.
        /// </summary>
        /// <param name="name">Name of property to update.</param>
        public void SendPropertyChange(string name)
        {
            sfBaseProperty property;
            switch (name)
            {
                case sfProp.SmallestOccluder: property = StaticOcclusionCulling.smallestOccluder; break;
                case sfProp.SmallestHole: property = StaticOcclusionCulling.smallestHole; break;
                case sfProp.BackfaceThreshold: property = StaticOcclusionCulling.backfaceThreshold; break;
                default: return;
            }
            sfObject obj;
            if (m_sceneToObjectMap.TryGetValue(SceneManager.GetActiveScene(), out obj))
            {
                sfDictionaryProperty properties = (sfDictionaryProperty)obj.Property;
                sfPropertyManager.Get().UpdateDictProperty(properties, name, property);
            }
        }

        /// <summary>Sends all changed occlusion properties for the active scene to the server.</summary>
        public void SendPropertyChanges()
        {
            sfObject obj;
            if (m_sceneToObjectMap.TryGetValue(SceneManager.GetActiveScene(), out obj))
            {
                sfDictionaryProperty properties = (sfDictionaryProperty)obj.Property;
                sfPropertyManager.Get().UpdateDictProperty(properties, sfProp.SmallestOccluder,
                    StaticOcclusionCulling.smallestOccluder);
                sfPropertyManager.Get().UpdateDictProperty(properties, sfProp.SmallestHole,
                    StaticOcclusionCulling.smallestHole);
                sfPropertyManager.Get().UpdateDictProperty(properties, sfProp.BackfaceThreshold,
                    StaticOcclusionCulling.backfaceThreshold);
            }
        }

        /// <summary>Called when a scene is closed. Removes the scene from the scene to object map.</summary>
        /// <param name="scene">Scene that closed.</param>
        private void OnCloseScene(Scene scene)
        {
            m_sceneToObjectMap.Remove(scene);
        }

        /// <summary>
        /// Called when an undo operation is registered on the undo stack. Syncs changes to the active scene's
        /// occlusion settings made from the operation. Unity doesn't have events for when occlusion settings change,
        /// so we detect changes this way instead.
        /// </summary>
        private void OnRegisterUndo()
        {
            string name;
            switch (Undo.GetCurrentGroupName())
            {
                case "Change Smallest Occluder": name = sfProp.SmallestOccluder; break;
                case "Change Smallest Hole": name = sfProp.SmallestHole; break;
                case "Change Backface Threshold": name = sfProp.BackfaceThreshold; break;
                case "Set Default Parameters":
                {
                    // The "Set Default Parameters" button was clicked which resets all properties to their default
                    // values.
                    if (m_pollingPropertyName != null)
                    {
                        // Stop polling for changes.
                        m_pollingPropertyName = null;
                        sfUnityEventDispatcher.Get().PreUpdate -= PollChanges;
                    }
                    // Check all properties for changes.
                    SendPropertyChanges();
                    sfUndoManager.Get().Record(new sfUndoOcclusionSettingsOperation(SceneManager.GetActiveScene()));
                    return;
                }
                default:
                {
                    if (m_pollingPropertyName != null)
                    {
                        // Stop polling for changes.
                        SendPropertyChange(m_pollingPropertyName);
                        m_pollingPropertyName = null;
                        sfUnityEventDispatcher.Get().PreUpdate -= PollChanges;
                    }
                    return;
                }
            }
            // If the user clicks and drags a property label, they can continue changing the property without
            // registering further undo operations on the undo stack, so we need to poll for changes until another undo
            // operation is registered on the stack.
            if (m_pollingPropertyName == null)
            {
                sfUnityEventDispatcher.Get().PreUpdate += PollChanges;
            }
            else if (m_pollingPropertyName != name)
            {
                SendPropertyChange(m_pollingPropertyName);
            }
            SendPropertyChange(name);
            m_pollingPropertyName = name;
            m_pollingScene = SceneManager.GetActiveScene();
            m_pollChangeTimer = POLL_CHANGE_INTERVAL;
            sfUndoManager.Get().Record(new sfUndoOcclusionSettingsOperation(m_pollingScene, name));
        }

        /// <summary>
        /// Updates the poll change timer and checks for and sends changes if it is time to poll changes.
        /// </summary>
        /// <param name="deltaTime">Time in seconds since the last update.</param>
        private void PollChanges(float deltaTime)
        {
            m_pollChangeTimer -= deltaTime;
            if (m_pollChangeTimer <= 0f)
            {
                m_pollChangeTimer = POLL_CHANGE_INTERVAL;
                sfUnityUtils.WithActiveScene(m_pollingScene, ()=> SendPropertyChange(m_pollingPropertyName));
            }
        }

        /// <summary>
        /// Redraws the occlusion window at the end of the frame if <paramref name="scene"/> is the active scene.
        /// </summary>
        /// <param name="scene">Redraw the window if this scene is active.</param>
        private void MarkOcclusionWindowStale(Scene scene)
        {
            if (!m_occlusionWindowStale && scene == SceneManager.GetActiveScene())
            {
                m_occlusionWindowStale = true;
                EditorApplication.update += RefreshWindow;
            }
        }

        /// <summary>Redraws the occlusion settings window.</summary>
        private void RefreshWindow()
        {
            EditorApplication.update -= RefreshWindow;
            m_occlusionWindowStale = false;
            if (m_occlusionWindow == null)
            {
                m_occlusionWindow = m_roOcclusionWindow.GetValue() as EditorWindow;
                if (m_occlusionWindow == null)
                {
                    return;
                }
            }
            m_occlusionWindow.Repaint();
        }
    }
}