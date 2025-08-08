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
    /// Predictor asset that creates instances of <see cref="ksLinearPredictor"/> when assigned in the entity or room
    /// type inspector.
    /// </summary>
    [CreateAssetMenu(menuName = ksMenuNames.REACTOR + "Linear Predictor", order = ksMenuGroups.BUILTIN_ASSETS)]
    public class ksLinearPredictorAsset : ksPredictor
    {
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

            [Tooltip("Property-correction interpolation rate per second. If less than or equal to zero, " +
                "PositionCorrectionRate is used for linearly interpolated properties and RotationCorrectionRate is " +
                "used for spherically interpolated propreties. Or if it uses wrap float interpolation, the " +
                "difference between the min and max range values is used.")]
            /// <summary>Correction interpolation rate. If less than or equal to zero,
            /// <see cref="PositionCorrectionRate"/> will be used if it is linearly interpolated, and
            /// <see cref="RotationCorrectionRate"/> will be used if it is spherically interpolated. If the property
            /// uses wrap float interpolation and has no rate set, it will use the difference between the min and the
            /// max range values.</summary>
            public float CorrectionRate;
        }

        [Tooltip("Position-correction interpolation rate per second.")]
        /// <summary>Position-correction interpolation rate per second.</summary>
        public float PositionCorrectionRate = 10f;

        [Tooltip("Rotation-correction interpolation rate in degrees per second.")]
        /// <summary>Rotation-correction interpolation rate in degrees per second.</summary>
        public float RotationCorrectionRate = 360f;

        [Tooltip("Scale-correction interpolation rate per second.")]
        /// <summary>Scale-correction interpolation rate per second.</summary>
        public float ScaleCorrectionRate = 10f;

        [Tooltip("Which properties to predict and how to predict them.")]
        /// <summary>Which properties to predict and how to predict them.</summary>
        public PropertyBehaviour[] PredictedProperties;

        private ksLinearPredictor.ConfigData m_config;

        /// <summary>
        /// Constructor
        /// </summary>
        public ksLinearPredictorAsset()
        {
            // Get the default values from the default config.
            ksLinearPredictor.ConfigData config = ksLinearPredictor.ConfigData.Default;
            PositionCorrectionRate = config.PositionCorrectionRate;
            RotationCorrectionRate = config.RotationCorrectionRate;
            ScaleCorrectionRate = config.ScaleCorrectionRate;
        }

        /// <summary>Creates an instance of the predictor.</summary>
        /// <returns>Predictor instance.</returns>
        public override ksIPredictor CreateInstance()
        {
            ksLinearPredictor predictor = ksLinearPredictor.Create();
            if (m_config == null)
            {
                m_config = new ksLinearPredictor.ConfigData();
                m_config.PositionCorrectionRate = PositionCorrectionRate;
                m_config.RotationCorrectionRate = RotationCorrectionRate;
                m_config.ScaleCorrectionRate = ScaleCorrectionRate;
                if (PredictedProperties != null)
                {
                    foreach (PropertyBehaviour prop in PredictedProperties)
                    {
                        m_config.PredictedProperties[prop.Property] =
                            new ksLinearPredictor.PropertyBehaviour(prop.Type, prop.CorrectionRate);
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
            ksLog.Error(this, "ksLinearPredictorAsset cannot be used as a predictor directly. Instead call " +
                "CreateInstance to create an instance to assign.");
            return false;
        }
    }
}
