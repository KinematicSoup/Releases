using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>Makes the Scene Fusion config appear in Project Settings.</summary>
    internal class ConfigSettingsProvider : SettingsProvider
    {
        private SerializedObject m_serializedObject;
        private string m_searchContext;

        /// <summary>Constructor</summary>
        /// <param name="path">Path for settings in the project settings.</param>
        /// <param name="scope">Determines where the settings appear.</param>
        public ConfigSettingsProvider(string path, SettingsScope scope = SettingsScope.User)
        : base(path, scope) 
        {

        }

        /// <summary>
        /// Called when the settings are opened. Initialized the serialized object for the config and the search key
        /// words.
        /// </summary>
        /// <param name="searchContext"></param>
        /// <param name="rootElement"></param>
        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            m_serializedObject = new SerializedObject(sfConfig.Get());
            List<string> keywordsList = new List<string>();
            // The names of all properties up to 2 levels deep are used as keywords.
            SerializedProperty iter = m_serializedObject.GetIterator();
            while (iter.NextVisible(iter.depth <= 0))
            {
                if (iter.name != "m_Script")
                {
                    keywordsList.Add(ConfigEditor.GetDisplayName(iter));
                }
            }
            keywords = keywordsList;
        }

        /// <summary>Draws the GUI.</summary>
        /// <param name="searchContext"></param>
        public override void OnGUI(string searchContext)
        {
            searchContext = searchContext.ToLowerInvariant();
            if (m_searchContext != searchContext)
            {
                m_searchContext = searchContext;
                ExpandGroupForSearch();
            }
            using (new GUILayout.HorizontalScope())
            {
                // Unity's other settings are indented by this amount. Using EditorGUI.indentLevel won't work because
                // it is an int and incrementing by 1 indents too much.
                GUILayout.Space(7f);
                using (new GUILayout.VerticalScope())
                {
                    ConfigEditor.Draw(m_serializedObject);
                }
            }
        }

        /// <summary>Expands the group with a property containing the search term.</summary>
        private void ExpandGroupForSearch()
        {
            // Do nothing if search context is less than 3 characters.
            if (m_searchContext == null || m_searchContext.Length < 3)
            {
                return;
            }
            SerializedProperty iter = m_serializedObject.GetIterator();
            string expandGroup = null;
            string currentGroup = null;
            bool currentExpanded = false;
            while (iter.NextVisible(iter.depth <= 0 && (expandGroup == null || currentExpanded)))
            {
                if (iter.depth == 0)
                {
                    if (!iter.hasChildren)
                    {
                        continue;
                    }
                    // We are iterating a new group.
                    currentGroup = iter.name;
                    currentExpanded = currentGroup == ConfigEditor.ExpandedGroup;
                }
                // If we already found a group to expand, do not check the property for search context unless this is
                // the currently expanded group.
                if ((expandGroup == null || currentExpanded) &&
                    ConfigEditor.GetDisplayName(iter).ToLowerInvariant().Contains(m_searchContext))
                {
                    if (currentExpanded)
                    {
                        // Found the search context in the currently expanded group. We don't need to do anything.
                        return;
                    }
                    expandGroup = currentGroup;
                }
            }
            if (expandGroup != null)
            {
                ConfigEditor.ExpandedGroup = expandGroup;
            }
        }

        /// <summary>Creates the settings provider for the Scene Fusion config.</summary>
        /// <returns>Settings provider</returns>
        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new ConfigSettingsProvider("Project/" + Product.NAME, SettingsScope.Project);
        }
    }
}
