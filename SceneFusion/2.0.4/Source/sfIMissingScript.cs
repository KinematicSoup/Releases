#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KS.SF.Unity;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion2.Unity
{
    /// <summary>
    /// Interface for missing script stand-ins that store serialized property data that can be used to sync the object
    /// with properties to other users.
    /// </summary>
    public interface sfIMissingScript
    {
        /// <summary>Map of property names to serialized property data.</summary>
        ksSerializableDictionary<string, byte[]> SerializedProperties { get; }

        /// <summary>
        /// Map of sfobject ids to uobjects referenced in the serialized data. Because sfobject ids can change between
        /// sessions, this is needed to ensure the object references are correct when deserializing data that was
        /// serialized in a different session.
        /// </summary>
        ksSerializableDictionary<uint, UObject> ReferenceMap { get; }

        /// <summary>The id of the session the serialized property data is from.</summary>
        uint SessionId { get; set; }
    }
}
#endif
