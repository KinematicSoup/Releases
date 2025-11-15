using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KS.SF.Reactor;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>
    /// Null terrain compressor that (de)serializes data without (de)compressing it.
    /// </summary>
    public class sfNullTerrainCompressor : sfITerrainCompressor
    {
        /// <summary>Encodes heightmap data./summary>
        /// <param name="heightmapData"></param>
        /// <returns>Encoded data</returns>
        public byte[] EncodeHeightmap(float[,] heightmapData)
        {
            byte[] data = new byte[heightmapData.GetLength(0) * heightmapData.GetLength(1) * sizeof(float)];
            Buffer.BlockCopy(heightmapData, 0, data, 0, data.Length);
            return data;
        }

        /// <summary>Decodes heightmap data.</summary>
        /// <param name="data">Data to decode.</param>
        /// <param name="width">Width of the region the data is for.</param>
        /// <param name="height">Height of the region the data is for.</param>
        /// <returns>Decoded heightmap data</returns>
        public float[,] DecodeHeightmap(byte[] data, int width, int height)
        {
            float[,] heightmapData = new float[height, width];
            Buffer.BlockCopy(data, 0, heightmapData, 0, data.Length);
            return heightmapData;
        }

        /// <summary>Encodes alphamap data.</summary>
        /// <param name="alphamapData"></param>
        /// <returns>Encoded data</returns>
        public byte[] EncodeAlphamap(float[,,] alphamapData)
        {
            byte[] data = new byte[alphamapData.GetLength(0) * alphamapData.GetLength(1) * alphamapData.GetLength(2) * sizeof(float)];
            Buffer.BlockCopy(alphamapData, 0, data, 0, data.Length);
            return data;
        }

        /// <summary>Decodes alphamap data.</summary>
        /// <param name="data">Data to decode.</param>
        /// <param name="width">Width of the region the data is for.</param>
        /// <param name="height">Height of the region the data is for.</param>
        /// <param name="numLayers">Number of alphamap layers.</param>
        /// <returns>Decoded alphamap data</returns>
        public float[,,] DecodeAlphamap(byte[] data, int width, int height, int numLayers)
        {
            int encodedLayers = data.Length / (4 * height * width);
            float[,,] alphamapData = new float[height, width, encodedLayers];
            Buffer.BlockCopy(data, 0, alphamapData, 0, data.Length);
            // Sometimes the number of layers in the encoded data is different than the current number of layers if new
            // layers were added since the data was encoded. The new layers are always on the end, so we copy the data
            // into a corrected data array that has the new layers with their values set to zero.
            if (encodedLayers != numLayers)
            {
                float[,,] correctedData = new float[height, width, numLayers];
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        for (int i = 0; i < encodedLayers; i++)
                        {
                            correctedData[y, x, i] = alphamapData[y, x, i];
                        }
                    }
                }
                return correctedData;
            }
            return alphamapData;
        }

        /// <summary>Encodes detail layer data.</summary>
        /// <param name="detailLayerData"></param>
        /// <returns>Encoded data</returns>
        public byte[] EncodeDetailLayer(int[,] detailLayerData)
        {
            byte[] data = new byte[detailLayerData.GetLength(0) * detailLayerData.GetLength(1) * sizeof(int)];
            Buffer.BlockCopy(detailLayerData, 0, data, 0, data.Length);
            return data;
        }

        /// <summary>Decodes detail layer data.</summary>
        /// <param name="data">Data to decode.</param>
        /// <param name="width">Width of the region the data is for.</param>
        /// <param name="height">Height of the region the data is for.</param>
        /// <returns>Decoded detail layer data</returns>
        public int[,] DecodeDetailLayer(byte[] data, int width, int height)
        {
            int[,] detailLayerData = new int[height, width];
            Buffer.BlockCopy(data, 0, detailLayerData, 0, data.Length);
            return detailLayerData;
        }

        /// <summary>Encodes tree data.</summary>
        /// <param name="data">Data to encode.</param>
        /// <returns>Encoded tree data</returns>
        public byte[] EncodeTrees(byte[] data)
        {
            return data;
        }

        /// <summary>Decodes tree data.</summary>
        /// <param name="data">Data to decode.</param>
        /// <returns>Decoded tree data</returns>
        public byte[] DecodeTrees(byte[] data)
        {
            return data;
        }
    }
}
