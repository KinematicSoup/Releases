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
    /// <summary>Factory for generating cone meshes. Caches meshes for resuse.</summary>
    public class ksConeMeshFactory
    {
        private static ksConeMeshFactory m_instance = new ksConeMeshFactory();
        private Dictionary<ksCylinderData, Mesh> m_cache = new Dictionary<ksCylinderData, Mesh>();

        /// <summary>Gets a cone mesh from the cache or generates one.</summary>
        /// <param name="radius">Radius</param>
        /// <param name="height">Height. Negative to invert the cone.</param>
        /// <param name="direction">Axis alignment.</param>
        /// <param name="numSides">Number of sides.</param>
        /// <returns>Cone mesh</returns>
        public Mesh GenerateMesh(float radius, float height, ksShape.Axis direction, int numSides)
        {
            ksCylinderData data = new ksCylinderData(radius, height, direction, numSides);
            Mesh mesh;
            if (m_cache.TryGetValue(data, out mesh) && mesh != null)
            {
                return mesh;
            }
            mesh = new Mesh();
            mesh.vertices = GenerateVertices(radius, height, direction, numSides);
            mesh.triangles = GenerateTriangles(height, numSides);
            mesh.uv = new Vector2[mesh.vertices.Length];
            m_cache[data] = mesh;
            return mesh;
        }

        /// <summary>Gets a cone mesh from the cache or generates one.</summary>
        /// <param name="radius">Radius</param>
        /// <param name="height">Height. Negative to invert the cone.</param>
        /// <param name="direction">Axis alignment.</param>
        /// <param name="numSides">Number of sides.</param>
        /// <returns>Cone mesh</returns>
        public static Mesh GetMesh(float radius, float height, ksShape.Axis direction, int numSides)
        {
            return m_instance.GenerateMesh(radius, height, direction, numSides);
        }

        /// <summary>Generates vertices for a cone mesh.</summary>
        /// <param name="radius">Radius</param>
        /// <param name="height">Height. Negative to invert the cone.</param>
        /// <param name="direction">Axis alignment.</param>
        /// <param name="numSides">Number of sides.</param>
        /// <returns>Vertices</returns>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref="numSides"/> is less than 3, or if <paramref="radius"/> is negative.
        /// </exception>
        public static Vector3[] GenerateVertices(float radius, float height, ksShape.Axis direction, int numSides)
        {
            Validate(radius, numSides);
            Vector3[] vertices = new Vector3[numSides + 1];
            switch (direction)
            {
                case ksShape.Axis.X:
                    vertices[0] = new Vector3(height / 2, 0, 0);
                    break;
                case ksShape.Axis.Y:
                default:
                    vertices[0] = new Vector3(0, height / 2, 0);
                    break;
                case ksShape.Axis.Z:
                    vertices[0] = new Vector3(0, 0, height / 2);
                    break;
            }
            float x, z;
            float y = -height / 2;
            double angle = Math.PI * 2.0 / numSides;
            for (int i = 0; i < numSides; i++)
            {
                x = radius * (float)Math.Cos(angle * i);
                z = radius * (float)Math.Sin(angle * i);
                switch (direction)
                {
                    case ksShape.Axis.X:
                        vertices[i + 1] = new Vector3(y, x, z);
                        break;
                    case ksShape.Axis.Y:
                    default:
                        vertices[i + 1] = new Vector3(x, y, z);
                        break;
                    case ksShape.Axis.Z:
                        vertices[i + 1] = new Vector3(x, z, y);
                        break;
                }
            }
            return vertices;
        }

        /// <summary>Generates triangle indexes for a cone mesh.</summary>
        /// <param name="height">Height. Negative to invert the cone.</param>
        /// <param name="numSides">Number of sides.</param>
        /// <returns>Indexes into the vertex array.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref="numSides"/> is less than 3.</exception>
        public static int[] GenerateTriangles(float height, int numSides)
        {
            Validate(numSides);
            int numTriangles = numSides * 2 - 2;
            int[] triangles = new int[numTriangles * 3];
            int t = 0;
            int p1 = height >= 0 ? 1 : 2;
            int p2 = height >= 0 ? 2 : 1;
            for (int i = 0; i < numSides; i++)
            {
                triangles[t] = 0;
                triangles[t + p1] = ((i + 1) % numSides) + 1;
                triangles[t + p2] = i + 1;
                t += 3;
            }
            for (int i = 0; i < numSides - 2; i++)
            {
                triangles[t] = 1;
                triangles[t + p1] = i + 2;
                triangles[t + p2] = i + 3;
                t += 3;
            }
            return triangles;
        }

        /// <summary>Validates arguments.</summary>
        /// <param name="radius">Radius. Cannot be negative.</param>
        /// <param name="numSides">Number of sides. Cannot be less than 3.</param>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref="numSides"/> is less than 3, or if <paramref="radius"/> is negative.
        /// </exception>
        private static void Validate(float radius, int numSides)
        {
            if (radius < 0)
            {
                throw new ArgumentException("radius cannot be negative. Value: " + radius, "radius");
            }
            Validate(numSides);
        }

        /// <summary>Validates the number of sides.</summary>
        /// <param name="numSides">Number of sides. Cannot be less than 3.</param>
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
