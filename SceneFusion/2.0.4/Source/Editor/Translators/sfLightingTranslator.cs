using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using KS.SceneFusion2.Client;
using KS.SceneFusion;
using KS.SF.Reactor;
using KS.SF.Unity.Editor;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>Manages syncing of lighting properties.</summary>
    public class sfLightingTranslator : sfBaseUObjectTranslator
    {
        private EditorWindow m_lightingWindow;

        /// <summary>Initialization</summary>
        public override void Initialize()
        {
            sfSceneTranslator sceneTranslator = sfObjectEventDispatcher.Get().GetTranslator<sfSceneTranslator>(
                sfType.Scene);
            sceneTranslator.PreUploadScene += CreateLightingObjects;

            // Do not sync the 'Auto Generate' lighting setting.
            sfPropertyManager.Get().Blacklist.Add<LightingSettings>("m_GIWorkflowMode");

            PostUObjectChange.Add<UObject>((UObject uobj) => RefreshWindow());
            sfAssetTranslator translator = sfObjectEventDispatcher.Get().GetTranslator<sfAssetTranslator>(sfType.Asset);
            translator.PostUObjectChange.Add<LightingSettings>((UObject uobj) => RefreshWindow());
        }

        /// <summary>
        /// Creates sfObjects for a scene's LightingSettings and RenderSettings as child objects of the scene object.
        /// </summary>
        /// <param name="scene">scene to create lighting objects for.</param>
        /// <param name="sceneObject">sceneObject to make the lighting objects children of.</param>
        private void CreateLightingObjects(Scene scene, sfObject sceneObj)
        {
            // We can only get the lighting objects for this scene when it is the active scene.
            sfUnityUtils.WithActiveScene(scene, () =>
            {
                sceneObj.AddChild(CreateObject(GetLightmapSettings(), sfType.LightmapSettings));
                sceneObj.AddChild(CreateObject(GetRenderSettings(), sfType.RenderSettings));
            });
        }

        /// <summary>Called when a lighting object is created by another user.</summary>
        /// <param name="obj">obj that was created.</param>
        /// <param name="childIndex">childIndex of the new object. -1 if the object is a root.</param>
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
            sfUnityUtils.WithActiveScene(scene, () =>
            {
                UObject uobj = null;
                switch (obj.Type)
                {
                    case sfType.LightmapSettings: uobj = GetLightmapSettings(); break;
                    case sfType.RenderSettings: uobj = GetRenderSettings(); break;
                }
                if (uobj != null)
                {
                    sfObjectMap.Get().Add(obj, uobj);
                    sfPropertyManager.Get().ApplyProperties(uobj, (sfDictionaryProperty)obj.Property);
                }
            });
        }

        /// <summary>
        /// Called when a locally-deleted object is confirmed as deleted. Removes the object from the sfObjectMap.
        /// </summary>
        /// <param name="obj">obj that was confirmed as deleted.</param>
        /// <param name="unsubscribed">
        /// true if the deletion occurred because we unsubscribed from the object's parent.
        /// </param>
        public override void OnConfirmDelete(sfObject obj, bool unsubscribed)
        {
            sfObjectMap.Get().Remove(obj);
        }

        /// <summary>Gets the lightmap settings object for the active scene.</summary>
        private LightmapSettings GetLightmapSettings()
        {
            return new ksReflectionObject(typeof(LightmapEditorSettings))
                .GetMethod("GetLightmapSettings").Invoke() as LightmapSettings;
        }

        /// <summary>Gets the render settings object for the active scene.</summary>
        private RenderSettings GetRenderSettings()
        {
            return new ksReflectionObject(typeof(RenderSettings))
                .GetMethod("GetRenderSettings").Invoke() as RenderSettings;
        }

        /// <summary>Refreshes the lighting window.</summary>
        private void RefreshWindow()
        {
            if (m_lightingWindow == null)
            {
                m_lightingWindow = ksEditorUtils.FindWindow("LightingWindow");
                if (m_lightingWindow == null)
                {
                    return;
                }
            }
            m_lightingWindow.Repaint();
        }
    }
}
