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
using Unity.Entities;

namespace KS.Reactor.Client.Unity.DOTS
{
    /// <summary>
    /// Shared data component for overriding the predictor on entities with player controllers using input prediction.
    /// </summary>
    public struct ksSharedControllerPredictorOverride : ISharedComponentData
    {
        /// <summary>Controller predictor for the entities this is attached to.</summary>
        public ksPredictor Predictor
        {
            get { return m_predictor.Value; }
        }
        private UnityObjectRef<ksPredictor> m_predictor;

        /// <summary>Constructor</summary>
        /// <param name="predictor">Predictor override</param>
        public ksSharedControllerPredictorOverride(ksPredictor predictor)
        {
            m_predictor = new UnityObjectRef<ksPredictor>() { Value = predictor };
        }
    }
}