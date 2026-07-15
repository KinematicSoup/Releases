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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KS.SceneFusion.Client.Unity.Editor
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
