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
using UnityEngine;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>Terrain brush data.</summary>
    public class sfTerrainBrush
    {
        /// <summary>The terrain the brush origin is on.</summary>
        public Terrain Terrain;

        /// <summary>The index of the brush in the brush list.</summary>
        public int Index;

        /// <summary>The position of the origin of the brush on the terrain from [0, 1].</summary>
        public Vector2 Position;

        /// <summary>The rotation of the brush.</summary>
        public float Rotation;

        /// <summary>The size of the brush.</summary>
        public float Size;
    }
}
