using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using KS.SceneFusion2.Client;
using KS.SF.Unity.Editor;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>
    /// Syncs navigation project settings through the <see cref="sfAssetTranslator"/> if a NavMeshSurface component is
    /// synced. Polls for new agents added to the navigation settings because there are no events in Unity we can use
    /// for that.
    /// </summary>
    [InitializeOnLoad]
    public class sfNavigation
    {
        private int m_lastAgentsCount;
        private UObject m_navigationSettings;
        private ksReflectionObject m_roNavigationWindow;
        private EditorWindow m_navigationWindow;

        /// <summary>Static constructor</summary>
        static sfNavigation()
        {
            sfObjectEventDispatcher.Get().OnInitialize += new sfNavigation().Initialize;
        }

        /// <summary>Constructor</summary>
        private sfNavigation()
        {

        }

        /// <summary>
        /// Initialization. Registers event handlers with the component and asset translators to handle syncing
        /// navigation settings.
        /// </summary>
        private void Initialize()
        {
            // NavMeshSurface is part of the Unity.AI.Navigation package which may not be part of the project, so we
            // try to load it with reflection.
            ksReflectionObject roNavMeshSurface = new ksReflectionObject("Unity.AI.Navigation",
                "Unity.AI.Navigation.NavMeshSurface", true);
            if (roNavMeshSurface.IsVoid)
            {
                // We did not detect the Unity.AI.Navigation package.
                return;
            }

            // Load the internal NavigationWindow using reflection. s_NavigationWindow is an internal static field
            // that stores the open NagivationWindow if there is one.
            m_roNavigationWindow = new ksReflectionObject("Unity.AI.Navigation.Editor",
                    "Unity.AI.Navigation.Editor.NavigationWindow").GetField("s_NavigationWindow");

            // Get the component translator.
            sfComponentTranslator componentTranslator = sfObjectEventDispatcher.Get()
                .GetTranslator<sfComponentTranslator>(sfType.Component);
            // Start syncing navigation settings when we find a NavMeshSurface component.
            // Register an object initializer that gets called before SF uploads an sfObject for a NavMeshSurface
            // component to the server.
            componentTranslator.ObjectInitializers.Add(roNavMeshSurface.Type, (sfObject obj, Component component) =>
            {
                // If we aren't polling for new agents, start polling and upload the nav mesh settings.
                Start(true);
            });
            // Register a component initializer that gets called after SF creates a NavMeshSurface component from an
            // sfObject from the server.
            componentTranslator.ComponentInitializers.Add(roNavMeshSurface.Type, (sfObject obj, Component component) =>
            {
                // If we aren't polling for new agents, start polling.
                Start(false);
            });

            // Get the asset translator that is used to sync the navigation settings.
            sfAssetTranslator assetTranslator = sfObjectEventDispatcher.Get()
                .GetTranslator<sfAssetTranslator>(sfType.Asset);
            // Register a handler that gets called after another user changes the m_Settings property. The nav mesh
            // settings doesn't have a specialized class and is just a UObject (UnityEngine.Object), so we register
            // the handler for UObject.
            assetTranslator.PostPropertyChange.Add<UObject>("m_Settings", (UObject uobj, sfBaseProperty prop) =>
            {
                // Check that the modifed object was the navigation settings.
                if (uobj == m_navigationSettings)
                {
                    // Update the last agents count so polling doesn't think new agents were added that we need to
                    // send to the server.
                    m_lastAgentsCount = NavMesh.GetSettingsCount();
                }
            });
            // Register a handler that gets called after an asset of type UObject is changed by another user to refresh
            // the navigation window if it is open and the changed object was the navigation settings so we can see the
            // changes in the navigation window.
            assetTranslator.PostUObjectChange.Add<UObject>((UObject uobj) =>
            {
                if (uobj == m_navigationSettings)
                {
                    RefreshWindow();
                }
            });
        }

        /// <summary>
        /// Starts polling for new agents added to the navigation settings, and optionally uploads navigation settings
        /// if they are already uploaded. We have to poll for new agents because Unity has no events when agents are
        /// added and doesn't register an undo transaction.
        /// </summary>
        /// <param name="uploadSettings">
        /// If true, creates an sfObject for the navigation settings through the <see cref="sfAssetTranslator"/>
        /// </param>
        private void Start(bool uploadSettings)
        {
            if (m_navigationSettings == null)
            {
                m_navigationSettings = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/NavMeshAreas.asset");
                m_lastAgentsCount = NavMesh.GetSettingsCount();
                sfUnityEventDispatcher.Get().PreUpdate += PollNewAgents;
                SceneFusion.Get().Service.OnDisconnect += Stop;
                if (uploadSettings)
                {
                    sfAssetTranslator assetTranslator = sfObjectEventDispatcher.Get()
                        .GetTranslator<sfAssetTranslator>(sfType.Asset);
                    assetTranslator.Create(m_navigationSettings);
                }
            }
        }

        /// <summary>Stops polling for new agents added to the navigation settings.</summary>
        /// <param name="session">Session we disconnected from. Unused.</param>
        /// <param name="errorMessage">Disconnect error message. Unused.</param>
        private void Stop(sfSession session, string errorMessage)
        {
            m_navigationSettings = null;
            sfUnityEventDispatcher.Get().PreUpdate -= PollNewAgents;
        }

        /// <summary>Syncs changes if new agents were added to the navigation settings.</summary>
        /// <param name="deltaTime">Deltatime in seconds since the last update. Unused.</param>
        private void PollNewAgents(float deltaTime)
        {
            int count = NavMesh.GetSettingsCount();
            if (count > m_lastAgentsCount)
            {
                sfObject obj = sfObjectMap.Get().GetSFObject(m_navigationSettings);
                if (obj != null)
                {
                    sfDictionaryProperty properties = (sfDictionaryProperty)obj.Property;
                    if (obj.IsLocked)
                    {
                        // Another user has the navigation settings locked. Apply the server values to revert our
                        // changes.
                        sfPropertyManager.Get().ApplyProperties(m_navigationSettings, properties);
                    }
                    else
                    {
                        sfPropertyManager.Get().SendPropertyChanges(m_navigationSettings, properties);
                    }
                }
            }
            m_lastAgentsCount = count;
        }

        /// <summary>Refreshes the navigation window, if it is open.</summary>
        private void RefreshWindow()
        {
            if (m_navigationWindow == null)
            {
                m_navigationWindow = m_roNavigationWindow.GetValue() as EditorWindow;
                if (m_navigationWindow == null)
                {
                    return;
                }
            }
            m_navigationWindow.Repaint();
        }
    }
}
