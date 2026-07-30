/*
KINEMATICSOUP CONFIDENTIAL
 Copyright(c) 2014-2026 KinematicSoup Technologies Incorporated 
 All Rights Reserved.

NOTICE:  All information contained herein is, and remains the property of 
KinematicSoup Technologies Incorporated and its suppliers, if any. The 
intellectual and technical concepts contained herein are proprietary to 
KinematicSoup Technologies Incorporated and its suppliers and may be covered by
U.S. and Foreign Patents, patents in process, and are protected by trade secret
or copyright law. Dissemination of this information or reproduction of this
material is strictly forbidden unless prior written permission is obtained from
KinematicSoup Technologies Incorporated.
*/
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KS.SF.Unity;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion.Client.Unity
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
