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
    /// <summary>Collision shape utils.</summary>
    public class ksShapeUtils
    {
        private static readonly string LOG_CHANNEL = typeof(ksShapeUtils).ToString();

        /// <summary>
        /// Converts enum <see cref="ksShape.Axis"/> to a ksQuaternion that would rotate a y-axis aligned shape
        /// to the given axis.
        /// </summary>
        /// <param name="axis">Axis alignment</param>
        /// <returns>Quaternion representing axis rotated from the identity rotation.</returns>
        public static ksQuaternion AxisToRotationOffset(ksShape.Axis axis)
        {
            switch (axis)
            {
                case ksShape.Axis.X:
                    return ksQuaternion.FromVectorDelta(ksVector3.Up, ksVector3.Right);
                case ksShape.Axis.Y:
                default:
                    return ksQuaternion.Identity;
                case ksShape.Axis.Z:
                    return ksQuaternion.FromVectorDelta(ksVector3.Up, ksVector3.Forward);
            }
        }

        /// <summary>Gets the center of two spheres on either end of a capsule.</summary>
        /// <param name="capsule">Capsule</param>
        /// <param name="position">Position</param>
        /// <param name="rotation">Rotation</param>
        /// <param name="point1">Point1</param>
        /// <param name="point2">Point2</param>
        public static void GetCapsuleEndPoints(
            ksCapsule capsule,
            ksVector3 position,
            ksQuaternion rotation,
            out ksVector3 point1,
            out ksVector3 point2)
        {
            ksVector3 offset = new ksVector3(0, capsule.Height / 2 - capsule.Radius, 0) * (rotation *
                AxisToRotationOffset(capsule.Alignment));
            point1 = position + offset;
            point2 = position - offset;
        }

        /// <summary>
        /// Gets the <see cref="ksShape"/> representation of a collider to use for queries. Mesh colliders are
        /// approximated as spheres or capsules.
        /// </summary>
        /// <param name="collider">Collider</param>
        /// <param name="suppressWarnings">
        /// If true, will not log warnings when an sphere or capsule is used to approximate an unsupported collider.
        /// </param>
        /// <returns>Collider shape</returns>
        public static ksShape GetColliderShape(ksIUnityCollider collider, bool suppressWarnings = false)
        {
            if (collider == null)
            {
                return null;
            }
            Transform transform = collider.Component.transform;

            switch (collider.ShapeType)
            {
                case ksShape.ShapeTypes.SPHERE:
                {
                    SphereCollider sphere = (SphereCollider)collider.Component;
                    Vector3 scale = transform.localScale;
                    float radiusScale = Mathf.Max(scale.x, scale.y, scale.z);
                    float radius = sphere.radius * radiusScale;
                    return new ksSphere(radius);
                }
                case ksShape.ShapeTypes.BOX:
                {
                    BoxCollider box = (BoxCollider)collider.Component;
                    ksVector3 size = ksVector3.Scale(box.size, transform.localScale);
                    return new ksBox(size);
                }
                case ksShape.ShapeTypes.CAPSULE:
                {
                    CapsuleCollider capsule = (CapsuleCollider)collider.Component;
                    Vector3 scale = transform.localScale;
                    ksShape.Axis alignment = (ksShape.Axis)capsule.direction;
                    Vector2 alignedScale = GetAxisAlignedScale(scale, alignment);
                    return CreateCapsuleOrSphere(capsule.radius * alignedScale.x, capsule.height * alignedScale.y,
                        alignment);
                }
                case ksShape.ShapeTypes.CAPSULE_CONTROLLER:
                {
                    CharacterController character = (CharacterController)collider.Component;
                    Vector3 scale = transform.localScale;
                    Vector3 alignedScale = GetAxisAlignedScale(scale, ksShape.Axis.Y);
                    return CreateCapsuleOrSphere(character.radius * alignedScale.x, character.height * alignedScale.y,
                        ksShape.Axis.Y);
                }
                case ksShape.ShapeTypes.CONVEX_MESH:
                case ksShape.ShapeTypes.TRIANGLE_MESH:
                {
                    MeshCollider mesh = (MeshCollider)collider.Component;
                    ksShape shape = CreateApproximateShape(mesh.bounds.extents);
                    if (!suppressWarnings)
                    {
                        LogApproximateShape(shape, collider.ShapeType);
                    }
                    return shape;
                }
                case ksShape.ShapeTypes.CYLINDER:
                {
                    ksCylinderCollider cylinder = (ksCylinderCollider)collider;
                    Vector3 scale = transform.localScale;
                    Vector2 alignedScale = GetAxisAlignedScale(scale, cylinder.Direction);
                    ksShape shape = CreateCapsuleOrSphere(
                        cylinder.Radius * alignedScale.x,
                        cylinder.Height * alignedScale.y,
                        cylinder.Direction);
                    if (!suppressWarnings)
                    {
                        LogApproximateShape(shape, collider.ShapeType);
                    }
                    return shape;
                }
                case ksShape.ShapeTypes.CONE:
                {
                    ksConeCollider cone = (ksConeCollider)collider;
                    Vector3 scale = transform.localScale;
                    Vector2 alignedScale = GetAxisAlignedScale(scale, cone.Direction);
                    ksShape shape = CreateCapsuleOrSphere(
                        cone.Radius * alignedScale.x,
                        cone.Height * alignedScale.y,
                        cone.Direction);
                    if (!suppressWarnings)
                    {
                        LogApproximateShape(shape, collider.ShapeType);
                    }
                    return shape;
                }
            }
            return null;
        }

        /// <summary>
        /// Creates a capsule or sphere to approximate box extents. A capsule will be created
        /// if the largest extent is at least twice as long as the next largest.
        /// </summary>
        /// <param name="extents">Extents to create an approximate shape for.</param>
        /// <returns>Approximate shape</returns>
        public static ksShape CreateApproximateShape(ksVector3 extents)
        {
            float radius;
            if (extents.X >= 2 * Math.Max(extents.Y, extents.Z))
            {
                radius = (extents.Y + extents.Z) / 2;
                return new ksCapsule(
                    radius,
                    extents.X * 2,
                    ksShape.Axis.X);
            }
            if (extents.Y >= 2 * Math.Max(extents.X, extents.Z))
            {
                radius = (extents.X + extents.Z) / 2;
                return new ksCapsule(
                    radius,
                    extents.Y * 2,
                    ksShape.Axis.Y);
            }
            if (extents.Z >= 2 * Math.Max(extents.X, extents.Y))
            {
                radius = (extents.X + extents.Y) / 2;
                return new ksCapsule(
                    radius,
                    extents.Z * 2,
                    ksShape.Axis.Z);
            }
            radius = (extents.X + extents.Y + extents.Z) / 3;
            return new ksSphere(radius);
        }

        /// <summary>
        /// Creates a capsule. Will create a sphere instead if the height is less than or equal to the diameter.
        /// </summary>
        /// <param name="radius">Radius</param>
        /// <param name="height">Height</param>
        /// <param name="alignment">Axis alignment</param>
        /// <returns>Capsule or sphere</returns>
        private static ksShape CreateCapsuleOrSphere(float radius, float height, ksShape.Axis alignment)
        {
            if (height <= radius * 2f)
            {
                return new ksSphere(radius);
            }
            return new ksCapsule(radius, height, alignment);
        }

        /// <summary>Logs a warning that we are approximating an unsupported shape type with another shape.</summary>
        /// <param name="shape">Approximate shape</param>
        /// <param name="originalType">Original shape type</param>
        private static void LogApproximateShape(ksShape shape, ksShape.ShapeTypes originalType)
        {
            ksLog.Warning(LOG_CHANNEL, "Scene queries in Unity are only supported with boxes, spheres, and capsules. Using a " +
                shape.Type + " to approximate a " + originalType);
        }

        /// <summary>
        /// Get the radius and axis scaling for a collider that uses a rotation axis alignment (capsule, cylinder, cone).
        /// </summary>
        /// <param name="scale">Game object scale</param>
        /// <param name="alignment">Axis of rotation.</param>
        /// <returns>A vector where x = radius scale, y = axis scale.</returns>
        private static Vector2 GetAxisAlignedScale(Vector3 scale, ksShape.Axis alignment)
        {
            switch (alignment)
            {
                case ksShape.Axis.X:
                    return new Vector2(Mathf.Max(scale.y, scale.z), scale.x);
                case ksShape.Axis.Z:
                    return new Vector2(Mathf.Max(scale.x, scale.y), scale.z);
                case ksShape.Axis.Y:
                default:
                    return new Vector2(Mathf.Max(scale.x, scale.z), scale.y);
            }
        }
    }
}
