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
using UnityEditor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>
    /// Utility for updating script GUIDs in Unity asset files to fix missing scripts when metafiles change.
    /// To use this, you must know the old file IDs and GUIDs that you want to replace, as well as the new file IDs and
    /// GUIDs. You must also set Unity's asset serialization mode (Edit > Project Settings > Editor > Asset
    /// Serialization > Mode) to Force Text BEFORE the metafiles change to the new GUIDs and scripts are missing. If you
    /// change it to Force Text after the metafiles change, Unity will be unable to serialize the prefabs that contain
    /// missing scripts, and we will be unable to update the GUIDs in those prefab files.
    /// </summary>
    public class ksScriptGuidUpdater
    {
        private ksScriptUpdater m_updater = new ksScriptUpdater();

        /// <summary>
        /// Adds a mapping between an old file ID + GUID to be replaced with a new file ID + GUID. Once you've added all
        /// the replacements you want, call <see cref="ReplaceAll()"/> to replace the old IDs with the new IDs in all
        /// prefab and scene files.
        /// </summary>
        /// <param name="oldFileId">Old file ID of missing script to replace.</param>
        /// <param name="oldGuid">Old GUID of missing script to replace.</param>
        /// <param name="newFileId">New file ID of the script.</param>
        /// <param name="newGuid">New GUID of the script.</param>
        public void AddReplacement(int oldFileId, string oldGuid, int newFileId, string newGuid)
        {
            string oldText = "fileID: " + oldFileId + ", guid: " + oldGuid;
            string newText = "fileID: " + newFileId + ", guid: " + newGuid;
            m_updater.AddReplacement(oldText, newText);
        }

        /// <summary>
        /// Replaces file IDs and GUIDs in all prefab and scene files for missing scripts with the file IDs and GUIDs
        /// for the new scripts that were registered using <see cref="AddReplacement(int, string, int, string)"/>.
        /// To use this, Asset Serialization Mode must be Force Text.
        /// </summary>
        public void ReplaceAll()
        {
            if (EditorSettings.serializationMode != SerializationMode.ForceText)
            {
                ksLog.Error(this,
                    "Cannot update missing script references when serialization mode is not Force Text.");
            }
            else
            {
                m_updater.ReplaceAll("Assets", true, "*.unity", "*.prefab", "*.asset");
            }
        }
    }
}