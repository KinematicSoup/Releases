/*
KINEMATICSOUP CONFIDENTIAL
 Copyright(c) 2014-2025 KinematicSoup Technologies Incorporated 
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
using UnityEngine;

namespace KS.Reactor.Client.Unity
{
    /// <summary>Factory for generating cylinder meshes. Caches meshes for resuse.</summary>
    public class ksCylinderMeshFactory
    {
        private static ksCylinderMeshFactory m_instance = new ksCylinderMeshFactory();
        private Dictionary<ksCylinderData, Mesh> m_cache = new Dictionary<ksCylinderData, Mesh>();

        /// <summary>Gets a cylinder mesh from the cache or generates one.</summary>
        /// <param name="radius">Radius of the cylinder.</param>
        /// <param name="height">Height of the cylinder.</param>
        /// <param name="direction">Axis alignment.</param>
        /// <param name="numSides">Number of sides used to appoximate the cylinder.</param>
        /// <returns>Cylinder mesh</returns>
        public Mesh GenerateMesh(float radius, float height, ksShape.Axis direction, int numSides)
        {
            ksCylinderData data = new ksCylinderData(radius, height, direction, numSides);
            Mesh mesh;
            if (m_cache.TryGetValue(data, out mesh))
            {
                return mesh;
            }
            mesh = new Mesh();
            mesh.vertices = GenerateVertices(radius, height, direction, numSides);
            mesh.triangles = GenerateTriangles(numSides);
            mesh.uv = new Vector2[mesh.vertices.Length];
            m_cache[data] = mesh;
            return mesh;
        }

        /// <summary>Gets a cylinder mesh from the cache or generates one.</summary>
        /// <param name="radius">Radius of the cylinder.</param>
        /// <param name="height">Height of the cylinder.</param>
        /// <param name="direction">Axis alignment.</param>
        /// <param name="numSides">Number of sides used to appoximate the cylinder.</param>
        /// <returns>Cylinder mesh</returns>
        public static Mesh GetMesh(float radius, float height, ksShape.Axis direction, int numSides)
        {
            return m_instance.GenerateMesh(radius, height, direction, numSides);
        }

        /// <summary>Generates vertices for a cylinder mesh.</summary>
        /// <param name="radius">Radius of the cylinder.</param>
        /// <param name="height">Height of the cylinder.</param>
        /// <param name="direction">Axis alignment.</param>
        /// <param name="numSides">Number of sides used to appoximate the cylinder.</param>
        /// <returns>Mesh vertices</returns>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref="numSides"/> is less than 3, or if <paramref="radius"/>
        /// or <paramref="height"/> is negative.
        /// </exception>
        public static Vector3[] GenerateVertices(float radius, float height, ksShape.Axis direction, int numSides)
        {
            Validate(radius, height, numSides);
            Vector3[] vertices = new Vector3[numSides * 2];
            float x, z;
            float y = height / 2;
            double angle = Math.PI * 2.0 / numSides;
            for (int i = 0; i < numSides; i++)
            {
                x = radius * (float)Math.Cos(angle * i);
                z = radius * (float)Math.Sin(angle * i);

                switch (direction)
                {
                    case ksShape.Axis.X:
                        vertices[i * 2] = new Vector3(y, x, z);
                        vertices[i * 2 + 1] = new Vector3(-y, x, z);
                        break;
                    case ksShape.Axis.Y:
                    default:
                        vertices[i * 2] = new Vector3(x, y, z);
                        vertices[i * 2 + 1] = new Vector3(x, -y, z);
                        break;
                    case ksShape.Axis.Z:
                        vertices[i * 2] = new Vector3(x, z, y);
                        vertices[i * 2 + 1] = new Vector3(x, z, -y);
                        break;
                }
            }
            return vertices;
        }

        /// <summary>Generates triangle indexes for a cylinder mesh.</summary>
        /// <param name="numSides">Number of sides.</param>
        /// <returns>Triangle indexes into the vertex array.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref="numSides"/> is less than 3.</exception>
        public static int[] GenerateTriangles(int numSides)
        {
            Validate(numSides);
            int numVertices = numSides * 2;
            int numTriangles = numSides * 4 - 4;
            int[] triangles = new int[numTriangles * 3];
            int t = 0;
            for (int i = 0; i < numSides; i++)
            {
                int p = i * 2;
                triangles[t] = p;
                triangles[t + 1] = (p + 2) % numVertices;
                triangles[t + 2] = p + 1;
                t += 3;

                triangles[t] = p + 1;
                triangles[t + 1] = (p + 2) % numVertices;
                triangles[t + 2] = (p + 3) % numVertices;
                t += 3;
            }
            for (int i = 0; i < numSides - 2; i++)
            {
                int p = (i + 1) * 2;
                triangles[t] = 0;
                triangles[t + 1] = p + 2;
                triangles[t + 2] = p;
                t += 3;

                triangles[t] = 1;
                triangles[t + 1] = p + 1;
                triangles[t + 2] = p + 3;
                t += 3;
            }
            return triangles;
        }

        /// <summary>Validates arguments.</summary>
        /// <param name="radius">Radius - cannot be negative.</param>
        /// <param name="height">Height - cannot be negative.</param>
        /// <param name="numSides">Number of sides - cannot be less than 3.</param>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref="numSides"/> is less than 3, or if <paramref="radius"/>
        /// or <paramref="height"/> is negative.
        /// </exception>
        private static void Validate(float radius, float height, int numSides)
        {
            if (radius < 0)
            {
                throw new ArgumentException("radius cannot be negative. Value: " + radius, "radius");
            }
            if (height < 0)
            {
                throw new ArgumentException("height cannot be negative. Value: " + height, "height");
            }
            Validate(numSides);
        }

        /// <summary>Validates the number of sides.</summary>
        /// <param name="numSides">Number of sides - cannot be less than 3.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref="numSides"/> is less than 3.</exception>
        private static void Validate(int numSides)
        {
            if (numSides < 3)
            {
                throw new ArgumentException("numSides cannot be less than 3. Value: " + numSides, "numSides");
            }
        }
    }
}
