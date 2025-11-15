using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KS.SceneFusion
{
    /// <summary>
    /// Base class for our components. Sets <see cref="HideFlags.DontSaveInBuild"/> to prevent the component from being
    /// included in builds.
    /// </summary>
    public abstract class sfBaseComponent : MonoBehaviour
    {
#if UNITY_EDITOR
        /// <summary>Constructor</summary>
        public sfBaseComponent()
        {
            // We cannot set the hidelflags from the constructor, so we register a delayCall delegate and do it from
            // there.
            EditorApplication.delayCall += Initialize;
        }

        /// <summary>Initialization. Sets <see cref="HideFlags.DontSaveInBuild"/>.</summary>
        protected void Initialize()
        {
            if (this != null)
            {
                hideFlags |= HideFlags.DontSaveInBuild;
            }
        }
#endif
    }
}
