#if UNITY_EDITOR
using UnityEngine;
using KS.SceneFusionCommon;

namespace KS.SceneFusion
{
    /// <summary>Add this to a game object to prevent Scene Fusion from syncing it.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu(Product.NAME + "/sfIgnore")]
    public class sfIgnore : sfBaseComponent
    {
    }
}
#endif