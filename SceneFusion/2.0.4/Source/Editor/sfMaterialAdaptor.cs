using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using KS.SceneFusion2.Client;
using KS.SceneFusion;
using KS.SF.Reactor;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>Syncs referenced materials through the <see cref="sfAssetTranslator"/>.</summary>
    [InitializeOnLoad]
    public class sfMaterialAdaptor
    {
        // These properties do not fire events when modified, so we need to poll for changes on selected materials.
        private static readonly string[] POLL_PROPERTIES = new string[] { "m_EnableInstancingVariants",
#if !UNITY_2022_3_OR_NEWER
            "m_LightmapFlags",
#endif
            "m_DoubleSidedGI" };
        // How often in seconds to check selected materials for changes to properties that don't have change events. We
        // also check when a material is deselected.
        private const float POLL_INTERVAL = .1f;

        private float m_pollChangesTimer;

        /// <summary>Static constructor</summary>
        static sfMaterialAdaptor()
        {
            sfObjectEventDispatcher.Get().OnInitialize += new sfMaterialAdaptor().Initialize;
        }

        /// <summary>Constructor</summary>
        private sfMaterialAdaptor()
        {

        }

        /// <summary>
        /// Initialization. Registers materials as syncable type and registers connection event handlers.
        /// </summary>
        private void Initialize()
        {
            sfLoader.Get().RegisterSyncableType<Material>(() =>
            {
                return new Material(sfUserMaterials.CameraMaterial.shader);
            });
            SceneFusion.Get().Service.OnConnect += HandleConnect;
            SceneFusion.Get().Service.OnDisconnect += HandleDisconnect;
        }

        /// <summary>
        /// Called after connecting to a session. Starts polling selected materials for changes if the connection was
        /// successful.
        /// </summary>
        /// <param name="session">Session. Null if there was a connection error.</param>
        /// <param name="errorMessage">Connect error message. Null if the connection was successful.</param>
        private void HandleConnect(sfSession session, string errorMessage)
        {
            if (session != null)
            {
                sfUnityEventDispatcher.Get().PreUpdate += PollChanges;
                sfSelectionWatcher.Get().OnDeselect += HandleDeselect;
                m_pollChangesTimer = POLL_INTERVAL;
            }
        }

        /// <summary>
        /// Called after disconnecting from a session. Stops polling selected materials for changes.
        /// </summary>
        /// <param name="session">Session</param>
        /// <param name="errorMessage">Disconnect error message</param>
        private void HandleDisconnect(sfSession session, string errorMessage)
        {
            sfUnityEventDispatcher.Get().PreUpdate -= PollChanges;
            sfSelectionWatcher.Get().OnDeselect -= HandleDeselect;
        }

        /// <summary>
        /// Called when an object is deseleted. If the object was a material, checks for changes to properties that
        /// don't have change events.
        /// </summary>
        /// <param name="uobj">Object that was deselected.</param>
        private void HandleDeselect(UObject uobj)
        {
            SendChanges(uobj as Material);
        }

        /// <summary>
        /// Updates the poll changes timer and checks selected materials for changes to properties that don't have
        /// change events if it is time to poll changes.
        /// </summary>
        /// <param name="deltaTime">Delta time in seconds since the last update.</param>
        private void PollChanges(float deltaTime)
        {
            m_pollChangesTimer -= deltaTime;
            if (m_pollChangesTimer > 0f)
            {
                return;
            }
            m_pollChangesTimer = POLL_INTERVAL;

            foreach (UObject uobj in Selection.objects)
            {
                SendChanges(uobj as Material);
            }
        }

        /// <summary>Checks for and sends changes to material properties that don't have change events.</summary>
        /// <param name="material">Material to check for changes.</param>
        private void SendChanges(Material material)
        {
            if (material == null)
            {
                return;
            }
            sfObject obj = sfObjectMap.Get().GetSFObject(material);
            if (obj == null || !obj.IsSyncing)
            {
                return;
            }
            sfDictionaryProperty properties = (sfDictionaryProperty)obj.Property;
            SerializedObject so = new SerializedObject(material);
            foreach (string propertyName in POLL_PROPERTIES)
            {
                SerializedProperty sprop = so.FindProperty(propertyName);
                if (sprop != null)
                {
                    if (sfPropertyManager.Get().IsDefaultValue(sprop))
                    {
                        properties.RemoveField(propertyName);
                    }
                    else
                    {
                        sfPropertyManager.Get().UpdateDictProperty(properties, propertyName,
                            sfPropertyManager.Get().GetValue(sprop));
                    }
                }
            }
        }
    }
}
