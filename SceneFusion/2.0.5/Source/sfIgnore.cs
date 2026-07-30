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
#if UNITY_EDITOR
using UnityEngine;

namespace KS.SceneFusion.Client.Unity
{
    /// <summary>Add this to a game object to prevent Scene Fusion from syncing it.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu(Product.NAME + "/sfIgnore")]
    public class sfIgnore : sfBaseComponent
    {
    }
}
#endif
