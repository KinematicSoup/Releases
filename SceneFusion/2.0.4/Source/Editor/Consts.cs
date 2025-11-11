using UnityEditor;
using UnityEngine;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>Property names</summary>
    public class sfProp
    {
        public const string Position = "m_LocalPosition";
        public const string Rotation = "m_LocalRotation";
        public const string Scale = "m_LocalScale";
        public const string Path = "#path";
        public const string Index = "#index";
        public const string Guid = "#guid";
        public const string Mesh = "#mesh";
        public const string UserId = "#userId";
        public const string Pivot = "#pivot";
        public const string SceneViewSize = "#sceneViewSize";
        public const string Orthographic = "#orthographic";
        public const string In2DMode = "#in2DMode";
        public const string CheckSum = "#checksum";
        public const string FileId = "#fileId";
        public const string IsLibraryAsset = "#isLibraryAsset";
        public const string Terrain = "#terrain";
        public const string Subscriptions = "#subscriptions";
        public const string Data = "#data";
        public const string Meta = "#meta";
        public const string Class = "#class";
        public const string Prefab = "#prefab";
        public const string PrefabSourcePath = "#prefabSourcePath";
        public const string PrefabSourceFileId = "#prefabSourceFileId";
        public const string PrefabInstanceHandleFileId = "#prefabInstanceHandleFileId";
        public const string InstanceRevisions = "#instanceRevision";
        public const string PrefabRevision = "#prefabRevision";

        // Config properties
        public const string SyncPrefabs = "#syncPrefabs";
        public const string SyncMaterials = "#syncMaterials";

        // Occlusion properties
        public const string SmallestOccluder = "#smallestOccluder";
        public const string SmallestHole = "#smallestHole";
        public const string BackfaceThreshold = "#backfaceTreshold";

        // TerrainData properties
        public const string TerrainSize = "#terrainSize";
        public const string Heightmap = "#heightmap";
        public const string HeightmapResolution = "#heightmapResolution";
        public const string Alphamap = "#alphamap";
        public const string AlphamapResolution = "#alphamapResolution";
        public const string Holes = "#holes";
        public const string DetailResolution = "#detailResolution";
        public const string DetailPatchResolution = "#detailPatchResolution";
        public const string DetailScatterMode = "#detailScatterMode";
        public const string Details = "#details";
        public const string Trees = "#trees";
        public const string TreeCount = "#treeCount";
        //public const string HolesResolution = "#holesResolution";
        public const string TerrainLayerPalette = "#terrainLayerPalette";
        public const string DetailPrototypePalette = "#detailPrototypePalette";
        public const string TreePrototypePalette = "#treePrototypePalette";
        public const string WavingGrassSpeed = "#wavingGrassSpeed";
        public const string WavingGrassStrength = "#wavingGrassStrength";
        public const string WavingGrassAmount = "#wavingGrassAmount";
        public const string WavingGrassTint = "#wavingGrassTint";
        public const string CompressHoles = "#compressHoles";
        public const string BaseMapResolution = "#baseMapResolution";

        // Detail/Tree Prototype properties
        public const string Prototype = "#prototype";
        public const string PrototypeTexture = "#prototypeTexture";
        public const string HealthyColor = "#healthyColor";
        public const string DryColor = "#dryColor";
        public const string MinWidth = "#minWidth";
        public const string MaxWidth = "#maxWidth";
        public const string MinHeight = "#minHeight";
        public const string MaxHeight = "#maxHeight";
        public const string NoiseSpread = "#noiseSpread";
        public const string BendFactor = "#bendFactor";
        public const string RenderMode = "#renderMode";
        public const string UsePrototypeMesh = "#usePrototypeMesh";
        public const string NoiseSeed = "#noiseSeed";
        public const string HoleEdgePadding = "#holeEdgePadding";
        public const string UseInstancing = "#useInstancing";
        public const string AlignToGround = "#alignToGround";
        public const string PositionJitter = "#positionJitter";
        public const string UseDensityScale = "#useDensityScale";
        public const string Density = "#Density";

        // Tree instance properties
        public const string TreePosition = "#TreePosition";
        public const string TreeWidthScale = "#TreeWidthScale";
        public const string TreeHeightScale = "#TreeHeightScale";
        public const string TreeRotation = "#TreeRotation";
        public const string TreeColor = "#TreeColor";
        public const string TreeLightmapColor = "#TreeLightmapColor";
        public const string TreePrototypeIndex = "#TreePrototypeIndex";
    }

    /// <summary>sfObject types</summary>
    public class sfType
    {
        public const string Scene = "Scene";
        public const string SceneLock = "SceneLock";
        public const string SceneSubscriptions = "SceneSubscriptions";
        public const string Hierarchy = "Hierarchy";
        public const string GameObject = "GameObject";
        public const string Component = "Component";
        public const string Avatar = "Avatar";
        public const string Terrain = "Terrain";
        public const string TerrainBrush = "TerrainBrush";
        public const string LightmapSettings = "LightmapSettings";
        public const string RenderSettings = "RenderSettings";
        public const string Asset = "Asset";
        public const string AssetPath = "AssetPath";
        public const string OcclusionSettings = "OcclusionSettings";
        public const string Prefab = "Prefab";
        public const string Config = "Config";
    }
}
