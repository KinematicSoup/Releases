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
    /// <summary>Stores a convex hull created from a mesh.</summary>
    public class ksConvexHull
    {
        private List<Vector3> m_vertices;
        private List<int> m_triangles;
        private float m_volume = 1.0f;
        private Vector3 m_geometricCenter = Vector3.zero;

        private static readonly string LOG_CHANNEL = typeof(ksConvexHull).ToString();

        /// <summary>Convex hull vertices.</summary>
        public List<Vector3> Vertices
        {
            get { return m_vertices; }
        }

        /// <summary>Convex hull volume.</summary>
        public float Volume
        {
            get { return m_volume; }
        }

        /// <summary>Geometric center of the convex hull.</summary>
        public Vector3 GeometricCenter
        {
            get { return m_geometricCenter; }
        }

        private static Dictionary<Mesh, ksConvexHull> m_cache = new Dictionary<Mesh, ksConvexHull>();
        private static ConvexHullCalculator m_calculator = new ConvexHullCalculator();
        private static HashSet<Mesh> m_rejects = new HashSet<Mesh>();

        /// <summary>
        /// Gets a convex hull from a mesh. Convex hulls are cached so calling <see cref="Get"/> with the same 
        /// mesh multiple times will always return the same object.
        /// </summary>
        /// <param name="mesh">Mesh to get convex hull for.</param>
        /// <returns>Convex hull</returns>
        public static ksConvexHull Get(Mesh mesh)
        {
            if (mesh == null || m_rejects.Contains(mesh))
            {
                return null;
            }

            ksConvexHull hull = null;
            if (!m_cache.TryGetValue(mesh, out hull))
            {
                List<Vector3> meshVertices = new List<Vector3>(mesh.vertices);
                ksVector3 size = mesh.bounds.size;
                if (meshVertices.Count > 3)
                {
                    try
                    {
                        float scale = 10.0f / Mathf.Min(size.X, size.Y, size.Z);
                        hull = new ksConvexHull(meshVertices, scale);
                    }
                    catch (Exception e)
                    {
                        ksLog.Warning(LOG_CHANNEL, "Error generating convex hull for mesh '" + mesh.name + "'. Approximating center of mass and volume using mesh bounds.\n" + e.Message);
                        // If we get here, then there was an error calculating the convex hull.  We will add all points
                        // to a non optimized hull and approximate the geometric center and volume using the mesh AABB.
                        hull = new ksConvexHull(meshVertices, mesh.bounds.center, size.X * size.Y * size.Z);
                    }
                    m_cache[mesh] = hull;
                }
                else
                {
                    ksLog.Error(LOG_CHANNEL, "Error generating convex hull for mesh '" + mesh.name + "'. Convex hulls require at least 4 vertices.");
                    m_rejects.Add(mesh);
                }
            }
            return hull;
        }

        /// <summary>Clear the list of rejected convex hull.</summary>
        public static void ClearRejects()
        {
            m_rejects.Clear();
        }

        /// <summary>Removes destroyed meshes from the cache.</summary>
        public static void ClearDestroyedMeshes()
        {
            List<Mesh> destroyedList = new List<Mesh>();
            foreach (Mesh mesh in m_cache.Keys)
            {
                if (mesh == null)
                {
                    destroyedList.Add(mesh);
                }
            }
            foreach (Mesh mesh in destroyedList)
            {
                m_cache.Remove(mesh);
            }
        }

        /// <summary>Construct a convex hull with a known center of mass and volume.</summary>
        /// <param name="vertices">Vertices to generate convex hull from.</param>
        /// <param name="centerOfMass">Center of mass.</param>
        /// <param name="volume">Volume of convex hull.</param>
        public ksConvexHull(List<Vector3> vertices, ksVector3 centerOfMass, float volume)
        {
            m_vertices = vertices;
            m_geometricCenter = centerOfMass;
            m_volume = volume;
        }

        /// <summary>Construct a scaled convex hull.</summary>
        /// <param name="vertices">Vertices to generate convex null from.</param>
        /// <param name="scale">Scale the vertices before and after convex hull calculations.</param>
        public ksConvexHull(List<Vector3> vertices, float scale)
        {
            List<Vector3> normals = null;
            for (int i = 0; i < vertices.Count; i++)
            {
                vertices[i] = vertices[i] * scale;
            }
            m_calculator.GenerateHull(vertices, false, ref m_vertices, ref m_triangles, ref normals);
            for (int i = 0; i < m_vertices.Count; i++)
            {
                m_vertices[i] = m_vertices[i] / scale;
            }
            CalculateGeometricProperties();
        }

        /// <summary>Calculates the volume and geometric center.</summary>
        private void CalculateGeometricProperties()
        {
            try
            {
                m_volume = 0f;
                m_geometricCenter = Vector3.zero;
                if (m_vertices.Count == 0 || m_triangles.Count < 3)
                {
                    return;
                }

                // To start we need an arbitrary reference point inside the volume. Use the average of the vertices as
                // the reference point.
                Vector3 point = Vector3.zero;
                foreach (Vector3 vertex in m_vertices)
                {
                    point += vertex;
                }
                point /= m_vertices.Count;

                // Divide the mesh into tetrahedrons by combining each triangle with the reference point.
                // Calculate each tetrahedron's volume and center of mass.
                // The mesh's center of mass is the sum of each tetrahedron's center of mass weighted by volume,
                // divided by the overall volume.
                for (int i = 0; i + 2 < m_triangles.Count; i += 3)
                {
                    Vector3 v1 = m_vertices[m_triangles[i]];
                    Vector3 v2 = m_vertices[m_triangles[i + 1]];
                    Vector3 v3 = m_vertices[m_triangles[i + 2]];
                    // Calculate colume of the tetrahedron
                    float volume = Math.Abs(Vector3.Dot(v1 - point, Vector3.Cross(v2 - point, v3 - point))) / 6f;
                    // Calculate center of mass of the tetrahedron as the average of its vertices
                    Vector3 centerOfMass = (v1 + v2 + v3 + point) / 4f;
                    m_geometricCenter += centerOfMass * volume;
                    m_volume += volume;
                }
                if (m_volume > 0f)
                {
                    m_geometricCenter /= m_volume;
                }
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Error calculating hull volume and center of mass", e);
            }
        }
    }
}