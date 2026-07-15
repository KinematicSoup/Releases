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
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KS.SceneFusion.Client.Unity.Editor
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
