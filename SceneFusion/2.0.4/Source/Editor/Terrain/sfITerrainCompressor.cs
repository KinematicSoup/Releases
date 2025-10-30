using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>Interface for compressing and decompressing terrain data.</summary>
    public interface sfITerrainCompressor
    {
        /// <summary>Encodes heightmap data./summary>
        /// <param name="heightmapData"></param>
        /// <returns>Encoded data</returns>
        byte[] EncodeHeightmap(float[,] heightmapData);

        /// <summary>Decodes heightmap data.</summary>
        /// <param name="data">Data to decode.</param>
        /// <param name="width">Width of the region the data is for.</param>
        /// <param name="height">Height of the region the data is for.</param>
        /// <returns>Decoded heightmap data</returns>
        float[,] DecodeHeightmap(byte[] data, int width, int height);

        /// <summary>Encodes alphamap data.</summary>
        /// <param name="alphamapData"></param>
        /// <returns>Encoded data</returns>
        byte[] EncodeAlphamap(float[,,] alphamapData);

        /// <summary>Decodes alphamap data.</summary>
        /// <param name="data">Data to decode.</param>
        /// <param name="width">Width of the region the data is for.</param>
        /// <param name="height">Height of the region the data is for.</param>
        /// <param name="numLayers">Number of alphamap layers.</param>
        /// <returns>Decoded alphamap data</returns>
        float[,,] DecodeAlphamap(byte[] data, int width, int height, int numLayers);

        /// <summary>Encodes detail layer data.</summary>
        /// <param name="detailLayerData"></param>
        /// <returns>Encoded data</returns>
        byte[] EncodeDetailLayer(int[,] detailLayerData);

        /// <summary>Decodes detail layer data.</summary>
        /// <param name="data">Data to decode.</param>
        /// <param name="width">Width of the region the data is for.</param>
        /// <param name="height">Height of the region the data is for.</param>
        /// <returns>Decoded detail layer data</returns>
        int[,] DecodeDetailLayer(byte[] data, int width, int height);

        /// <summary>Encodes tree data.</summary>
        /// <param name="data">Data to encode.</param>
        /// <returns>Encoded tree data</returns>
        byte[] EncodeTrees(byte[] data);

        /// <summary>Decodes tree data.</summary>
        /// <param name="data">Data to decode.</param>
        /// <returns>Decoded tree data</returns>
        byte[] DecodeTrees(byte[] data);
    }
}
