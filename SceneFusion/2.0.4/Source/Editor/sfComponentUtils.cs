using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using KS.SF.Unity.Editor;
using KS.SF.Reactor;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>Component utility functions.</summary>
    public class sfComponentUtils
    {
        private static readonly char ASSEMBLY_SEPARATOR = '#';
        private static readonly string DEFAULT_ASSEMBLY = "Assembly-CSharp";
        private static readonly string DEFAULT_NAMESPACE = "UnityEngine.";
        private static readonly int DEFAULT_NAMESPACE_LENGTH = DEFAULT_NAMESPACE.Length;
        private static readonly string LOG_CHANNEL = typeof(sfComponentUtils).ToString();

        private static ksReflectionObject m_roAddComponent;
        private static ksReflectionObject m_roLoadPrefabIntoPreviewScene;

        /// <summary>Static constructor</summary>
        static sfComponentUtils()
        {
            m_roAddComponent = new ksReflectionObject(typeof(GameObject)).GetMethod("AddComponentInternal");
            m_roLoadPrefabIntoPreviewScene = new ksReflectionObject(typeof(PrefabStageUtility))
                .GetMethod("LoadPrefabIntoPreviewScene");
        }

        /// <summary>
        /// Gets the name of a component. For Unity components the namespace is only included if it is not part of
        /// UnityEngine. For Monobehaviours this is the assembly name + class name with name space seperated by a '#'.
        /// If the assembly is Assembly-CSharp (the default for scripts) the assembly name is not included and the name
        /// begins with a '#'.
        /// </summary>
        /// <param name="component">component to get name for.</param>
        /// <returns>name</returns>
        public static string GetName(Component component)
        {
            if (component == null)
            {
                return null;
            }
            string name;
            if (component is MonoBehaviour)
            {
                sfMissingComponent missingComponent = component as sfMissingComponent;
                if (missingComponent != null)
                {
                    return missingComponent.Name;
                }

                Type type = component.GetType();
                string assemblyName = type.Assembly.FullName.Split(',')[0];
                if (assemblyName == DEFAULT_ASSEMBLY)
                {
                    assemblyName = "";
                }
                name = assemblyName + ASSEMBLY_SEPARATOR + type.ToString();
            }
            else
            {
                // Some Unity components such as Halo do not have a corresponding C# class and instead are of type
                // Behaviour, so we cannot get the type using GetType(). Instead we get it from ToString which puts the
                // type name in brackets at the end of the string.
                string str = component.ToString();
                int index = str.LastIndexOf("(");
                name = str.Substring(index + 1, str.Length - index - 2);
                if (name.StartsWith(DEFAULT_NAMESPACE))
                {
                    name = name.Substring(DEFAULT_NAMESPACE_LENGTH);
                }
            }
            return name;
        }

        /// <summary>Adds a component to a game object by its type name. You can get the name using GetName.</summary>
        /// <param name="gameObject">gameObject to add component to.</param>
        /// <param name="name">name of component to add.</param>
        public static Component AddComponent(GameObject gameObject, string name)
        {
            try
            {
                Component component = null;
                int index = name.LastIndexOf(ASSEMBLY_SEPARATOR);
                if (index == -1)
                {
                    // This is a Unity component. Add by name.
                    component = m_roAddComponent.InstanceInvoke(gameObject, name) as Component;
                }
                else
                {
                    // This is a Monobehaviour. Add by type.
                    Type type = GetMonobehaviourTypeByName(name);
                    if (type == null)
                    {
                        return null;
                    }
                    component = gameObject.AddComponent(type);
                }
                if (component != null)
                {
                    EditorUtility.SetDirty(component);
                }
                return component;
            }
            catch (Exception e)
            {
                LogAddComponentWarning(name, null, e);
                return null;
            }
        }

        /// <summary>
        /// Converts a component name from GetName to a display name. If the name begins with a '#' it is removed, and
        /// all other '#'s are changed to '.'s.
        /// </summary>
        /// <param name="name"></param>
        /// <returns>display name.</returns>
        public static string GetDisplayName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }
            if (name.StartsWith('#'))
            {
                return name.Substring(1);
            }
            return name.Replace('#', '.');
        }

        /// <summary>
        /// Gets the class name and assembly name from a component name, and returns true if the component is a
        /// Monobehaviour. The assembly name will is null for non-Monobehaviours, and empty string for Monobehaviours in
        /// the default assembly Assembly-CSharp. Use GetName to get the component name.
        /// </summary>
        /// <param name="of">of the component returned from GetName.</param>
        /// <param name="className">className including namespace.</param>
        /// <param name="Null">
        /// Null for non-Monobehaviours and empty string for Monobehaviours in the default
        /// assembly.
        /// </param>
        /// <returns>true if the component is a Monobehaviour.</returns>
        public static bool GetClassAndAssemblyName(string name, out string className, out string assemblyName)
        {
            if (name == null)
            {
                className = null;
                assemblyName = null;
                return false;
            }
            int index = name.IndexOf(ASSEMBLY_SEPARATOR);
            if (index < 0)
            {
                className = name;
                assemblyName = null;
                return false;
            }
            className = name.Substring(index + 1);
            assemblyName = name.Substring(0, index);
            return true;
        }

        /// <summary>
        /// Sets a transform's parent without modifying the transform's local position, rotation, or scale values.
        /// Applies pending serialized property changes to the transform, its old parent, and new parent to prevent
        /// corrupting the hierarchy.
        /// </summary>
        /// <param name="child">child to set parent for.</param>
        /// <param name="parent">parent to set. Null to make the child a root object.</param>
        public static void SetParent(GameObject child, GameObject parent)
        {
            SetParent(child.transform, parent == null ? null : parent.transform);
        }

        /// <summary>
        /// Sets a transform's parent without modifying the transform's local position, rotation, or scale values.
        /// Applies pending serialized property changes to the transform, its old parent, and new parent to prevent
        /// corrupting the hierarchy. Unlike Unity's parent changing methods, this also works on prefab asset transforms
        /// as long as the child and parent reside within the same prefab.
        /// </summary>
        /// <param name="child">child to set parent for.</param>
        /// <param name="parent">parent to set. Null to make the child a root object.</param>
        public static void SetParent(Transform child, Transform parent)
        {
            // When serialized properties are applied, the transform's child list and parent state is reverted to the
            // state it had when the serialized object was created. This can corrupt the hierarchy if there were
            // hierarchy changes since the serialized object was created, so we need to apply serialized property
            // changes for the child transform, its old parent and its new parent, before modifying the transform
            // hierarchy.
            sfPropertyManager.Get().ApplySerializedProperties(child);
            sfPropertyManager.Get().ApplySerializedProperties(child.parent);
            sfPropertyManager.Get().ApplySerializedProperties(parent);

            if (parent == null || !PrefabUtility.IsPartOfPrefabAsset(parent))
            {
                child.SetParent(parent, false);
                return;
            }

            if (parent.transform.root != child.transform.root)
            {
                ksLog.Error(LOG_CHANNEL,
                    "Could not set prefab parent; parent and child must belong to the same prefab.");
                return;
            }

            // Tranform.SetParent does not work on prefab objects. To change the parent of a prefab child, we have to
            // load a copy of the prefab into a preview scene, change the preview child's parent, then save the
            // preview object back to the prefab and close the preview scene.
            sfPrefabPreviewScene preview = new sfPrefabPreviewScene(parent.gameObject);
            try
            {
                if (preview.RootObject == null)
                {
                    ksLog.Error(LOG_CHANNEL, "Could not set prefab parent; could not load prefab into preview scene.");
                    return;
                }
                Transform previewChild = preview.FindEquivalentTransform(child);
                Transform previewParent = preview.FindEquivalentTransform(parent);
                if (previewChild != null && previewParent != null)
                {
                    previewChild.SetParent(previewParent, false);
                    preview.Save();
                }
                else
                {
                    ksLog.Error(LOG_CHANNEL,
                        "Could not set prefab parent; could not find child or parent preview object.");
                }
            }
            finally
            {
                preview.Close();
            }
        }

        /// <summary>Gets a monobehaviour's type by its name.</summary>
        /// <param name="typeName">
        /// assembly name and class name separated by a '#'. If the assembly name is empty,
        /// uses the default assembly.
        /// </param>
        /// <returns></returns>
        private static Type GetMonobehaviourTypeByName(string typeName)
        {
            int index = typeName.LastIndexOf(ASSEMBLY_SEPARATOR);
            string assemblyName = typeName.Substring(0, index);
            string className = typeName.Substring(index + 1);
            if (assemblyName == "")
            {
                assemblyName = DEFAULT_ASSEMBLY;
            }
            Assembly assembly = Assembly.Load(assemblyName);
            if (assembly == null)
            {
                LogAddComponentWarning(typeName, "Could not load assembly '" + assemblyName + "'.");
                return null;
            }

            Type type = assembly.GetType(className);
            if (type == null)
            {
                LogAddComponentWarning(typeName, "Could not find type '" + className + "'.");
                return null;
            }
            return type;
        }

        /// <summary>Logs a warning that a component failed to load.</summary>
        /// <param name="name">name of the component.</param>
        /// <param name="reason">reason for the failure.</param>
        /// <param name="exception">exception that caused the failure.</param>
        private static void LogAddComponentWarning(string name, string reason = null, Exception exception = null)
        {
            string message = "Error adding component '" + name + "'";
            if (reason != null)
            {
                message += ": " + reason;
            }
            else
            {
                message += ".";
            }
            if (exception == null)
            {
                ksLog.Warning(LOG_CHANNEL, message);
            }
            else
            {
                ksLog.Error(LOG_CHANNEL, message, exception);
            }
        }
    }
}
