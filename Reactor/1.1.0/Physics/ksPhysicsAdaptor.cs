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
using System.Collections.Generic;
using UnityEngine;

namespace KS.Reactor.Client.Unity
{
    /// <summary>
    /// <see cref="ksBasePhysics"/> implementation that uses Unity physics.
    /// </summary>
    public class ksPhysicsAdaptor : ksBasePhysics
    {
        /// <summary>Delegate used for initialization of type <typeparamref name="T"/>.</summary>
        /// <typeparam name="T">Type to initialize</typeparam>
        /// <param name="value">Value to initialize</param>
        private delegate void Initializer<T>(ref T value);

        private ksRoom m_room;

        /// <summary>Constructor</summary>
        /// <param name="room">The room to restrict physics queries to.</param>
        internal ksPhysicsAdaptor(ksRoom room)
        {
            m_room = room;
        }

        /// <summary>
        /// Apply entity transform updates to the physics simulation now rather than 
        /// waiting for the next simulation step.
        /// </summary>
        public override void SyncAll()
        {
            Physics.SyncTransforms();
        }

        /// <summary>
        /// Apply entity transform updates to the physics simulation now rather than 
        /// waiting for the next simulation step.
        /// </summary>
        public override void SyncTransforms()
        {
            Physics.SyncTransforms();
        }


        /// <summary>Gravity</summary>
        public new ksVector3 Gravity
        {
            get { return m_gravity; }
            set { m_gravity = value; }
        }

        /// <summary>Raycast query that checks if anything was hit.</summary>
        /// <param name="args">Raycast parameters</param>
        /// <returns>True if the raycast hit anything.</returns>
        public override bool RaycastAny(ksRaycastParams args)
        {
            return Raycast(args, ksQueryResultTypes.ANY).HasBlock;
        }

        /// <summary>Raycast query that finds the nearest hit.</summary>
        /// <param name="args">Raycast parameters</param>
        /// <param name="hit">Set to the nearest hit.</param>
        /// <returns>True if the raycast hit anything.</returns>
        public override bool RaycastNearest(ksRaycastParams args, out ksRaycastResult hit)
        {
            ksQueryHitResults<ksRaycastResult> results = Raycast(args, ksQueryResultTypes.NEAREST);
            hit = results.Block;
            return results.HasBlock;
        }

        /// <summary>
        /// Raycast query that returns the nearest blocking hit and any touching hits before the blocking hit.
        /// </summary>
        /// <param name="args">Raycast parameters</param>
        /// <returns>Raycast results</returns>
        public override ksQueryHitResults<ksRaycastResult> Raycast(ksRaycastParams args)
        {
            return Raycast(args, ksQueryResultTypes.MULTIPLE);
        }

        /// <summary>Raycast query</summary>
        /// <param name="args">Raycast parameters</param>
        /// <param name="resultType">The type of results the query will return.</param>
        /// <returns>Raycast results</returns>
        private ksQueryHitResults<ksRaycastResult> Raycast(
            ksRaycastParams args,
            ksQueryResultTypes resultType)
        {
            ksQueryHitResults<ksRaycastResult> results = new ksQueryHitResults<ksRaycastResult>();
            if (!args.Validate())
            {
                return results;
            }
            if (AutoSync)
            {
                SyncTransforms();
            }

            if ((args.Flags & ksQueryFlags.EXCLUDE_OVERLAPS) == 0)
            {
                // Unity raycasts ignore starting overlaps and ours don't. To make the behaviour consistent, we do a point
                // overlap test using a sphere overlap with radius 0.
                Collider[] overlaps = Physics.OverlapSphere(args.Origin, 0f);
                foreach (Collider unityCollider in overlaps)
                {
                    ksEntity entity;
                    ksICollider collider;
                    ksQueryHitTypes hitType = GetHitType(null, unityCollider, args, resultType,
                        out entity, out collider);
                    if (hitType == ksQueryHitTypes.NONE || 
                        ((args.Flags & ksQueryFlags.EXCLUDE_TOUCHES) != 0 && 
                        hitType == ksQueryHitTypes.TOUCH))
                    {
                        continue;
                    }
                    if (resultType != ksQueryResultTypes.MULTIPLE || hitType == ksQueryHitTypes.BLOCK)
                    {
                        if (!results.HasBlock)
                        {
                            if (resultType == ksQueryResultTypes.ANY)
                            {
                                results.HasBlock = true;
                                return results;
                            }
                            results.Block = new ksRaycastResult()
                            {
                                Entity = entity,
                                Collider = collider,
                                Point = args.Origin,
                                Normal = args.Direction.Normalized() * -1f,
                                Distance = 0f
                            };
                            if (resultType != ksQueryResultTypes.MULTIPLE)
                            {
                                return results;
                            }
                        }
                    }
                    else if (resultType == ksQueryResultTypes.MULTIPLE)
                    {
                        results.Touches.Add(new ksRaycastResult()
                        {
                            Entity = entity,
                            Collider = collider,
                            Point = args.Origin,
                            Normal = args.Direction.Normalized() * -1f,
                            Distance = 0f
                        });
                    }
                }
            }

            RaycastHit[] hits = Physics.RaycastAll(args.Origin, args.Direction, args.Distance);
            ProcessHits(ref results, hits, args, resultType);
            return results;
        }

        /// <summary>Sweep query that checks if anything was hit.</summary>
        /// <param name="args">Sweep arguments</param>
        /// <returns>True if the sweep hit anything.</returns>
        public override bool SweepAny(ksSweepParams args)
        {
            if (args.Validate())
            {
                switch (args.ObjectType)
                {
                    case ksQueryObjectTypes.SHAPE:
                    {
                        return ShapeSweep(args, ksQueryResultTypes.ANY).HasBlock;
                    }
                    case ksQueryObjectTypes.ENTITY:
                    {
                        return EntitySweep(args, ksQueryResultTypes.ANY).HasBlock;
                    }
                    case ksQueryObjectTypes.COLLIDER:
                    {
                        return ColliderSweep(args, ksQueryResultTypes.ANY).HasBlock;
                    }
                }
            }
            return false;
        }

        /// <summary>Sweep query that finds the nearest hit.</summary>
        /// <param name="args">Sweep arguments</param>
        /// <param name="hit">Set to the nearest hit.</param>
        /// <returns>True if the sweep hit anything.</returns>
        public override bool SweepNearest(ksSweepParams args, out ksSweepResult hit)
        {
            if (args.Validate())
            {
                ksQueryHitResults<ksSweepResult> results;
                switch (args.ObjectType)
                {
                    case ksQueryObjectTypes.SHAPE:
                    {
                        results =  ShapeSweep(args, ksQueryResultTypes.NEAREST);
                        break;
                    }
                    case ksQueryObjectTypes.ENTITY:
                    {
                        results = EntitySweep(args, ksQueryResultTypes.NEAREST);
                        break;
                    }
                    case ksQueryObjectTypes.COLLIDER:
                    {
                        results = ColliderSweep(args, ksQueryResultTypes.NEAREST);
                        break;
                    }
                    default:
                    {
                        hit = new ksSweepResult();
                        return false;
                    }
                }
                hit = results.Block;
                return results.HasBlock;
            }
            hit = new ksSweepResult();
            return false;
        }

        /// <summary>
        /// Sweep query that returns the nearest blocking hit and any touching hits before the blocking hit.
        /// </summary>
        /// <param name="args">Sweep parameters</param>
        /// <returns>List of sweep results</returns>
        public override ksQueryHitResults<ksSweepResult> Sweep(ksSweepParams args)
        {
            if (args.Validate())
            {
                switch (args.ObjectType)
                {
                    case ksQueryObjectTypes.SHAPE:
                    {
                        return ShapeSweep(args, ksQueryResultTypes.MULTIPLE);
                    }
                    case ksQueryObjectTypes.ENTITY:
                    {
                        return EntitySweep(args, ksQueryResultTypes.MULTIPLE);
                    }
                    case ksQueryObjectTypes.COLLIDER:
                    {
                        return ColliderSweep(args, ksQueryResultTypes.MULTIPLE);
                    }
                }
            }
            return new ksQueryHitResults<ksSweepResult>();
        }

        /// <summary>Sweep query.</summary>
        /// <param name="args">Sweep parameters</param>
        /// <param name="resultType">The type of results the query will return.</param>
        /// <returns>Sweep results</returns>
        private ksQueryHitResults<ksSweepResult> ShapeSweep(ksSweepParams args, ksQueryResultTypes resultType)
        {
            ksQueryHitResults<ksSweepResult> results = new ksQueryHitResults<ksSweepResult>();
            if (AutoSync)
            {
                SyncTransforms();
            }
            RaycastHit[] hits = SweepShape(args.Shape, args.Origin, args.Rotation, args.Direction, args.Distance);
            ProcessHits(ref results, hits, args, resultType);
            return results;
        }

        /// <summary>Entity sweep query.</summary>
        /// <param name="args">Sweep parameters</param>
        /// <param name="resultType">The type of results the query will return.</param>
        /// <returns>Sweep results</returns>
        private ksQueryHitResults<ksSweepResult> EntitySweep(ksSweepParams args, ksQueryResultTypes resultType)
        {
            ksQueryHitResults<ksSweepResult> results = new ksQueryHitResults<ksSweepResult>();
            if (AutoSync)
            {
                SyncTransforms();
            }
            ksEntityComponent entityComponent = ((ksEntity)args.Entity).GameObject.GetComponent<ksEntityComponent>();
            Transform transform = entityComponent.transform;
            foreach (ksIUnityCollider collider in entityComponent.GetColliders())
            {
                if (collider == null || (!collider.IsEnabled && (args.Flags & ksQueryFlags.USE_DISABLED_COLLIDERS) == 0))
                {
                    continue;
                }

                ksShape shape = ksShapeUtils.GetColliderShape(collider);
                if (shape == null)
                {
                    continue;
                }

                if (args.EntityColliderFilter != null && !args.EntityColliderFilter.Filter(collider, args))
                {
                    continue;
                }

                ksVector3 position = args.Origin + args.Rotation * Vector3.Scale(transform.localScale,
                    collider.Center);
                RaycastHit[] hits = SweepShape(shape, position, args.Rotation, args.Direction, args.Distance);

                if (ProcessHits(ref results, hits, args, resultType, collider, 
                    (ref ksSweepResult hit) => hit.QueryCollider = collider))
                {
                    break;
                }
            }
            return results;
        }

        /// <summary>Collider sweep query.</summary>
        /// <param name="args">Sweep parameters</param>
        /// <param name="resultType">The type of results the query will return.</param>
        /// <returns>Sweep results</returns>
        private ksQueryHitResults<ksSweepResult> ColliderSweep(ksSweepParams args, ksQueryResultTypes resultType)
        {
            ksQueryHitResults<ksSweepResult> results = new ksQueryHitResults<ksSweepResult>();
            if (AutoSync)
            {
                SyncTransforms();
            }
            ksIUnityCollider collider = args.Collider as ksIUnityCollider;
            if (collider == null || (!collider.IsEnabled && (args.Flags & ksQueryFlags.USE_DISABLED_COLLIDERS) == 0))
            {
                return results;
            }
            ksShape shape = ksShapeUtils.GetColliderShape(collider);
            if (shape == null)
            {
                return results;
            }
            Transform transform = ((ksEntity)collider.Entity).GameObject.transform;
            ksVector3 position = args.Origin + args.Rotation * Vector3.Scale(transform.localScale, collider.Center);
            RaycastHit[] hits = SweepShape(shape, position, args.Rotation, args.Direction, args.Distance);
            ProcessHits(ref results, hits, args, resultType, collider,
                (ref ksSweepResult hit) => hit.QueryCollider = collider);
            return results;
        }

        /// <summary>Filters Unity's raycast results and converts to our format.</summary>
        /// <typeparam name="T">
        /// Type to convert to. Either <see cref="ksRaycastResult"/> or <see cref="ksSweepResult"/>.
        /// </typeparam>
        /// <param name="results">Results struct to update.</param>
        /// <param name="args">Query parameters</param>
        /// <param name="resultType">The type of results the query will return.</param>
        /// <param name="queryCollider">Collider used in the query. Null for shape queries.</param>
        /// <param name="initializer">Callback to initialize the hit results.</param>
        /// <returns>
        /// True if the result type is <see cref="ksQueryResultTypes.ANY"/> and the sweep hit something, or if the
        /// blocking hit distance is zero.
        /// </returns>
        private bool ProcessHits<T>(
            ref ksQueryHitResults<T> results,
            RaycastHit[] hits,
            ksBaseQueryParams args,
            ksQueryResultTypes resultType,
            ksICollider queryCollider = null,
            Initializer<T> initializer = null) where T : ksIQueryHitResult, new()
        {
            foreach (RaycastHit hit in hits)
            {
                if ((args.Flags & ksQueryFlags.EXCLUDE_OVERLAPS) != 0 && hit.distance <= 0)
                {
                    continue;
                }
                ksEntity entity;
                ksICollider collider;
                ksQueryHitTypes hitType = GetHitType(queryCollider, hit.collider, args, resultType,
                    out entity, out collider);
                if (hitType == ksQueryHitTypes.NONE ||
                    ((args.Flags & ksQueryFlags.EXCLUDE_TOUCHES) != 0 &&
                    hitType == ksQueryHitTypes.TOUCH))
                {
                    continue;
                }
                if (resultType == ksQueryResultTypes.ANY)
                {
                    results.HasBlock = true;
                    return true;
                }
                if (resultType != ksQueryResultTypes.MULTIPLE || hitType == ksQueryHitTypes.BLOCK)
                {
                    if (!results.HasBlock || results.Block.Distance > hit.distance)
                    {
                        T block = new T()
                        {
                            Entity = entity,
                            Collider = collider,
                            Point = hit.point,
                            Normal = hit.normal,
                            Distance = hit.distance
                        };
                        if (initializer != null)
                        {
                            initializer(ref block);
                        }
                        results.Block = block;
                        // Remove all hits farther than the nearest blocking hit.
                        results.Touches.RemoveAll((T result) => result.Distance > hit.distance);
                        if (hit.distance <= 0f)
                        {
                            return true;
                        }
                    }
                }
                else if (resultType == ksQueryResultTypes.MULTIPLE && 
                    (!results.HasBlock || hit.distance <= results.Block.Distance))
                {
                    T touch = new T()
                    {
                        Entity = entity,
                        Collider = collider,
                        Point = hit.point,
                        Normal = hit.normal,
                        Distance = hit.distance
                    };
                    if (initializer != null)
                    {
                        initializer(ref touch);
                    }
                    results.Touches.Add(touch);
                }
            }
            return false;
        }

        /// <summary>Overlap query that checks for any overlap.</summary>
        /// <param name="args">Overlap parameters</param>
        /// <returns>True if anything was overlapping.</returns>
        public override bool OverlapAny(ksOverlapParams args)
        {
            if (args.Validate())
            {
                switch (args.ObjectType)
                {
                    case ksQueryObjectTypes.SHAPE:
                    {
                        return ShapeOverlap(null, args);
                    }
                    case ksQueryObjectTypes.ENTITY:
                    {
                        return EntityOverlap(null, args);
                    }
                    case ksQueryObjectTypes.COLLIDER:
                    {
                        return ColliderOverlap(null, args);
                    }
                }
            }
            return false;
        }

        /// <summary>Overlap query.</summary>
        /// <param name="args">Overlap parameters</param>
        /// <returns>List of overlap results.</returns>
        public override List<ksOverlapResult> Overlap(ksOverlapParams args)
        {
            List<ksOverlapResult> results = new List<ksOverlapResult>();
            if (args.Validate())
            {
                switch (args.ObjectType)
                {
                    case ksQueryObjectTypes.SHAPE:
                    {
                        ShapeOverlap(results, args);
                        break;
                    }
                    case ksQueryObjectTypes.ENTITY:
                    {
                        EntityOverlap(results, args);
                        break;
                    }
                    case ksQueryObjectTypes.COLLIDER:
                    {
                        ColliderOverlap(results, args);
                        break;
                    }
                }
            }
            return results;
        }

        /// <summary>Overlap query</summary>
        /// <param name="results">Results to fill. If null, check for any hit.</param>
        /// <param name="args">Overlap parameters</param>
        /// <returns>True if there were any overlapping entities.</returns>
        private bool ShapeOverlap(List<ksOverlapResult> results, ksOverlapParams args)
        {
            if (AutoSync)
            {
                SyncTransforms();
            }
            Collider[] colliders = OverlapShape(args.Shape, args.Origin, args.Rotation);
            if (ProcessOverlaps(results, colliders, args))
            {
                return true;
            }
            return results != null && results.Count > 0;
        }

        /// <summary>Entity overlap query</summary>
        /// <param name="results">Results to fill. If null, check for any hit.</param>
        /// <param name="args">Overlap parameters</param>
        /// <returns>True if there were any overlapping entities.</returns>
        private bool EntityOverlap(List<ksOverlapResult> results, ksOverlapParams args)
        {
            if (AutoSync)
            {
                SyncTransforms();
            }
            ksEntityComponent entityComponent = ((ksEntity)args.Entity).GameObject.GetComponent<ksEntityComponent>();
            Transform transform = entityComponent.transform;
            foreach (ksIUnityCollider collider in entityComponent.GetColliders())
            {
                if (!collider.IsEnabled && (args.Flags & ksQueryFlags.USE_DISABLED_COLLIDERS) == 0)
                {
                    continue;
                }

                ksShape shape = ksShapeUtils.GetColliderShape(collider);
                if (shape == null)
                {
                    continue;
                }

                if (args.EntityColliderFilter != null && !args.EntityColliderFilter.Filter(collider, args))
                {
                    continue;
                }

                ksVector3 position = args.Origin + args.Rotation * Vector3.Scale(transform.localScale,
                    collider.Center);
                Collider[] colliders = OverlapShape(shape, position, args.Rotation);

                if (ProcessOverlaps(results, colliders, args, collider))
                {
                    return true;
                }
            }
            return results != null && results.Count > 0;
        }
        /// <summary>Collider overlap query</summary>
        /// <param name="results">Results to fill. If null, check for any hit.</param>
        /// <param name="args">Overlap parameters</param>
        /// <returns>True if there were any overlapping entities.</returns>
        private bool ColliderOverlap(List<ksOverlapResult> results, ksOverlapParams args)
        {
            if (AutoSync)
            {
                SyncTransforms();
            }
            ksIUnityCollider collider = args.Collider as ksIUnityCollider;
            if (collider == null || (!collider.IsEnabled && (args.Flags & ksQueryFlags.USE_DISABLED_COLLIDERS) == 0))
            {
                return false;
            }
            ksShape shape = ksShapeUtils.GetColliderShape(collider);
            if (shape == null)
            {
                return false;
            }

            Transform transform = ((ksEntity)collider.Entity).GameObject.transform;
            ksVector3 position = args.Origin + args.Rotation * Vector3.Scale(transform.localScale, collider.Center);
            Collider[] colliders = OverlapShape(shape, position, args.Rotation);

            if (ProcessOverlaps(results, colliders, args, collider))
            {
                return true;
            }
            return results != null && results.Count > 0;
        }

        /// <summary>Filters Unity's overlap results and converts to our format.</summary>
        /// <param name="results">Results to fill. If null, looks for any overlap.</param>
        /// <param name="colliders">Overlapping colliders</param>
        /// <param name="args">Overlap parameters</param>
        /// <param name="queryCollider">Collider used in the query. Null for shape queries.</param>
        /// <returns>True if the <paramref name="results"/> is null and an overlap was found.</returns>
        private bool ProcessOverlaps(
            List<ksOverlapResult> results,
            Collider[] colliders,
            ksBaseQueryParams args,
            ksICollider queryCollider = null)
        {
            ksQueryResultTypes resultType = results == null ?
                ksQueryResultTypes.ANY : ksQueryResultTypes.MULTIPLE;
            foreach (Collider unityCollider in colliders)
            {
                ksEntity entity;
                ksICollider collider;
                ksQueryHitTypes hitType = GetHitType(queryCollider, unityCollider, args, resultType,
                    out entity, out collider);
                if (hitType == ksQueryHitTypes.NONE ||
                    ((args.Flags & ksQueryFlags.EXCLUDE_TOUCHES) != 0 &&
                    hitType == ksQueryHitTypes.TOUCH))
                {
                    continue;
                }
                if (results == null)
                {
                    return true;
                }
                results.Add(new ksOverlapResult()
                {
                    QueryCollider = queryCollider,
                    Entity = entity,
                    Collider = collider
                });
            }
            return false;
        }

        /// <summary>
        /// Compute the minimal translation required to separate the given colliders apart at specified poses.
        /// </summary>
        /// <param name="collider0"></param>
        /// <param name="position0"></param>
        /// <param name="rotation0"></param>
        /// <param name="collider1"></param>
        /// <param name="position1"></param>
        /// <param name="rotation1"></param>
        /// <param name="direction">Direction along which the translation required to separate the colliders apart is minimal.</param>
        /// <param name="distance">The distance along direction that is required to separate the colliders apart.</param>
        /// <returns>True if the colliders were overlapping.</returns>
        public override bool ComputePenetration(
            ksICollider collider0, ksVector3 position0, ksQuaternion rotation0,
            ksICollider collider1, ksVector3 position1, ksQuaternion rotation1,
            out ksVector3 direction, out float distance)
        {
            direction = ksVector3.Zero;
            distance = 0;
            if (collider0 == null || collider1 == null)
            {
                return false;
            }

            position0 = ApplyReactorColliderScale(collider0, position0, rotation0);
            position1 = ApplyReactorColliderScale(collider1, position1, rotation1);

            Vector3 unityDir;
            bool result = Physics.ComputePenetration(
                ((ksIUnityCollider)collider0).Collider, position0, rotation0,
                ((ksIUnityCollider)collider1).Collider, position1, rotation1,
                out unityDir, out distance);
            direction = unityDir;
            return result;
        }

        /// <summary>
        /// Get the closest point on a collider to another point.
        /// Currently supported colliders: box, sphere, capsule, convexmesh.
        /// </summary>
        /// <param name="point">Point to measure from.</param>
        /// <param name="collider">Collider to get the closest point from.</param>
        /// <returns>Closest point on the collider.</returns>
        public override ksVector3 GetClosestPoint(ksVector3 point, ksICollider collider)
        {
            if (collider == null)
            {
                return point;
            }
            Component component = ((ksIUnityCollider)collider).Component;
            if (component == null)
            {
                return point;
            }
            return GetClosestPoint(point, collider, component.transform.position,
                component.transform.rotation);
        }

        /// <summary>
        /// Get the closest point on a collider to another point.
        /// Currently supported colliders: box, sphere, capsule, convexmesh.
        /// </summary>
        /// <param name="point">Point to measure from.</param>
        /// <param name="collider">Collider to get the closest point from.</param>
        /// <param name="position">Collider position</param>
        /// <param name="rotation">Collider rotation</param>
        /// <returns>Closest point on the collider.</returns>
        public override ksVector3 GetClosestPoint(ksVector3 point, ksICollider collider, ksVector3 position, ksQuaternion rotation)
        {
            if (collider == null)
            {
                return point;
            }
            position = ApplyReactorColliderScale(collider, position, rotation);
            return Physics.ClosestPoint(point, ((ksIUnityCollider)collider).Collider, position, rotation);
        }

        /// <summary>
        /// Apply scale and offset from a Reactor cone and cylinder colliders to a collider position.
        /// </summary>
        /// <param name="collider">Reactor cone or cylinder collider</param>
        /// <param name="position">Postion to adjust</param>
        /// <param name="rotation">Rotation to apply to position adjustment</param>
        /// <returns>Position adjusted by the collider offset.</returns>
        private ksVector3 ApplyReactorColliderScale(ksICollider collider, ksVector3 position, ksQuaternion rotation)
        {
            ksCylinderCollider cylinder = collider as ksCylinderCollider;
            if (cylinder != null)
            {
                return position + rotation * ksVector3.Scale(cylinder.Center, cylinder.transform.lossyScale);
            }
            ksConeCollider cone = collider as ksConeCollider;
            if (cone != null)
            {
                return position + rotation * ksVector3.Scale(cone.Center, cone.transform.lossyScale);
            }
            return position;
        }

        /// <summary>Gets a <see cref="ksIUnityCollider"/> from a <see cref="ksColliderData"/>.</summary>
        /// <param name="colliderData">Collider data</param>
        /// <returns>Collider</returns>
        private ksIUnityCollider GetCollider(ksColliderData colliderData)
        {
            Collider collider = colliderData.Collider as Collider;
            if (collider != null)
            {
                return new ksUnityCollider(collider, colliderData);
            }
            return colliderData.Collider as ksIUnityCollider;
        }

        /// <summary>
        /// Applies our query filtering rules to a potential query hit. Gets the <see cref="ksEntity"/> and
        /// <see cref="ksICollider"/> for a successful hit.
        /// </summary>
        /// <param name="queryCollider">Collider used in the query. Null for shape queries.</param>
        /// <param name="unityCollider">Collider that was hit.</param>
        /// <param name="args">Query parameters</param>
        /// <param name="resultType">The type of results the query will return.</param>
        /// <param name="entity">Entity for the hit collider.</param>
        /// <param name="collider">Collider that was hit, behind our interface.</param>
        /// <returns>Hit type</returns>
        private ksQueryHitTypes GetHitType(
            ksICollider queryCollider,
            Collider unityCollider,
            ksBaseQueryParams args,
            ksQueryResultTypes resultType,
            out ksEntity entity,
            out ksICollider collider)
        {
            entity = null;
            collider = null;
            if (unityCollider == null)
            {
                return ksQueryHitTypes.NONE;
            }

            Component colliderDataComponent;
            ksEntityComponent entityComponent = GetColliderEntityComponent(unityCollider, out colliderDataComponent);
            if (entityComponent == null || entityComponent.Entity == null)
            {
                return ksQueryHitTypes.NONE;
            }
            entity = entityComponent.Entity;

            // Ignore entities from other rooms.
            if (entity.Room != m_room)
            {
                return ksQueryHitTypes.NONE;
            }

            // Ignore the excluded entity.
            if (entity == args.ExcludeEntity)
            {
                return ksQueryHitTypes.NONE;
            }

            bool isStatic = entityComponent.GetComponent<Rigidbody>() == null;
            // Ignore static objects if the static filter flag is not set.
            if (isStatic && (args.Flags & ksQueryFlags.STATIC) == 0)
            {
                return ksQueryHitTypes.NONE;
            }
            // Ignore dynamic objects if the dynamic filter flag is not set.
            if (!isStatic && (args.Flags & ksQueryFlags.DYNAMIC) == 0)
            {
                return ksQueryHitTypes.NONE;
            }

            // Ignore non-query colliders.
            ksColliderData colliderData = entityComponent.GetColliderData(colliderDataComponent);
            if (colliderData == null || !colliderData.IsQuery)
            {
                return ksQueryHitTypes.NONE;
            }

            collider = GetCollider(colliderData);
            if (args.Filter == null)
            {
                return ksQueryHitTypes.TOUCH;
            }
            return args.Filter.Filter(queryCollider, collider, resultType, args);
        }

        /// <summary>Gets the entity component for a collider.</summary>
        /// <param name="collider">Collider</param>
        /// <param name="colliderDataComponent">
        /// The collider component the <see cref="ksColliderData"/> is associated with. If <paramref name="collider"/>
        /// is a child mesh collider created for <see cref="ksCylinderCollider"/> or <see cref="ksConeCollider"/>, this
        /// is the cylinder or cone collider. Otherwise this is <paramref name="collider"/>.
        /// </param>
        /// <returns>Entity component, or null if none was found.</returns>
        private ksEntityComponent GetColliderEntityComponent(Collider collider, out Component colliderDataComponent)
        {
            ksEntityComponent entityComponent = collider.GetComponent<ksEntityComponent>();
            if (entityComponent != null)
            {
                colliderDataComponent = collider;
                return entityComponent;
            }
            // ksCone and ksCylinder create a child object with a MeshCollider and a ksEntity.Indicator. Look for an
            // entity component in the parent if we see a ksEntity.Indicator.
            ksEntity.Indicator indicator = collider.GetComponent<ksEntity.Indicator>();
            if (indicator != null)
            {
                colliderDataComponent = indicator.Collider.Component;
                if (collider.transform.parent == null)
                {
                    return null;
                }
                return collider.transform.parent.GetComponent<ksEntityComponent>();
            }
            colliderDataComponent = null;
            return null;
        }

        /// <summary>
        /// Gets the <see cref="ksIUnityCollider"/> and <see cref="ksEntityComponent"/> for a collider.
        /// </summary>
        /// <param name="collider">Collider attached to the entity we are looking for.</param>
        /// <param name="unityCollider">Is assigned the <see cref="ksIUnityCollider"/> found on the entity.</param>
        /// <param name="entityComponent">Is assigned the <see cref="ksEntityComponent"/> found on the entity.</param>
        public static void TryGetKSCollider(
            Collider unityCollider,
            out ksIUnityCollider ksCollider,
            out ksEntityComponent entityComponent)
        {
            ksCollider = null;
            entityComponent = unityCollider.GetComponent<ksEntityComponent>();
            if (entityComponent == null)
            {
                ksEntity.Indicator indicator = unityCollider.GetComponent<ksEntity.Indicator>();
                if (indicator != null)
                {
                    entityComponent = unityCollider.transform.parent == null ?
                        null : unityCollider.transform.parent.GetComponent<ksEntityComponent>();
                    ksCollider = indicator.Collider;
                }
            }
            else
            {
                ksCollider = new ksUnityCollider(unityCollider);
            }
        }

        /// <summary>Shape sweep query</summary>
        /// <param name="shape">Shape to sweep</param>
        /// <param name="origin">Origin of the sweep</param>
        /// <param name="rotation">Shape rotation</param>
        /// <param name="direction">Sweep direction</param>
        /// <param name="distance">Sweep distance</param>
        /// <returns>Sweep hits</returns>
        private RaycastHit[] SweepShape(
            ksShape shape,
            ksVector3 origin,
            ksQuaternion rotation,
            ksVector3 direction,
            float distance)
        {
            switch (shape.Type)
            {
                case ksShape.ShapeTypes.SPHERE:
                {
                    ksSphere sphere = (ksSphere)shape;
                    return Physics.SphereCastAll(origin, sphere.Radius, direction, distance);
                }
                case ksShape.ShapeTypes.BOX:
                {
                    ksBox box = (ksBox)shape;
                    return Physics.BoxCastAll(origin, box.HalfExtents, direction, rotation, distance);
                }
                case ksShape.ShapeTypes.CAPSULE:
                {
                    ksCapsule capsule = (ksCapsule)shape;
                    ksVector3 point1, point2;
                    ksShapeUtils.GetCapsuleEndPoints(capsule, origin, rotation, out point1, out point2);
                    return Physics.CapsuleCastAll(point1, point2, capsule.Radius, direction, distance);
                }
            }
            return new RaycastHit[0];
        }

        /// <summary>Shape overlap query</summary>
        /// <param name="shape">Overlap shape</param>
        /// <param name="position">Shape position</param>
        /// <param name="rotation">Shape rotation</param>
        /// <returns>Overlapping colliders</returns>
        private Collider[] OverlapShape(ksShape shape, ksVector3 position, ksQuaternion rotation)
        {
            switch (shape.Type)
            {
                case ksShape.ShapeTypes.SPHERE:
                {
                    ksSphere sphere = (ksSphere)shape;
                    return Physics.OverlapSphere(position, sphere.Radius);
                }
                case ksShape.ShapeTypes.BOX:
                {
                    ksBox box = (ksBox)shape;
                    return Physics.OverlapBox(position, box.HalfExtents, rotation);
                }
                case ksShape.ShapeTypes.CAPSULE:
                {
                    ksCapsule capsule = (ksCapsule)shape;
                    ksVector3 point1, point2;
                    ksShapeUtils.GetCapsuleEndPoints(capsule, position, rotation, out point1, out point2);
                    return Physics.OverlapCapsule(point1, point2, capsule.Radius);
                }
            }
            return new Collider[0];
        }
    }
}