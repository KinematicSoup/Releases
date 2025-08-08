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

namespace KS.Reactor.Client.Unity
{
    /// <summary>Cylinder dimension data used as a key in mesh caches.</summary>
    public struct ksCylinderData
    {
        /// <summary>Radius</summary>
        public float Radius;
        /// <summary>Height</summary>
        public float Height;
        /// <summary>Direction</summary>
        public ksShape.Axis Direction;
        /// <summary>Number of sides</summary>
        public int NumSides;

        /// <summary>Constructor</summary>
        /// <param name="radius">Radius of the cylinder.</param>
        /// <param name="height">Height of the cylinder.</param>
        /// <param name="direction">Axis alignment.</param>
        /// <param name="numSides">Number of sides used to appoximate the cylinder.</param>
        public ksCylinderData(float radius, float height, ksShape.Axis direction, int numSides)
        {
            Radius = radius;
            Height = height;
            Direction = direction;
            NumSides = numSides;
        }

        /// <summary>Checks if two cylinders are the same.</summary>
        /// <param name="lhs">Cylinder on the left hand side of the operator.</param>
        /// <param name="rhs">Cylinder on the right hand side of the operator.</param>
        /// <returns>True the compared cylinders have the same radius, height, direction, and sides.</returns>
        public static bool operator ==(ksCylinderData lhs, ksCylinderData rhs)
        {
            return lhs.Radius == rhs.Radius &&
                lhs.Height == rhs.Height &&
                lhs.Direction == rhs.Direction &&
                lhs.NumSides == rhs.NumSides;
        }

        /// <summary>Checks if two cylinders are different.</summary>
        /// <param name="lhs">Cylinder on the left hand side of the operator.</param>
        /// <param name="rhs">Cylinder on the right hand side of the operator.</param>
        /// <returns>True the compared cylinders are not the same.</returns>
        public static bool operator !=(ksCylinderData lhs, ksCylinderData rhs)
        {
            return !(lhs == rhs);
        }

        /// <summary>Checks if this cylinder is equal to an object.</summary>
        /// <param name="obj">Object to compare with.</param>
        /// <returns>True if this and the object are the same.</returns>
        public override bool Equals(object obj)
        {
            return obj is ksCylinderData && this == (ksCylinderData)obj;
        }

        /// <summary>Returns the hash code.</summary>
        /// <returns>Hash code.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Radius.GetHashCode();
                hash *= 7;
                hash += Height.GetHashCode();
                hash *= 7;
                hash += (int)Direction;
                hash *= 7;
                hash += NumSides;
                return hash;
            }
        }
    }
}
