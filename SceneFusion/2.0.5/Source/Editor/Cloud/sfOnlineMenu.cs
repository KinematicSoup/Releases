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
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using KS.SF.Unity.Editor;
using KS.SF.Reactor;
using System;

namespace KS.SceneFusion.Client.Unity.Editor
{
    // Implementation specific to the cloud package.
    public partial class sfOnlineMenu : ksAuthenticatedMenu
    {
        /// <summary>Unity on enable.</summary>
        private void OnEnable()
        {
            SetLoginMenu(typeof(sfLoginMenu));
            hideFlags = HideFlags.HideAndDontSave;
        }

        /// <summary>Sets the local user's color.</summary>
        /// <param name="color">Color to set.</param>
        private void SetUserColor(ksColor color)
        {
            Service.SetUserColor(sfConfig.Get().ProjectId, color, null);
        }
    }
}
