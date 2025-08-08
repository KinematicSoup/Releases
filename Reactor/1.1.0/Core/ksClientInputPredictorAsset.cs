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
    /// Predictor asset that creates instances of <see cref="ksClientInputPredictor"/> when assigned in the entity or 
    /// room type inspector.
    /// </summary>
    public class ksClientInputPredictorAsset : ksPredictor
    {
        /// <summary>Does the predictor require a player controller?</summary>
        public override bool RequiresController
        {
            get { return true; }
        }

        /// <summary>Creates an instance of the predictor.</summary>
        /// <returns>Predictor instance.</returns>
        public override ksIPredictor CreateInstance()
        {
            return new ksClientInputPredictor();
        }

        /// <summary>
        /// Logs an error stating the predictor cannot be used directly and to call <see cref="CreateInstance()"/>
        /// instead.
        /// </summary>
        /// <returns>false</returns>
        public override bool Initialize()
        {
            ksLog.Error(this, "ksClientInputPredictorAsset cannot be used as a predictor directly. Instead call " +
                "CreateInstance to create an instance to assign.");
            return false;
        }
    }
}
