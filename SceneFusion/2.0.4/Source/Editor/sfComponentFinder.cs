using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using KS.SF.Reactor;
using KS.SceneFusion2.Client;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>
    /// Provides a method of finding components on a game object by their type name. Each component can only be returned
    /// once. This is used to get components by name when linking components with sfObjects. Each component is returned
    /// once to prevent the component from being linked to multiple objects.
    /// </summary>
    public class sfComponentFinder
    {
        /// <summary>True if the components were found in the order they were requested.</summary>
        public bool InOrder
        {
            get { return m_inOrder; }
        }

        private bool m_inOrder = true;
        private ksLinkedList<ComponentInfo> m_components = new ksLinkedList<ComponentInfo>();
        private sfComponentTranslator m_translator;

        /// <summary>Info about a component.</summary>
        private struct ComponentInfo
        {
            /// <summary>Component</summary>
            public Component Component;

            /// <summary>Type name</summary>
            public string Name;

            /// <summary>
            /// Local file id of prefab source component. 0 if the component is not a prefab component instance or is a
            /// Transform.
            /// </summary>
            public long SourceFileId;

            /// <summary>
            /// Local file id of the component. 0 if the component is not part of a prefab asset or is a prefab variant.
            /// </summary>
            public long FileId;

            /// <summary>Constructor</summary>
            /// <param name="component"></param>
            /// <param name="-">if true, will try to get the file id of the component's prefab source.</param>
            /// <param name="-">if true, will try to get the component's file id.</param>
            public ComponentInfo(Component component, bool getSourceFileId, bool getFileId)
            {
                Component = component;
                Name = sfComponentUtils.GetName(component);
                FileId = getFileId ? sfLoader.Get().GetLocalFileId(component) : 0;
                if (getSourceFileId)
                {
                    Component prefabComponent = PrefabUtility.GetCorrespondingObjectFromSource(component);
                    SourceFileId = prefabComponent == null ? 0 : sfLoader.Get().GetLocalFileId(prefabComponent);
                }
                else
                {
                    SourceFileId = 0;
                }
            }
        }

        /// <summary>Constructor</summary>
        /// <param name="gameObject">gameObject to get components from.</param>
        /// <param name="translator"></param>
        public sfComponentFinder(GameObject gameObject, sfComponentTranslator translator)
        {
            m_translator = translator;
            bool isPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(gameObject);
            bool isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(gameObject);
            Component[] components = gameObject.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (translator.IsSyncable(component))
                {
                    m_components.Add(new ComponentInfo(component, isPrefabInstance && i != 0, 
                        isPrefabAsset && !isPrefabInstance));
                }
            }
        }

        /// <summary>
        /// If fileId is non-zero, gets the component with that file id and the given type name and source file id. If
        /// none is found or fileId is zero, gets the first component with the given type name and prefab source file id
        /// that hasn't been returned yet. Each component returned is removed from the finder.
        /// </summary>
        /// <param name="name">
        /// name of component type to find. You can get this type name from a component using
        /// sfComponentUtils.GetName.
        /// </param>
        /// <param name="sourceFileId">
        /// sourceFileId of the component's source prefab. 0 for non-prefab component instances and
        /// transforms.
        /// </param>
        /// <param name="fileId">fileId of the component. 0 for non-prefab asset components.</param>
        /// <param name="fileIdMisMatch">
        /// set to true if fileId was non-zero and the returned component is not null
        /// and has a different file id.
        /// </param>
        /// <returns>component that matches the type name and file ids, or null if not found.</returns>
        public Component Find(string name, long sourceFileId, long fileId, out bool fileIdMismatch)
        {
            fileIdMismatch = false;
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            bool isFirst = true;
            if (fileId != 0)
            {
                // Look for the component with the given file id and return it if the type name and prefab source file
                // ids match.
                foreach (ComponentInfo info in m_components)
                {
                    if (info.FileId == fileId)
                    {
                        if (info.Name == name && info.SourceFileId == sourceFileId)
                        {
                            if (!isFirst)
                            {
                                m_inOrder = false;
                            }
                            m_components.RemoveCurrent();
                            return info.Component;
                        }
                        else
                        {
                            m_translator.DestroyComponent(info.Component);
                            m_components.RemoveCurrent();
                            break;
                        }
                    }
                    isFirst = false;
                }
            }

            // Look for the first component with the given type name and prefab source file id.
            isFirst = true;
            foreach (ComponentInfo info in m_components)
            {
                if (info.Name == name && info.SourceFileId == sourceFileId)
                {
                    if (!isFirst)
                    {
                        m_inOrder = false;
                    }
                    m_components.RemoveCurrent();
                    fileIdMismatch = fileId != 0;
                    return info.Component;
                }
                isFirst = false;
            }

            if (!isFirst)
            {
                m_inOrder = false;
            }
            return null;
        }

        /// <summary>
        /// Finds the component for an sfObject using it's class, prefab source file id, and file id properties.
        /// See <see cref="Find(string, long, long, out bool)"/>.
        /// </summary>
        /// <param name="obj">
        /// obj find find component for.
        /// return   Component component for the sfObject, or null if none was found.
        /// </param>
        public Component Find(sfObject obj)
        {
            bool fileIdMismatch;
            return Find(obj, out fileIdMismatch);
        }

        /// <summary>
        /// Finds the component for an sfObject using it's class, prefab source file id, and file id properties.
        /// See <see cref="Find(string, long, long, out bool)"/>.
        /// </summary>
        /// <param name="obj">obj find find component for.</param>
        /// <param name="fileIdMisMatch">
        /// set to true if the sfObject's file id property was non-zero and the
        /// returned component is not null and has a different file id.
        /// return   Component component for the sfObject, or null if none was found.
        /// </param>
        public Component Find(sfObject obj, out bool fileIdMismatch)
        {
            fileIdMismatch = false;
            if (obj == null)
            {
                return null;
            }
            sfDictionaryProperty properties = (sfDictionaryProperty)obj.Property;
            string name = (string)properties[sfProp.Class];
            sfBaseProperty prop;
            long sourceFileId = properties.TryGetField(sfProp.PrefabSourceFileId, out prop) ? (long)prop : 0;
            long fileId = properties.TryGetField(sfProp.FileId, out prop) ? (long)prop : 0;
            return Find(name, sourceFileId, fileId, out fileIdMismatch);
        }
    }
}
