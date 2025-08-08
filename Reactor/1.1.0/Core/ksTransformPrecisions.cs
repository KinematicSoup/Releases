/*
KINEMATICSOUP CONFIDENTIAL
 Copyright(c) 2014-2025 KinematicSoup Technologies Incorporated 
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

using System;
using UnityEngine;

namespace KS.Reactor.Client.Unity
{
    /// <summary>
    /// Precision used by the server to determine if an update needs to be sent to connected clients.
    /// These values may be set on a scene or entity. A value of -1f means that the value should be 
    /// inherited. If set on a <see cref="ksEntityComponent"/> component then the value will be
    /// inherited from the room <see cref="ksRoomType"/> component. If set on a <see cref="ksRoomType"/>
    /// component, then the default values will be used.
    /// </summary>
    [Serializable]
    public class ksTransformPrecisions
    {
        /// <summary>Position network precision.</summary>
        [Tooltip("Amount an entity position needs to change to trigger an update to clients. This value is applied "+
            "separatetly on the x, y, and z components of the position vector. 0 = full float precision value")]
        [Min(-1.0f)]
        public float Position = ksCompressionConstants.DEFAULT_POSITION_PRECISION;

        /// <summary>Rotation network precision.</summary>
        [Tooltip("Amount an entity rotation needs to change to trigger an update to clients. This value is applied " +
            "separatetly on the x, y, z and w components of the rotation quaternion. 0 = full float precision value")]
        [Min(-1.0f)]
        public float Rotation = ksCompressionConstants.DEFAULT_ROTATION_PRECISION;

        /// <summary>Scale network precision.</summary>
        [Tooltip("Amount an entity scale needs to change by to trigger an update to clients. 0 = full float precision value")]
        [Min(-1.0f)]
        public float Scale = ksCompressionConstants.DEFAULT_SCALE_PRECISION;

        /// <summary>Constructor</summary>
        public ksTransformPrecisions()
        {
            Position = ksCompressionConstants.DEFAULT_POSITION_PRECISION;
            Rotation = ksCompressionConstants.DEFAULT_ROTATION_PRECISION;
            Scale = ksCompressionConstants.DEFAULT_SCALE_PRECISION;
        }

        /// <summary>Constructor</summary>
        /// <param name="position">Position network precision.</param>
        /// <param name="rotation">Rotation network precision.</param>
        /// <param name="scale">Scale network precision.</param>
        public ksTransformPrecisions(
            float position = ksCompressionConstants.DEFAULT_POSITION_PRECISION, 
            float rotation = ksCompressionConstants.DEFAULT_ROTATION_PRECISION, 
            float scale = ksCompressionConstants.DEFAULT_SCALE_PRECISION)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        /// <summary>
        /// Check if the values are valid. If a value is a negative float, then snap it to -1f. 
        /// During publishing a values of -1f is used to indicate that a parent or default value should be used.
        /// </summary>
        public void Validate()
        {
            if (Position < 0f) Position = -1f;
            if (Rotation < 0f) Rotation = -1f;
            if (Scale < 0f) Scale = -1f;
        }
    }
}
