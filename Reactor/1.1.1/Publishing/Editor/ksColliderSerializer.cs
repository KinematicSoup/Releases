using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KS.LZMA;
using KS.Unity.Editor;
using UnityEngine;

namespace KS.Reactor.Client.Unity.Editor
{
    public static class ksColliderSerializer
    {
        /// <summary>Serializes the collider by shape type.</summary>
        /// <returns>Serialized shape.</returns>
        public static byte[] Serialize(ksIUnityCollider collider)
        {
            switch (collider.ShapeType)
            {
                case ksShape.ShapeTypes.SPHERE: return SerializeSphereCollider((SphereCollider)collider.Collider);
                case ksShape.ShapeTypes.BOX: return SerializeBoxCollider((BoxCollider)collider.Collider);
                case ksShape.ShapeTypes.CAPSULE: return SerializeCapsuleCollider((CapsuleCollider)collider.Collider);
                case ksShape.ShapeTypes.CONVEX_MESH: return SerializeConvexMesh((MeshCollider)collider.Collider);
                case ksShape.ShapeTypes.TRIANGLE_MESH: return SerializeTriangleMesh((MeshCollider)collider.Collider);
                case ksShape.ShapeTypes.HEIGHT_FIELD: return SerializeTerrainCollider((TerrainCollider)collider.Collider);
            }
            return null;
        }

        /// <summary>Serializes radius data from a sphere collider.</summary>
        /// <param name="collider">Sphere collider</param>
        /// <returns>Serialized data.</returns>
        private static byte[] SerializeSphereCollider(SphereCollider collider)
        {
            byte[] geometry = new byte[sizeof(float)];
            using (MemoryStream memStream = new MemoryStream(geometry))
            {
                using (BinaryWriter writer = new BinaryWriter(memStream))
                {
                    writer.Write(collider.radius);
                }
            }
            return geometry;
        }

        /// <summary>Serializes x, y, and z data from a box collider.</summary>
        /// <param name="collider">Box collider</param>
        /// <returns>Serialized data.</returns>
        private static byte[] SerializeBoxCollider(BoxCollider collider)
        {
            byte[] geometry = new byte[
                3 * sizeof(float)       // width + height + depth
            ];
            using (MemoryStream memStream = new MemoryStream(geometry))
            {
                using (BinaryWriter writer = new BinaryWriter(memStream))
                {
                    writer.Write(collider.size.x);
                    writer.Write(collider.size.y);
                    writer.Write(collider.size.z);
                }
            }
            return geometry;
        }

        /// <summary>Serializes radius, height, and axis data from a capsule collider.</summary>
        /// <param name="collider">Capsule collider</param>
        /// <returns>Serialized data.</returns>
        private static byte[] SerializeCapsuleCollider(CapsuleCollider collider)
        {
            byte[] geometry = new byte[
                1 +                     // axis orientation
                2 * sizeof(float)       // radius + height
            ];
            using (MemoryStream memStream = new MemoryStream(geometry))
            {
                using (BinaryWriter writer = new BinaryWriter(memStream))
                {
                    writer.Write((byte)collider.direction);
                    writer.Write(collider.radius);
                    writer.Write(collider.height);
                }
            }
            return geometry;
        }

        /// <summary>Serializes vertex data from a mesh collider.</summary>
        /// <param name="collider">Mesh collider</param>
        /// <returns>Serialized data.</returns>
        private static byte[] SerializeConvexMesh(MeshCollider collider)
        {
            ksConvexHull hull = ksConvexHull.Get(collider.sharedMesh);
            if (hull == null)
            {
                return null;
            }
            
            uint vertexCount = (uint)hull.Vertices.Count;
            byte[] geometry = new byte[
                sizeof(int) +                       // vertex count
                vertexCount * 3 * sizeof(float)     // vertex count * vertex position (float + float + float)
            ];
            using (MemoryStream memStream = new MemoryStream(geometry))
            {
                using (BinaryWriter writer = new BinaryWriter(memStream))
                {
                    writer.Write(vertexCount);
                    foreach (Vector3 vertex in hull.Vertices)
                    {
                        writer.Write(vertex.x);
                        writer.Write(vertex.y);
                        writer.Write(vertex.z);
                    }
                }
            }
            return ksLZMA.Compress(geometry);
        }

        /// <summary>Serializes vertex and triangle data from a mesh collider.</summary>
        /// <param name="collider">Mesh collider</param>
        /// <returns>Serialized data.</returns>
        private static byte[] SerializeTriangleMesh(MeshCollider collider)
        {
            Mesh mesh = collider.sharedMesh;
            if (mesh == null)
            {
                return null;
            }
            uint vertexCount = (uint)mesh.vertexCount;
            uint triangleCount = (uint)mesh.triangles.Length / 3;
            byte[] geometry = new byte[
                sizeof(int) +                       // vertex count
                vertexCount * 3 * sizeof(float) +   // vertex count * vertex position (float + float + float)
                sizeof(int) +                       // triangle count
                triangleCount * 3 * sizeof(int)     // triangle count * vertex indices (int + int  + int)
            ];
            using (MemoryStream memStream = new MemoryStream(geometry))
            {
                using (BinaryWriter writer = new BinaryWriter(memStream))
                {
                    writer.Write(vertexCount);
                    foreach (Vector3 vertex in mesh.vertices)
                    {
                        writer.Write(vertex.x);
                        writer.Write(vertex.y);
                        writer.Write(vertex.z);
                    }

                    writer.Write(triangleCount);
                    foreach (int vertexIndex in mesh.triangles)
                    {
                        writer.Write((uint)vertexIndex);
                    }
                }
            }
            return ksLZMA.Compress(geometry);
        }

        /// <summary>Serializes heightmap data from a terrain collider.</summary>
        /// <param name="collider">Terrain collider</param>
        /// <returns>Serialized data.</returns>
        private static byte[] SerializeTerrainCollider(TerrainCollider collider)
        {
            TerrainData heightMap = collider.terrainData;
            if (heightMap == null)
            {
                return null;
            }
#if UNITY_2019_3_OR_NEWER
            int xRes = heightMap.heightmapResolution;
            int yRes = heightMap.heightmapResolution;
#else
            int xRes = heightMap.heightmapWidth;
            int yRes = heightMap.heightmapHeight;
#endif

            int dataOffset =
                3 * sizeof(float) +     // x, y, and z sizes (float + float + float)
                2 * sizeof(int) +       // resolution (int + int)
                sizeof(float);          // thickness (float)

            int heightMapDataSize = dataOffset + xRes * yRes * sizeof(ushort);
            int treeInstanceDataSize = (sizeof(float) * 6 + sizeof(uint)) * heightMap.treeInstanceCount;

            //byte[] geometry = new byte[dataOffset + xRes * yRes * sizeof(ushort)];
            byte[] geometry = new byte[heightMapDataSize + treeInstanceDataSize];

            using (MemoryStream memStream = new MemoryStream(geometry))
            {
                using (BinaryWriter writer = new BinaryWriter(memStream))
                {
                    writer.Write(heightMap.size.x);
                    writer.Write(heightMap.size.y);
                    writer.Write(heightMap.size.z);
                    writer.Write(xRes);
                    writer.Write(yRes);
#if UNITY_2019_3_OR_NEWER
                    // Unity removed the thickness property from the terrain data. So we write the default value 1.
                    writer.Write(1f);
#else
                    writer.Write(heightMap.thickness);
#endif
                }
            }

            // Write heights as XOR deltas between sequential height values
            float[,] heights = heightMap.GetHeights(0, 0, xRes, yRes);
            int Xmax = heights.GetLength(0);
            int Ymax = heights.GetLength(1);
            int i = dataOffset;
            int value;
            int lastValue = 0;
            float range = (float)short.MaxValue;
            for (int x = 0; x < Xmax; ++x)
            {
                for (int y = 0; y < Ymax; ++y)
                {
                    value = (int)(heights[x, y] * range);
                    WriteUShortBytes((ushort)(value ^ lastValue), geometry, i);
                    lastValue = value;
                    i += 2;
                }
            }

            // Write tree instance data
            // Tree prototype indices (list of asset ids)
            Dictionary<int, uint> indexToAssetIdMap = new Dictionary<int, uint>();

            for (i = 0; i < heightMap.treePrototypes.Length; ++i)
            {
                uint id = ksAssetIdData.Get().GetId(heightMap.treePrototypes[i].prefab);
                indexToAssetIdMap.Add(i, id);
            }
            using (MemoryStream memStream = new MemoryStream(geometry))
            {
                memStream.Position = heightMapDataSize;
                using (BinaryWriter writer = new BinaryWriter(memStream))
                {
                    for (i = 0; i < heightMap.treeInstances.Length; ++i)
                    {
                        TreeInstance tree = heightMap.treeInstances[i];
                        uint id;
                        if (indexToAssetIdMap.TryGetValue(i, out id))
                        {
                            writer.Write(id);
                            writer.Write(tree.position.x);
                            writer.Write(tree.position.y);
                            writer.Write(tree.position.z);
                            writer.Write(tree.widthScale);
                            writer.Write(tree.heightScale);
                            writer.Write(tree.rotation);
                        }
                    }
                }
            }

            return ksLZW.Compress(geometry);
        }

        /// <summary>
        /// Write a ushort value into a byte array.
        /// </summary>
        /// <param name="value">Value to write</param>
        /// <param name="array">Array to write the uint value to</param>
        /// <param name="offset">Offset in the array to start writing the bytes</param>
        private static void WriteUShortBytes(ushort value, byte[] array, int offset)
        {
            if (array.Length > offset + 4)
            {
                array[offset] = (byte)value;
                array[offset + 1] = (byte)(value >> 8);
            }
        }
    }
}
