using System.Collections.Generic;
using System.Linq;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>IComparer for sorting assets first by type then by name.</summary>
    public class AssetSorter : IComparer<KeyValuePair<int, string>>
    {
        private UnityEngine.Object m_mainAsset;

        /// <summary>Constructor</summary>
        public AssetSorter()
        {

        }

        /// <summary>
        /// Sorts assets first by type then by name, keeping the main asset first. This is a stable sort--if two assets
        /// have the same type and name their order is preserved.
        /// </summary>
        /// <param name="assets">assets to sort.</param>
        /// <param name="mainAsset">mainAsset to keep at front of array.</param>
        /// <returns>sorted assets.</returns>
        public UnityEngine.Object[] Sort(UnityEngine.Object[] assets, UnityEngine.Object mainAsset)
        {
            m_mainAsset = mainAsset;
            UnityEngine.Object[] sortedAssets = new UnityEngine.Object[assets.Length];
            int i = 0;
            // OrderBy is a stable sort--it preserves the order of elements with equal values. Array.Sort does not so
            // we use OrderBy.
            foreach (UnityEngine.Object asset in assets.OrderBy(GetSortKey, this))
            {
                sortedAssets[i] = asset;
                i++;
            }
            m_mainAsset = null;
            return sortedAssets;
        }

        /// <summary>
        /// Compares two int+string key value pairs to see which should come first. First compares the ints, and if they
        /// are the same, compares strings.
        /// </summary>
        /// <param name="lhs"></param>
        /// <param name="rhs"></param>
        /// <returns>
        /// less than 0 if lhs should come first, greater than 0 if rhs should come first, and 0 if they
        /// are equal.
        /// </returns>
        public int Compare(KeyValuePair<int, string> lhs, KeyValuePair<int, string> rhs)
        {
            int delta = lhs.Key - rhs.Key;
            return delta == 0 ? lhs.Value.CompareTo(rhs.Value) : delta;
        }

        /// <summary>Gets the key used to sort an asset. Makes sure the main asset is the first subasset.</summary>
        /// <param name="asset."></param>
        /// <returns>
        /// sort key for the asset. The int is zero for the main asset, and the Unity
        /// class id for all other assets. The string is the name of the asset.
        /// </returns>
        private KeyValuePair<int, string> GetSortKey(UnityEngine.Object asset)
        {
            if (asset == m_mainAsset && m_mainAsset != null)
            {
                // Keep the main asset first
                return new KeyValuePair<int, string>(-1, "");
            }
            if (asset == null)
            {
                return new KeyValuePair<int, string>(int.MaxValue, "");
            }
            if (asset is UnityEngine.ScriptableObject)
            {
                return new KeyValuePair<int, string>(0, asset.name);
            }
            return new KeyValuePair<int, string>(
                sfBuiltInAssetsLoader.Get().StringToClassId(asset.GetType().Name), asset.name);
        }
    }
}
