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
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using System.Runtime.InteropServices;

namespace KS.Reactor.Client.Unity.DOTS
{
    public static class ksVectorExtensions
    {
        public static float2 ToFloat2(this ksVector2 value)
        {
            return new Vector2Union(value).Float2;
        }

        public static ksVector2 ToKSVector2(this float2 value)
        {
            return new Vector2Union(value).KSVector2;
        }

        public static bool EqualsFloat2(this ksVector2 lhs, float2 rhs)
        {
            return lhs.X == rhs.x && lhs.Y == rhs.y;
        }

        public static bool EqualsKSVector2(this float2 lhs, ksVector2 rhs)
        {
            return lhs.x == rhs.X && lhs.y == rhs.Y;
        }

        public static float3 ToFloat3(this ksVector3 value)
        {
            return new Vector3Union(value).Float3;
        }

        public static ksVector3 ToKSVector3(this float3 value)
        {
            return new Vector3Union(value).KSVector3;
        }

        public static bool EqualsFloat3(this ksVector3 lhs, float3 rhs)
        {
            return lhs.X == rhs.x && lhs.Y == rhs.y && lhs.Z == rhs.z;
        }

        public static bool EqualsKSVector3(this float3 lhs, ksVector3 rhs)
        {
            return lhs.x == rhs.X && lhs.y == rhs.Y && lhs.z == rhs.Z;
        }

        public static quaternion ToQuaternion(this ksQuaternion value)
        {
            return new QuaternionUnion(value).Quaternion;
        }

        public static ksQuaternion ToKSQuaternion(this quaternion value)
        {
            return new QuaternionUnion(value).KSQuaternion;
        }

        public static bool EqualsQuaternion(this ksQuaternion lhs, quaternion rhs)
        {
            return lhs.X == rhs.value.x && lhs.Y == rhs.value.y && lhs.Z == rhs.value.z && lhs.W == rhs.value.w;
        }

        public static bool EqualsKSQuaternion(this quaternion lhs, ksQuaternion rhs)
        {
            return lhs.value.x == rhs.X && lhs.value.y == rhs.Y && lhs.value.z == rhs.Z && lhs.value.w == rhs.W;
        }

        public static float4 ToFloat4(this ksColor value)
        {
            return new ColorUnion(value).Float4;
        }

        public static ksColor ToKSColor(this float4 value)
        {
            return new ColorUnion(value).KSColor;
        }

        public static bool EqualsFloat4(this ksColor lhs, float4 rhs)
        {
            return lhs.R == rhs.x && lhs.G == rhs.y && lhs.B == rhs.z && lhs.A == rhs.w;
        }

        public static bool EqualsKSColor(this float4 lhs, ksColor rhs)
        {
            return lhs.x == rhs.R && lhs.y == rhs.G && lhs.z == rhs.B && lhs.w == rhs.A;
        }

        public static int2 ToInt2(this ksVector2Int value)
        {
            return new Vector2IntUnion(value).Int2;
        }

        public static ksVector2Int ToKSVector2Int(this int2 value)
        {
            return new Vector2IntUnion(value).KSVector2Int;
        }

        public static bool EqualsInt2(this ksVector2Int lhs, int2 rhs)
        {
            return lhs.X == rhs.x && lhs.Y == rhs.y;
        }

        public static bool EqualsKSVector2Int(this int2 lhs, ksVector2Int rhs)
        {
            return lhs.x == rhs.X && lhs.y == rhs.Y;
        }

        public static int3 ToInt3(this ksVector3Int value)
        {
            return new Vector3IntUnion(value).Int3;
        }

        public static ksVector3Int ToKSVector3Int(this int3 value)
        {
            return new Vector3IntUnion(value).KSVector3Int;
        }

        public static bool EqualsInt3(this ksVector3Int lhs, int3 rhs)
        {
            return lhs.X == rhs.x && lhs.Y == rhs.y && lhs.Z == rhs.z;
        }

        public static bool EqualsKSVector3Int(this int3 lhs, ksVector3Int rhs)
        {
            return lhs.x == rhs.X && lhs.y == rhs.Y && lhs.z == rhs.Z;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct Vector2Union
        {
            [FieldOffset(0)]
            public ksVector2 KSVector2;
            [FieldOffset(0)]
            public float2 Float2;

            public Vector2Union(ksVector2 value) : this()
            {
                KSVector2 = value;
            }

            public Vector2Union(float2 value) : this()
            {
                Float2 = value;
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct Vector3Union
        {
            [FieldOffset(0)]
            public ksVector3 KSVector3;
            [FieldOffset(0)]
            public float3 Float3;

            public Vector3Union(ksVector3 value) : this()
            {
                KSVector3 = value;
            }

            public Vector3Union(float3 value) : this()
            {
                Float3 = value;
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct QuaternionUnion
        {
            [FieldOffset(0)]
            public ksQuaternion KSQuaternion;
            [FieldOffset(0)]
            public quaternion Quaternion;

            public QuaternionUnion(ksQuaternion value) : this()
            {
                KSQuaternion = value;
            }

            public QuaternionUnion(quaternion value) : this()
            {
                Quaternion = value;
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct ColorUnion
        {
            [FieldOffset(0)]
            public ksColor KSColor;
            [FieldOffset(0)]
            public float4 Float4;

            public ColorUnion(ksColor value) : this()
            {
                KSColor = value;
            }

            public ColorUnion(float4 value) : this()
            {
                Float4 = value;
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct Vector2IntUnion
        {
            [FieldOffset(0)]
            public ksVector2Int KSVector2Int;
            [FieldOffset(0)]
            public int2 Int2;

            public Vector2IntUnion(ksVector2Int value) : this()
            {
                KSVector2Int = value;
            }

            public Vector2IntUnion(int2 value) : this()
            {
                Int2 = value;
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct Vector3IntUnion
        {
            [FieldOffset(0)]
            public ksVector3Int KSVector3Int;
            [FieldOffset(0)]
            public int3 Int3;

            public Vector3IntUnion(ksVector3Int value) : this()
            {
                KSVector3Int = value;
            }

            public Vector3IntUnion(int3 value) : this()
            {
                Int3 = value;
            }
        }
    }
}