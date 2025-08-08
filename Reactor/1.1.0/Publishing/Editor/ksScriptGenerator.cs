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
using System.IO;
using UnityEditor;
using UnityEngine;
using KS.Unity.Editor;

#if UNITY_2019
using UnityEditor.Experimental.SceneManagement;
#else
using UnityEditor.SceneManagement;
#endif

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Provides methods for generating scripts that can be used by script templates.</summary>
    public class ksScriptGenerator : ksSingleton<ksScriptGenerator>
    {
        [SerializeField]
        private string m_newScript;

        [SerializeField]
        private GameObject[] m_instances;

        /// <summary>
        /// Called when Unity starts. Sometimes we need to attach newly created scripts to game objects, but we cannot
        /// attach the script until Unity compiles it, and when Unity compiles scripts we lose any non-serialized
        /// state. This function attaches scripts to game objects based on serialized data written before scripts were
        /// recompiled.
        /// </summary>
        public void LoadAttachments()
        {
            if (m_newScript == null || m_newScript == "")
            {
                return;
            }
            try
            {
                MonoScript script = (MonoScript)AssetDatabase.LoadAssetAtPath(m_newScript, typeof(MonoScript));
                foreach (GameObject gameObject in m_instances)
                {
                    gameObject.AddComponent(script.GetClass());
                    PrefabStage stage = PrefabStageUtility.GetPrefabStage(gameObject);
                    if (stage != null)
                    {
#if UNITY_2020_1_OR_NEWER
                        PrefabUtility.SaveAsPrefabAsset(gameObject, stage.assetPath);
#else
                        PrefabUtility.SaveAsPrefabAsset(gameObject, stage.prefabAssetPath);
#endif
                    }
                }
            }
            catch (Exception e)
            {
                ksLog.Error(typeof(ksScriptGenerator), "Error attaching script", e);
            }
            m_newScript = null;
            m_instances = null;
        }

        /// <summary>
        /// Saves script attachment data so a script can be attached to game objects once Unity finishes compiling
        /// scripts.
        /// </summary>
        /// <param name="path">Path to script to attach.</param>
        /// <param name="gameObjects">Game objects to attach script to.</param>
        public void SaveAttachments(string path, GameObject[] gameObjects)
        {
            m_newScript = path;
            m_instances = gameObjects;
        }

        /// <summary>Writes a file.</summary>
        /// <param name="path">Path to write to.</param>
        /// <param name="data">Data to write.</param>
        /// <returns>True if the file was written successfully.</returns>
        public bool Write(string path, string data)
        {
            if (!ksPathUtils.Create(path))
            {
                return false;
            }
            try
            {
                using (StreamWriter writer = new StreamWriter(path))
                {
                    writer.Write(data);
                    writer.Close();
                }
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Error writing to " + path, e);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Deletes all lines that have an occurence of <paramref name="substr"/> from <paramref name="template"/>.
        /// </summary>
        /// <param name="template">Template to delete lines from.</param>
        /// <param name="substr">Delete lines that contain this string.</param>
        /// <returns>
        /// The <paramref name="template"/> with lines that contain <paramref name="substr"/> deleted.
        /// </returns>
        public string DeleteMatchingLines(string template, string substr)
        {
            int index = 0;
            while ((index = template.IndexOf(substr, index)) > 0)
            {
                int startIndex = Math.Max(0, template.LastIndexOf("\n", index));
                int endIndex = template.IndexOf("\n", index);
                if (endIndex < 0)
                {
                    endIndex = template.Length;
                }
                template = template.Remove(startIndex, endIndex - startIndex);
                index = startIndex;
            }
            return template;
        }
    }
}
