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
using System.Linq;
using System.Text;
using UnityEngine;

namespace KS.Reactor.Client.Unity
{
    /// <summary>
    /// Predictor asset that creates instances of <see cref="ksConvergingInputPredictor"/> when assigned in the entity
    /// or room type inspector.
    /// </summary>
    [CreateAssetMenu(menuName = ksMenuNames.REACTOR + "Converging Input Predictor", order = ksMenuGroups.BUILTIN_ASSETS)]
    public class ksConvergingInputPredictorAsset : ksPredictor
    {
        /// <summary>Does the predictor require a player controller?</summary>
        public override bool RequiresController
        {
            get { return true; }
        }

        /// <summary>Describes how to predict a property.</summary>
        [Serializable]
        public struct PropertyBehaviour
        {
            [Tooltip("Property id")]
            /// <summary>Property id.</summary>
            public uint Property;

            [Tooltip("The prediction behaviour of the property.")]
            /// <summary>Prediction behaviour.</summary>
            public ksPredictionBehaviour Type;

            [Tooltip("If the difference between predicted and server property change rates exceeds the tolerance, inputs " +
            "will be replayed using the new server property value and change rate to get a more accurate prediction.")]
            /// <summary>
            /// If the difference between predicted and server property change rates exceeds this, inputs will be replayed using
            /// the new server property value and change rate to get a more accurate prediction. <= 0 for no limit.
            /// </summary>
            public float Tolerance;
        }

        [Tooltip("Lower values make the predictor try to converge more aggressively")]
        /// <summary>Lower values mean we try to converge more aggressively.</summary>
        public float ConvergeMultiplier;

        [Tooltip("Maximum time step for the client over a single frame. Limiting the maximum time step can improve " +
            "behaviour when there are dips in frame rate.")]
        /// <summary>
        /// Maximum time step for the client over a single frame. If a frame's time delta is larger than this, this
        /// is used as the time delta instead. The predictor behaves poorly with large time steps, and limiting the
        /// maximum time step can improve the behaviour when there are dips in frame rate.
        /// </summary>
        public float MaxDeltaTime;

        [Tooltip("Latency in seconds to use in calculations before the real latency is determined.")]
        /// <summary>Latency in seconds to use before latency is determined.</summary>
        public float DefaultLatency;

        [Tooltip("If true, converging prediction is not run on position and the player controller alone determines " +
            "the client position.")]
        /// <summary>
        /// If true, converging prediction is not run on the position. Instead the player controller alone
        /// determines the client position.
        /// </summary>
        public bool UseClientOnlyPosition;

        [Tooltip("If true, converging prediction is not run on rotation and the player controller alone determines " +
            "the client rotation.")]
        /// <summary>
        /// If true, converging prediction is not run on the rotation. Instead the player controller alone
        /// determines the client rotation.
        /// </summary>
        public bool UseClientOnlyRotation;

        [Tooltip("If true, converging prediction is not run on scale and the player controller alone determines " +
            "the client scale.")]
        /// <summary>
        /// If true, converging prediction is not run on the scale. Instead the player controller alone determines
        /// the client scale.
        /// </summary>
        public bool UseClientOnlyScale;

        [Range(1, 10)]
        [Tooltip("Max number of sweep-and-slide iterations for predicting collisions. One to stop after the first " +
            "sweep without sliding.")]
        /// <summary>
        /// Max number of sweep-and-slide iterations for predicting collisions. One to stop after the first sweep
        /// without sliding.
        /// </summary>
        public int SweepAndSlideIterations;

        [Tooltip("Do not allow slides that change the movement direction by more than this angle in degrees. " +
        "Less than zero for no limit.")]
        /// <summary>
        /// Do not allow slides that change the movement direction by more than this angle in degrees. Less than zero
        /// for no limit.
        /// </summary>
        [Range(-1, 90)]
        public float MaxSlideAngle;

        [Tooltip("Do not allow sliding in a direction that differs from the original direction by more than this " +
            "angle in degrees. Less than zero for no limit.")]
        /// <summary>
        /// Do not allow sliding in a direction that differs from the original direction by more than this angle in
        /// degrees. Less than zero for no limit.
        /// </summary>
        [Range(-1, 180)]
        public float MaxTotalSlideAngle;

        [Min(0f)]
        [Tooltip("Keep this much distance between other objects. Helps prevent penetrations.")]
        /// <summary>Keep this much distance between other objects. Helps prevent penetrations.</summary>
        public float SeparationOffset;

        [Tooltip("If true and the controlled entity is a kinematic rigid body, will check for collisions with " +
            "dynamic rigid bodies when updating the render position to prevent the render position from embedding " +
            "with dynamic rigid bodies that will get pushed out of the way.")]
        /// <summary>
        /// If true and the controlled entity is a kinematic rigid body, 
        /// <see cref="ksConvergingInputPredictor.MoveCheck"/> will check for collisions with dynamic rigid bodies when
        /// updating the render position to prevent the render position from embedding with dynamic rigid bodies that
        /// will get pushed out of the way.
        /// </summary>
        public bool CheckKinematicDynamicCollisions;

        [Tooltip("Maximum distance between client and server before the client will tunnel through objects to get " +
            "to the server position.")]
        /// <summary>
        /// Maximum distance between client and server before the client will tunnel through objects to get to the
        /// server position.
        /// </summary>
        public float TunnelDistance;

        [Tooltip("Maximum difference between server and predicted velocity before inputs are replayed. Less than or " +
            "equal to zero for no limit.")]
        /// <summary>
        /// Maximum difference between server and predicted velocity before inputs are replayed. Less than or equal to
        /// zero for no limit.
        /// </summary>
        public float VelocityTolerance;

        [Tooltip("Maximum difference between server and predicted angular velocity before inputs are replayed. Less " +
            "than or equal to zero for no limit.")]
        /// <summary>
        /// Maximum difference between server and predicted angular velocity before inputs are replayed. Less than or
        /// equal to zero for no limit.
        /// </summary>
        public float AngularVelocityTolerance;

        [Tooltip("Which properties to predict and how to predict them.")]
        /// <summary>Which properties to predict and how to predict them.</summary>
        public PropertyBehaviour[] PredictedProperties;

        private ksConvergingInputPredictor.ConfigData m_config;

        /// <summary>Constructor</summary>
        public ksConvergingInputPredictorAsset()
        {
            // Get the default values from the default config.
            ksConvergingInputPredictor.ConfigData config = ksConvergingInputPredictor.ConfigData.Default;
            ConvergeMultiplier = config.ConvergeMultiplier;
            MaxDeltaTime = config.MaxDeltaTime;
            DefaultLatency = config.DefaultLatency;
            UseClientOnlyPosition = config.UseClientOnlyPosition;
            UseClientOnlyRotation = config.UseClientOnlyRotation;
            UseClientOnlyScale = config.UseClientOnlyScale;
            SweepAndSlideIterations = config.SweepAndSlideIterations;
            MaxSlideAngle = config.MaxSlideAngle;
            MaxTotalSlideAngle = config.MaxTotalSlideAngle;
            SeparationOffset = config.SeparationOffset;
            CheckKinematicDynamicCollisions = config.CheckKinematicDynamicCollisions;
            TunnelDistance = config.TunnelDistance;
            VelocityTolerance = config.VelocityTolerance;
            AngularVelocityTolerance = config.AngularVelocityTolerance;
        }

        /// <summary>Creates an instance of the predictor.</summary>
        /// <returns>Predictor instance.</returns>
        public override ksIPredictor CreateInstance()
        {
            ksConvergingInputPredictor predictor = new ksConvergingInputPredictor();
            if (m_config == null)
            {
                m_config = new ksConvergingInputPredictor.ConfigData();
                m_config.ConvergeMultiplier = ConvergeMultiplier;
                m_config.MaxDeltaTime = MaxDeltaTime;
                m_config.DefaultLatency = DefaultLatency;
                m_config.UseClientOnlyPosition = UseClientOnlyPosition;
                m_config.UseClientOnlyRotation = UseClientOnlyRotation;
                m_config.UseClientOnlyScale = UseClientOnlyScale;
                m_config.SweepAndSlideIterations = SweepAndSlideIterations;
                m_config.MaxSlideAngle = MaxSlideAngle;
                m_config.MaxTotalSlideAngle = MaxTotalSlideAngle;
                m_config.SeparationOffset = SeparationOffset;
                m_config.CheckKinematicDynamicCollisions = CheckKinematicDynamicCollisions;
                m_config.TunnelDistance = TunnelDistance;
                m_config.VelocityTolerance = VelocityTolerance;
                m_config.AngularVelocityTolerance = AngularVelocityTolerance;
                if (PredictedProperties != null)
                {
                    foreach (PropertyBehaviour prop in PredictedProperties)
                    {
                        m_config.PredictedProperties[prop.Property] =
                            new ksConvergingInputPredictor.PropertyBehaviour(prop.Type, prop.Tolerance);
                    }
                }
            }
            predictor.Config = m_config;
            return predictor;
        }

        /// <summary>
        /// Logs an error stating the predictor cannot be used directly and to call <see cref="CreateInstance()"/>
        /// instead.
        /// </summary>
        /// <returns>false</returns>
        public override bool Initialize()
        {
            ksLog.Error(this, "ksConvergingInputPredictorAsset cannot be used as a predictor directly. Instead call " +
                "CreateInstance to create an instance to assign.");
            return false;
        }

        /// <summary>
        /// Finds the largest collider size along the x or z axis. Returns one if the entity has no colliders.
        /// </summary>
        /// <param name="baseEntity">Entity to calculate velocity tolerance for.</param>
        /// <returns>Velocity tolerance for the entity.</returns>
        public static float CalculateMaxSizeXZ(ksBaseEntity baseEntity)
        {
            ksEntity entity = (ksEntity)baseEntity;
            float maxSize = 0f;
            if (entity.GameObject != null)
            {
                foreach (ksIUnityCollider collider in entity.GameObject.GetComponent<ksEntityComponent>().GetColliders())
                {
                    if (collider.IsEnabled)
                    {
                        maxSize = Math.Max(maxSize, GetMaxSizeXZ(collider));
                    }
                }
            }
            return maxSize <= 0f ? 1f : maxSize;
        }

        /// <summary>
        /// Gets the size of the collider in either along either the x or z axis; whichever is larger.
        /// </summary>
        /// <param name="collider">Collider to get size from.</param>
        /// <returns>The the size of the collider along the x or z axis; whichever is larger.</returns>
        public static float GetMaxSizeXZ(ksIUnityCollider collider)
        {
            if (collider is ksUnityCollider)
            {
                Collider ucollider = (Collider)collider.Component;
                return Math.Max(ucollider.bounds.size.x, ucollider.bounds.size.z);
            }
            ksCylinderCollider cylinder = collider as ksCylinderCollider;
            if (cylinder != null)
            {
                return cylinder.Radius * 2f * Math.Max(cylinder.transform.localScale.x, cylinder.transform.localScale.z);
            }
            ksConeCollider cone = collider as ksConeCollider;
            if (cone != null)
            {
                return cone.Radius * 2f * Math.Max(cone.transform.localScale.x, cone.transform.localScale.z);
            }
            return 0f;
        }
    }
}
