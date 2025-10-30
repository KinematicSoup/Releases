using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>Holds information used to load an asset.</summary>
    public struct sfAssetInfo
    {
        /// <summary>The asset's class name including namespace.</summary>
        public string ClassName;

        /// <summary>The asset's path.</summary>
        public string Path;

        /// <summary>The asset's index. If greater than zero, the asset is a subasset.</summary>
        public int Index;

        /// <summary>Is the asset contained in another asset?</summary>
        public bool IsSubasset
        {
            get { return Index > 0; }
        }

        /// <summary>
        /// Is this asset info valid? To be valid, <see cref="Path"/> and <see cref="ClassName"/> must be non null and
        /// non empty.
        /// </summary>
        public bool IsValid
        {
            get { return !string.IsNullOrEmpty(Path) && !string.IsNullOrEmpty(ClassName); }
        }

        /// <summary>Is the asset a built-in asset?</summary>
        public bool IsBuiltIn
        {
            get { return Path != null && Path.StartsWith(sfLoader.BUILT_IN_PREFIX); }
        }

        /// <summary>Constructor</summary>
        /// <param name="type">Type of asset</param>
        /// <param name="path">Asset path</param>
        /// <param name="index">The asset's index. Greater than zero for subassets.</param>
        public sfAssetInfo(Type type, string path, int index = 0)
        {
            ClassName = type.ToString();
            Path = path;
            Index = index;
        }

        /// <summary>Constructor</summary>
        /// <param name="className">Asset class name</param>
        /// <param name="path">Asset path</param>
        /// <param name="index">Asset index</param>
        public sfAssetInfo(string className, string path, int index = 0)
        {
            ClassName = className;
            Path = path;
            Index = index;
        }

        /// <summary>
        /// Returns the string "<see cref="ClassName"/> '<see cref="Path"/>'" with "[<see cref="Index"/>]" appended if
        /// it is larger than zero.
        /// </summary>
        /// <returns>The asset info as a string.</returns>
        public override string ToString()
        {
            string str = ClassName + " at '" + Path + "'";
            if (Index > 0)
            {
                str += "[" + Index + "]";
            }
            return str;
        }

        /// <summary>Checks if two asset infos are the same.</summary>
        /// <param name="lhs"></param>
        /// <param name="rhs"></param>
        /// <returns>True if the asset infos are the same.</returns>
        public static bool operator ==(sfAssetInfo lhs, sfAssetInfo rhs)
        {
            return lhs.ClassName == rhs.ClassName &&
                lhs.Path == rhs.Path &&
                lhs.Index == rhs.Index;
        }

        /// <summary>Checks if two asset infos are different.</summary>
        /// <param name="lhs"></param>
        /// <param name="rhs"></param>
        /// <returns>True if the asset infos are different.</returns>
        public static bool operator !=(sfAssetInfo lhs, sfAssetInfo rhs)
        {
            return !(lhs == rhs);
        }

        /// <summary>Checks if this asset info is equal to an object.</summary>
        /// <param name="obj"></param>
        /// <returns>True if this asset info is the same as the object.</returns>
        public override bool Equals(object obj)
        {
            return obj is sfAssetInfo && (this == (sfAssetInfo)obj);
        }

        /// <summary>Gets a hash code from this asset info.</summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Path == null ? 0 : Path.GetHashCode();
                hash *= 7;
                hash += ClassName == null ? 0 : ClassName.GetHashCode();
                hash *= 7;
                hash += Index.GetHashCode();
                return hash;
            }
        }
    }
}
