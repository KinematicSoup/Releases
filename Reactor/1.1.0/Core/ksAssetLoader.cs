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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace KS.Reactor.Client.Unity
{
    /// <summary>
    /// Asset loader that loads <see cref="ksScriptAsset"/>s from <see cref="ksProxyScriptAsset"/>s.
    /// </summary>
    public class ksAssetLoader : ksBaseAssetLoader
    {
        /// <summary>Delegate for converting a Unity proxy object to the KS equivalent.</summary>
        /// <param name="value">Proxy value to convert.</param>
        /// <returns>Converted value.</returns>
        private delegate object Converter(object value);

        private static readonly char[] DELIMITERS = { '/', ':' };
        private Dictionary<string, AssetBundle> m_assetBundles;
        private Assembly m_assembly;

        // Maps types to their converters
        private static Dictionary<Type, Converter> m_converters = new Dictionary<Type, Converter>();
        // Maps proxy type to script types for built-in types.
        private static Dictionary<Type, Type> m_typeMap = new Dictionary<Type, Type>();

        /// <summary>Static initialization.</summary>
        static ksAssetLoader()
        {
            m_converters[typeof(ksVector2)] = (object value) => (ksVector2)(Vector2)value;
            m_converters[typeof(ksVector3)] = (object value) => (ksVector3)(Vector3)value;
            m_converters[typeof(ksQuaternion)] = (object value) => (ksQuaternion)(Quaternion)value;
            m_converters[typeof(ksColor)] = (object value) => (ksColor)(Color)value;
            m_converters[typeof(ksCurve)] = (object value) => ConvertCurve((AnimationCurve)value);

            m_typeMap[typeof(ksCollisionFilterAsset)] = typeof(ksCollisionFilter);
        }

        /// <summary>Constructor</summary>
        /// <param name="assetBundles">Map of asset bundle names to asset bundles.</param>
        /// <param name="assembly">Assembly to load script asset types from.</param>
        internal ksAssetLoader(Dictionary<string, AssetBundle> assetBundles, Assembly assembly)
        {
            m_assetBundles = assetBundles;
            m_assembly = assembly;
        }

        /// <summary>Stores a mapping between an asset id and an asset path.</summary>
        /// <param name="assetId">Asset id</param>
        /// <param name="assetPath">Asset path</param>
        internal void RegisterAsset(uint assetId, string assetPath)
        {
            string oldPath;
            int index;
            if (m_paths.TryGetValue(assetId, out oldPath) && oldPath != assetPath)
            {
                ksLog.Warning(this, "Multiple assets found with asset ID " + assetId + ". '" +
                    oldPath + "' will be replaced with '" + assetPath + "'.");
                // Remove old path to id mapping.
                m_pathToIdMap.Remove(oldPath);
                index = assetPath.LastIndexOfAny(DELIMITERS);
                if (index >= 0)
                {
                    List<uint> ids;
                    if (m_nameToIdMap.TryGetValue(oldPath, out ids))
                    {
                        ids.Remove(assetId);
                    }
                }
            }
            m_paths[assetId] = assetPath;
            m_pathToIdMap[assetPath] = assetId;
            index = assetPath.LastIndexOfAny(DELIMITERS);
            if (index >= 0)
            {
                MapNameToId(assetPath.Substring(index + 1), assetId);
            }
        }

        /// <summary>
        /// Loads an asset from a <see cref="ksProxyScriptAsset"/>. Returns the cached asset if it's already loaded.
        /// </summary>
        /// <typeparam name="T">
        /// Type of asset to load. If the <paramref name="proxy"/> is not for this type, the asset will not be loaded.
        /// </typeparam>
        /// <param name="proxy">Proxy asset to load from.</param>
        /// <param name="silent">If true, don't log warnings if the asset is the wrong type.</param>
        /// <returns>The loaded asset, or null if the asset could not be loaded or was the wrong type.</returns>
        public T FromProxy<T>(ksProxyScriptAsset proxy, bool silent = false) where T : ksScriptAsset
        {
            if (proxy == null)
            {
                return null;
            }
            ksScriptAsset asset = GetCached(proxy.AssetId);
            if (asset != null)
            {
                return asset as T;
            }
            Type proxyType = proxy.GetType();
            Type type;
            if (!m_typeMap.TryGetValue(proxyType, out type))
            {
                string typeStr = proxyType.FullName.Substring(18);// Remove "KSProxies.Scripts."
                type = m_assembly.GetType(typeStr);
                if (type == null)
                {
                    ksLog.Error(this, "Could not find script '" + typeStr + "' in '" + m_assembly.FullName + "'.");
                    return null;
                }
            }
            if (!typeof(T).IsAssignableFrom(type))
            {
                // Don't load if the asset is the wrong type.
                if (!silent)
                {
                    ksLog.Warning(this, "Cannot load '" + type.FullName + "' asset " + proxy.AssetId + " as type '" +
                        typeof(T).FullName + "'.");
                }
                return null;
            }

            try
            {
                asset = (ksScriptAsset)Activator.CreateInstance(type);
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Unable to construct instance of " + type.FullName, e);
                return null;
            }
            CacheAsset(proxy.AssetId, asset);
            try
            {
                BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                foreach (MemberInfo member in ksReflectionUtils.GetEditableMembers(type))
                {
                    FieldInfo proxyField = proxyType.GetField(member.Name, flags);
                    if (proxyField == null)
                    {
                        continue;
                    }
                    ksReflectionUtils.SetValue(asset, member, Convert(proxyField.GetValue(proxy),
                        ksReflectionUtils.GetType(member)));
                }
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Error converting proxy to " + type.FullName + ".", e);
            }
            return (T)asset;
        }

        /// <summary>Loads an asset.</summary>
        /// <typeparam name="T">
        /// Type of asset to load. The asset will not be loaded if it is not of this type.
        /// </typeparam>
        /// <param name="assetId">Id of the asset to load.</param>
        /// <param name="silent">If true, don't log warnings if the asset isn't found or is the wrong type.</param>
        /// <returns>The loaded asset, or null if the asset could not be loaded or was the wrong type.</returns>
        protected override T Load<T>(uint assetId, bool silent)
        {
            if (m_assembly == null)
            {
                return null;
            }
            string path;
            if (!m_paths.TryGetValue(assetId, out path))
            {
                if (!silent)
                {
                    ksLog.Error(this, "Could not find asset with id " + assetId + ".");
                }
                return null;
            }
            ksProxyScriptAsset proxy = LoadProxy(path, silent);
            if (proxy == null)
            {
                return null;
            }
            return FromProxy<T>(proxy, silent);
        }

        /// <summary>Converts a value from a proxy field to the given <paramref name="type"/>.</summary>
        /// <param name="value">Value to convert.</param>
        /// <param name="type">Type to convert to.</param>
        /// <returns>Converted type.</returns>
        private object Convert(object value, Type type)
        {
            if (value == null || value.GetType() == type)
            {
                return value;
            }
            // Arrays
            if (type.IsArray)
            {
                Type elementType = type.GetElementType();
                IList srcArray = (IList)value;
                IList array = Array.CreateInstance(elementType, srcArray.Count);
                for (int i = 0; i < array.Count; i++)
                {
                    array[i] = Convert(srcArray[i], elementType);
                }
                return array;
            }
            // Enums
            if (type.IsEnum)
            {
                return Enum.ToObject(type, value);
            }
            // Use the converter delegate for the type if there is one.
            Converter converter;
            if (m_converters.TryGetValue(type, out converter))
            {
                return converter(value);
            }
            // Structs
            if (type.IsValueType && !type.IsPrimitive)
            {
                // Convert proxy struct
                FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.Instance);
                object obj = Activator.CreateInstance(type);
                Type proxyType = value.GetType();
                foreach (FieldInfo field in fields)
                {
                    FieldInfo proxyField = proxyType.GetField(field.Name);
                    if (proxyField != null)
                    {
                        field.SetValue(obj, Convert(proxyField.GetValue(value), field.FieldType));
                    }
                }
                return obj;
            }
            // Script asset references.
            if (typeof(ksScriptAsset).IsAssignableFrom(type))
            {
                ksProxyScriptAsset proxy = value as ksProxyScriptAsset;
                ksScriptAsset asset = FromProxy<ksScriptAsset>(proxy);
                if (asset != null && type.IsAssignableFrom(asset.GetType()))
                {
                    return asset;
                }
                // Could not find an asset of the reference type.
                return null;
            }
            // Lists
            if (typeof(IList).IsAssignableFrom(type))
            {
                foreach (Type itype in type.GetInterfaces())
                {
                    if (itype.IsGenericType && itype.GetGenericTypeDefinition() == typeof(IList<>))
                    {
                        // Convert IList
                        Type elementType = itype.GetGenericArguments()[0];
                        IList list = (IList)Activator.CreateInstance(type);
                        IList srcArray = (IList)value;
                        for (int i = 0; i < srcArray.Count; i++)
                        {
                            list.Add(Convert(srcArray[i], elementType));
                        }
                        return list;
                    }
                }
            }
            // Dictionaries
            else if (typeof(IDictionary).IsAssignableFrom(type))
            {
                foreach (Type itype in type.GetInterfaces())
                {
                    if (itype.IsGenericType && itype.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                    {
                        // deserialize IDictionary
                        Type keyType = itype.GetGenericArguments()[0];
                        Type valueType = itype.GetGenericArguments()[1];
                        IDictionary dict = (IDictionary)Activator.CreateInstance(type);
                        IDictionary srcDict = (IDictionary)value;
                        foreach (object k in srcDict.Keys)
                        {
                            object key = Convert(k, keyType);
                            object val = Convert(srcDict[k], valueType);
                            dict[key] = val;
                        }
                        return dict;
                    }
                }
            }
            ksLog.Warning(this, "Could not convert '" + value.GetType().FullName + "' to '" + type.FullName + "'.");
            return null;
        }

        /// <summary>Loads a <see cref="ksProxyScriptAsset"/> from Resources or an asset bundle.</summary>
        /// <param name="path">
        /// Asset path to load from. If loading from Resources, must contain 'Resources/' in the path. If loading from
        /// an asset bundle the path should be '[asset bundle name]:[asset name]'.
        /// </param>
        /// <param name="silent">If true, don't log warnings if the asset isn't found.</param>
        /// <returns>Proxy script asset, or null if it could not be loaded.</returns>
        private ksProxyScriptAsset LoadProxy(string path, bool silent)
        {
            ksProxyScriptAsset proxy;
            int index = path.IndexOf("Resources/");
            if (index >= 0)
            {
                path = path.Substring(index + 10);// Path relative to Resources.
                proxy = Resources.Load<ksProxyScriptAsset>(path);
                if (proxy == null && !silent)
                {
                    ksLog.Error(this, "Could not load proxy asset from Resources/ " + path + ".");
                }
                return proxy;
            }
            index = path.IndexOf(":");
            if (index < 0)
            {
                if (!silent)
                {
                    ksLog.Error(this, "Invalid asset path '" + path + 
                        "'. Path must be in Resources or an asset bundle.");
                }
                return null;
            }
            string bundleName = path.Substring(0, index);
            string assetName = path.Substring(index + 1);
            AssetBundle bundle;
            if (!m_assetBundles.TryGetValue(bundleName, out bundle))
            {
                if (!silent)
                {
                    ksLog.Error(this, "Could not find asset bundle '" + bundleName + "'.");
                }
                return null;
            }
            proxy = bundle.LoadAsset<ksProxyScriptAsset>(assetName);
            if (proxy == null && !silent)
            {
                ksLog.Error(this, "Could not load asset '" + assetName + "' in asset bundle '" + bundleName + "'.");
            }
            return proxy;
        }

        /// <summary>Converts an <see cref="AnimationCurve"/> to a <see cref="ksCurve"/>.</summary>
        /// <param name="curve">Curve to convert.</param>
        /// <returns>Converted curve.</returns>
        private static ksCurve ConvertCurve(AnimationCurve curve)
        {
            ksCurve result = new ksCurve();
            foreach (Keyframe frame in curve.keys)
            {
                result.AddKeyframe(new ksCurve.Keyframe(frame.time, frame.value, frame.inTangent, frame.outTangent,
                    frame.inWeight, frame.outWeight));
            }
            result.PreWrapMode = ConvertWrapMode(curve.preWrapMode);
            result.PostWrapMode = ConvertWrapMode(curve.postWrapMode);
            return result;
        }

        /// <summary>Converts a <see cref="WrapMode"/> to a <see cref="ksCurve.WrapModes"/>.</summary>
        /// <param name="mode">Mode to convert.</param>
        /// <returns>Converted mode.</returns>
        private static ksCurve.WrapModes ConvertWrapMode(WrapMode mode)
        {
            switch (mode)
            {
                case WrapMode.ClampForever: return ksCurve.WrapModes.Last;
                case WrapMode.Loop: return ksCurve.WrapModes.Loop;
                case WrapMode.PingPong: return ksCurve.WrapModes.PingPong;
                default: return ksCurve.WrapModes.First;
            }
        }
    }
}