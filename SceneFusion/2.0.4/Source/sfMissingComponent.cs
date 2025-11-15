#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KS.SF.Unity;
using KS.SF.Reactor;
using KS.SceneFusion;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion2.Unity
{
    /// <summary>
    /// Stand-in component attached in place of missing components. Stores the missing component name and property data
    /// so that when copied, the data for the component can be synced to other users. Will be replaced by the correct
    /// component when it becomes available during a session.
    /// </summary>
    [AddComponentMenu("")]
    public class sfMissingComponent : sfBaseComponent, sfIMissingScript
    {
        /// <summary>Name of the missing component. See sfComponentUtils.GetName.</summary>
        public string Name
        {
            get { return m_name; }
            set { m_name = value; }
        }
        [SerializeField]
        private string m_name;

        /// <summary>Map of property names to serialized property data.</summary>
        public ksSerializableDictionary<string, byte[]> SerializedProperties
        {
            get { return m_serializedProperties; }
        }
        [SerializeField]
        private ksSerializableDictionary<string, byte[]> m_serializedProperties = 
            new ksSerializableDictionary<string, byte[]>();

        /// <summary>
        /// Map of sfobject ids to uobjects referenced in the serialized data. Because sfobject ids can change between
        /// sessions, this is needed to ensure the object references are correct when deserializing data that was
        /// serialized in a different session.
        /// </summary>
        public ksSerializableDictionary<uint, UObject> ReferenceMap
        {
            get { return m_referenceMap; }
        }
        [SerializeField]
        private ksSerializableDictionary<uint, UObject> m_referenceMap = new ksSerializableDictionary<uint, UObject>();

        /// <summary>The id of the session the serialized property data is from.</summary>
        public uint SessionId
        {
            get { return m_sessionId; }
            set { m_sessionId = value; }
        }
        [SerializeField]
        private uint m_sessionId;

        /// <summary>Logs a warning for the missing component.</summary>
        private void Awake()
        {
            // Monobehaviours have the assembly name followed by '#' then the class name. Remove everything before '#'.
            int index = m_name.IndexOf('#');
            string className = index < 0 ? m_name : m_name.Substring(index + 1);
            ksLog.Warning(this, gameObject.name + " has missing component '" + className + "'.", gameObject);
        }
    }
}
#endif
