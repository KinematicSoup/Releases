using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using KS.SceneFusion;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>Syncs changes made by an occlusion settings undo operation.</summary>
    public class sfUndoOcclusionSettingsOperation : sfBaseUndoOperation
    {
        private Scene m_scene;
        private string m_propertyName;

        /// <summary>Constructor</summary>
        /// <param name="scene">Scene with changed occlusion settings.</param>
        /// <param name="propertyName">
        /// Name of the property that changed. If null, all properties will be checked for changes.
        /// </param>
        public sfUndoOcclusionSettingsOperation(Scene scene, string propertyName = null)
        {
            m_scene = scene;
            m_propertyName = propertyName;
        }

        /// <summary>Syncs occlusion settings changed by the undo or redo operation.</summary>
        /// <param name="isUndo">True if this is an undo operation, false if it is a redo.</param>
        public override void HandleUndoRedo(bool isUndo)
        {
            sfOcclusionTranslator translator = sfObjectEventDispatcher.Get().GetTranslator<sfOcclusionTranslator>(
                sfType.OcclusionSettings);
            // Changing the active scene does not register an undo operation, so the active scene may have changed and
            // we need to temporarily change it to the scene this operation affects.
            sfUnityUtils.WithActiveScene(m_scene, () =>
            {
                if (m_propertyName == null)
                {
                    translator.SendPropertyChanges();
                }
                else
                {
                    translator.SendPropertyChange(m_propertyName);
                }
            });
        }
    }
}
