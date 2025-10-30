using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using KS.SF.Reactor;
using UnityEngine;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>Contains methods for serializing TreeInstance data.</summary>
    internal class sfTreeInstanceSync
    {
        private delegate void Setter(ref TreeInstance tree, sfBaseProperty property);
        private Dictionary<string, Setter> m_setters = new Dictionary<string, Setter>();
        private int m_treeInstanceSize = 0;

        public sfTreeInstanceSync()
        {
            m_treeInstanceSize = Marshal.SizeOf(typeof(TreeInstance));
        }

        /// <summary>Serialize a subset of tree instance data.</summary>
        /// <param name="-">Tree instance data</param>
        /// <param name="-">Start index</param>
        /// <param name="-">Number of trees to serialize.</param>
        /// <returns>serialized data.</returns>
        public byte[] Serialize(TreeInstance[] trees, int index, int count)
        {
            byte[] data = new byte[count * m_treeInstanceSize];
            try
            {
                IntPtr ptr = Marshal.AllocHGlobal(m_treeInstanceSize);
                for (int i = 0; i < count; ++i)
                {
                    Marshal.StructureToPtr(trees[index + i], ptr, true);
                    Marshal.Copy(ptr, data, i * m_treeInstanceSize, m_treeInstanceSize);
                }
                Marshal.FreeHGlobal(ptr);
            }
            catch(Exception ex)
            {
                ksLog.Error(this, "Error serializing TreeInstance array", ex);
                return null;
            }
            return data;
        }

        /// <summary>Deserialize tree data into a tree instance array.</summary>
        /// <param name="-">Tree instance data</param>
        /// <param name="data">data to deserialize.</param>
        /// <param name="-">Start index</param>
        public void Deserialize(TreeInstance[] trees, byte[] data, int index)
        {
            try
            {
                IntPtr ptr = Marshal.AllocHGlobal(m_treeInstanceSize);
                int i = 0;
                Type type = typeof(TreeInstance);
                for (int byteIndex = 0; byteIndex < data.Length; byteIndex += m_treeInstanceSize)
                {
                    Marshal.Copy(data, byteIndex, ptr, m_treeInstanceSize);
                    trees[index + i] = (TreeInstance)Marshal.PtrToStructure(ptr, type);
                    i++;
                }
                Marshal.FreeHGlobal(ptr);
            }
            catch(Exception ex)
            {
                ksLog.Error(this, "Error deserializing TreeInstance array", ex);
            }
        }
    }
}
