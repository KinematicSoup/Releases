using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UIElements;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.Presets;
using KS.SF.Reactor;
using KS.SF.Unity.Editor;
using KS.SceneFusion;
using KS.SceneFusion2.Client;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>Loads and caches assets.</summary>
    internal class sfLoader
    {
        /// <summary>Singleton instance</summary>
        public static sfLoader Get()
        {
            return m_instance;
        }
        private static sfLoader m_instance = new sfLoader();

        /// <summary>Load error event handler.</summary>
        /// <param name="assetInfo">assetInfo that failed to load.</param>
        /// <param name="message"></param>
        public delegate void LoadErrorHandler(sfAssetInfo assetInfo, string message);

        /// <summary>Invoked when an asset cannot be loaded.</summary>
        public LoadErrorHandler OnLoadError;

        /// <summary>Find missing asset event handler.</summary>
        /// <param name="assetInfo">assetInfo for the asset that is no longer missing.</param>
        /// <param name="asset">asset that used to be missing.</param>
        public delegate void FindMissingAssetHandler(sfAssetInfo assetInfo, UObject asset);

        /// <summary>Invoked when an asset that was missing is created.</summary>
        public FindMissingAssetHandler OnFindMissingAsset;

        /// <summary>New asset event handler.</summary>
        /// <param name="assetInfo">assetInfo for the new asset.</param>
        /// <param name="asset">asset that was created.</param>
        public delegate void NewAssetHandler(sfAssetInfo assetInfo, UObject asset);

        /// <summary>Invoked when an asset is added to the cache.</summary>
        public NewAssetHandler OnCacheAsset;

        /// <summary>Generator for an asset.</summary>
        /// <returns>generated asset.</returns>
        public delegate UObject Generator();

        /// <summary>Callback to parse and possibly change a line of text in a file.</summary>
        /// <param name="line">line of text</param>
        private delegate void LineParser(ref string line);
        
        private Dictionary<sfAssetInfo, UObject> m_cache = new Dictionary<sfAssetInfo, UObject>();
        private Dictionary<UObject, sfAssetInfo> m_infoCache = new Dictionary<UObject, sfAssetInfo>();
        // Syncable types are keys. If the generator is null, we call ScriptableObject.CreateInstance or attempt to
        // call the default constructor.
        private Dictionary<Type, Generator> m_generators = new Dictionary<Type, Generator>();
        private Dictionary<Type, Generator> m_standInGenerators = new Dictionary<Type, Generator>();
        // When we need to create a stand-in instance, we first check for a template asset in this map to copy, and if
        // there isn't one we check for a stand-in generator.
        private Dictionary<Type, UObject> m_standInTemplates = new Dictionary<Type, UObject>();
        // When we need to create a temporary library asset and set its GUID, we first check if there is an asset for
        // that type to copy in the library overrides, and then we check m_standInTemplates. This is for when we want
        // to use a different template for the library asset than we use for the stand-in instance. See comments in
        // LoadStandInTemplates for details.
        private Dictionary<Type, UObject> m_standInTemplateLibraryOverrides = new Dictionary<Type, UObject>();
        private Dictionary<UObject, UObject> m_standInReplacements = new Dictionary<UObject, UObject>();
        private HashSet<UObject> m_standInInstances = new HashSet<UObject>();
        private HashSet<UObject> m_createdAssets = new HashSet<UObject>();

        // Built-in assets will use their name with this prefix as their path.
        public const string BUILT_IN_PREFIX = "BuiltIn/";

        /// <summary>Singleton constructor</summary>
        private sfLoader()
        {
            LoadGenerators();
            LoadStandInTemplates();
        }

        /// <summary>Constructor that sets the info cache for testing.</summary>
        /// <param name="map">map of UObjects to asset info.</param>
        internal sfLoader(Dictionary<UObject, sfAssetInfo> infoCache)
        {
            m_infoCache = infoCache;
        }

        /// <summary>Loads syncable asset generators and stand-in generators.</summary>
        private void LoadGenerators()
        {
            m_generators[typeof(TerrainData)] = () => new TerrainData();
            m_generators[typeof(TerrainLayer)] = () => new TerrainLayer();
            m_generators[typeof(LightingSettings)] = () => new LightingSettings();

            m_standInGenerators[typeof(SparseTexture)] = () => new SparseTexture(1, 1, TextureFormat.Alpha8, 1);
            m_standInGenerators[typeof(CustomRenderTexture)] = () => new CustomRenderTexture(1, 1);
            m_standInGenerators[typeof(RenderTexture)] = () => new RenderTexture(1, 1, 1);
            m_standInGenerators[typeof(CubemapArray)] = () => new CubemapArray(1, 1, TextureFormat.Alpha8, false);
            m_standInGenerators[typeof(Preset)] = () => new Preset(m_standInTemplates[typeof(GameObject)]);

            // Video files can only be imported on Windows if QuickTime is installed. Otherwise an error is logged when
            // Unity tries to import the video. To avoid the error we store the asset as a binary file and change the
            // file extension and reimport it when it's needed.
            m_standInGenerators[typeof(VideoClip)] = delegate ()
            {
                string path = sfPaths.PackageRoot + "Stand-Ins/BlackFrame";
                string oldPath = path + ".bin";
                string newPath = path + ".avi";
                if (RenameFile(oldPath, newPath) && File.Exists(newPath))
                {
                    VideoClip asset = AssetDatabase.LoadAssetAtPath<VideoClip>(newPath);
                    if (asset == null)
                    {
                        ksLog.Error(this, "Unable to load VideoClip '" + newPath + "'.");
                    }
                    else
                    {
                        m_standInTemplates[typeof(VideoClip)] = asset;
                        return UObject.Instantiate(asset);
                    }
                }
                return null;
            };
        }

        /// <summary>
        /// Renames a file and deletes the meta file for it. Returns true if the file was successfully renamed.
        /// </summary>
        /// <param name="oldPath"></param>
        /// <param name="newPath"></param>
        /// <returns></returns>
        private bool RenameFile(string oldPath, string newPath)
        {
            if (File.Exists(oldPath) && !File.Exists(newPath))
            {
                try
                {
                    File.Move(oldPath, newPath);
                }
                catch (Exception e)
                {
                    ksLog.Error(this, "Error moving '" + oldPath + "' to '" + newPath + "'.", e);
                    return false;
                }
                string oldMetaFilePath = oldPath + ".meta";
                try
                {
                    if (File.Exists(oldMetaFilePath))
                    {
                        File.Delete(oldMetaFilePath);
                    }
                }
                catch (Exception e)
                {
                    ksLog.Error(this, "Error deleting '" + oldMetaFilePath + "'.", e);
                }
                AssetDatabase.Refresh();
                return true;
            }
            return false;
        }

        /// <summary>Loads built-in and stand-in assets.</summary>
        public void Initialize()
        {
            LoadBuiltInAssets();
            sfUnityEventDispatcher.Get().OnImportAssets += HandleImportAssets;
        }

        /// <summary>Destroys stand-in instances and clears the cache.</summary>
        public void CleanUp()
        {
            foreach (UObject standIn in m_standInInstances)
            {
                UObject.DestroyImmediate(standIn);
            }
            m_createdAssets.Clear();
            m_standInInstances.Clear();
            m_cache.Clear();
            m_infoCache.Clear();
            m_standInReplacements.Clear();
            sfUnityEventDispatcher.Get().OnImportAssets -= HandleImportAssets;
        }

        /// <summary>Checks if we can create a stand-in of the given type.</summary>
        /// <param name="type">type to check.</param>
        /// <returns>true if we can create a stand-in for the type.</returns>
        public bool CanCreateStandIn(Type type)
        {
            return m_standInTemplates.ContainsKey(type) || m_standInGenerators.ContainsKey(type) ||
                typeof(ScriptableObject).IsAssignableFrom(type) || !new ksReflectionObject(type).GetConstructor(
                    BindingFlags.Public | BindingFlags.Instance, true).IsVoid;
        }

        /// <summary>Checks if a Unity object is an asset or asset stand-in.</summary>
        /// <param name="obj">obj to check.</param>
        /// <returns>true if the object is an asset or asset stand-in.</returns>
        public bool IsAsset(UObject obj)
        {
            return obj != null && (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(obj)) || IsStandIn(obj));
        }

        /// <summary>Checks if an object is a built-in asset.</summary>
        /// <param name="uobj">uobj to check.</param>
        /// <returns>true if the uobject is a built-in asset.</returns>
        public bool IsBuiltInAsset(UObject uobj)
        {
            return uobj != null && IsBuiltInAsset(AssetDatabase.GetAssetPath(uobj));
        }

        /// <summary>Checks if an asset path is for a built-in asset.</summary>
        /// <param name="path">path to check.</param>
        /// <returns>true if the path is for a built-in asset.</returns>
        public bool IsBuiltInAsset(string path)
        {
            return path == "Resources/unity_builtin_extra" || path == "Library/unity default resources";
        }

        /// <summary>
        /// Checks if an object is a library asset. Library assets are processed and written to the library folder and
        /// loaded from there. References to assets are saved with a "type" value that indicates if the asset is a
        /// library asset (type 3) or not (type 2). If this type value is incorrect, Unity will log an unknown error
        /// when it tries to load the asset.
        /// </summary>
        /// <returns>true if the asset is a library asset.</returns>
        public bool IsLibraryAsset(UObject obj)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            // The asset is a library asset if its importer is not AssetImporter, or if it is a SceneAsset or
            // DefaultAsset and not a subsasset.
            return !string.IsNullOrEmpty(path) && (GetImporterType(path) != typeof(AssetImporter) ||
                ((obj is SceneAsset || obj is DefaultAsset) && !AssetDatabase.IsSubAsset(obj)));
        }

        /// <summary>Gets the asset importer type for the asset at the given path.</summary>
        /// <param name="path">path of asset to get importer type for.</param>
        /// <returns>of importer for the asset.</returns>
        private Type GetImporterType(string path)
        {
#if UNITY_2022_2_OR_NEWER
            return AssetDatabase.GetImporterType(path);
#else
            AssetImporter importer = AssetImporter.GetAtPath(path);
            return importer == null ? null : importer.GetType();
#endif
        }

        /// <summary>Checks if an object is an asset stand-in.</summary>
        /// <param name="obj">obj to check.</param>
        /// <returns>true if the object is an asset stand-in.</returns>
        public bool IsStandIn(UObject obj)
        {
            return obj != null && m_standInInstances.Contains(obj);
        }

        /// <summary>Was this asset created when we tried to load it?</summary>
        /// <param name="obj">obj to check.</param>
        /// <returns>true if the object was created on load.</returns>
        public bool WasCreatedOnLoad(UObject obj)
        {
            return obj != null && m_createdAssets.Contains(obj);
        }

        /// <summary>
        /// Is this object a syncable asset type? These assets are created if they are not found during loading.
        /// </summary>
        /// <param name="obj">obj to check.</param>
        /// <returns>true if the object is a syncable asset type.</returns>
        public bool IsSyncableAssetType(UObject obj)
        {
            return obj != null && m_generators.ContainsKey(obj.GetType());
        }

        /// <summary>Is this a type of asset that the loader will create if it is not found during loading?</summary>
        /// <param name="type">type to check.</param>
        /// <returns>true if the type is a syncable asset type.</returns>
        public bool IsSyncableAssetType(Type type)
        {
            return m_generators.ContainsKey(type);
        }

        /// <summary>
        /// Registers an asset type as a syncable type. Scene Fusion will attempt to sync these assets if they are
        /// referenced in the scene and will create the asset if it does not exist locally. Scene Fusion syncs the assets
        /// serialized properties. This will not work for binary assets whose data is not available via serialized
        /// properties.
        /// </summary>
        /// <param name="generator">
        /// optional generator for creating an instance of the asset type if the asset
        /// was not found. If null, ScriptableObject.CreateInstance will be used for scriptable objects,
        /// and the default constructor will be called if it exists for UObjects. Non scriptable objects
        /// without a default constructor cannot be generated without a generator.
        /// </param>
        public void RegisterSyncableType<T>(Generator generator = null)
        {
            m_generators[typeof(T)] = generator;
        }

        /// <summary>Gets the local file id of an asset.</summary>
        /// <param name="asset">asset to get local file id for.</param>
        /// <returns>local file id of the asset, or 0 if the uobject is not an asset.</returns>
        public long GetLocalFileId(UObject asset)
        {
            if (asset == null)
            {
                return 0;
            }
            string guid;
            long fileId;
            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out guid, out fileId) ? fileId : 0;
        }

        /// <summary>Gets the asset info for an asset used to load the object from the asset cache.</summary>
        /// <param name="asset">asset to get asset info for.</param>
        /// <returns>asset info.</returns>
        public sfAssetInfo GetAssetInfo(UObject asset)
        {
            if (asset == null)
            {
                return new sfAssetInfo();
            }
            sfAssetInfo info;
            if (!m_infoCache.TryGetValue(asset, out info))
            {
                string path = AssetDatabase.GetAssetPath(asset);
                if (IsBuiltInAsset(path))
                {
                    info = new sfAssetInfo();
                    info.Path = BUILT_IN_PREFIX + asset.name;
                    info.ClassName = asset.GetType().ToString();

                    // If there's already an asset cached for this path, it means we tried and failed to load this
                    // built-in asset before, so invoke the find missing asset event.
                    if (m_cache.ContainsKey(info) && OnFindMissingAsset != null)
                    {
                        OnFindMissingAsset(info, asset);
                    }
                    m_infoCache[asset] = info;
                    m_cache[info] = asset;
                }
                else if (!string.IsNullOrEmpty(path))
                {
                    info = CacheAssets(path, asset);
                }
            }
            return info;
        }

        /// <summary>Creates a uobject of the given type.</summary>
        /// <param name="type"></param>
        /// <returns>created uobject, or null if one could not be created.</returns>
        public UObject Create(Type type)
        {
            Generator generator;
            if (m_generators.TryGetValue(type, out generator) && generator != null)
            {
                return generator();
            }
            return Construct(type);
        }

        /// <summary>
        /// Loads an asset of type T. Tries first to load from the cache, and if it's not found, caches it. If the asset
        /// is not found but is a syncable type, generates one.
        /// </summary>
        /// <param name="info">info of asset to load.</param>
        /// <param name="guid">guid to assign the generated asset, if one is generated.</param>
        /// <returns>asset, or null if the asset was not found.</returns>
        public T Load<T>(sfAssetInfo info, string guid = null) where T : UObject
        {
            return Load(info, guid) as T;
        }

        /// <summary>
        /// Loads an asset. Tries first to load from the cache, and if it's not found, caches it. If the asset is not
        /// found but is a syncable type, generates one.
        /// </summary>
        /// <param name="info">info of asset to load.</param>
        /// <param name="guid">guid to assign the generated asset, if one is generated.</param>
        /// <returns>asset, or null if the asset was not found.</returns>
        public UObject Load(sfAssetInfo info, string guid = null)
        {
            if (!info.IsValid)
            {
                return null;
            }
            UObject asset;
            if (!m_cache.TryGetValue(info, out asset) || asset.IsDestroyed())
            {
                Type type = sfTypeCache.Get().Load(info.ClassName);
                if (type == null)
                {
                    type = typeof(UObject);
                    ksLog.Warning(this, "Cannot determine type of asset " + info.Path + ". Trying " + type);
                }

                if (info.IsBuiltIn)
                {
                    // Use the slow fallback method to find and cache built-ins that can't be loaded normally.
                    FindUncachedBuiltInsSlow(type);
                    m_cache.TryGetValue(info, out asset);
                }
                else
                {
                    // Load and cache the assets at the path.
                    asset = CacheAssets(info.Path, info.Index);
                }

                if (asset == null)
                {
                    Generator generator;
                    if (info.Index == 0 && m_generators.TryGetValue(type, out generator) && !info.IsBuiltIn)
                    {
                        ksLog.Info(this, "Generating " + type + " '" + info.Path + "'.");
                        if (generator != null)
                        {
                            asset = generator();
                        }
                        else
                        {
                            asset = Construct(type);
                        }

                        if (asset != null && asset.GetType() == type)
                        {
                            ksPathUtils.Create(info.Path);
                            AssetDatabase.CreateAsset(asset, info.Path);
                            if (!string.IsNullOrEmpty(guid))
                            {
                                SetGuid(info.Path, guid);
                                // Reload the asset since changing it's guid makes it a different object.
                                AssetDatabase.Refresh();
                                asset = AssetDatabase.LoadAssetAtPath(info.Path, asset.GetType());
                            }

                            if (asset != null)
                            {
                                m_infoCache[asset] = info;
                                m_cache[info] = asset;
                                m_createdAssets.Add(asset);
                            }
                            else
                            {
                                ksLog.Warning(this, "Could not load generated " + info + ".");
                            }
                        }
                        else
                        {
                            ksLog.Warning(this, "Could not generate " + type + ".");
                        }
                    }
                    else
                    {
                        string message = "Unable to load " + info + ".";
                        if (OnLoadError != null)
                        {
                            OnLoadError(info, message);
                        }
                        else
                        {
                            ksLog.Error(this, message);
                        }
                    }
                }

                if (asset != null && asset.GetType() != type)
                {
                    string message = "Expected asset at '" + info.Path + "' index " + info.Index + " to be type " + type +
                        " but found " + asset.GetType();
                    if (OnLoadError != null)
                    {
                        OnLoadError(info, message);
                    }
                    else
                    {
                        ksLog.Error(this, message);
                    }
                    asset = null;
                }

                if (asset == null)
                {
                    Generator generator;
                    if (m_standInTemplates.TryGetValue(type, out asset))
                    {
                        asset = UObject.Instantiate(asset);
                    }
                    else if (m_standInGenerators.TryGetValue(type, out generator))
                    {
                        asset = generator();
                    }
                    else
                    {
                        asset = Construct(type);
                    }
                    if (asset != null)
                    {
                        m_infoCache[asset] = info;
                        asset.name = "Missing " + type.Name + " (" + info.Path + ")";
                        if (info.Index != 0)
                        {
                            asset.name += "[" + info.Index + "]";
                        }
                        asset.hideFlags = HideFlags.HideAndDontSave;
                        m_standInInstances.Add(asset);
                    }
                    else
                    {
                        ksLog.Warning(this, "Could not create " + type + " stand-in.");
                    }
                    m_cache[info] = asset;
                }
            }
            return asset;
        }

        /// <summary>
        /// Constructs an instance of a UObject type. Returns null if the type could not be constructed.
        /// </summary>
        /// <param name="type">type to construct.</param>
        /// <error>UObject instance of type, or null if it could not be constructed.</error>
        private UObject Construct(Type type)
        {
            if (typeof(ScriptableObject).IsAssignableFrom(type))
            {
                return ScriptableObject.CreateInstance(type);
            }
            if (typeof(UObject).IsAssignableFrom(type))
            {
                try
                {
                    return (UObject)Activator.CreateInstance(type);
                }
                catch (Exception)
                {
                    // Ignore the exception; we'll log something later.
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the correct asset for a stand-in if it is available. This will not attempt to load the asset if it is
        /// not already cached.
        /// </summary>
        /// <param name="uobj">uobj to get replacement asset for.</param>
        /// <returns>replacement asset for the uobj, or null if none was found.</returns>
        public T GetAssetForStandIn<T>(T uobj) where T : UObject
        {
            if (uobj == null)
            {
                return null;
            }
            UObject asset;
            m_standInReplacements.TryGetValue(uobj, out asset);
            return asset as T;
        }

        /// <summary>
        /// Creates an asset in a temp folder for a stand-in with a specific guid and optional file id.
        /// </summary>
        /// <param name="standIn">standIn to create asset for.</param>
        /// <param name="guid"></param>
        /// <param name="isLibraryAsset">
        /// is the stand-in for a library asset? Library assets are processed and written
        /// to the library folder and loaded from there.
        /// </param>
        /// <param name="fileId">
        /// optional file id. File ids are used to identify subassets in non-library assets and
        /// prefabs. If the stand-in is not for a non-library subasset or prefab, leave this as zero.
        /// </param>
        /// <returns>asset created for the stand-in, or null if an asset could not be created.</returns>
        public UObject CreateStandInAssetWithGuid(UObject standIn, string guid, bool isLibraryAsset, long fileId = 0)
        {
            if (!m_standInInstances.Contains(standIn))
            {
                return null;
            }
            sfAssetInfo info = GetAssetInfo(standIn);
            string path = sfPaths.Temp + "StandIn" + standIn.GetInstanceID();
            ksPathUtils.Create(path);

            if (isLibraryAsset)
            {
                GameObject standInGameObject = standIn as GameObject;
                if (standInGameObject != null)
                {
                    // Don't save hide flags must be removed to save the object as a prefab.
                    standInGameObject.hideFlags = HideFlags.HideInHierarchy;
                    path += ".prefab";
                    PrefabUtility.SaveAsPrefabAsset(standInGameObject, path);
                    standInGameObject.hideFlags = HideFlags.HideAndDontSave;
                    SetFileId(path, fileId);
                }
                else if (standIn is DefaultAsset)
                {
                    AssetDatabase.CreateFolder(ksPathUtils.Clean(sfPaths.Temp), "StandIn" + standIn.GetInstanceID());
                }
                else
                {
                    string templatePath = null;
                    UObject template;
                    if (m_standInTemplateLibraryOverrides.TryGetValue(standIn.GetType(), out template))
                    {
                        templatePath = AssetDatabase.GetAssetPath(template);
                    }
                    else if (m_standInTemplates.TryGetValue(standIn.GetType(), out template))
                    {
                        templatePath = AssetDatabase.GetAssetPath(template);
                    }
                    if (string.IsNullOrEmpty(templatePath))
                    {
                        return null;
                    }

                    int index = templatePath.LastIndexOf('.');
                    if (index >= 0)
                    {
                        path += templatePath.Substring(index);
                    }
                    try
                    {
                        File.Copy(templatePath, path);
                        File.Copy(templatePath + ".meta", path + ".meta");
                    }
                    catch (Exception e)
                    {
                        ksLog.Error(this, "Error copying " + templatePath + " to " + path + ".", e);
                    }
                }
                SetGuid(path, guid);
            }
            else
            {
                path += ".asset";
                standIn = UObject.Instantiate(standIn);
                standIn.hideFlags = HideFlags.None;
                AssetDatabase.CreateAsset(standIn, path);
                // Setting the guid does not always change the stand-in's guid. Instead it sometimes makes the asset we
                // just created into a different object with a different guid.
                SetGuid(path, guid);
                if (fileId != 0 && info.IsSubasset)
                {
                    SetFileId(path, fileId);
                }
            }
            AssetDatabase.Refresh();
            // Load the asset we just created.
            UObject asset = AssetDatabase.LoadAssetAtPath(path, standIn.GetType());
            // If the real asset is a library asset and the stand-in is not, or vice versa, Unity will log an unknown
            // error when the real asset is available and it tries to load the reference to it, so we return null in
            // this case.
            if (asset == null || IsLibraryAsset(asset) != isLibraryAsset)
            {
                return null;
            }
            return asset;
        }

        /// <summary>Sets the guid for an asset by parsing and updating the meta file for the asset.</summary>
        /// <param name="path">path to the asset to set the guid for. This is not the path to the meta file.</param>
        /// <param name="guid">guid to set.</param>
        private void SetGuid(string path, string guid)
        {
            UpdateFile(path + ".meta", (ref string line) =>
            {
                int index = 0;
                while (index < line.Length && char.IsWhiteSpace(line[index]))
                {
                    index++;
                }
                if (index >= line.Length)
                {
                    return;
                }
                int endIndex = line.IndexOf(':');
                if (endIndex <= index)
                {
                    return;
                }
                string paramName = line.Substring(index, endIndex - index);
                if (paramName == "guid")
                {
                    line = line.Substring(0, endIndex + 1) + " " + guid;
                }
            });
        }

        /// <summary>
        /// Sets the file id of the first asset in an asset file by parsing and updating the asset file. Does not work
        /// and logs a warning if serialization mode is not ForceText.
        /// </summary>
        /// <param name="path">path to the asset to set the file id for.</param>
        /// <param name="fileId">fileId to set.</param>
        private void SetFileId(string path, long fileId)
        {
            if (EditorSettings.serializationMode != SerializationMode.ForceText)
            {
                ksLog.Warning(this, "Cannot set file id for '" + path + "' when serialization mode is not ForceText.");
                return;
            }
            string oldFileId = null;
            UpdateFile(path, (ref string line) =>
            {
                if (oldFileId != null)
                {
                    int index = line.IndexOf("{fileID: " + oldFileId + "}");
                    if (index >= 0)
                    {
                        line = line.Substring(0, index + 9) + fileId + line.Substring(index + 9 + oldFileId.Length);
                    }
                }
                else if (line.StartsWith("--- !u!"))
                {
                    int index = line.IndexOf('&');
                    if (index >= 0)
                    {
                        oldFileId = line.Substring(index + 1);
                        line = line.Substring(0, index + 1) + fileId;
                    }
                }
            });
        }

        /// <summary>Parses and updates a file line by line.</summary>
        /// <param name="path">path to file to update.</param>
        /// <param name="parser">parser for parsing and updating each line.</param>
        private void UpdateFile(string path, LineParser parser)
        {
            // Write to a temp file.
            string tmpPath = path + ".tmp";
            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    using (StreamWriter writer = new StreamWriter(tmpPath))
                    {
                        string line = reader.ReadLine();
                        while (line != null)
                        {
                            parser(ref line);
                            writer.WriteLine(line);
                            line = reader.ReadLine();
                        }
                    }
                }
                // Delete the old file and replace it with the temp file
                File.Delete(path);
                File.Move(tmpPath, path);
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Error updating '" + path + "'.", e);
                ksPathUtils.Delete(tmpPath);
            }
        }

        /// <summary>Caches all assets at the given path. Replaces missing references to the cached assets.</summary>
        /// <param name="path">path of assets to cache.</param>
        /// <returns>asset at the given path.</returns>
        private UObject CacheAssets(string path)
        {
            sfAssetInfo info = new sfAssetInfo();
            UObject asset = null;
            CacheAssets(path, 0, ref asset, ref info);
            return asset;
        }

        /// <summary>
        /// Caches all assets at the given path. Replaces missing references to the cached assets. Returns the asset
        /// info for the given asset.
        /// </summary>
        /// <param name="path">path of assets to cache.</param>
        /// <param name="asset">asset to get sub-asset path for.</param>
        /// <returns>sfAssetInfo asset info for the asset. Paths is empty if the asset was not found.</returns>
        private sfAssetInfo CacheAssets(string path, UObject asset)
        {
            sfAssetInfo info = new sfAssetInfo();
            CacheAssets(path, -1, ref asset, ref info);
            return info;
        }

        /// <summary>
        /// Caches all assets at the given path. Replaces missing references to the cached assets. Returns the asset at
        /// the given index.
        /// </summary>
        /// <param name="path">path of assets to cache.</param>
        /// <param name="index">index of sub-asset to get.</param>
        /// <returns>asset at the given index, or null if none was found.</returns>
        private UObject CacheAssets(string path, int index)
        {
            UObject asset = null;
            sfAssetInfo info = new sfAssetInfo();
            CacheAssets(path, index, ref asset, ref info);
            return asset;
        }

        /// <summary>
        /// Caches all assets at the given path. Replaces missing references to the cached assets. Optionally retrieves
        /// a sub-asset by index or an asset index for an asset.
        /// </summary>
        /// <param name="path">path of assets to cache.</param>
        /// <param name="index">
        /// index of sub-asset to retrieve. Pass negative number to not retrieve a sub asset.
        /// </param>
        /// <param name="asset">
        /// set to sub-asset at the given index if one is found. Otherwise
        /// retrieves the sub-asset path for this asset.
        /// </param>
        /// <param name="assetInfo">set to info for asset if it is not null.</param>
        private void CacheAssets(
            string path,
            int index,
            ref UObject asset,
            ref sfAssetInfo assetInfo)
        {
            // Load all assets if this is not a scene asset (loading all assets from a scene asset causes an error)
            UObject[] assets = null;
            if (!path.EndsWith(".unity"))
            {
                assets = AssetDatabase.LoadAllAssetsAtPath(path);
            }
            if (assets == null || assets.Length == 0)
            {
                // Some assets (like folders) will return 0 results if you use LoadAllAssetsAtPath, but can be loaded
                // using LoadAssetAtPath.
                assets = new UObject[] { AssetDatabase.LoadAssetAtPath<UObject>(path) };
                if (assets[0] == null)
                {
                    return;
                }
            }
            else if (assets.Length > 1)
            {
                // Sub-asset order is not guaranteed so we sort based on type and name. This may fail if two sub-assets
                // have the exact same type and name...
                assets = new AssetSorter().Sort(assets, AssetDatabase.LoadAssetAtPath<UObject>(path));
            }
            for (int i = 0; i < assets.Length; i++)
            {
                UObject uobj = assets[i];
                if (uobj == null)
                {
                    continue;
                }
                sfAssetInfo info = new sfAssetInfo(uobj.GetType(), path, i);

                // If the cache contains a different asset for this path, check if it is a stand-in for a previously
                // missing asset.
                UObject current;
                if (m_cache.TryGetValue(info, out current))
                {
                    if (current == uobj)
                    {
                        continue;
                    }
                    if (IsStandIn(current))
                    {
                        if (current != null)
                        {
                            // Map the stand-in to the correct asset so if we find references to the stand-in later, we can
                            // update them to the correct asset.
                            m_standInReplacements[current] = uobj;
                        }
                        if (OnFindMissingAsset != null)
                        {
                            OnFindMissingAsset(info, uobj);
                        }
                    }
                }

                m_infoCache[uobj] = info;
                m_cache[info] = uobj;
                
                if (index == i)
                {
                    asset = uobj;
                }
                else if (asset == uobj)
                {
                    assetInfo = info;
                }

                if (OnCacheAsset != null)
                {
                    OnCacheAsset(info, uobj);
                }
            }
        }

        /// <summary>
        /// Loads built-in assets into the cache. Built-in assets cannot be loaded programmatically, so we assign
        /// references to them to a scriptable object in the editor, and load the scriptable object to get asset
        /// references.
        /// </summary>
        private void LoadBuiltInAssets()
        {
            CacheBuiltIns(ksIconUtility.Get().GetBuiltInIcons());

            sfBuiltInAssetsLoader loader = sfBuiltInAssetsLoader.Get();
            CacheBuiltIns(loader.LoadBuiltInAssets<Material>());
            CacheBuiltIns(loader.LoadBuiltInAssets<Texture2D>());
            CacheBuiltIns(loader.LoadBuiltInAssets<Sprite>());
            CacheBuiltIns(loader.LoadBuiltInAssets<LightmapParameters>());
            CacheBuiltIns(loader.LoadBuiltInAssets<Mesh>());
            CacheBuiltIns(loader.LoadBuiltInAssets<Font>());
            CacheBuiltIns(loader.LoadBuiltInAssets<Shader>());
        }

        /// <summary>
        /// Loads stand-in template assets. When an asset is missing, we use a stand-in asset of the same type to
        /// represent it.
        /// </summary>
        private void LoadStandInTemplates()
        {
            CacheStandInFromBuiltIn<LightmapParameters>("Default-HighResolution");

            CacheStandInFromPath<Material>(sfPaths.StandIns + "Material.mat");
            CacheStandInFromPath<Texture2D>(sfPaths.Textures + "QuestionSmall.png");
            CacheStandInFromPath<Texture>(sfPaths.Textures + "QuestionSmall.png");
            CacheStandInFromPath<Sprite>(sfPaths.Textures + "QuestionSmall.png");
            CacheStandInFromPath<Mesh>(sfPaths.StandIns + "Cube.fbx");
            CacheStandInFromPath<AnimationClip>(sfPaths.StandIns + "Cube.fbx");
            CacheStandInFromPath(sfPaths.StandIns + "AudioMixer.mixer", 
                new ksReflectionObject(typeof(EditorWindow).Assembly, "UnityEditor.Audio.AudioMixerController").Type);
            CacheStandInFromPath(sfPaths.StandIns + "AudioMixer.mixer",
                new ksReflectionObject(typeof(EditorWindow).Assembly, 
                "UnityEditor.Audio.AudioMixerGroupController").Type);
            CacheStandInFromPath(sfPaths.StandIns + "AudioMixer.mixer",
                new ksReflectionObject(typeof(EditorWindow).Assembly, 
                "UnityEditor.Audio.AudioMixerSnapshotController").Type);
            CacheStandInFromPath<AudioClip>(sfPaths.StandIns + "AudioClip.wav");
            CacheStandInFromPath<Avatar>(sfPaths.StandIns + "StandIn.fbx");
            CacheStandInFromPath<UObject>(sfPaths.Textures + "QuestionSmall.png");
            CacheStandInFromPath<TextAsset>(sfPaths.StandIns + "TextAsset.txt");
            CacheStandInFromPath<MonoScript>(sfPaths.PackageRoot + "FusionRoot.cs");
            CacheStandInFromPath<Shader>(sfPaths.PackageRoot + "Shaders/Missing.shader");
            CacheStandInFromPath<ComputeShader>(sfPaths.StandIns + "DoNothing.compute");
            CacheStandInFromPath<RayTracingShader>(sfPaths.StandIns + "RayTracingShader.raytrace");
            CacheStandInFromPath<LightingDataAsset>(sfPaths.StandIns + "LightingData.asset");
            CacheStandInFromPath<Cubemap>(sfPaths.StandIns + "TextureCube.png");
            CacheStandInFromPath<Texture2DArray>(sfPaths.StandIns + "TextureArray.png");
            CacheStandInFromPath<Texture3D>(sfPaths.StandIns + "Texture3D.png");
            CacheStandInFromPath<StyleSheet>(sfPaths.StandIns + "StyleSheet.uss");
            CacheStandInFromPath<ThemeStyleSheet>(sfPaths.StandIns + "ThemeStyleSheet.tss");
            CacheStandInFromPath<VisualTreeAsset>(sfPaths.StandIns + "VisualTreeAsset.uxml");
            CacheStandInFromPath<SceneAsset>(sfPaths.StandIns + "Scene.unity");
            CacheStandInFromPath<DefaultAsset>(ksPathUtils.Clean(sfPaths.StandIns));

            // Cache stand-in library asset overrides. These assets are only used when creating a stand-in library
            // asset with a specific guid.
            
            // We cannot use a font template to create the stand-in instance because Unity logs an error when you try
            // to clone the font, so we instead use a generator for the instance.
            CacheStandInFromPath<Font>(sfPaths.StandIns + "DummyText.ttf", true);
            // We use a different material for the instance because we wanted a Magenta material that looks like
            // Unity's error material for stand-in instances and I couldn't figure out how to create that in Blender,
            // and we need a material from an .fbx or some other file extension that isn't specific to Unity in order
            // to create a library asset.
            CacheStandInFromPath<Material>(sfPaths.StandIns + "Cube.fbx", true);
        }

        /// <summary>Attempt to cache a stand-in asset found at a known path location</summary>
        /// <param name="assetPath"></param>
        /// <param name="isLibraryAssetOverride">
        /// if true, the asset will be added to the stand-in template library
        /// overrides cache which are only used to create a stand-in library asset with a specific guid when we
        /// want to use a different asset than what we use for the stand-in instance.
        /// </param>
        private void CacheStandInFromPath<T>(string assetPath, bool isLibraryAssetOverride = false) where T : UObject
        {
            CacheStandInFromPath(assetPath, typeof(T), isLibraryAssetOverride);
        }

        /// <summary>Attempt to cache a stand-in asset found at a known path location</summary>
        /// <param name="assetPath"></param>
        /// <param name="type">type of asset to cache.</param>
        /// <param name="isLibraryAssetOverride">
        /// if true, the asset will be added to the stand-in template library
        /// overrides cache which are only used to create a stand-in library asset with a specific guid when we
        /// want to use a different asset than what we use for the stand-in instance.
        /// </param>
        private void CacheStandInFromPath(string assetPath, Type type, bool isLibraryAssetOverride = false)
        {
            if (type == null)
            {
                return;
            }
            UObject asset = AssetDatabase.LoadAssetAtPath(assetPath, type);
            if (asset != null)
            {
                if (isLibraryAssetOverride)
                {
                    m_standInTemplateLibraryOverrides[type] = asset;
                }
                else
                {
                    m_standInTemplates[type] = asset;
                }
                return;
            }
            ksLog.Warning("Unable to cache asset " + assetPath + " of type " + type);
        }

        /// <summary>Attempt to cache a stand-in asset found in the built-in assets</summary>
        /// <param name="assetName"></param>
        private void CacheStandInFromBuiltIn<T>(string assetName) where T : UObject
        {
            sfBuiltInAssetsLoader loader = sfBuiltInAssetsLoader.Get();
            T[] assets = loader.LoadBuiltInAssets<T>();

            foreach (T asset in assets)
            {
                if (asset.name == assetName)
                {
                    m_standInTemplates[typeof(T)] = asset;
                    return;
                }
            }

            ksLog.Warning("Unable to cache built-in asset " + assetName + " of type " + typeof(T).ToString());
        }

        /// <summary>Adds built-in assets to the cache.</summary>
        /// <param name="assets">assets to cache.</param>
        private void CacheBuiltIns<T>(T[] assets) where T : UObject
        {
            string className = typeof(T).ToString();
            foreach (T asset in assets)
            {
                if (asset != null)
                {
                    sfAssetInfo info = new sfAssetInfo(className, BUILT_IN_PREFIX + asset.name);
                    m_cache[info] = asset;
                    m_infoCache[asset] = info;
                }
            }
        }

        /// <summary>
        /// Finds and caches all uncached built-ins of the given type using Resources.FindObjectOfTypeAll, which is
        /// slow. There are some built-in assets that cannot be loaded the normal way, so we use this as a fallback when
        /// we cannot load a built-in asset.
        /// </summary>
        /// <param name="type">type of asset to cache built-ins for.</param>
        private void FindUncachedBuiltInsSlow(Type type)
        {
            string className = type.ToString();
            foreach (UObject asset in Resources.FindObjectsOfTypeAll(type))
            {
                if (!m_infoCache.ContainsKey(asset) && IsBuiltInAsset(asset))
                {
                    sfAssetInfo info = new sfAssetInfo(className, BUILT_IN_PREFIX + asset.name);
                    m_cache[info] = asset;
                    m_infoCache[asset] = info;
                }
            }
        }

        /// <summary>
        /// Called when assets are imported. Caches the assets and replaces missing asset references with the new
        /// assets. Syncs assets that are syncable.
        /// </summary>
        /// <param name="paths">paths to imported assets.</param>
        private void HandleImportAssets(string[] paths)
        {
            foreach (string path in paths)
            {
                UObject asset = CacheAssets(path);
                // Sync the asset if it can be synced and isn't already. When new synced assets are first imported,
                // in the sfObjectMap yet so we wait for delay call to check if the asset is already synced.
                EditorApplication.delayCall += () =>
                {
                    sfObjectEventDispatcher.Get().TryCreateSFObject(asset);
                };
            }
        }
    }
}
