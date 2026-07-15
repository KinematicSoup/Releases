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
    /// <summary>Syncs changes made by a terrain undo operation.</summary>
    public class sfUndoTerrainOperation : sfBaseUndoOperation
    {
        private TerrainData m_terrainData;
        private sfTerrainTranslator.TerrainType m_type;
        private RectInt m_changeArea;

        /// <summary>Always true; this operation can be combined with other terrain operations.</summary>
        public override bool CanCombine
        {
            get { return true; }
        }

        /// <summary>Constructor</summary>
        /// <param name="that">that changed.</param>
        /// <param name="type">type of terrain data that changed.</param>
        /// <param name="changeArea"></param>
        public sfUndoTerrainOperation(TerrainData terrainData, sfTerrainTranslator.TerrainType type, RectInt changeArea)
        {
            m_terrainData = terrainData;
            m_type = type;
            m_changeArea = changeArea;
        }

        /// <summary>Syncs terrain changes from the undo or redo operation.</summary>
        /// <param name="isUndo">true if this is an undo operation, false if it is a redo.</param>
        public override void HandleUndoRedo(bool isUndo)
        {
            sfTerrainTranslator translator = 
                sfObjectEventDispatcher.Get().GetTranslator<sfTerrainTranslator>(sfType.Terrain);
            if (m_type == sfTerrainTranslator.TerrainType.DETAILS)
            {
                // Sync all detail layers since undo can undo changes made by other users to any layer.
                translator.OnDetailChange(m_terrainData, m_changeArea, -1);
                translator.SendDetailChanges();
            }
            else
            {
                translator.OnTerrainChange(m_terrainData, m_type, m_changeArea, true);
                translator.SendTerrainChanges(m_type);
            }
            // The undo/redo can revert other users's changes in other regions, so we reapply the server data for
            // all regions outside the change area.
            translator.ApplyServerTerrainData(m_terrainData, m_type, m_changeArea);
        }

        /// <summary>Combines another operation with this one.</summary>
        /// <returns>
        /// true if the operations could be combined. To combine the other operation must be a terrain
        /// operation of the same type on the same terrain data.
        /// </returns>
        public override bool CombineWith(sfBaseUndoOperation other)
        {
            sfUndoTerrainOperation op = other as sfUndoTerrainOperation;
            if (op == null || op.m_terrainData != m_terrainData || op.m_type != m_type)
            {
                return false;
            }
            m_changeArea.min = Vector2Int.Min(m_changeArea.min, op.m_changeArea.min);
            m_changeArea.max = Vector2Int.Max(m_changeArea.max, op.m_changeArea.max);
            return true;
        }
    }
}
