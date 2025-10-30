using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AnimatedValues;
using KS.SF.Reactor;
using KS.SceneFusion;
using KS.SceneFusion2.Client;
using KS.SF.Unity.Editor;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion2.Unity.Editor
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
