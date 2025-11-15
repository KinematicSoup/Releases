using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KS.SceneFusion2.Client;
using KS.SceneFusion;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>Syncs config settings that need to be the same for everyone in the session.</summary>
    public class sfConfigTranslator : sfBaseTranslator
    {
        /// <summary>Config synced handler.</summary>
        public delegate void ConfigInitializedHandler();

        /// <summary>Invoked after synced config settings for the session are synced.</summary>
        public ConfigInitializedHandler OnConfigSynced;

        /// <summary>
        /// Called after connecting to the session. If the local user started the session, creates the config objec,
        /// prevents editing config settings that cannot be changed during the session, and invokes the on config
        /// synced event.
        /// </summary>
        public override void OnSessionConnect()
        {
            if (SceneFusion.Get().Service.IsSessionCreator)
            {
                sfDictionaryProperty dict = new sfDictionaryProperty();
                dict[sfProp.SyncPrefabs] = (int)sfConfig.Get().SyncPrefabs;
                dict[sfProp.SyncMaterials] = sfConfig.Get().SyncMaterials;
                sfObject obj = new sfObject(sfType.Config, dict);
                SceneFusion.Get().Service.Session.Create(obj);

                sfConfig.Get().SessionSettingsLocked = true;
                if (OnConfigSynced != null)
                {
                    OnConfigSynced();
                }
            }
        }

        /// <summary>
        /// Called after disconnecting from the session. Enables editing config settings that cannot be changed during
        /// a session.
        /// </summary>
        public override void OnSessionDisconnect()
        {
            sfConfig.Get().SessionSettingsLocked = false;
        }

        /// <summary>
        /// Called when a config sfObject is created by another user. Syncs the config settings, prevents editing
        /// settings that cannot be changed during a session, and invokes the on config synced event.
        /// </summary>
        /// <param name="obj">Config object that was created.</param>
        /// <param name="childIndex">Child index. -1 for a root object.</param>
        public override void OnCreate(sfObject obj, int childIndex)
        {
            sfDictionaryProperty dict = (sfDictionaryProperty)obj.Property;
            sfConfig.Get().SyncPrefabs = (sfConfig.PrefabSyncMode)(int)dict[sfProp.SyncPrefabs];
            sfConfig.Get().SyncMaterials = (bool)dict[sfProp.SyncMaterials];

            sfConfig.Get().SessionSettingsLocked = true;
            if (OnConfigSynced != null)
            {
                OnConfigSynced();
            }
        }
    }
}
