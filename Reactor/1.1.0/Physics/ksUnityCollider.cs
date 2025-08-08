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
using System.IO;
using KS.LZMA;
using UnityEngine;

#if !UNITY_6000_0_OR_NEWER
using PhysicsMaterial = UnityEngine.PhysicMaterial;
#endif

namespace KS.Reactor.Client.Unity
{
    /// <summary>Wraps a Unity collider for use with Reactor.</summary>
    public class ksUnityCollider : ksIUnityCollider
    {
        private Collider m_collider = null;
        private ksColliderData m_data;
        private ksShape.ShapeTypes m_shapeType = ksShape.ShapeTypes.NO_COLLIDER;
        private ksVector3 m_size = ksVector3.Zero;
        private object m_asset;
        private ksConvexHull m_hull;

        /// <summary>Get the entity this collider is attached to.</summary>
        public ksIEntity Entity
        {
            get
            {
                ksEntityComponent entityComponent = EntityComponent;
                return entityComponent == null ? null : entityComponent.Entity;
            }
        }

        /// <summary>Get/Set the enabled state of a collider</summary>
        public bool IsEnabled
        {
            get { return m_collider.enabled; }
            set { m_collider.enabled = value; }
        }

        /// <summary>Unity collider component.</summary>
        public Component Component
        {
            get { return m_collider; }
        }

        /// <summary>Unity collider.</summary>
        public Collider Collider
        {
            get { return m_collider; }
        }

        /// <summary>Physics material.</summary>
        public PhysicsMaterial Material
        {
            get { return m_collider.sharedMaterial; }
        }

        /// <summary>Get the collider shape type.</summary>
        public ksShape.ShapeTypes ShapeType
        {
            get { return m_shapeType; }
        }

        /// <summary>Get the <see cref="ksEntityComponent"/> on the collider gameobject.</summary>
        private ksEntityComponent EntityComponent
        {
            get { return m_collider != null ? m_collider.gameObject.GetComponent<ksEntityComponent>() : null; }
        }

        /// <summary>The collider data.</summary>
        public ksColliderData Data
        {
            get
            {
                if (m_data == null)
                {
                    ksEntityComponent entity = EntityComponent;
                    if (entity != null)
                    {
                        m_data = entity.GetColliderData(m_collider);
                    }
                }
                return m_data;
            }
        }

        /// <summary>
        /// Does this collider exist on the client, server, or both? Note that this only affects whether or not the
        /// collider is written to server configs during publishing or whether the collider is deleted on the client
        /// when the entity is spawned and may not be indicative of the current existance of the collider on the server
        /// at runtime.
        /// </summary>
        public ksExistenceModes Existance
        {
            get { return Data == null ? ksExistenceModes.CLIENT_ONLY : Data.Existence; }
        }

        /// <summary>Get/Set if this collider is used in scene queries.</summary>
        public bool IsQueryCollider
        {
            get
            {
                return Data != null ? Data.IsQuery : true;
            }
            set
            {
                if (Data != null)
                {
                    Data.IsQuery = value;
                }
            }
        }

        /// <summary>Get/Set if this collider is used in physics simulations.</summary>
        public bool IsSimulationCollider
        {
            get
            {
                return Data != null ? Data.IsSimulation : true;
            }
            set
            {
                if (Data != null)
                {
                    Data.IsSimulation = value;
                }
            }
        }

        /// <summary>Get/Set if this collider is a trigger.</summary>
        public bool IsTrigger
        {
            get { return m_collider.isTrigger; }
            set { m_collider.isTrigger = value; }
        }

        /// <summary>Get/Set the collision filter on the collider.</summary>
        public ksCollisionFilter CollisionFilter
        {
            get {
                ksEntityComponent entity = EntityComponent;
                return entity != null ? entity.GetCollisionFilter(m_collider) : new ksCollisionFilter();
            }
            set {
                ksEntityComponent entity = EntityComponent;
                if (entity)
                {
                    entity.SetCollisionFilter(m_collider, value);
                }
            }
        }

        /// <summary>
        /// Get the bounds of a collider at the position of its attached entity.
        /// </summary>
        public ksBounds Bounds
        {
            get { return m_collider.bounds; }
        }

        /// <summary>Get the collider center.</summary>
        public Vector3 Center
        {
            get {
                switch (m_shapeType)
                {
                    case ksShape.ShapeTypes.SPHERE: return ((SphereCollider)m_collider).center;
                    case ksShape.ShapeTypes.BOX: return ((BoxCollider)m_collider).center;
                    case ksShape.ShapeTypes.CAPSULE: return ((CapsuleCollider)m_collider).center;
                }
                return Vector3.zero;
            }
        }

        /// <summary>Get/Set contact offset on a collider</summary>
        public float ContactOffset
        {
            get { return m_collider.contactOffset; }
            set 
            { 
                m_collider.contactOffset = value;
                if (Data != null)
                {
                    Data.ContactOffset = value;
                }
            }
        }

        /// <summary>Constructor</summary>
        /// <param name="collider">
        /// Determines shape type base on the Unity collider type. Not all colliders are supported.
        /// </param>
        /// <param name="data">Collider data associated with the collider.</param>
        public ksUnityCollider(Collider collider, ksColliderData data = null)
        {
            // Null collider exception
            if ((object)collider == null)
            {
                throw new ArgumentNullException("collider");
            }
            m_collider = collider;
            m_data = data;

            if (Data != null && Data.ContactOffset > 0f)
            {
                collider.contactOffset = Data.ContactOffset;
            }

            SphereCollider sphere;
            BoxCollider box;
            CapsuleCollider capsule;
            MeshCollider mesh;
            TerrainCollider terrain;
            CharacterController characterController;

            // Determine the shape type and size or asset
            // We extract size and asset from the Unity collider so we can use it for equality comparisons after the
            // Unity collider is destroyed.
            if ((sphere = collider as SphereCollider) != null)
            {
                m_shapeType = ksShape.ShapeTypes.SPHERE;
                m_size.X = sphere.radius;
            }
            else if ((box = collider as BoxCollider) != null)
            {
                m_shapeType = ksShape.ShapeTypes.BOX;
                m_size = box.size;
            }
            else if ((capsule = collider as CapsuleCollider) != null)
            {
                m_shapeType = ksShape.ShapeTypes.CAPSULE;
                m_size = new ksVector3(capsule.radius, capsule.height, capsule.direction);
            }
            else if ((mesh = collider as MeshCollider) != null)
            {
                m_shapeType = mesh.convex ? ksShape.ShapeTypes.CONVEX_MESH : ksShape.ShapeTypes.TRIANGLE_MESH;
                m_asset = mesh.sharedMesh;
            }
            else if ((terrain = collider as TerrainCollider) != null)
            {
                m_shapeType = ksShape.ShapeTypes.HEIGHT_FIELD;
                m_asset = terrain.terrainData;
            }
            else if ((characterController = collider as CharacterController) != null)
            {
                m_shapeType = ksShape.ShapeTypes.CAPSULE_CONTROLLER;
                m_size = new ksVector3(characterController.radius, characterController.height, 1);
            }

            // Invalid shape exception
            if (m_shapeType == ksShape.ShapeTypes.NO_COLLIDER)
            {
                throw new ArgumentException("Unsupported collider type");
            }
        }

        /// <summary>
        /// Checks if a collider is one of the types Reactor supports.
        /// </summary>
        /// <param name="collider">Collider to check</param>
        /// <returns>True if the collider is one of the supported types.</returns>
        public static bool IsSupported(Collider collider)
        {
            return collider != null &&
                (collider is SphereCollider ||
                collider is BoxCollider ||
                collider is CapsuleCollider ||
                collider is MeshCollider ||
                collider is TerrainCollider ||
                collider is CharacterController);
        }

        /// <summary>Calculate the volume of the collider.</summary>
        /// <returns>Volume of the collider.</returns>
        public float Volume()
        {
            switch (m_shapeType)
            {
                case ksShape.ShapeTypes.SPHERE:
                {
                    return 4f * (float)Math.PI * m_size.X * m_size.X * m_size.X / 3f;
                }
                case ksShape.ShapeTypes.BOX:
                {
                    return m_size.X * m_size.Y * m_size.Z;
                }
                case ksShape.ShapeTypes.CAPSULE:
                {
                    return 4f * (float)Math.PI * m_size.X * m_size.X * m_size.X / 3f +
                        (float)Math.PI * m_size.X * m_size.X * Math.Max(0f, (m_size.Y - 2f * m_size.X));
                }
                case ksShape.ShapeTypes.CONVEX_MESH:
                {
                    if (m_hull == null)
                    {
                        m_hull = ksConvexHull.Get((Mesh)m_asset);
                        if (m_hull == null)
                        {
                            return 0f;
                        }
                    }
                    return m_hull.Volume;
                }
            }
            return 0f;
        }

        /// <summary>The geometric center of the collider in local space.</summary>
        /// <returns>Geometric center</returns>
        public Vector3 GeometricCenter()
        {
            if (m_shapeType != ksShape.ShapeTypes.CONVEX_MESH)
            {
                return Center;
            }
            if (m_hull == null)
            {
                m_hull = ksConvexHull.Get((Mesh)m_asset);
                if (m_hull == null)
                {
                    return Vector3.zero;
                }
            }
            return m_hull.GeometricCenter;
        }

        /// <summary>Serializes the collider by shape type.</summary>
        /// <returns>Serialized shape.</returns>
        public byte[] Serialize()
        {
            switch(m_shapeType)
            {
                case ksShape.ShapeTypes.SPHERE: return SerializeSphereCollider((SphereCollider)m_collider);
                case ksShape.ShapeTypes.BOX: return SerializeBoxCollider((BoxCollider)m_collider);
                case ksShape.ShapeTypes.CAPSULE: return SerializeCapsuleCollider((CapsuleCollider)m_collider);
                case ksShape.ShapeTypes.CONVEX_MESH: return SerializeConvexMesh((MeshCollider)m_collider);
                case ksShape.ShapeTypes.TRIANGLE_MESH: return SerializeTriangleMesh((MeshCollider)m_collider);
                case ksShape.ShapeTypes.HEIGHT_FIELD: return SerializeTerrainCollider((TerrainCollider)m_collider);
            }
            return null;
        }

        /// <summary>Serializes radius data from a sphere collider.</summary>
        /// <param name="collider">Sphere collider</param>
        /// <returns>Serialized data.</returns>
        private byte[] SerializeSphereCollider(SphereCollider collider)
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
        private byte[] SerializeBoxCollider(BoxCollider collider)
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
        private byte[] SerializeCapsuleCollider(CapsuleCollider collider)
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
        private byte[] SerializeConvexMesh(MeshCollider collider)
        {
            if (m_hull == null)
            {
                m_hull = ksConvexHull.Get(collider.sharedMesh);
                if (m_hull == null)
                {
                    return null;
                }
            }
            uint vertexCount = (uint)m_hull.Vertices.Count;
            byte[] geometry = new byte[
                sizeof(int) +                       // vertex count
                vertexCount * 3 * sizeof(float)     // vertex count * vertex position (float + float + float)
            ];
            using (MemoryStream memStream = new MemoryStream(geometry))
            {
                using (BinaryWriter writer = new BinaryWriter(memStream))
                {
                    writer.Write(vertexCount);
                    foreach (Vector3 vertex in m_hull.Vertices)
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
        private byte[] SerializeTriangleMesh(MeshCollider collider)
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
        private byte[] SerializeTerrainCollider(TerrainCollider collider)
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
            byte[] geometry = new byte[dataOffset + xRes * yRes * sizeof(ushort)];

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
            return ksLZW.Compress(geometry);
        }

        /// <summary>
        /// Write a ushort value into a byte array.
        /// </summary>
        /// <param name="value">Value to write</param>
        /// <param name="array">Array to write the uint value to</param>
        /// <param name="offset">Offset in the array to start writing the bytes</param>
        private void WriteUShortBytes(ushort value, byte[] array, int offset)
        {
            if (array.Length > offset + 4)
            {
                array[offset] = (byte)value;
                array[offset + 1] = (byte)(value >> 8);
            }
        }

        /// <summary>Compare the geometry sources of each collider and return true if they are equal.</summary>
        /// <param name="other">Collider to compare to.</param>
        /// <returns>True if the collider geometry is equal.</returns>
        public bool IsGeometryEqual(ksIUnityCollider other)
        {
            if (other.ShapeType != m_shapeType)
            {
                return false;
            }
            ksUnityCollider wrapper = (ksUnityCollider)other;
            return m_size == wrapper.m_size && m_asset == wrapper.m_asset;
        }
    }
}
