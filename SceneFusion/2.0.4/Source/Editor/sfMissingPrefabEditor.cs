using UnityEditor;
using UnityEngine;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>Custom editor for sfMissingPrefab.</summary>
    [CustomEditor(typeof(sfMissingPrefab))]
    internal class sfMissingPrefabEditor : UnityEditor.Editor
    {
        /// <summary>Creates the GUI. Displays the path to the missing prefab in a warning box.</summary>
        public override void OnInspectorGUI()
        {
            sfMissingPrefab script = target as sfMissingPrefab;
            if (script != null)
            {
                if (SceneFusion.Get().Service.IsConnected)
                {
                    // Prevent removing the script while in a session.
                    script.hideFlags |= HideFlags.NotEditable;
                }
                else
                {
                    script.hideFlags &= ~HideFlags.NotEditable;
                }
                string message = "Missing prefab: " + script.PrefabPath;
                if (script.FileId != 0)
                {
                    message += "\nLocal file id: " + script.FileId;
                }
                // End disabled group so the warning box does not appear faded when the component is not editable.
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.HelpBox(message, MessageType.Warning);
                EditorGUI.BeginDisabledGroup((script.hideFlags & HideFlags.NotEditable) == HideFlags.NotEditable);
            }
        }   
    }
}
