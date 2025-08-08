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
using System.Linq;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using KS.Unity.Editor;
using KS.LZMA;

#if !UNITY_6000_0_OR_NEWER
using PhysicsMaterial = UnityEngine.PhysicMaterial;
using PhysicsMaterialCombine = UnityEngine.PhysicMaterialCombine;
#endif

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>
    /// Performs compile/build operations and writes game and scene JSON config files and geometry data.
    /// </summary>
    public class ksConfigWriter
    {
        /// <summary>Stores a collider with its serialized geometry data.</summary>
        private class GeometryData : IEquatable<GeometryData>
        {
            /// <summary>Geometry ID</summary>
            public int Id = 0;
            /// <summary>Collider</summary>
            public ksIUnityCollider Collider = null;
            /// <summary>Serialized geometry</summary>
            public byte[] Geometry = null;

            /// <summary>Checks if this geometry is the same as another.</summary>
            /// <param name="other">Geometry to compare to.</param>
            /// <returns>True if the geometries are the same.</returns>
            public bool Equals(GeometryData other)
            {
                return Collider.IsGeometryEqual(other.Collider);
            }
        }

        /// <summary>Callback called for a component before writing the component to the config.</summary>
        /// <param name="component">Component to write to the config.</param>
        private delegate void ScriptCallback(Component component);

        /// <summary>Scene build results.</summary>
        private enum SceneBuildResults
        {
            SUCCESS = 0,
            NO_ROOM_TYPE = 1,
            ERROR = 2
        }

        // Scene change tracking
        private static ksSceneTracker m_sceneTracker = null;

        // Geometry Tracking
        private static ushort GEOMETRY_FILE_VERSION = 2;
        private List<GeometryData> m_geometryCache = new List<GeometryData>();
        private List<GeometryData> m_instanceGeometryCache = new List<GeometryData>();

        // Script variable defaults
        private GameObject m_scriptDefaults;

        // Mappings
        private Dictionary<uint, ksJSON> m_assetJsonMap = new Dictionary<uint, ksJSON>();
        private Dictionary<string, List<KeyValuePair<uint, string>>> m_assetBundleMap =
            new Dictionary<string, List<KeyValuePair<uint, string>>>();
        private Dictionary<string, int> m_nameCounts = new Dictionary<string, int>();
        private Dictionary<string, string> m_typeToFullPath = new Dictionary<string, string>();
        private Dictionary<int, ksIUnityCollider> m_shapeMap = new Dictionary<int, ksIUnityCollider>();
        private int m_nextShapeId = 0;

        // Progress
        private bool m_cancelled = false;
        private string m_progressTitle = "";
        private string m_progressMessage = "";
        private float m_progress = 0.0f;


        /// <summary>Set the scriptable object that is used for tracking scene changes.</summary>
        public static ksSceneTracker SceneTracker
        {
            set
            {
                if (m_sceneTracker != null && m_sceneTracker != value)
                {
                    ScriptableObject.DestroyImmediate(m_sceneTracker);
                }
                m_sceneTracker = value;
            }
        }

        /// <summary>Get / Set pretty JSON config writing.</summary>
        public bool PrettyConfigs
        {
            get { return m_prettyConfigs; }
            set { m_prettyConfigs = value; }
        }
        private bool m_prettyConfigs = false;

        /// <summary>Data required for scene publishing API calls</summary>
        public ksJSON ScenePublishing
        {
            get { return m_scenePublishing; }
        }
        private ksJSON m_scenePublishing = null;

        /// <summary>Get the build summary.</summary>
        public string Summary
        {
            get { return m_summary; }
        }
        private string m_summary = "";

        /// <summary>Build the project.</summary>
        /// <param name="buildConfigs">Build server configs files when this parameter is true.</param>
        /// <param name="buildRuntime">Build the server runtime when this parameter is true.</param>
        /// <param name="rebuildAllConfigs">Rebuild all server config files when <paramref name="buildConfigs"/> is true.</param>
        /// <param name="runtimeConfig">Sets the runtime configuration to build when <paramref name="buildRuntime"/> is true.</param>
        /// <param name="regenProjectFiles">Regenerate the runtime solution and project files.</param>
        /// <returns>True if no errors were encountered.</returns>
        public bool Build(
            bool buildConfigs = true,
            bool buildRuntime = true,
            bool rebuildAllConfigs = true,
            ksServerScriptCompiler.Configurations runtimeConfig = ksServerScriptCompiler.Configurations.LOCAL_RELEASE,
            bool regenProjectFiles = false)
        {
            ksBuildEvents.InvokePreBuild();
            if (m_sceneTracker == null)
            {
                m_sceneTracker = ScriptableObject.CreateInstance<ksSceneTracker>();
            }

            m_scenePublishing = new ksJSON();
            try
            {
                ksConvexHull.ClearRejects();
                EditorSceneManager.SaveOpenScenes();

                // Create the output directory
                if (!Directory.Exists(ksPaths.ImageDir))
                {
                    ksPathUtils.Create(ksPaths.ImageDir, true);
                }

                // Build the runtime
                if (buildRuntime)
                {
                    UpdateProgressBar("KS Reactor - Building runtime", null, 0.1f);
                    if (!BuildRuntime(runtimeConfig, regenProjectFiles))
                    {
                        ksBuildEvents.InvokePostBuild(false);
                        return false;
                    }
                }

                // Build config files
                if (buildConfigs)
                {
                    UpdateProgressBar("KS Reactor - Writing common configs", null, 0.4f);
                    if (!BuildConfigs(rebuildAllConfigs))
                    {
                        ksBuildEvents.InvokePostBuild(false);
                        return false;
                    }
                }

                
                ksBuildEvents.InvokePostBuild(true);
                return true;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                ksAssetIdData.Get().RemoveMissingAssets();

                // Calling ExitGUI is needed to prevent an EndLayoutGroup error when publishing. This sometimes throws
                // an exception which we catch and ignore.
                try
                {
                    GUIUtility.ExitGUI();
                }
                catch (Exception)
                {

                }
            }
        }

        /// <summary>Rebuild the server runtime.</summary>
        /// <param name="configuration">Build configuration.</param>
        /// <param name="regenProjectFiles">Regenerate the runtime solution and project files.</param>
        /// <returns>True if no errors were encountered.</returns>
        private bool BuildRuntime(
            ksServerScriptCompiler.Configurations configuration,
            bool regenProjectFiles)
        {
            if (regenProjectFiles && !(ksPathUtils.Delete(ksPaths.ServerRuntimeProject) && ksPathUtils.Delete(ksPaths.ServerRuntimeSolution)))
            {
                AddSummaryMessage("Canceling build. Unable to delete KSServerRuntime project files");
                return false;
            }
            else
            {
                ksServerProjectUpdater.Instance.UpdateOutputPath();
            }
            // Rebuild the new runtime
            if (ksServerScriptCompiler.Instance.BuildServerRuntimeSync(true, configuration, !regenProjectFiles) ==
                ksServerScriptCompiler.CompileResults.SUCCESS)
            {
                AddSummaryMessage("Updated " + ksPaths.ImageDir + "KSServerRuntime.dll");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Build the Reactor config files.
        /// </summary>
        /// <param name="rebuildAllConfigs">Delete existing config files and rebuild all of them.</param>
        /// <returns>True if the build process completed without errors.</returns>
        private bool BuildConfigs(bool rebuildAllConfigs)
        {
            // Build the game config
            ksBuildEvents.InvokePreBuildConfig(new Scene());
            bool buildSuccess = WriteCommonConfig();
            ksBuildEvents.InvokePostBuildConfig(new Scene(), buildSuccess);
            if (!buildSuccess)
            {
                return false;
            }

            if (rebuildAllConfigs)
            {
                DeleteAllSceneConfigs();
                m_sceneTracker.Clear();
            }

            bool success = true;
            // Track the current hierarchy layout
            ksHierarchyPreserver hierarchyPreserver = new ksHierarchyPreserver();
            hierarchyPreserver.StoreHierarchy();

            // Write new scene configs
            List<string> scenePaths = m_sceneTracker.GetModifiedScenes();
            for (int i = 0; i < scenePaths.Count; i++)
            {
                string path = scenePaths[i];
                if (!ksBuildEvents.ValidateScenePath(path))
                {
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                UpdateProgressBar(
                    "KS Reactor - Writing scene configs (" + (i + 1) + " of " + scenePaths.Count + "): " + scene.name,
                    null, 0.6f
                );

                ksBuildEvents.InvokePreBuildConfig(scene);
                SceneBuildResults result = WriteSceneConfig(scene);
                ksBuildEvents.InvokePostBuildConfig(scene, result != SceneBuildResults.ERROR);
                if (result == SceneBuildResults.ERROR)
                {
                    success = false;
                    break;
                }
                m_sceneTracker.TrackScene(path, result == SceneBuildResults.SUCCESS);
            }
            // Restore the hierarchy layout.
            hierarchyPreserver.RestoreHierarchy();

            if (!success)
            {
                return false;
            }

            // Write geometry data file
            UpdateProgressBar("KS Reactor - Writing geometry data.", null, 0.8f);
            return WriteGeometryCache(ksPaths.GeometryFile, m_geometryCache, null);
        }

        /// <summary>Create and write a new common config.</summary>
        /// <returns>True if no errors were encountered.</returns>
        private bool WriteCommonConfig()
        {
            // Delete the common config
            if (!TryDeleteFile(ksPaths.GameConfig, "Unable to delete old common config"))
            {
                return false;
            }

            // Create the common config
            ksJSON commonConfig;
            try
            {
                commonConfig = CreateGameJson();
            }
            catch (Exception e)
            {
                ksLog.Error("Error creating game config.", e);
                if (m_scriptDefaults != null)
                {
                    GameObject.DestroyImmediate(m_scriptDefaults);
                }
                return false;
            }

            // Write the common config file
            try
            {
                using (StreamWriter writer = new StreamWriter(ksPaths.GameConfig))
                {
                    writer.Write(commonConfig.Print(m_prettyConfigs));
                }
                AddSummaryMessage("Updated " + ksPaths.GameConfig);
            }
            catch (Exception ex)
            {
                ksLog.Error("Unable to write common config '" + ksPaths.GameConfig + "'. ", ex);
                return false;
            }

            return WriteAssetBundleData();
        }

        /// <summary>Write asset bundle data to the asset map.</summary>
        /// <returns>True if no errors were encountered.</returns>
        private bool WriteAssetBundleData()
        {
            // Delete the asset bundle data
            ksPathUtils.Delete(ksPaths.AssetBundles, true);

            // Write the asset data for each asset bundle
            foreach (KeyValuePair<string, List<KeyValuePair<uint, string>>> assetBundleData in m_assetBundleMap)
            {
                try
                {
                    WriteAssetData(assetBundleData.Key, assetBundleData.Value);
                }
                catch (Exception ex)
                {
                    string message = "Unable to write asset data";
                    if (!string.IsNullOrEmpty(assetBundleData.Key))
                    {
                        message += " from asset bundle '" + assetBundleData.Key + "'";
                    }
                    message += ".";
                    ksLog.Error(message, ex);
                    return false;
                }
            }
            m_assetBundleMap.Clear();
            return true;
        }

        /// <summary>Create and write a new config for the active scene.</summary>
        /// <param name="scene">Scene to write config files for.</param>
        /// <returns>Result of the build.</returns>
        private SceneBuildResults WriteSceneConfig(Scene scene)
        {
            m_instanceGeometryCache.Clear();

            // Delete the current scene config
            UpdateProgressBar(null, "Deleting old configs");
            if (!TryDeleteFile(ksPaths.SceneConfigFile(scene.name), "Unable to delete old scene config"))
            {
                return SceneBuildResults.ERROR;
            }
            if (!TryDeleteFile(ksPaths.SceneGeometryFile(scene.name), "Unable to delete old scene geometry"))
            {
                return SceneBuildResults.ERROR;
            }

            // If the scene does not have any rooms, then return NO_ROOM_TYPE.
            ksRoomType[] rooms = GameObject.FindObjectsByType<ksRoomType>(FindObjectsSortMode.None);
            if (rooms == null || rooms.Length == 0)
            {
                return SceneBuildResults.NO_ROOM_TYPE;
            }

            // Create the scene config
            ksJSON sceneConfig;
            try
            {
                sceneConfig = CreateSceneJson();
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Error creating scene config.", e);
                if (m_scriptDefaults != null)
                {
                    GameObject.DestroyImmediate(m_scriptDefaults);
                }
                return SceneBuildResults.ERROR;
            }
            EditorSceneManager.SaveScene(scene);

            // Write the scene config file
            string sceneConfigFile = ksPaths.SceneConfigFile(scene.name);
            try
            {
                UpdateProgressBar(null, "Writing scene config '" + sceneConfigFile + "'");
                using (StreamWriter writer = new StreamWriter(sceneConfigFile))
                {
                    writer.Write(sceneConfig.Print(m_prettyConfigs));
                }
                AddSummaryMessage("Updated " + sceneConfigFile);
            }
            catch (Exception ex)
            {
                ksLog.Error("Unable to write scene config '" + sceneConfigFile + "'. ", ex);
                return SceneBuildResults.ERROR;
            }

            if (m_instanceGeometryCache.Count == 0)
            {
                return SceneBuildResults.SUCCESS;
            }
            // Write the scene geometry file
            return WriteGeometryCache(ksPaths.SceneGeometryFile(scene.name), m_instanceGeometryCache, scene.path) ?
                SceneBuildResults.SUCCESS : SceneBuildResults.ERROR;
        }

        /// <summary>Delete all scene configs.</summary>
        private void DeleteAllSceneConfigs()
        {
            try
            {
                DirectoryInfo dirInfo = new DirectoryInfo(ksPaths.ImageDir);
                foreach (FileInfo fileInfo in dirInfo.GetFiles("config.*.json"))
                {
                    fileInfo.Delete();
                }
            }
            catch (Exception ex)
            {
                ksLog.Error("Unable to delete scene configs. ", ex);
            }
        }

        /// <summary>
        /// Try to delete a file if it exists. If an exception is thrown then compose an error message
        /// from the exception message provided, the filepath, and the exception.
        /// </summary>
        /// <param name="filepath">Path to try delete.</param>
        /// <param name="exMsg">Exception message to log if an error occurs.</param>
        /// <returns>True if the file was deleted or did not exist.</returns>
        private bool TryDeleteFile(string filepath, string exMsg)
        {
            if (File.Exists(filepath))
            {
                try
                {
                    File.Delete(filepath);
                    return true;
                }
                catch (Exception ex)
                {
                    ksLog.Error(exMsg + " '" + filepath + "'. ", ex);
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Writes asset ID to asset path mapping for entities and script assets in an assetbundle or resources.
        /// </summary>
        /// <param name="assetBundle">
        /// Asset bundle to write for. Null or empty string if the assets are from resources instead of an asset
        /// bundle.
        /// </param>
        /// <param name="assetList">List of asset IDs and asset paths to write.</param>
        private void WriteAssetData(string assetBundle, List<KeyValuePair<uint, string>> assetList)
        {
            string path = ksPaths.GetAssetDataPath(assetBundle);
            ksPathUtils.Create(path);
            using (StreamWriter writer = new StreamWriter(path))
            {
                foreach (KeyValuePair<uint, string> pair in assetList)
                {
                    writer.WriteLine(pair.Key + "|" + pair.Value);
                }
            }
            AddSummaryMessage("Updated " + path);
            AssetDatabase.Refresh();
            if (!string.IsNullOrEmpty(assetBundle))
            {
                AssetImporter importer = AssetImporter.GetAtPath(path);
                if (importer != null)
                {
                    int index = assetBundle.IndexOf(".");
                    if (index >= 0)
                    {
                        importer.assetBundleName = assetBundle.Substring(0, index);
                        importer.assetBundleVariant = assetBundle.Substring(index + 1);
                    }
                    else
                    {
                        importer.assetBundleName = assetBundle;
                    }
                }
                else
                {
                    ksLog.Warning(this, "Could not get asset importer for '" + path + 
                        ". You'll need to manually assign it to asset bundle " + assetBundle);
                }
            }
        }

        /// Creates a JSON config for a physic material.
        /// <param name="material">Physics material</param>
        /// <returns>JSON physics material config.</returns>
        private ksJSON CreateMaterial(PhysicsMaterial material)
        {
            ksJSON jsonMaterial = new ksJSON();
            jsonMaterial["StaticFriction"] = material.staticFriction;
            jsonMaterial["DynamicFriction"] = material.dynamicFriction;
            jsonMaterial["Restitution"] = material.bounciness;
            jsonMaterial["FrictionCombineMode"] = ConvertCombineMode(material.frictionCombine);
            jsonMaterial["RestitutionCombineMode"] = ConvertCombineMode(material.bounceCombine);
            return jsonMaterial;
        }

        /// <summary>
        /// Unity uses different combine mode enum values than we do (we match PhysX's enum). This converts Unity's
        /// enum to the values we use.
        /// </summary>
        /// <param name="mode">Combine mode</param>
        /// <returns>int value that matches our combine mode enum.</returns>
        private int ConvertCombineMode(PhysicsMaterialCombine mode)
        {
            switch (mode)
            {
                default:
                case PhysicsMaterialCombine.Average:
                    return 0;
                case PhysicsMaterialCombine.Minimum:
                    return 1;
                case PhysicsMaterialCombine.Multiply:
                    return 2;
                case PhysicsMaterialCombine.Maximum:
                    return 3;
            }
        }

        /// <summary>Creates a JSON config for an asset.</summary>
        /// <param name="asset">Asset</param>
        /// <returns>JSON asset.</returns>
        private ksJSON CreateAsset(UnityEngine.Object asset)
        {
            ksProxyScriptAsset script = asset as ksProxyScriptAsset;
            if (script != null)
            {
                return CreateScriptAsset(script);
            }
            ksJSON jsonAsset = new ksJSON();
            jsonAsset["asset id"] = ksAssetIdData.Get().GetId(asset);
            PhysicsMaterial material = asset as PhysicsMaterial;
            if (material != null)
            {
                jsonAsset["name"] = ".ksPhysicsMaterial";
                jsonAsset["fields"] = CreateMaterial(material);
            }
            return jsonAsset;
        }

        /// <summary>Creates JSON describing a script asset.</summary>
        /// <param name="script">Script to create json for.</param>
        /// <returns>JSON script asset.</returns>
        private ksJSON CreateScriptAsset(ksProxyScriptAsset script)
        {
            ksJSON jsonScript = new ksJSON();
            jsonScript["asset id"] = script.AssetId;

            string name;
            if (script is ksCollisionFilterAsset)
            {
                name = "/ksCollisionFilter";
            }
            else
            {
                name = script.GetType().ToString().Substring("KSProxies.Scripts.".Length);
            }
            jsonScript["name"] = name;

            ksJSON fields = new ksJSON();
            SerializedObject serialized = new SerializedObject(script);
            SerializedProperty iter = serialized.GetIterator();
            bool enterChildren = true;
            while (iter.NextVisible(enterChildren))
            {
                enterChildren = false;
                // Store the name because creating the field may advance the iterator.
                name = iter.name;
                ksJSON field = CreateField(iter);
                if (field != null)
                {
                    fields[name] = field;
                }
            }
            if (fields.Count > 0)
            {
                jsonScript["fields"] = fields;
            }
            return jsonScript;
        }

        /// <summary>
        /// Creates an asset reference as json. The json will be an object with an asset field containing the id of the
        /// asset, or json null if the object is null or not an asset.
        /// </summary>
        /// <param name="asset">Asset to reference.</param>
        /// <returns>Json asset reference.</returns>
        private ksJSON CreateAssetReference(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return new ksJSON();
            }
            ksProxyScriptAsset script = asset as ksProxyScriptAsset;
            uint assetId = script != null ? script.AssetId : ksAssetIdData.Get().GetId(asset);
            if (assetId == 0)
            {
                return new ksJSON();
            }
            ksJSON json = new ksJSON();
            json["asset"] = assetId;
            if (asset is ksPlayerControllerAsset && !ksEditorUtils.IsInResourcesOrAssetBundle(asset))
            {
                ksLog.Warning(this, "Player controller asset '" + AssetDatabase.GetAssetPath(asset) +
                    "' cannot be used because it is not in Resources or an asset bundle.");
            }
            return json;
        }

        /// <summary>
        /// Gets the name to use for an asset in the config. Removes "Assets/" from the start of the path and removes
        /// the file extension. Returns null if the asset is null or has no asset path.
        /// </summary>
        /// <param name="asset">Asset to get name for.</param>
        /// <returns>Name to use for the asset.</returns>
        private string GetAssetName(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return null;
            }
            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }
            path = path.Replace('\\', '/');
            string assetBundle = AssetDatabase.GetImplicitAssetBundleName(path);
            int index = path.LastIndexOf(".");
            int start = string.IsNullOrEmpty(assetBundle) ? "Assets/".Length : path.LastIndexOf('/');
            path = index >= 0 ? path.Substring(start, index - start) : path.Substring(start);
            return string.IsNullOrEmpty(assetBundle) ? path : assetBundle + ":" + path;
        }

        /// <summary>Iterate through game objects in the hierarchy and creates the scene config.</summary>
        /// <returns>JSON scene config</returns>
        public ksJSON CreateSceneJson()
        {
            m_nextShapeId = -1;
            m_scriptDefaults = new GameObject();
            ksJSON jsonScene = new ksJSON(ksJSON.Types.OBJECT);
            ksJSON jsonRooms = new ksJSON();
            ksJSON jsonEntities = new ksJSON();
            ksJSON jsonShapes = new ksJSON();
            List<GameObject> entityObjects = new List<GameObject>();
            List<GameObject> roomObjects = new List<GameObject>();

            GameObject[] instanceList = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (GameObject instance in instanceList)
            {
                if (m_cancelled)
                {
                    GameObject.DestroyImmediate(m_scriptDefaults);
                    return null;
                }

                // Store room objects to be processed in the second pass after entity ids are assigned so references to
                // entities work properly.
                if (instance.GetComponent<ksRoomType>() != null)
                {
                    roomObjects.Add(instance);
                    continue;
                }

                // Only objects with ksEntityComponents are written
                ksEntityComponent entity = GetSingleComponent<ksEntityComponent>(instance);
                if (entity == null)
                {
                    continue;
                }

                GameObject prefab = GetConfigPrefab(instance);
                ksEntityComponent prefabEntity = prefab == null ? null : prefab.GetComponent<ksEntityComponent>();
                if (prefabEntity != null && !m_assetJsonMap.ContainsKey(prefabEntity.AssetId))
                {
                    prefabEntity = null;
                }
                if (prefabEntity == null)
                {
                    prefab = null;
                    entity.AssetId = 0;
                }
                else
                {
                    prefabEntity.IsStatic = prefabEntity.GetComponent<Rigidbody>() == null;
                    entity.Type = prefabEntity.Type;
                    entity.AssetId = prefabEntity.AssetId;
                }
                entity.IsStatic = entity.GetComponent<Rigidbody>() == null;

                // Do not write entities to the config that are auto-spawned per player.
                ksAutoSpawn autoSpawn = entity.GetComponent<ksAutoSpawn>();
                if (autoSpawn != null && autoSpawn.SpawnOwned && autoSpawn.enabled)
                {
                    entity.EntityId = 0;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(entity);
                    EditorSceneManager.MarkSceneDirty(entity.gameObject.scene);
                    continue;
                }

                ksJSON jsonInstance = new ksJSON(ksJSON.Types.OBJECT);
                if (entity.AssetId != 0)
                {
                    jsonInstance["asset id"] = entity.AssetId;
                }

                // Transform
                if (entity.transform.position != ksVector3.Zero)
                {
                    jsonInstance["position"] = entity.transform.position;
                }
                if (entity.transform.rotation != Quaternion.identity)
                {
                    jsonInstance["rotation"] = entity.transform.rotation;
                }
                Vector3 defaultScale = prefabEntity == null ? Vector3.one : prefabEntity.transform.lossyScale;
                if (entity.transform.lossyScale != defaultScale)
                {
                    jsonInstance["scale"] = entity.transform.lossyScale;
                }

                // Permanence
                bool prefabPermanent = prefabEntity == null ? false : prefabEntity.IsPermanent;
                if (entity.IsPermanent != prefabPermanent)
                {
                    jsonInstance["is permanent"] = entity.IsPermanent ? 1 : 0;
                }

                bool prefabDestroyOwnerDC = prefabEntity == null ? true : prefabEntity.DestroyOnOwnerDisconnect;
                if (entity.DestroyOnOwnerDisconnect != prefabDestroyOwnerDC)
                {
                    jsonInstance["destroy on owner disconnect"] = entity.DestroyOnOwnerDisconnect;
                }

                if ((prefabEntity == null && entity.SyncGroup != 0) ||
                    (prefabEntity != null && entity.SyncGroup != prefabEntity.SyncGroup))
                {
                    jsonInstance["sync group"] = Math.Min(entity.SyncGroup, ksFixedDataWriter.ENCODE_4BYTE);
                }

                // Collision filter
                if ((prefabEntity == null && entity.CollisionFilter != null) ||
                    (prefabEntity != null && entity.CollisionFilter != prefabEntity.CollisionFilter))
                {
                    jsonInstance["collision filter"] = CreateAssetReference(entity.CollisionFilter);
                }

                // Physics material
                if ((prefabEntity == null && entity.PhysicsMaterial != null) || 
                    (prefabEntity != null && entity.PhysicsMaterial != prefabEntity.PhysicsMaterial))
                {
                    jsonInstance["physics material"] = CreateAssetReference(entity.PhysicsMaterial);
                }

                // Compression level
                ksJSON jsonPrecision = null;
                UpdateProgressBar(null, "Writing compression level configs for " + entity.name);
                jsonPrecision = CreateCompressionLevel(entity.TransformPrecisionOverrides, prefabEntity == null ? null : prefabEntity.TransformPrecisionOverrides);
                if (jsonPrecision != null)
                {
                    jsonInstance["transform precision"] = jsonPrecision;
                }

                entityObjects.Add(instance);
                entityObjects.Add(prefab);
                jsonEntities.Add(jsonInstance);
                entity.EntityId = (uint)jsonEntities.Count;
                PrefabUtility.RecordPrefabInstancePropertyModifications(entity);
                EditorSceneManager.MarkSceneDirty(entity.gameObject.scene);
            }

            // Use a callback to check if any physics scripts were written to the config for an entity instance.
            bool foundPhysicsScript = false;
            ScriptCallback callback = (Component script) =>
            {
                foundPhysicsScript = foundPhysicsScript || IsPhysicsComponent(script);
            };

            // Serialize scripts in second pass, after entity IDs are assigned so we can serialize references
            for (int i = 0; i < entityObjects.Count; i += 2)
            {
                GameObject instance = entityObjects[i];
                GameObject prefab = entityObjects[i + 1];
                ksJSON jsonScripts = CreateScripts<ksProxyEntityScript>(instance, prefab, true, callback);
                if (jsonScripts != null)
                {
                    ksJSON json = jsonEntities[i / 2];
                    json["scripts"] = jsonScripts;
                }
            }
            // Serialize room types
            foreach (GameObject instance in roomObjects)
            {
                CreateRoomType(jsonRooms, instance);
                if (foundPhysicsScript && instance.GetComponent<ksPhysicsSettings>() == null)
                {
                    ksLog.Warning(this, "Room type '" + instance.name + "' in scene '" + instance.scene.name +
                        "' has no physics settings but physics components were found on scene entities. Physics " +
                        "components cannot be used without adding a ksPhysicsSettings component to the room type " +
                        "object.");
                }
            }

            if (jsonRooms.Count > 0)
            {
                jsonScene["rooms"] = jsonRooms;
            }
            if (jsonEntities.Count > 0)
            {
                jsonScene["entities"] = jsonEntities;
            }
            if (jsonShapes.Count > 0)
            {
                jsonScene["shapes"] = jsonShapes;
            }
            GameObject.DestroyImmediate(m_scriptDefaults);
            return jsonScene;
        }

        /// <summary>
        /// Creates a JSON config describing a <see cref="ksRoomType"/> and adds it to <paramref name="jsonSettings"/>.
        /// </summary>
        /// <param name="jsonSettings">JSON config to add the room type configs to.</param>
        /// <param name="gameObject">Game object to create JSON from.</param>
        /// <returns>
        /// False if a JSON was not created because <paramref name="gameObject"/> did not have a <see cref="ksRoomType"/>.
        /// </returns>
        private bool CreateRoomType(ksJSON jsonSettings, GameObject gameObject)
        {
            UpdateProgressBar(null, " Writing RoomType config for " + gameObject.name);
            ksRoomType roomType = GetSingleComponent<ksRoomType>(gameObject);
            if (roomType == null)
            {
                return false;
            }

            // Track resource requirements for publishing
            if (!PrefabUtility.IsPartOfPrefabAsset(gameObject))
            {
                ksJSON resources = new ksJSON();
                resources["required core count"] = roomType.RequiredCoreCount;
                resources["required memory"] = roomType.RequiredMemory;
                if (!m_scenePublishing.HasField(gameObject.scene.name))
                {
                    m_scenePublishing[gameObject.scene.name] = new ksJSON();
                }
                m_scenePublishing[gameObject.scene.name][gameObject.name] = resources;
            }

            GameObject prefab = GetConfigPrefab(gameObject);
            ksRoomType prefabType = prefab == null ? null : prefab.GetComponent<ksRoomType>();
            if (prefabType == null)
            {
                prefab = null;
            }
            if (prefab != null)
            {
                string prefabPath = AssetDatabase.GetAssetPath(prefab);
                if (!prefabPath.Contains("Resources"))
                {
                    prefab = null;
                    prefabType = null;
                }
            }
            ksJSON json = new ksJSON(ksJSON.Types.OBJECT);
            if (prefabType != null)
            {
                json["prefab"] = prefab.name;
            }
            if (prefabType == null || prefabType.PlayersVisible != roomType.PlayersVisible)
            {
                json["players visible"] = roomType.PlayersVisible;
            }
            if (prefabType == null || prefabType.PlayerLimit != roomType.PlayerLimit)
            {
                json["player limit"] = (roomType.PlayerLimit > 0) ? roomType.PlayerLimit : 0;
            }
            if (prefabType == null || prefabType.ServerFrameRate != roomType.ServerFrameRate)
            {
                json["fps"] = roomType.ServerFrameRate;
            }
            if (prefabType == null || prefabType.ServerFramesPerSync != roomType.ServerFramesPerSync)
            {
                json["frames per sync"] = roomType.ServerFramesPerSync;
            }
            if (prefabType == null || prefabType.RecoverUpdateTime != roomType.RecoverUpdateTime)
            {
                json["recover update time"] = roomType.RecoverUpdateTime;
            }
            if (prefabType == null || prefabType.AllowPlayerSpawning != roomType.AllowPlayerSpawning)
            {
                json["allow player spawning"] = roomType.AllowPlayerSpawning;
            }

            ksJSON jsonPrecision = CreateCompressionLevel(roomType.TransformPrecisionOverrides, prefabType == null ? null : prefabType.TransformPrecisionOverrides);
            if (jsonPrecision != null)
            {
                json["transform precision"] = jsonPrecision;
            }

            // Cluster
            if (prefabType == null || prefabType.ClusterReconnectDelay != roomType.ClusterReconnectDelay)
            {
                json["cluster reconnect delay"] = (int)roomType.ClusterReconnectDelay;
            }
            if (prefabType == null || prefabType.ClusterReconnectAttempts != roomType.ClusterReconnectAttempts)
            {
                json["cluster reconnect attempts"] = roomType.ClusterReconnectAttempts;
            }

            // Threading
            if (prefabType == null || prefabType.PhysicsThreads != roomType.PhysicsThreads)
            {
                json["physics threads"] = roomType.PhysicsThreads;
            }
            if (prefabType == null || prefabType.NetworkThreads != roomType.NetworkThreads)
            {
                json["network threads"] = roomType.NetworkThreads;
            }
            if (prefabType == null || prefabType.EncodingThreads != roomType.EncodingThreads)
            {
                json["encoding threads"] = roomType.EncodingThreads;
            }
            if (prefabType == null || prefabType.MaxConcurrentAuthentications != roomType.MaxConcurrentAuthentications)
            {
                json["max concurrent authentications"] = roomType.MaxConcurrentAuthentications;
            }
            if (prefabType == null || prefabType.ParallelControllerUpdates != roomType.ParallelControllerUpdates)
            {
                json["parallel controllers"] = roomType.ParallelControllerUpdates;
            }
            if (prefabType == null || prefabType.ParallelOwnedEntityUpdates != roomType.ParallelOwnedEntityUpdates)
            {
                json["parallel owned entity updates"] = roomType.ParallelOwnedEntityUpdates;
            }

            CreatePhysicsConfig(json, gameObject);

            ksJSON jsonScripts = CreateScripts<ksProxyRoomScript>(gameObject, prefab);
            ksJSON jsonPlayerScripts = CreateScripts<ksProxyPlayerScript>(gameObject, prefab);
            if (jsonScripts != null)
            {
                json["scripts"] = jsonScripts;
            }
            if (jsonPlayerScripts != null)
            {
                json["player scripts"] = jsonPlayerScripts;
            }

            // Replace invalid characters in room type name with underscores
            string name = gameObject.name;
            if (name.Contains('"') || name.Contains('\\'))
            {
                name = name.Replace('"', '_').Replace('\\', '_');
                ksLog.Warning(this, "Found room type with invalid name " + gameObject.name + ". Renaming to " + name);
            }

            if (jsonSettings.HasField(name))
            {
                ksLog.Warning(this, "Multiple room types found with the name " + name +
                    ". The first will be overwritten.");
            }
            jsonSettings[name] = json;

            if (gameObject.GetComponent<ksEntityComponent>() != null)
            {
                ksLog.Warning(this,
                    "ksRoomType and ksEntityComponent found on the same GameObject. Ignoring the ksEntityComponent.",
                    gameObject);
            }
            return true;
        }

        /// <summary>Create and add physics configuration values to the room config.</summary>
        /// <param name="roomJson">Room configuration JSON</param>
        /// <param name="gameObject">Room object</param>
        private void CreatePhysicsConfig(ksJSON roomJson, GameObject gameObject)
        {
            ksPhysicsSettings physics = GetSingleComponent<ksPhysicsSettings>(gameObject);
            if (physics == null)
            {
                return;
            }

            GameObject prefab = GetConfigPrefab(gameObject);
            ksPhysicsSettings prefabPhysics = prefab == null ? null : prefab.GetComponent<ksPhysicsSettings>();
            if (prefabPhysics == null)
            {
                prefab = null;
            }
            if (prefab != null)
            {
                string prefabPath = AssetDatabase.GetAssetPath(prefab);
                if (!prefabPath.Contains("Resources"))
                {
                    prefab = null;
                    prefabPhysics = null;
                }
            }
            ksJSON json = new ksJSON();
            roomJson["physics"] = json;

            // Scene settings
            if (prefabPhysics == null || prefabPhysics.Gravity != physics.Gravity)
            {
                json["gravity"] = physics.Gravity;
            }
            if (prefabPhysics == null || prefabPhysics.BounceThreshold != physics.BounceThreshold)
            {
                json["bounce threshold"] = physics.BounceThreshold;
            }
            if (prefabPhysics == null || prefabPhysics.QueryHitBackFaces != physics.QueryHitBackFaces)
            {
                json["query hit back faces"] = physics.QueryHitBackFaces;
            }
            if (prefabPhysics == null || prefabPhysics.EnableAdaptiveForce != physics.EnableAdaptiveForce)
            {
                json["enable adaptive force"] = physics.EnableAdaptiveForce;
            }
            if (prefabPhysics == null || prefabPhysics.EnableEnhancedDeterminism != physics.EnableEnhancedDeterminism)
            {
                json["enable enhanced determinism"] = physics.EnableEnhancedDeterminism;
            }
            if (prefabPhysics == null || prefabPhysics.AutoSync != physics.AutoSync)
            {
                json["auto sync"] = physics.AutoSync;
            }
            if (prefabPhysics == null || prefabPhysics.SolverType != physics.SolverType)
            {
                json["solver type"] = (int)physics.SolverType;
            }
            if (prefabPhysics == null || prefabPhysics.DefaultCollisionFilter != physics.DefaultCollisionFilter)
            {
                json["collision filter"] = CreateAssetReference(physics.DefaultCollisionFilter);
            }
            if (prefabPhysics == null || prefabPhysics.DefaultMaterial != physics.DefaultMaterial)
            {
                json["material"] = CreateAssetReference(physics.DefaultMaterial);
            }

            ksJSON jsonEntityPhysicsSettings = CreateEntityPhysicsSettings(
                physics.DefaultEntityPhysics, 
                prefabPhysics == null ? null : prefabPhysics.DefaultEntityPhysics
            );
            if (jsonEntityPhysicsSettings != null)
            {
                json["entity"] = jsonEntityPhysicsSettings;
            }

            // debugging
            if (prefabPhysics == null || prefabPhysics.UsePhysXVisualDebugger != physics.UsePhysXVisualDebugger)
            {
                if (physics.UsePhysXVisualDebugger)
                {
                    json["PVD Address"] = physics.PVDAddress;
                    json["PVD Port"] = (int)physics.PVDPort;
                }
                else
                {
                    json["PVD Address"] = "";
                    json["PVD Port"] = 0;
                }
            }
            return;
        }

        /// <summary>Creates a JSON config for an entity collection.</summary> 
        /// <param name="type">Entity type name for the collection.</param>
        /// <param name="prefab">Prefab to create collection from.</param>
        /// <param name="usedIds">Set of asset IDs already in use.</param>
        /// <param name="nextId">Asset ID to use for next entity without an asset ID.</param>
        /// <returns>JSON config for a collection of entities.</returns>
        private ksJSON CreateEntityCollection(
            string type,
            GameObject prefab,
            HashSet<uint> usedIds,
            ref uint nextId)
        {
            int index = type.IndexOf(":");
            string assetBundle = "";
            if (index >= 0)
            {
                assetBundle = type.Substring(0, index);
                // Remove asset bundle from type
                type = type.Substring(index + 1);
            }
            List<KeyValuePair<uint, string>> assetList;
            if (!m_assetBundleMap.TryGetValue(assetBundle, out assetList))
            {
                assetList = new List<KeyValuePair<uint, string>>();
                m_assetBundleMap[assetBundle] = assetList;
            }

            ksJSON json = new ksJSON(ksJSON.Types.ARRAY);
            Vector3 position = prefab.transform.position;
            Quaternion rotation = prefab.transform.rotation;
            prefab.transform.position = Vector3.zero;
            prefab.transform.rotation = Quaternion.identity;
            CreateEntityCollection(type, prefab, json, assetList, usedIds, ref nextId);
            prefab.transform.position = position;
            prefab.transform.rotation = rotation;
            return json;
        }

        /// <summary>Creates a JSON config for an entity collection.</summary>
        /// <param name="type">Entity type name for the collection, without the asset bundle part.</param>
        /// <param name="prefab">Prefab to create collection from.</param>
        /// <param name="jsonCollection">JSON config to add entity configs to.</param>
        /// <param name="assetList">List to add asset IDs and types to.</param>
        /// <param name="usedIds">Set of asset ids already in use.</param>
        /// <param name="nextId">Asset ID to use for next entity without an asset ID.</param>
        private void CreateEntityCollection(
            string type,
            GameObject prefab,
            ksJSON jsonCollection,
            List<KeyValuePair<uint, string>> assetList,
            HashSet<uint> usedIds,
            ref uint nextId)
        {
            ksEntityComponent entity = GetSingleComponent<ksEntityComponent>(prefab);
            if (entity != null && entity.enabled)
            {
                jsonCollection.Add(CreateEntityType(type, entity, assetList, usedIds, ref nextId));
            }
            foreach (Transform child in prefab.transform)
            {
                if (child.gameObject.activeSelf)
                {
                    CreateEntityCollection(type, child.gameObject, jsonCollection, assetList, usedIds, ref nextId);
                }
            }
        }

        /// <summary>Creates a JSON config for a single type of entity.</summary>
        /// <param name="type">Type of entity.</param>
        /// <param name="entity">Entity component</param>
        /// <param name="assetList">List to add asset IDs and types to.</param>
        /// <param name="usedIds">Set of asset ids already in use.</param>
        /// <param name="nextId">Asset ID to use for next entity without an asset ID.</param>
        /// <returns>JSON config for a entity type.</returns>
        private ksJSON CreateEntityType(
            string type,
            ksEntityComponent entity,
            List<KeyValuePair<uint, string>> assetList,
            HashSet<uint> usedIds,
            ref uint nextId)
        {
            entity.Type = type;
            ksJSON json = new ksJSON();
            if (entity.AssetId == 0)
            {
                // This is a new entity without an asset ID. Generate a new unique asset ID.
                while (usedIds.Contains(nextId))
                {
                    nextId++;
                }
                entity.AssetId = nextId;
                nextId++;
                EditorUtility.SetDirty(entity.gameObject);
            }
            json["asset id"] = entity.AssetId;
            m_assetJsonMap[entity.AssetId] = json;
            assetList.Add(new KeyValuePair<uint, string>(entity.AssetId, type));

            if (entity.transform.position != Vector3.zero)
            {
                json["position"] = entity.transform.position;
            }
            if (entity.transform.rotation != Quaternion.identity)
            {
                json["rotation"] = entity.transform.rotation;
            }
            if (entity.transform.lossyScale != Vector3.one)
            {
                json["scale"] = entity.transform.lossyScale;
            }

            entity.IsStatic = entity.GetComponent<Rigidbody>() == null;
            if (entity.IsPermanent)
            {
                json["is permanent"] = 1;
            }

            if (!entity.DestroyOnOwnerDisconnect)
            {
                json["destroy on owner disconnect"] = false;
            }

            if (entity.SyncGroup != 0)
            {
                json["sync group"] = Math.Min(entity.SyncGroup, ksFixedDataWriter.ENCODE_4BYTE);
            }

            if (entity.CollisionFilter != null)
            {
                json["collision filter"] = CreateAssetReference(entity.CollisionFilter);
            }

            if (entity.PhysicsMaterial != null)
            {
                json["physics material"] = CreateAssetReference(entity.PhysicsMaterial);
            }

            UpdateProgressBar(null, "Writing compression level configs for " + entity.name);
            ksJSON jsonPrecision = CreateCompressionLevel(entity.TransformPrecisionOverrides);
            if (jsonPrecision != null)
            {
                json["transform precision"] = jsonPrecision;
            }

            ksJSON jsonScripts = CreateScripts<ksProxyEntityScript>(entity.gameObject);
            if (jsonScripts != null)
            {
                json["scripts"] = jsonScripts;
            }

            if (EditorUtility.IsDirty(entity.gameObject))
            {
                // Due to a Unity bug, marking a prefab dirty will lose the changes if the prefab has never been
                // inspected, so we have to force a save.
                PrefabUtility.SavePrefabAsset(entity.gameObject.transform.root.gameObject);
            }
            return json;
        }

        /// <summary>Gets a default instance of a script.</summary>
        /// <param name="type">Type of script to get.</param>
        /// <returns>Default script instance</returns>
        private object GetScript(Type type)
        {
            UnityEngine.Object script = m_scriptDefaults.GetComponent(type);
            if (script == null)
            {
                script = m_scriptDefaults.AddComponent(type);
            }
            return script;
        }


        /// <summary>
        /// Check if a component can be serialized and returns the name of the script for that component.
        /// </summary>
        /// <param name="gameObject">Game object containing the component.</param>
        /// <param name="component">Component to check.</param>
        /// <returns>
        /// Name of the server script which the component matches. 
        /// Null if the script is not supported by the server.
        /// Returns an empty string if the component is disabled.
        /// Internal scripts are prefixed with a period and will be found in the namespace "KS.Reactor.Server"
        /// </returns>
        private string GetScriptName<ProxyScript>(GameObject gameObject, Component component) where ProxyScript : ksProxyScript
        {
            // Is this an appropriate script type
            ProxyScript script = component as ProxyScript;
            if (script != null)
            {
                return script.enabled ? script.GetType().ToString().Substring("KSProxies.Scripts.".Length) : "";
            }

            // Does the game object have an entity component
            ksEntityComponent entityComponent = gameObject.GetComponent<ksEntityComponent>();
            if (entityComponent != null)
            {
                if (!entityComponent.enabled)
                {
                    return null;
                }

                Rigidbody rigidbody = component as Rigidbody;
                if (rigidbody != null)
                {
                    if (entityComponent.RigidBodyExistence == ksExistenceModes.CLIENT_ONLY)
                    {
                        return "";
                    }
                    return ".ksBaseRigidBody";
                }

                // Check Unity collider types
                Collider collider = component as Collider;
                if (collider != null)
                {
                    // Ignore client-only colliders.
                    ksColliderData colliderData = entityComponent.GetColliderData(collider);
                    if (colliderData == null || colliderData.Existence == ksExistenceModes.CLIENT_ONLY)
                    {
                        return "";
                    }
                    if (component is SphereCollider) return ".ksSphereCollider";
                    if (component is BoxCollider) return ".ksBoxCollider";
                    if (component is CapsuleCollider) return ".ksCapsuleCollider";
                    if (component is MeshCollider)
                    {
                        return (component as MeshCollider).convex ? ".ksConvexMeshCollider" : ".ksTriangleMeshCollider";
                    }
                    if (component is TerrainCollider) return ".ksHeightFieldCollider";
                    if (component is CharacterController) return ".ksCharacterController";
                    return null;
                }

                // Check custom KS collider types and return them as convex meshes
                ksCylinderCollider cylinderCollider = component as ksCylinderCollider;
                if (cylinderCollider != null)
                {
                    if (cylinderCollider.Existance == ksExistenceModes.CLIENT_ONLY)
                    {
                        return "";
                    }
                    return ".ksConvexMeshCollider";
                }

                ksConeCollider coneCollider = component as ksConeCollider;
                if (coneCollider != null)
                {
                    if (coneCollider.Existance == ksExistenceModes.CLIENT_ONLY)
                    {
                        return "";
                    }
                    return ".ksConvexMeshCollider";
                }

                // Check joints
                Joint joint = component as Joint;
                if (joint != null) 
                {
#if UNITY_2020_2_OR_NEWER
                    if (joint.connectedArticulationBody != null)
                    {
                        // Reactor does not support articulation bodies.
                        return null;
                    }
#endif
                    if (joint.connectedBody != null)
                    {
                        ksEntityComponent connectedEntity = joint.connectedBody.GetComponent<ksEntityComponent>();
                        if (connectedEntity == null || !connectedEntity.enabled)
                        {
                            // Do not publish joints which are connected to non-entities
                            return null;
                        }
                    }

                    if (joint is FixedJoint) return ".ksFixedJoint";
                    //if (joint is HingeJoint) return ".ksRevoluteJoint";
                    //if (joint is SpringJoint) return ".ksDistanceJoint";
                    //if (joint is ConfigurableJoint) return ".ksD6Joint";
                    //if (joint is CharacterJoint) return ".ksSphericalJoint";
                }
            }
            return null;
        }

        /// <summary>
        /// Creates a JSON config describing all <see cref="ksProxyScript"/> instances of type
        /// <typeparamref name="ProxyScript"/> on a game object.
        /// </summary>
        /// <typeparam name="ProxyScript">Proxy script</typeparam>
        /// <param name="gameObject">Game object to with <typeparamref name="ProxyScript"/> scripts.</param>
        /// <param name="prefab">
        /// Prefab with default script values. Null if <paramref name="gameObject"/> is not a prefab instance.
        /// </param>
        /// <param name="isInstance">Is the gameobject an instance in the scene.</param>
        /// <param name="callback">Callback to call before writing a script to the config.</param>
        /// <returns>JSON config describing the <typeparamref name="ProxyScript"/> instances.</returns>
        private ksJSON CreateScripts<ProxyScript>(
            GameObject gameObject,
            GameObject prefab = null,
            bool isInstance = false,
            ScriptCallback callback = null) where ProxyScript : ksProxyScript
        {
            ksJSON json = new ksJSON(ksJSON.Types.ARRAY);
            Component[] components = gameObject.GetComponents<Component>();
            Component[] prefabComponents = prefab == null ? null : prefab.GetComponents<Component>();

            int prefabIndex = 0;
            bool hasScriptData = false;
            foreach (Component component in components)
            {
                bool isPrefabComponent = false;
                Component prefabComponent = null;
                if (prefabComponents != null)
                {
                    // Look for the prefab version of this script
                    prefabComponent = GetConfigPrefab(component);
                    if (prefabComponent != null)
                    {
                        string prefabScriptName = GetScriptName<ProxyScript>(
                            prefabComponent.gameObject,
                            prefabComponent);
                        isPrefabComponent = !string.IsNullOrEmpty(prefabScriptName);
                    }
                }

                string scriptName = GetScriptName<ProxyScript>(gameObject, component);
                if (scriptName == null || scriptName == "")
                {
                    if (prefabComponent != null)
                    {
                        if (scriptName == "" && isPrefabComponent)
                        {
                            hasScriptData = true;
                            json.Add(null);
                        }
                        prefabIndex++;
                    }
                    else
                    {
                        // Fill disabled prefab scripts with null when scripts are disabled.
                        if (!isInstance && scriptName == "")
                        {
                            hasScriptData = true;
                            json.Add(null);
                        }
                    }
                    continue;
                }

                UpdateProgressBar(null, "Writing proxy script config for " + scriptName);
                if (prefabComponents != null)
                {
                    // Prefab scripts are always in the same order on the instance, so if this prefab script is not at
                    // the prefab index, then a prefab script was removed from the instance.
                    while (prefabIndex < prefabComponents.Length &&
                        prefabComponents[prefabIndex] != prefabComponent)
                    {
                        // null indicates a prefab script was removed from the instance
                        json.Add(null);
                        prefabIndex++;
                        hasScriptData = true;
                    }
                    prefabIndex++;
                }

                if (callback != null)
                {
                    callback(component);
                }

                // Call the approriate method for the script type
                ksJSON jsonScript = null;
                if (component is ProxyScript) {
                    jsonScript = CreateScript<ProxyScript>(scriptName, component, isPrefabComponent);
                }
                else if (component is CharacterController)
                {
                    jsonScript = CreateCharacterControllerScript(scriptName, component, isPrefabComponent);
                }
                else if (component is Collider || component is ksCylinderCollider || component is ksConeCollider)
                {
                    jsonScript = CreateColliderScript(scriptName, gameObject, component, isPrefabComponent, isInstance);
                }
                else if (component is Rigidbody)
                {
                    jsonScript = CreateRigidBodyScript(scriptName, gameObject, component, isPrefabComponent);
                }
                else if (component is FixedJoint)
                {
                    jsonScript = CreateFixedJointScript(scriptName, component, isPrefabComponent);
                }

                if (jsonScript != null)
                {
                    json.Add(jsonScript);
                    if (jsonScript.Count > 0)
                    {
                        hasScriptData = true;
                    }
                }
            }
            if (prefabComponents != null)
            {
                // Prefab index is the index after the last prefab script we found on the instance, so if it is less
                // than the prefab scripts length, then all prefab scripts from that index onwards were removed from
                // the instance.
                while (prefabIndex < prefabComponents.Length)
                {
                    // null indicates a prefab script was removed from the instance
                    json.Add(null);
                    prefabIndex++;
                    hasScriptData = true;
                }
            }
            return hasScriptData ? json : null;
        }

        /// <summary>
        /// Creates a JSON config describing a <see cref="ksProxyScript"/> instance of type 
        /// <typeparamref name="ProxyScript"/> from a component
        /// </summary>
        /// <typeparam name="ProxyScript">Proxy script</typeparam>
        /// <param name="scriptName">Name of the server script.</param>
        /// <param name="component">Component to write the script config for.</param>
        /// <param name="isPrefabComponent">True if the component was defined on a prefab.</param>
        /// <returns>JSON config describing the <typeparamref name="ProxyScript"/> instance.</returns>
        private ksJSON CreateScript<ProxyScript>(string scriptName, Component component, bool isPrefabComponent) where ProxyScript : Component
        {
            // Prepare JSON structures
            ksJSON jsonScript = new ksJSON(ksJSON.Types.OBJECT);
            ksJSON jsonFields = new ksJSON();

            // Get Unity serialization objects
            SerializedObject so = new SerializedObject(component);
            SerializedObject defaults = null;
            if (!isPrefabComponent)
            {
                jsonScript["name"] = scriptName;
                defaults = new SerializedObject(GetScript(component.GetType()) as ProxyScript);
            }
            else
            {
                defaults = new SerializedObject(GetConfigPrefab(component) as ProxyScript);
            }
            SerializedProperty iter = so.GetIterator();
            SerializedProperty defaultsIter = defaults.GetIterator();

            // Iterate serialized properties and write values if they are not scripts defaults
            // or different than the prefab values.
            bool enterChildren = true;
            while (iter.NextVisible(enterChildren))
            {
                if (defaultsIter != null)
                {
                    defaultsIter.NextVisible(enterChildren);
                }

                enterChildren = false;
                if (defaultsIter != null && SerializedProperty.DataEquals(iter, defaultsIter))
                {
                    continue;
                }

                // Store the name because creating the field may advance the iterator.
                string name = iter.name;
                ksJSON field = CreateField(iter);
                if (field != null)
                {
                    jsonFields[name] = field;
                }
            }
            if (jsonFields.Count > 0)
            {
                jsonScript["fields"] = jsonFields;
            }
            return jsonScript;
        }

        /// <summary>Checks if a property is referencing a scene object.</summary>
        /// <param name="property">Property to check.</param>
        /// <returns>True if the property is referencing a scene object.</returns>
        private bool IsSceneObjectReference(SerializedProperty property)
        {
            return property.propertyType == SerializedPropertyType.ObjectReference &&
                property.objectReferenceValue != null && 
                string.IsNullOrEmpty(AssetDatabase.GetAssetPath(property.objectReferenceValue));
        }

        /// <summary>Checks if a component is one a physics component supported by Reactor.</summary>
        /// <param name="component">Component to check.</param>
        /// <returns>True if the component is one of the physics components supported by Reactor.</returns>
        private bool IsPhysicsComponent(Component component)
        {
            return ksUnityCollider.IsSupported(component as Collider) 
                || component is Rigidbody 
                || component is FixedJoint;
        }

        /// <summary>
        /// Create a config for a <see cref="KS.Reactor.Server.ksCollider"/> script.
        /// </summary>
        /// <param name="scriptName">Name of the server script.</param>
        /// <param name="gameObject">Object the component is attached to.</param>
        /// <param name="component">Component to write the script config for.</param>
        /// <param name="isPrefabComponent">True if the component was defined on a prefab.</param>
        /// <param name="isInstance">Is the gameobject an instance in the scene.</param>
        /// <returns>JSON script configuration for a collider.</returns>
        private ksJSON CreateColliderScript(string scriptName, GameObject gameObject, Component component, bool isPrefabComponent, bool isInstance)
        {
            ksJSON jsonScript = new ksJSON(ksJSON.Types.OBJECT);
            ksIUnityCollider collider = component is Collider ? new ksUnityCollider((Collider)component) : component as ksIUnityCollider;
            ksEntityComponent entity = gameObject.GetComponent<ksEntityComponent>();
            ksCollisionFilterAsset collisionFilter = entity.GetCollisionFilterAsset(component, false);
            ksColliderData colliderData = entity.GetColliderData(component);

            ksIUnityCollider prefabCollider = null;
            ksCollisionFilterAsset prefabCollisionFilter = null;
            ksColliderData prefabColliderData = null;

            // Check for differences between the prefab and instance colliders
            bool isDefault = false;
            string prefabScriptName = null;
            if (isInstance && isPrefabComponent)
            {
                // Get prefab collider objects and components
                ksEntityComponent prefabEntity = GetConfigPrefab(entity);
                Component prefabComponent = GetConfigPrefab(component);
                prefabCollider = prefabEntity.GetCollider(prefabComponent);
                prefabCollisionFilter = prefabEntity.GetCollisionFilterAsset(prefabComponent, false);
                if (prefabCollider != null)
                {
                    prefabColliderData = prefabEntity.GetColliderData(prefabComponent);
                    prefabScriptName = GetScriptName<ksProxyEntityScript>(
                            prefabComponent.gameObject,
                            prefabComponent);
                }

                isDefault = true;
                // Check Unity component for override values
                SerializedObject serialized = new SerializedObject(component);
                SerializedObject prefabSO = new SerializedObject(prefabComponent);

                SerializedProperty iter = serialized.GetIterator();
                bool enterChildren = true;
                while (iter.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    // Since the prefab may not be the direct prefab of the instance we need to compare
                    // data values between the instance and the prefab instead of relying on the
                    // value of SerializedProperty.prefabOverride.
                    SerializedProperty prefabSP = prefabSO.FindProperty(iter.name);
                    if (!SerializedProperty.DataEquals(prefabSP, iter))
                    {
                        isDefault = false;
                        break;
                    }
                }

                // Check Reactor collider data for override values
                isDefault &= collider.IsQueryCollider == prefabCollider.IsQueryCollider &&
                        collider.IsSimulationCollider == prefabCollider.IsSimulationCollider &&
                        collisionFilter == prefabCollisionFilter;
            }
            if (isDefault)
            {
                return jsonScript;
            }

            // If the component is a MeshCollider and the instance overrides the MeshCollider.convex,
            // the script name will be different from the prefab. If MeshCollider.convex is true,
            // the name will be ".ksConvexMeshCollider". Otherwise, it will be ".ksTriangleMeshCollider".
            if (scriptName != prefabScriptName)
            {
                jsonScript["name"] = scriptName;
            }

            // Add constructor parameters
            ksJSON jsonParams = new ksJSON();
            jsonParams["shape id"] = CacheGeometry(entity, collider, isInstance, isInstance ? null : m_shapeMap, ref m_nextShapeId);
            jsonParams["material"] = CreateAssetReference(collider.Material);
            jsonParams["filter"] = CreateAssetReference(collisionFilter);
            jsonParams["offset"] = collider.Center;
            jsonParams["trigger flag"] = collider.IsTrigger;
            jsonParams["simulation flag"] = collider.IsSimulationCollider;
            jsonParams["query flag"] = collider.IsQueryCollider;
            if (colliderData != null && (prefabColliderData == null ? 
                colliderData.ContactOffset >= 0 : colliderData.ContactOffset != prefabColliderData.ContactOffset))
            {
                jsonParams["contact offset"] = colliderData.ContactOffset;
            }
            jsonParams["enabled"] = collider.IsEnabled;
            if (jsonParams.Count > 0)
            {
                jsonScript["params"] = jsonParams;
            }
            return jsonScript;
        }

        /// <summary>
        /// Create a config for a <see cref="KS.Reactor.Server.ksRigidBody"/> script.
        /// </summary>
        /// <param name="scriptName">Name of the server script.</param>
        /// <param name="gameObject">Object the component is attached to.</param>
        /// <param name="component">Component to write the script config for.</param>
        /// <param name="isPrefabComponent">True if the component was defined on a prefab.</param>
        /// <returns>JSON script configuration for a <see cref="KS.Reactor.Server.ksRigidBody"/>.</returns>
        private ksJSON CreateRigidBodyScript(string scriptName, GameObject gameObject, Component component, bool isPrefabComponent)
        {
            ksJSON jsonScript = new ksJSON(ksJSON.Types.OBJECT);
            ksJSON jsonParams = new ksJSON(ksJSON.Types.OBJECT);

            SerializedObject so = new SerializedObject(component);
            SerializedObject defaults = null;

            ksEntityComponent entity = gameObject.GetComponent<ksEntityComponent>();
            ksEntityPhysicsSettings physics = entity.PhysicsOverrides;

            ksEntityComponent defaultEntity = GetConfigPrefab(entity);
            ksEntityPhysicsSettings defaultPhysics = defaultEntity != null ? defaultEntity.PhysicsOverrides : null;

            if (!isPrefabComponent)
            {
                jsonScript["name"] = scriptName;
                defaults = new SerializedObject(GetScript(component.GetType()) as Rigidbody);
            }
            else
            {
                defaults = new SerializedObject(GetConfigPrefab(component) as Rigidbody);
            }

            // Write Unity RigidBody properties
            ksJSON value = GetOverrideProperty("m_Mass", so, defaults);
            if (value != null) jsonParams["mass"] = value;

            value = GetOverrideProperty("m_UseGravity", so, defaults);
            if (value != null) jsonParams["use gravity"] = value;

            value = GetOverrideProperty("m_IsKinematic", so, defaults);
            if (value != null) jsonParams["is kinematic"] = value;

            value = GetOverrideProperty("m_Drag", so, defaults);
            if (value != null) jsonParams["drag"] = value;

            value = GetOverrideProperty("m_AngularDrag", so, defaults);
            if (value != null) jsonParams["angular drag"] = value;

            value = GetOverrideProperty("m_CollisionDetection", so, defaults);
            if (value != null) jsonParams["collision detection"] = value;

            value = GetOverrideProperty("m_Constraints", so, defaults);
            if (value != null) jsonParams["constraints"] = value;

            // If default physics is null, then create a new default physics that has all
            // values of -1 (use defaults)
            if (defaultPhysics == null)
            {
                defaultPhysics = new ksEntityPhysicsSettings(
                    -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1, -1, ksRigidBodyModes.DEFAULT);
            }

            if (physics.SleepThreshold != defaultPhysics.SleepThreshold)
                jsonParams["sleep threshold"] = physics.SleepThreshold;

            if (physics.MaxLinearSpeed != defaultPhysics.MaxLinearSpeed)
                jsonParams["max linear speed"] = physics.MaxLinearSpeed;

            if (physics.MaxAngularSpeed != defaultPhysics.MaxAngularSpeed)
                jsonParams["max angular speed"] = physics.MaxAngularSpeed;

            if (physics.MaxDepenetrationSpeed != defaultPhysics.MaxDepenetrationSpeed)
                jsonParams["max depenetration speed"] = physics.MaxDepenetrationSpeed;

            if (physics.PositionIterations != defaultPhysics.PositionIterations)
                jsonParams["position iterations"] = physics.PositionIterations;

            if (physics.VelocityIterations != defaultPhysics.VelocityIterations)
                jsonParams["velocity iterations"] = physics.VelocityIterations;

            if (physics.RigidBodyMode != defaultPhysics.RigidBodyMode)
                jsonParams["rigid body mode"] = (int)physics.RigidBodyMode;

            if (jsonParams.Count > 0)
            {
                jsonScript["params"] = jsonParams;
            }
            return jsonScript;
        }

        /// <summary>
        /// Create a config for a <see cref="KS.Reactor.Server.ksFixedJoint"/> script.
        /// </summary>
        /// <param name="scriptName">Name of the server script.</param>
        /// <param name="component">Component to write the script config for.</param>
        /// <param name="isPrefabComponent">True if the component was defined on a prefab.</param>
        /// <returns>JSON script configuration for a <see cref="KS.Reactor.Server.ksFixedJoint"/>.</returns>
        private ksJSON CreateFixedJointScript(string scriptName, Component component, bool isPrefabComponent)
        {
            FixedJoint joint = component as FixedJoint;
            ksJSON jsonScript = new ksJSON(ksJSON.Types.OBJECT);
            ksJSON jsonParams = new ksJSON(ksJSON.Types.OBJECT);

            SerializedObject so = new SerializedObject(component);
            SerializedObject defaults = null;

            // Create new joint config
            if (!isPrefabComponent)
            {
                jsonScript["name"] = scriptName;
                defaults = new SerializedObject(GetScript(component.GetType()) as FixedJoint);
            }
            else
            {
                defaults = new SerializedObject(GetConfigPrefab(component) as FixedJoint);
            }

            ksJSON value = GetOverrideProperty("m_axis", so, defaults);
            if (value != null) jsonParams["Axis"] = value;

            value = GetOverrideProperty("m_anchor", so, defaults);
            if (value != null) jsonParams["Anchor"] = value;

            value = GetOverrideProperty("m_BreakForce", so, defaults);
            if (value != null) jsonParams["BreakForce"] = value;

            value = GetOverrideProperty("m_BreakTorque", so, defaults);
            if (value != null) jsonParams["BreakTorque"] = value;

            value = GetOverrideProperty("m_EnableCollision", so, defaults);
            if (value != null) jsonParams["EnableCollision"] = value;

            value = GetOverrideProperty("m_EnablePreprocessing", so, defaults);
            if (value != null) jsonParams["EnablePreprocessing"] = value;

            value = GetOverrideProperty("m_MassScale", so, defaults);
            if (value != null) jsonParams["MassScale"] = value;

            value = GetOverrideProperty("m_ConnectedMassScale", so, defaults);
            if (value != null) jsonParams["ConnectedMassScale"] = value;

            value = GetOverrideProperty("m_ConnectedBody", so, defaults);
            if (value != null) jsonParams["ConnectedBody"] = value;

            if (jsonParams.Count > 0)
            {
                jsonScript["fields"] = jsonParams;
            }
            return jsonScript;
        }

        /// <summary>Creates a JSON config describing a character controller.</summary>
        /// <param name="scriptName">Name of the script.</param>
        /// <param name="gameObject">GameObject to write the script config for.</param>
        /// <param name="component">Component to write the script config for.</param>
        /// <param name="isPrefabComponent">True if the component was defined on a prefab.</param>
        /// <returns>JSON config describing the <typeparamref name="ProxyScript"/> instance.</returns>
        private ksJSON CreateCharacterControllerScript(string scriptName, Component component, bool isPrefabComponent)
        {
            ksJSON jsonScript = new ksJSON(ksJSON.Types.OBJECT);
            ksJSON jsonParams = new ksJSON(ksJSON.Types.OBJECT);

            SerializedObject so = new SerializedObject(component);
            SerializedObject defaults = null;

            // Create new collider config
            if (!isPrefabComponent)
            {
                jsonScript["name"] = scriptName;
                defaults = new SerializedObject(GetScript(component.GetType()) as CharacterController);
            }
            else
            {
                defaults = new SerializedObject(GetConfigPrefab(component) as CharacterController);
            }

            ksJSON value = GetOverrideProperty("m_SlopeLimit", so, defaults);
            if (value != null) jsonParams["slope limit"] = value;

            value = GetOverrideProperty("m_StepOffset", so, defaults);
            if (value != null) jsonParams["step offset"] = value;

            value = GetOverrideProperty("m_SkinWidth", so, defaults);
            if (value != null) jsonParams["skin width"] = value;

            value = GetOverrideProperty("m_MinMoveDistance", so, defaults);
            if (value != null) jsonParams["min move distance"] = value;

            value = GetOverrideProperty("m_Center", so, defaults);
            if (value != null) jsonParams["offset"] = value;

            value = GetOverrideProperty("m_Radius", so, defaults);
            if (value != null) jsonParams["radius"] = value;

            value = GetOverrideProperty("m_Height", so, defaults);
            if (value != null) jsonParams["height"] = value;

            value = GetOverrideProperty("m_Enabled", so, defaults);
            if (value != null) jsonParams["enabled"] = value;

            ksIUnityCollider collider = new ksUnityCollider((Collider)component);
            ksEntityComponent entity = component.GetComponent<ksEntityComponent>();
            ksCollisionFilterAsset collisionFilter = entity.GetCollisionFilterAsset(component);

            if (isPrefabComponent)
            {
                Component prefabComponent = GetConfigPrefab(component);
                ksIUnityCollider prefabCollider = new ksUnityCollider((Collider)prefabComponent);
                ksEntityComponent prefabEntity = GetConfigPrefab(entity);
                ksCollisionFilterAsset prefabCollisionFilter = prefabEntity.GetCollisionFilterAsset(prefabComponent);

                if (collisionFilter != prefabCollisionFilter)
                {
                    jsonParams["filter"] = CreateAssetReference(collisionFilter);
                }
            }
            else
            {
                jsonParams["filter"] = CreateAssetReference(collisionFilter);
            }

            if (jsonParams.Count > 0)
            {
                jsonScript["params"] = jsonParams;
            }
            return jsonScript;
        }
        
        /// <summary>
        /// Get a property value from a serialized object when it is different than the same property value in 
        /// a default serialized object.
        /// </summary>
        /// <param name="propertyPath">Property value to fetch</param>
        /// <param name="so">SerializedObject</param>
        /// <param name="dso">default SerializedObject</param>
        /// <returns>Property value returned as a <see cref="ksJSON"/> value</returns>
        private ksJSON GetOverrideProperty(string propertyPath, SerializedObject so, SerializedObject defaultSO)
        {
            if (string.IsNullOrEmpty(propertyPath) || so == null || defaultSO == null)
            {
                return null;
            }

            SerializedProperty sp = so.FindProperty(propertyPath);
            if (sp == null)
            {
                return null;
            }

            SerializedProperty defaultSP = defaultSO.FindProperty(propertyPath);
            if (defaultSP == null)
            {
                return null;
            }

            // Since the defaultSO may not be the direct prefab of the instance we need to compare
            // data values instead of relying on the value of SerializedProperty.prefabOverride.
            return SerializedProperty.DataEquals(sp, defaultSP) ? null : CreateField(sp);
        }

        /// <summary>Creates a JSON config describing a serialized property value.</summary>
        /// <param name="property">Serialized property.</param>
        /// <returns>JSON config for a <see cref="SerializedProperty"/></returns>
        private ksJSON CreateField(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                {
                    return property.boolValue;
                }
                case SerializedPropertyType.Character:
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.Integer:
                {
                    return property.longValue;
                }
                case SerializedPropertyType.Color:
                {
                    return property.colorValue;
                }
                case SerializedPropertyType.Float:
                {
                    return property.doubleValue;
                }
                case SerializedPropertyType.Quaternion:
                {
                    return property.quaternionValue;
                }
                case SerializedPropertyType.String:
                {
                    return property.stringValue;
                }
                case SerializedPropertyType.Vector2:
                {
                    return property.vector2Value;
                }
                case SerializedPropertyType.Vector3:
                {
                    return property.vector3Value;
                }
                case SerializedPropertyType.ObjectReference:
                {
                    if (property.objectReferenceValue is MonoScript)
                    {
                        break;
                    }
                    ksJSON json = new ksJSON();
                    ksEntityComponent entity = property.objectReferenceValue as ksEntityComponent;
                    if (entity != null)
                    {
                        json["id"] = entity.EntityId;
                        return json;
                    }
                    Component component = property.objectReferenceValue as Component;
                    if (component == null)
                    {
                        return CreateAssetReference(property.objectReferenceValue);
                    }
                    entity = component.GetComponent<ksEntityComponent>();
                    if (entity == null)
                    {
                        return json;
                    }
                    // Find the index of the referenced script.
                    int index = 0;
                    bool found = false;
                    // Iterate all components and attempt to get the script name.  If the script name is null
                    // then the component will not be serialized and we can skip that component when determining
                    // the script index.
                    foreach(Component c in entity.GetComponents<Component>())
                    {
                        if (GetScriptName<ksProxyScript>(entity.gameObject, c) != null)
                        {
                            if (c == component)
                            {
                                found = true;
                                break;
                            }
                            index++;
                        }
                    }
                    if (!found)
                    {
                        return json;
                    }
                    json["id"] = entity.EntityId;
                    json["index"] = index;
                    return json;
                }
                case SerializedPropertyType.Generic:
                {
                    if (property.type == "ksSerializableDictionary`2")
                    {
                        property.NextVisible(true); // List of key/values
                    }
                    if (property.isArray)
                    {
                        ksJSON array = new ksJSON(ksJSON.Types.ARRAY);
                        int size = property.arraySize;
                        property.NextVisible(true);
                        for (int i = 0; i < size; i++)
                        {
                            property.NextVisible(false);
                            ksJSON element = CreateField(property);
                            if (element == null)
                            {
                                break;
                            }
                            array.Add(element);
                        }
                        return array;
                    }
                    if (property.type == "ksSerializableMultiType")
                    {
                        ksSerializableMultiType smultiType = ksEditorUtils.GetPropertyValue<ksSerializableMultiType>(
                            property);
                        if (smultiType == null)
                        {
                            return new ksJSON();
                        }
                        return smultiType.Value.ToJSON();
                    }
                    ksJSON container = new ksJSON(ksJSON.Types.OBJECT);
                    if (!property.hasVisibleChildren)
                    {
                        return container;
                    }
                    property = property.Copy();
                    property.NextVisible(true);
                    int depth = property.depth;
                    while (property.depth == depth)
                    {
                        string name = property.name;
                        ksJSON field = CreateField(property);
                        if (field != null)
                        {
                            container[name] = field;
                        }
                        if (!property.NextVisible(false))
                        {
                            break;
                        }
                    }
                    return container;
                }
                case SerializedPropertyType.AnimationCurve:
                {
                    return SerializeAnimationCurve(property.animationCurveValue);
                }
            }
            if (property.propertyPath != "m_Script")
            {
                ksLog.Warning(this, "Cannot serialize " + property.propertyPath + " because its type (" +
                    property.propertyType + ") is not supported.");
            }
            return null;
        }

        /// <summary>
        /// Creates a JSON config for entity physics settings configured in the room or overriden on entities.
        /// </summary>
        /// <param name="data">Entity physics settings</param>
        /// <param name="defaults">Default entity physics settings that will be overriden.</param>
        /// <returns>JSON config containing entity physics settings.</returns>
        private ksJSON CreateEntityPhysicsSettings(
            ksEntityPhysicsSettings data,
            ksEntityPhysicsSettings defaults = null)
        {
            if (data == null)
            {
                // Return a JSON null if the defaults was not null. This indicates a removal override.
                return (defaults != null) ? new ksJSON(ksJSON.Types.NULL) : null;
            }
            ksJSON json = new ksJSON(ksJSON.Types.OBJECT);

            if (data.ContactOffset >= 0f && (defaults == null || data.ContactOffset != defaults.ContactOffset))
                json["contact offset"] = data.ContactOffset;

            if (data.SleepThreshold >= 0f && (defaults == null || data.SleepThreshold != defaults.SleepThreshold))
                json["sleep threshold"] = data.SleepThreshold;

            if (data.MaxLinearSpeed >= 0f && (defaults == null || data.MaxLinearSpeed != defaults.MaxLinearSpeed))
                json["max linear speed"] = data.MaxLinearSpeed;

            if (data.MaxAngularSpeed >= 0f && (defaults == null || data.MaxAngularSpeed != defaults.MaxAngularSpeed))
                json["max angular speed"] = data.MaxAngularSpeed;

            if (data.MaxDepenetrationSpeed >= 0f && (defaults == null || data.MaxDepenetrationSpeed != defaults.MaxDepenetrationSpeed))
                json["max depenetration speed"] = data.MaxDepenetrationSpeed;

            if (data.PositionIterations > 0 && (defaults == null || data.PositionIterations != defaults.PositionIterations))
                json["position iterations"] = data.PositionIterations;

            if (data.VelocityIterations >= 0 && (defaults == null || data.VelocityIterations != defaults.VelocityIterations))
                json["velocity iterations"] = data.VelocityIterations;

            if (data.RigidBodyMode != ksRigidBodyModes.DEFAULT && (defaults == null || data.RigidBodyMode != defaults.RigidBodyMode))
                json["rigid body mode"] = (int)data.RigidBodyMode;

            return json.Count > 0 ? json : null;
        }

        /// <summary>Creates a JSON config describing transform precision.</summary>
        /// <param name="data">Transform precision</param>
        /// <param name="defaults">Default transform precision that will be overriden.</param>
        /// <returns>JSON config containing transform precision.</returns>
        private ksJSON CreateCompressionLevel(
            ksTransformPrecisions data,
            ksTransformPrecisions defaults = null)
        {
            if (data == null)
            {
                return null;
            }
            ksJSON json = new ksJSON(ksJSON.Types.OBJECT);
            if (data.Position >= 0f && (defaults == null || data.Position != defaults.Position))
                json["position"] = data.Position;

            if (data.Rotation >= 0f && (defaults == null || data.Rotation != defaults.Rotation))
                json["rotation"] = data.Rotation;

            if (data.Scale >= 0f && (defaults == null || data.Scale != defaults.Scale))
                json["scale"] = data.Scale;

            return json.Count > 0 ? json : null;
        }

        /// <summary>
        /// Checks if two floats are the same. Unlike an == comparison, will return true if they are both NaN.
        /// </summary>
        /// <param name="val1">First value</param>
        /// <param name="val2">Second value</param>
        /// <returns>True if the floats are the same.</returns>
        private bool CompareFloats(float val1, float val2)
        {
            return (val1 == val2 || (float.IsNaN(val1) && float.IsNaN(val2)));
        }

        /// <summary>
        /// Checks if two Vector3s are the same. Unlike an == comparison, will return true if some of their elements
        /// are both NaN.
        /// </summary>
        /// <param name="v1">First value</param>
        /// <param name="v2">Second value</param>
        /// <returns>True if the Vector3s are the same.</returns>
        private bool CompareVector3(Vector3 v1, Vector3 v2)
        {
            return CompareFloats(v1.x, v2.x) && CompareFloats(v1.y, v2.y) && CompareFloats(v1.z, v2.z);
        }

        /// <summary>Creates the game config file.</summary>
        /// <returns>JSON game config</returns>
        public ksJSON CreateGameJson()
        {
            m_nextShapeId = 1;
            m_scriptDefaults = new GameObject();
            ksJSON jsonGame = new ksJSON(ksJSON.Types.OBJECT);
            ksJSON jsonRooms = new ksJSON();
            ksJSON jsonEntities = new ksJSON();
            ksJSON jsonScriptAssets = new ksJSON();

            HashSet<uint> usedIds = new HashSet<uint>();
            // Keys are script asset names.
            List<KeyValuePair<string, UnityEngine.Object>> scriptAssets = 
                new List<KeyValuePair<string, UnityEngine.Object>>();
            string[] guids = AssetDatabase.FindAssets("t:PhysicMaterial t:ksProxyScriptAsset");
            foreach (string guid in guids)
            {
                if (m_cancelled)
                {
                    GameObject.DestroyImmediate(m_scriptDefaults);
                    return null;
                }
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath(path, typeof(UnityEngine.Object));
                string name = GetAssetName(asset);
                scriptAssets.Add(new KeyValuePair<string, UnityEngine.Object>(name, asset));
                ksProxyScriptAsset script = asset as ksProxyScriptAsset;
                if (script != null)
                {
                    if (script.AssetId != 0 && !usedIds.Add(script.AssetId))
                    {
                        LogDuplicateAssetId(script.AssetId);
                        script.AssetId = 0;
                    }
                }
                else
                {
                    uint assetId = ksAssetIdData.Get().GetId(asset);
                    if (assetId != 0 && !usedIds.Add(assetId))
                    {
                        LogDuplicateAssetId(assetId);
                        ksAssetIdData.Get().SetId(asset, 0);
                    }
                }
            }

            List<KeyValuePair<string, GameObject>> collections = new List<KeyValuePair<string, GameObject>>();
            foreach (ksBuildUtils.PrefabInfo prefab in ksBuildUtils.IterateResourceAndAssetBundlePrefabs())
            {
                if (m_cancelled)
                {
                    GameObject.DestroyImmediate(m_scriptDefaults);
                    return null;
                }
                if (!prefab.GameObject.activeSelf)
                {
                    continue;
                }

                if (CreateRoomType(jsonRooms, prefab.GameObject))
                {
                    continue;
                }

                // Find entity components and record their asset IDs
                if (FindEntityComponents(prefab.GameObject, usedIds, m_shapeMap))
                {
                    string entityType = ksPaths.GetEntityType(prefab.Path, prefab.AssetBundle);
                    CheckNameCollisions(prefab.Path, entityType);
                    collections.Add(new KeyValuePair<string, GameObject>(entityType, prefab.GameObject));
                }
            }
            m_typeToFullPath.Clear();
            m_nameCounts.Clear();

            // Assign asset ids to script assets.
            uint nextId = 1;
            foreach (KeyValuePair<string, UnityEngine.Object> pair in scriptAssets)
            {
                UnityEngine.Object asset = pair.Value;
                ksProxyScriptAsset script = asset as ksProxyScriptAsset;
                uint assetId = script != null ? script.AssetId : ksAssetIdData.Get().GetId(asset);
                if (assetId == 0)
                {
                    while (usedIds.Contains(nextId))
                    {
                        nextId++;
                    }
                    assetId = nextId++;
                    if (script != null)
                    {
                        script.AssetId = assetId;
                        EditorUtility.SetDirty(script);
                    }
                    else
                    {
                        ksAssetIdData.Get().SetId(asset, assetId);
                    }
                }
                // Add to asset mapping
                int index = pair.Key.IndexOf(':');
                string assetBundle = "";
                string assetPath = pair.Key;
                if (index >= 0)
                {
                    assetBundle = assetPath.Substring(0, index);
                    assetPath = assetPath.Substring(index + 1);
                }
                List<KeyValuePair<uint, string>> assetList;
                if (!m_assetBundleMap.TryGetValue(assetBundle, out assetList))
                {
                    assetList = new List<KeyValuePair<uint, string>>();
                    m_assetBundleMap[assetBundle] = assetList;
                }
                // Prepend '*' to the path to indicate this is a script asset.
                assetList.Add(new KeyValuePair<uint, string>(assetId, "*" + assetPath));
            }

            // Create json for script assets after asset ids are assigned so references work.
            foreach (KeyValuePair<string, UnityEngine.Object> pair in scriptAssets)
            {
                jsonScriptAssets[pair.Key] = CreateAsset(pair.Value);
            }

            // Create json for entities after asset ids are assigned so references work.
            foreach (KeyValuePair<string, GameObject> pair in collections)
            {
                ksJSON jsonCollection = CreateEntityCollection(pair.Key, pair.Value, usedIds, ref nextId);
                jsonEntities[pair.Key] = jsonCollection;
            }

            if (jsonRooms.Count > 0)
            {
                jsonGame["rooms"] = jsonRooms;
            }
            if (jsonEntities.Count > 0)
            {
                jsonGame["entities"] = jsonEntities;
            }
            if (jsonScriptAssets.Count > 0)
            {
                jsonGame["assets"] = jsonScriptAssets;
            }

            GameObject.DestroyImmediate(m_scriptDefaults);
            return jsonGame;
        }

        /// <summary>
        /// Searches for <see cref="ksEntityComponent"/> on an object and its descendants. Puts non-zero asset IDs
        /// from those entities into the <paramref name="usedIds"/> hash set, and non-zero shape ids in the
        /// <paramref name="shapeMap"/>.
        /// </summary>
        /// <param name="gameObject">Game object to find entity components on.</param>
        /// <param name="usedIds">Non-zero asset IDs from entity components will be added here.</param>
        /// <param name="shapeMap">
        /// Maps IDs to shapes. Shapes with non-zero IDs will be added to this map. Used to find ID collisions.
        /// </param>
        /// <returns>True if any entity components were found on the object or its descendants.</returns>
        private bool FindEntityComponents(GameObject gameObject, HashSet<uint> usedIds, Dictionary<int, ksIUnityCollider> shapeMap)
        {
            bool foundEntity = false;
            ksEntityComponent[] entities = gameObject.GetComponentsInChildren<ksEntityComponent>();
            foreach (ksEntityComponent entity in entities)
            {
                if (!entity.enabled)
                {
                    continue;
                }
                foundEntity = true;
                // If this is a nested prefab with the same id as the source prefab, change the nested prefab's id so
                // it will override the source prefab.
                ksEntityComponent prefab = GetConfigPrefab(entity);
                if (entity.AssetId != 0 &&
                    ((prefab != null && prefab.AssetId == entity.AssetId) || !usedIds.Add(entity.AssetId)))
                {
                    LogDuplicateAssetId(entity.AssetId);
                    entity.AssetId = 0;
                }
                foreach (ksColliderData data in entity.gameObject.GetComponents<ksColliderData>())
                {
                    data.ShapeId = Math.Max(0, data.ShapeId);
                    if (data.ShapeId != 0 && data.Collider != null)
                    {
                        ksIUnityCollider collider;
                        if (shapeMap.TryGetValue(data.ShapeId, out collider))
                        {
                            if (!(data.Collider is CharacterController) &&
                                !collider.IsGeometryEqual(entity.GetCollider(data.Collider)))
                            {
                                ksLog.Warning(this, "Multiple colliders with the same shape ID (" + data.ShapeId + 
                                    ") were found. One of the IDs will change. This may break compatibility with " +
                                    "old builds.");
                                data.ShapeId = 0;
                            }
                        }
                        else
                        {
                            collider = entity.GetCollider(data.Collider);
                            if (collider != null)
                            {
                                // Cache the geometry now so if there's another collider matching this one without a
                                // shape ID, we'll find a cached shape when we process it and won't generate a new ID.
                                CacheGeometry(entity, collider, shapeMap);
                                shapeMap[data.ShapeId] = collider;

                                if (prefab != null)
                                {
                                    // If a nested prefab collider has the same shape id as the source prefab collider
                                    // but they have different geometry and we change the source's id, the nested id
                                    // will change to match the source at the end of the frame, so we need to change it
                                    // back.
                                    ksColliderData prefabData = GetConfigPrefab(data);
                                    if (prefabData != null && data.ShapeId == prefabData.ShapeId &&
                                        !collider.IsGeometryEqual(prefab.GetCollider(prefabData.Collider)))
                                    {
                                        int shapeId = data.ShapeId;
                                        EditorApplication.delayCall += () =>
                                        {
                                            data.ShapeId = shapeId;
                                            EditorUtility.SetDirty(data);
                                        };
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return foundEntity;
        }

        /// <summary>
        /// Logs a warning that a duplicate asset id was detected and one of them will change, which may break
        /// compatibility with old builds.
        /// </summary>
        /// <param name="assetId">Duplicate asset id.</param>
        private void LogDuplicateAssetId(uint assetId)
        {
            ksLog.Warning(this, "Multiple assets with the same asset ID (" + assetId +
                ") were found. One of the IDs will change. This may break compatibility with old builds.");
        }

        /// <summary>
        /// Gets a component of type <typeparamref name="T"/> from a game object. Logs a warning if the object has more
        /// than one coponent of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Type of component to get.</typeparam>
        /// <param name="gameObject">The object to get the component from.</param>
        /// <returns>The component, or null if none was found.</returns>
        private T GetSingleComponent<T>(GameObject gameObject) where T : Component
        {
            T[] components = gameObject.GetComponents<T>();
            if (components.Length > 1)
            {
                ksLog.Warning(this, "Multiple " + typeof(T).Name + "s were found on object '" + gameObject.name +
                    "'. Only the first will be used.", gameObject);
            }
            return components.Length == 0 ? null : components[0];
        }

        /// <summary>Gets the prefab name from an asset path.</summary>
        /// <param name="assetPath">Path to prefab</param>
        /// <returns>Prefab name</returns>
        private string GetPrefabName(string assetPath)
        {
            int index = assetPath.LastIndexOfAny(new char[] { '/', ':' });
            if (index >= 0)
            {
                return assetPath.Substring(index + 1);
            }
            return assetPath;
        }

        /// <summary>
        /// Get the prefab for an entity object that is written to the common config.
        /// </summary>
        /// <returns>
        /// Returns the first corresponding game object that exists in a resources directory
        /// or bundle, or null if no prefab can be found.
        /// </returns>
        private T GetConfigPrefab<T>(T instance) where T: UnityEngine.Object
        {
            if (instance == null || !PrefabUtility.IsPartOfAnyPrefab(instance))
            {
                return null;
            }

            T prefab = PrefabUtility.GetCorrespondingObjectFromSource(instance);
            while (prefab != null)
            {
                if (ksEditorUtils.IsInResourcesOrAssetBundle(prefab))
                {
                    return prefab;
                }
                prefab = PrefabUtility.GetCorrespondingObjectFromSource(prefab);
            }
            return null;
        }

        /// <summary>
        /// Increments the counter of prefabs with the given name, and logs a warning if this is the second prefab
        /// found with this name.
        /// </summary>
        /// <param name="assetPath">Path to prefab</param>
        /// <param name="entityType">Entity type</param>
        /// <exception cref="Exception">Thrown if two prefabs have the exact same entity type.</exception>
        private void CheckNameCollisions(string assetPath, string entityType)
        {
            string repeatPath;
            if (m_typeToFullPath.TryGetValue(entityType, out repeatPath))
            {
                throw new Exception("Entity prefabs '" + repeatPath + "' and '" + assetPath +
                    "' have the same entity type name. You must rename one of them.");
            }
            m_typeToFullPath[entityType] = assetPath;

            string prefabName = GetPrefabName(entityType);
            if (!m_nameCounts.ContainsKey(prefabName))
            {
                m_nameCounts[prefabName] = 0;
            }
            m_nameCounts[prefabName]++;
            if (m_nameCounts[prefabName] == 2)
            {
                ksLog.Warning(this, "Multiple entity prefabs with name " + prefabName +
                    " found. Use a path when spawning (relative to Resources).");
            }
        }

        /// <summary>Extract and cache the geometry data from a ksCollider. Uses the prefab cache only.</summary>
        /// <param name="entity">Entity the collider is from.</param>
        /// <param name="collider">Collider to extract geometry from.</param>
        /// <param name="shapeMap">Map of IDs to shapes. Used to prevent ID collisions.</param>
        public void CacheGeometry(ksEntityComponent entity, ksIUnityCollider collider, Dictionary<int, ksIUnityCollider> shapeMap)
        {
            int nextId = 0;
            CacheGeometry(entity, collider, false, shapeMap, ref nextId);
        }

        /// <summary>Extract and cache the geometry data from a <see cref="ksIUnityCollider"/>.</summary>
        /// <param name="entity">Entity the collider is from.</param>
        /// <param name="collider">Collider to extract geometry from.</param>
        /// <param name="useInstanceGeometry">
        /// if true, will check the instance cache if geometry is not found in the prefab cache, and will store
        /// geometry in the instance cache if it's not found in either cache.
        /// </param>
        /// <param name="shapeMap">
        /// Map of IDs to shapes. Used to prevent ID collisions. Not used if <paramref name="useInstanceGeometry"/> is
        /// true.
        /// </param>
        /// <param name="nextId">
        /// ID to assign if a new ID is needed. Will increment this number until an unused ID is found. Not used if
        /// <paramref name="useInstanceGeometry"/>is true. Instead a negative ID is assigned.
        /// </param>
        /// <returns>Shape ID</returns>
        public int CacheGeometry(
            ksEntityComponent entity,
            ksIUnityCollider collider,
            bool useInstanceGeometry,
            Dictionary<int, ksIUnityCollider> shapeMap,
            ref int nextId)
        {
            // Missing Collider
            if (collider == null)
            {
                return 0;
            }

            GeometryData data = new GeometryData();
            data.Collider = collider;

            // Check for duplicate colliders.
            int index = m_geometryCache.IndexOf(data);
            if (index == -1)
            {
                index = useInstanceGeometry ? m_instanceGeometryCache.IndexOf(data) : index;
                if (index == -1)
                {
                    // New collider, get or assign an ID and add it to the list of colliders
                    List<GeometryData> cache = useInstanceGeometry ? m_instanceGeometryCache : m_geometryCache;
                    if (useInstanceGeometry)
                    {
                        // negative ID indicates an instance shape
                        data.Id = -cache.Count - 1;
                    }
                    else
                    {
                        // Get the ID if it already has one
                        data.Id = entity.GetColliderData(collider.Component).ShapeId;
                        // Assign an unused ID if it doesn't already have one
                        if (data.Id == 0)
                        {
                            if (shapeMap == null || nextId <= 0)
                            {
                                ksLog.Warning(this, "Invalid arguments to CacheGeometry. shapeMap must be non-null " +
                                    "and nextId must be > 0 when useInstanceGeometry is false");
                                return 0;
                            }
                            while (shapeMap.ContainsKey(nextId))
                            {
                                nextId++;
                            }
                            shapeMap[nextId] = collider;
                            data.Id = nextId;
                            nextId++;
                        }
                    }
                    data.Geometry = collider.Serialize();
                    cache.Add(data);
                }
                else
                {
                    data = m_instanceGeometryCache[index];
                }
            }
            else
            {
                data = m_geometryCache[index];
            }
            if (!useInstanceGeometry && entity.SetShapeId(collider, data.Id))
            {
                EditorUtility.SetDirty(entity.gameObject);
            }
            return data.Id;
        }

        /// <summary>Write cached collider data to a file.</summary>
        /// <param name="path">Path to write to.</param>
        /// <param name="cache">Cache of data to write.</param>
        /// <param name="scene">Set to trye if the cache contains scene geometry</param>
        /// <returns>True if no errors were encountered.</returns>
        private bool WriteGeometryCache(string path, List<GeometryData> cache, string scenePath)
        {
            try
            {
                // Remove all asset references to the Scene. These will be rebuilt further down for the current tracked assets.
                // This effectively deletes scene references from assets that are no longer used by the scene.
                if (scenePath != null)
                {
                    m_sceneTracker.RemoveAssetSceneReferences(scenePath);
                }
                using (FileStream fileStream = new FileStream(path, FileMode.Create))
                {
                    using (BinaryWriter writer = new BinaryWriter(fileStream))
                    {
                        // data offset
                        int dataStart = sizeof(ushort) + sizeof(uint) + cache.Count * (3 * sizeof(int) + 1);

                        // Version
                        writer.Write(GEOMETRY_FILE_VERSION);

                        // Write header information
                        writer.Write((uint)cache.Count);
                        uint offset = (uint)dataStart;
                        foreach (GeometryData data in cache)
                        {
                            writer.Write(data.Id);
                            writer.Write((byte)data.Collider.ShapeType);
                            writer.Write(offset);

                            uint geometryDataSize = data.Geometry == null ? 0u : (uint)data.Geometry.Length;
                            writer.Write(geometryDataSize);
                            offset += geometryDataSize;

                            // Track collider assets used in the scene
                            if (scenePath != null)
                            {
                                string assetPath = GetColliderAssetPath(data.Collider);
                                if (assetPath != null)
                                {
                                    m_sceneTracker.TrackAsset(assetPath, scenePath);
                                }
                            }
                        }

                        // Write collider data
                        foreach (GeometryData data in cache)
                        {
                            if (data.Geometry == null)
                            {
                                continue;
                            }
                            fileStream.Write(data.Geometry, 0, data.Geometry.Length);
                        }
                    }
                }
                AddSummaryMessage("Updated " + path);
            }
            catch (Exception ex)
            {
                ksLog.Error("Error writing geometry data to '" + path + "'", ex);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Get the path to an asset (mesh or terrain data) reference by a collider
        /// </summary>
        /// <param name="collider">Collider</param>
        /// <returns>Asset path or null if the collider did not have an asset reference.</returns>
        private string GetColliderAssetPath(ksICollider collider)
        {
            ksUnityCollider wrapper = collider as ksUnityCollider;
            if (wrapper == null)
            {
                return null;
            }

            TerrainCollider terrainCollider = wrapper.Component as TerrainCollider;
            if (terrainCollider != null)
            {
                return AssetDatabase.GetAssetPath(terrainCollider.terrainData);
            }

            MeshCollider meshCollider = wrapper.Component as MeshCollider;
            if (meshCollider != null)
            {
                return AssetDatabase.GetAssetPath(meshCollider.sharedMesh);
            }

            return null;
        }

        /// <summary>Add a message to the build summary.</summary>
        /// <param name="message">Message to add to the summary.</param>
        /// <param name="indent">Number of spaces of indent.</param>
        private void AddSummaryMessage(string message, int indent = 2)
        {
            m_summary += "\n" + new string(' ', indent) + message;
        }

        /// <summary>
        /// Update the editor progress bar. If an argument is null or negative then do not change the prexisting value.
        /// </summary>
        /// <param name="title">Title to display.</param>
        /// <param name="message">Message to display.</param>
        /// <param name="progress">Progress between 0 and 1.</param>
        private void UpdateProgressBar(string title, string message, float progress = -1.0f)
        {
            if (title != null)
            {
                m_progressTitle = title;
            }

            if (message != null)
            {
                m_progressMessage = message;
            }

            if (progress >= 0.0f)
            {
                m_progress = progress;
            }

            EditorUtility.DisplayProgressBar(m_progressTitle, m_progressMessage, m_progress);
        }

        /// <summary>
        /// Extract the keyframes and wrapping information from an AnimationCurve and write that data into a JSON
        /// object that can be loaded into a ksCurve.
        /// </summary>
        /// <param name="curve">Curve to serialize.</param>
        /// <returns>JSON curve</returns>
        private ksJSON SerializeAnimationCurve(AnimationCurve curve)
        {
            if (curve == null)
            {
                ksLog.Warning(this, "Serialized null AnimationCurve");
                return ksJSON.Literals.NULL;
            }

            ksJSON json = new ksJSON();
            switch (curve.preWrapMode)
            {
                case WrapMode.Once: json["preWrap"] = (int)ksCurve.WrapModes.First; break;
                case WrapMode.ClampForever: json["preWrap"] = (int)ksCurve.WrapModes.Last; break;
                case WrapMode.Loop: json["preWrap"] = (int)ksCurve.WrapModes.Loop; break;
                case WrapMode.PingPong: json["preWrap"] = (int)ksCurve.WrapModes.PingPong; break;
                default: json["preWrap"] = (int)ksCurve.WrapModes.First; break;
            }

            switch (curve.postWrapMode)
            {
                case WrapMode.Once: json["postWrap"] = (int)ksCurve.WrapModes.First; break;
                case WrapMode.ClampForever: json["postWrap"] = (int)ksCurve.WrapModes.Last; break;
                case WrapMode.Loop: json["postWrap"] = (int)ksCurve.WrapModes.Loop; break;
                case WrapMode.PingPong: json["postWrap"] = (int)ksCurve.WrapModes.PingPong; break;
                default: json["postWrap"] = (int)ksCurve.WrapModes.First; break;
            }

            ksJSON jsonKeyframes = new ksJSON(ksJSON.Types.ARRAY);
            Keyframe[] keys = curve.keys;
            for (int i = 0; i < keys.Length; ++i)
            {
                Keyframe key = keys[i];
                ksJSON jsonKeyframe = new ksJSON(ksJSON.Types.ARRAY);
                jsonKeyframe.Add(key.time);
                jsonKeyframe.Add(key.value);
                jsonKeyframe.Add(key.inTangent);
                jsonKeyframe.Add(key.outTangent);
                jsonKeyframe.Add(key.weightedMode == (WeightedMode.In | WeightedMode.Both) ? key.inWeight : 0.33333f);
                jsonKeyframe.Add(key.weightedMode == (WeightedMode.Out | WeightedMode.Both) ? key.outWeight : 0.33333f);
                jsonKeyframes.Add(jsonKeyframe);
            }
            json["keyframes"] = jsonKeyframes;
            return json;
        }
    }
}
