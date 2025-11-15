using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;

namespace KS.SceneFusion2.Unity.Editor
{
    [CustomEditor(typeof(sfMissingComponent))]
    public class sfMissingComponentEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            sfMissingComponent script = target as sfMissingComponent;
            if (script != null)
            {
                string message;
                if (string.IsNullOrEmpty(script.Name))
                {
                    message = "Missing component";
                }
                else
                {
                    string className;
                    string assemblyName;
                    bool isMonobehaviour = sfComponentUtils.GetClassAndAssemblyName(script.Name,
                        out className, out assemblyName);
                    if (isMonobehaviour)
                    {
                        message = "Missing script: " + className;
                        if (!string.IsNullOrEmpty(assemblyName))
                        {
                            message += "\nAssembly: " + assemblyName;
                        }
                    }
                    else
                    {
                        message = "Missing Unity component: " + className;
                    }
                }
                // End disabled group so the warning box does not appear faded when the component is not editable.
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.HelpBox(message, MessageType.Warning);
                EditorGUI.BeginDisabledGroup((script.hideFlags & HideFlags.NotEditable) == HideFlags.NotEditable);
            }
        }
    }
}
