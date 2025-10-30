using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>Syncs changes made by a terrain tree undo operation.</summary>
    public class sfUndoTerrainTreeOperation : sfBaseUndoOperation
    {
        private TerrainData m_terrainData;

        /// <summary>Constructor</summary>
        /// <param name="that">that changed.</param>
        public sfUndoTerrainTreeOperation(TerrainData terrainData)
        {
            m_terrainData = terrainData;
        }

        /// <summary>Syncs terrain tree changes from the undo or redo operation.</summary>
        /// <param name="isUndo">true if this is an undo operation, false if it is a redo.</param>
        public override void HandleUndoRedo(bool isUndo)
        {
            sfTerrainTranslator translator =
                sfObjectEventDispatcher.Get().GetTranslator<sfTerrainTranslator>(sfType.Terrain);
            translator.OnTreeChange(m_terrainData, true);
        }
    }
}
