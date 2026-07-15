using UnityEngine;

namespace KS.SceneFusion.Client.Unity
{
    /// <summary>Provides access to Unity's OnGUI method for drawing a GUI in the game view.</summary>
    internal class GUIHook : MonoBehaviour
    {
        /// <summary>On draw callback.</summary>
        public delegate void DrawCallback();

        /// <summary>Called from OnGUI.</summary>
        public event DrawCallback OnDraw;

        /// <summary>Calls the OnDraw callback.</summary>
        private void OnGUI()
        {
            if (OnDraw != null)
            {
                OnDraw();
            }
        }
    }
}
