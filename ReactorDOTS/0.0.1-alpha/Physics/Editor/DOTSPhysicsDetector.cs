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
#if !REACTOR_DOTS_PHYSICS
using System.Reflection;
using System.IO;
using UnityEditor;
using KS.Unity.Editor;

namespace KS.Reactor.Client.Unity.DOTS.Editor
{
    /// <summary>
    /// Detects if the Unity.Physics package is installed and if it is, sets the REACTOR_DOTS_PHYSICS define symbol.
    /// </summary>
    [InitializeOnLoad]
    internal class DOTSPhysicsDetector
    {
        static DOTSPhysicsDetector()
        {
            try
            {
                Assembly.Load("Unity.Physics");
            }
            catch (FileNotFoundException)
            {
                return;
            }
            ksEditorUtils.SetDefineSymbol("REACTOR_DOTS_PHYSICS");
        }
    }
}
#endif