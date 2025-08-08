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
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KS.Reactor.Client.Unity
{
    public class ksPlayerControllerAsset : ksProxyScriptAsset
    {
        /// <summary>
        /// Does this player controller use input prediction?
        /// </summary>
        public bool UseInputPrediction
        {
            get { return m_useInputPrediction; }
            set { m_useInputPrediction = value; }
        }
        [SerializeField]
        [Tooltip("Does this player controller use input prediction?")]
        protected bool m_useInputPrediction = true;
    }
}
