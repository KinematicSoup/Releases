using System;
using System.Collections.Generic;
using System.Reflection;
using KS.SF.Reactor;
using KS.SceneFusion;
using KS.SceneFusion2.Client;
using KS.SF.Unity.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TerrainUtils;
using UObject = UnityEngine.Object;
using UnityEngine.TerrainTools;
using UnityEditor.TerrainTools;

namespace KS.SceneFusion2.Unity.Editor
{
    [CustomEditor(typeof(Terrain))]
    public class sfTerrainEditor : ksOverrideEditor
    {
        /// <summary>Terrain editor states.</summary>
        public enum States
        {
            // The editor behaves like the regular base Unity terrain inspector.
            BASE = 0,
            // The editor overrides the default behaviour to track editing data and generate events used to sync terrain.
            OVERRIDE = 1,
            // Terrain editing is disabled and an upgrade message is shown.
            DISABLED = 2
        }

        // Corresponds to a specific tool in a tool category
        public class Tools
        {
            public const int NONE = -1;
            public const int PAINT_HEIGHT = 0;
            public const int SET_HEIGHT = 1;
            public const int SMOOTH_HEIGHT = 2;
            public const int PAINT_TEXTURE = 3;
            public const int PLACE_TREE = 4;
            public const int PLACE_DETAIL = 5;
            public const int TERRAIN_SETTINGS = 6;
            public const int TRANSFORM = 7;
            public const int PAINT_HOLE = 8;
        }

        // Corresponds to one of the five tool category buttons in the terrain editor
        public class ToolCategories
        {
            public const int NONE = -1;
            public const int CREATE_NEIGHBOR = 0;
            public const int PAINT = 1;
            public const int PLACE_TREE = 2;
            public const int PAINT_DETAIL = 3;
            public const int TERRAIN_SETTINGS = 4;
            public const int TERRAIN_TOOL_COUNT = 5;
        }

        private static readonly List<string> m_heightPaintingTools = new List<string>()
        {
            "PaintHeightTool",
            "SetHeightTool",
            "SmoothHeightTool",
            "StampTool",
            "BridgeTool",
            "CloneBrushTool",
            "NoiseHeightTool",
            "TerraceErosion",
            "ContrastTool",
            "SharpenPeaksTool",
            "SlopeFlattenTool",
            "HydroErosionTool",
            "ThermalErosionTool",
            "WindErosionTool",
            "MeshStampTool"
        };

        private static readonly List<string> m_transformTools = new List<string>()
        {
            "PinchHeightTool",
            "SmudgeHeightTool",
            "TwistHeightTool"
        };

        // The id of the tree brush texture.
        private const int TREE_BRUSH_INDEX = 0;
        private const float TERRAIN_CHECK_INTERVAL = 0.1f;
        private static readonly string LOG_CHANNEL = typeof(sfTerrainEditor).FullName;

        /// <summary>Handler for pre and post scene gui events.</summary>
        public delegate void SceneGUIHandler();

        /// <summary>Invoked before drawing the scene GUI.</summary>
        public static event SceneGUIHandler PreSceneGUI;

        /// <summary>Invoked after drawing the scene GUI.</summary>
        public static event SceneGUIHandler PostSceneGUI;

        /// <summary>Callback used with ForEachPaintedTerrain.</summary>
        /// <param name="terrain">terrain that was painted on.</param>
        /// <param name="bounds">bounds of painted area.</param>
        private delegate void ForEachPaintedTerrainCallback(Terrain terrain, RectInt bounds);

        private const string TERRAIN_TOOLS_NAMESPACE = "UnityEditor.TerrainTools";

        /// <summary>Terrain editor state.</summary>
        public static States State
        {
            get { return m_state; }
            set
            {
                if (m_state == value)
                {
                    return;
                }
                if (m_instance != null)
                {
                    if (value == States.OVERRIDE)
                    {
                        m_instance.Initialize();
                    }
                    else if (m_state == States.OVERRIDE)
                    {
                        m_instance.CleanUp();
                    }
                }
                m_state = value;
            }
        }
        private static States m_state = States.BASE;

        /// <summary>Is the terrain editor open?</summary>
        public static bool IsOpen
        {
            get { return m_instance != null; }
        }
        private static sfTerrainEditor m_instance;

        /// <summary>The selected tool category.</summary>
        public static int ToolCategory
        {
            get { return m_toolCategory; }
        }
        private static int m_toolCategory = ToolCategories.NONE;
        
        /// <summary>The selected tool.</summary>
        public static int Tool
        {
            get { return m_tool; }
        }
        private static int m_tool = Tools.NONE;

        /// <summary>
        /// The terrain that is currently selected, or the last selected terrain if no terrain is selected.
        /// </summary>
        public static Terrain Terrain
        {
            get { return m_terrain; }
        }
        private static Terrain m_terrain;

        /// <summary>The terrain brush. Brush.Terrain is null if the brush isn't showing.</summary>
        public static sfTerrainBrush Brush
        {
            get { return m_brush; }
        }
        private static sfTerrainBrush m_brush = new sfTerrainBrush();

        /// <summary>
        /// Has the missing brush warning been shown? If false, the next time a brush index can't be found in the brush
        /// list, a warning is logged. This is set to false when joining a session.
        /// </summary>
        public static bool MissingBrushWarningShown
        {
            get { return m_missingBrushWarningShown; }
            set { m_missingBrushWarningShown = value; }
        }
        private static bool m_missingBrushWarningShown = false;

        /// <summary>The selected texture layer index.</summary>
        public static int LayerIndex
        {
            get
            {
                object value = SelectedLayerField.GetValue();
                return value == null ? -1 : (int)value;
            }

            set
            {
                SelectedLayerField.SetValue(value);
            }
        }

        /// <summary>The selected detail prototype index.</summary>
        public static int DetailIndex
        {
            get
            {
                object value = SelectedDetailField.GetValue();
                return value == null ? -1 : (int)value;
            }

            set
            {
                SelectedDetailField.SetValue(value);
            }
        }

        /// <summary>Reflection object for the selected layer index field.</summary>
        private static ksReflectionObject SelectedLayerField
        {
            get
            {
                if (m_roSelectedLayer == null)
                {
#if UNITY_6000_0_OR_NEWER
                    ksReflectionObject roPaintTextureTool = new ksReflectionObject(m_editorAssembly,
                        "UnityEditor.TerrainTools.PaintTextureTool");
                    m_roSelectedLayer = new ksReflectionObject(m_editorAssembly,
                        "UnityEditor.EditorTools.EditorToolManager")
                        .Call("GetSingleton", roPaintTextureTool.Type)
                        .GetField("m_SelectedTerrainLayerIndex");
#else
                    m_roSelectedLayer = new ksReflectionObject(m_editorAssembly,
                        "UnityEditor.TerrainTools.PaintTextureTool")
                        .GetProperty("instance")
                        .GetField("m_SelectedTerrainLayerIndex");
#endif
                }
                return m_roSelectedLayer;
            }
        }

        /// <summary>Reflection object for the selected detail index field.</summary>
        private static ksReflectionObject SelectedDetailField
        {
            get
            {
                if (m_roSelectedDetail == null)
                {
#if UNITY_6000_0_OR_NEWER
                    ksReflectionObject roPaintDetailsTool = new ksReflectionObject(m_editorAssembly,
                        "UnityEditor.TerrainTools.PaintDetailsTool");
                    m_roSelectedDetail = new ksReflectionObject(m_editorAssembly,
                        "UnityEditor.EditorTools.EditorToolManager")
                        .Call("GetSingleton", roPaintDetailsTool.Type)
                        .GetProperty("selectedDetail");
#else
                    m_roSelectedDetail = new ksReflectionObject(m_editorAssembly,
                        "UnityEditor.TerrainTools.PaintDetailsTool")
                        .GetProperty("instance")
                        .GetProperty("selectedDetail");
#endif

                }
                return m_roSelectedDetail;
            }
        }

        private static object m_activeTool;
        private static object m_onInspectorGUIEditContext;
        private static object m_brushList;

        private static Assembly m_editorAssembly;
        private static ksReflectionObject m_roActiveTerrainInspectorField;
        private static ksReflectionObject m_roActiveTerrainInspectorInstanceField;
        private static ksReflectionObject m_roTerrainColliderRaycastMethod;
        private static ksReflectionObject m_roCalcPixelRectFromBoundsMethod;
        private static ksReflectionObject m_roGetActiveToolMethod;
        private static ksReflectionObject m_roInitializeMethod;
        private static ksReflectionObject m_roShowSettingsMethod;
        private static ksReflectionObject m_roGetWindowTerrain;
        private static ksReflectionObject m_roSelectedTool;
        private static ksReflectionObject m_roSelectedLayer;
        private static ksReflectionObject m_roSelectedDetail;
        private static ksReflectionObject m_roSelectedBrush;
        private static ksReflectionObject m_roBrushList;
        private static ksReflectionObject m_roBrushSize;
        private static ksReflectionObject m_roTexture;
#if UNITY_6000_0_OR_NEWER
        private static ksReflectionObject m_roSelectCategory;
#endif

        private EventType m_sceneGuiEventType = EventType.Used;
        private Vector2 m_sceneGuiMousePosition = ksVector2.Zero;
        private int m_sceneGuiMouseButton = -1;
        private bool m_sceneGuiShift = false;
        private bool m_sceneGuiCtrl = false;

        private GUIContent[] m_toolIcons;
        private GUIStyle m_commandStyle;

        // The RectInt is the change area, and bool is true if shift was held at any point, meaning all detail layers
        // need to be checked for changes. These are static because if you mouse down on a terrain that is not
        // selected, Unity selects it and destroys the current editor and creates a new one, so the mouse down and
        // mouse up events occur with different editors but we want them to track the same edits.
        private static Dictionary<Terrain, KeyValuePair<RectInt, bool>> m_detailEdits =
            new Dictionary<Terrain, KeyValuePair<RectInt, bool>>();
        // Keys are true if we need to check for deleted trees, false for added trees only.
        private static Dictionary<Terrain, bool> m_treeEdits = new Dictionary<Terrain, bool>();

        // These are terrain objects that are temporarily locked while the user is painting trees on them without
        // having them selected.
        private List<sfObject> m_tempLockedObjects = new List<sfObject>();
        private float m_timeToPaletteCheck = 0;
        private bool m_isPlaceTreeWizardOpen = false;

        /// <summary>Static initialization. Load the unity editor assembly.</summary>
        static sfTerrainEditor()
        {
            m_editorAssembly = ksEditorUtils.LoadAssembly("UnityEditor");
        }

        /// <summary>
        /// Adjusts the selected layer index to preserve the selected layer after adding or removing texture layers.
        /// If the selected layer was removed, clears the selection.
        /// </summary>
        /// <param name="terrainData">
        /// terrainData with added or removed layers. If this is not the last selected terrain,
        /// does nothing.
        /// </param>
        /// <param name="isInsertion">were layers added or removed?</param>
        /// <param name="index">index layers were inserted at or removed from.</param>
        /// <param name="count">number of layers added or removed.</param>
        /// 

        public static void AdjustLayerIndex(TerrainData terrainData, bool isInsertion, int index, int count)
        {
            if (m_terrain == null || m_terrain.terrainData != terrainData)
            {
                return;
            }
            int layerIndex = LayerIndex;
            if (isInsertion)
            {
                if (layerIndex >= index)
                {
                    LayerIndex = layerIndex + count;
                }
            }
            else if (layerIndex > index + count - 1)
            {
                LayerIndex = layerIndex - count;
            }
            else if (layerIndex >= index)
            {
                LayerIndex = -1;
            }
        }

        /// <summary>
        /// Adjusts the selected detail index to preserve the selected detail prototype after adding or removing detail
        /// prototypes. If the selected prototype was removed, clears the selection.
        /// </summary>
        /// <param name="terrainData">
        /// terrainData with added or removed detail prototypes. If this is not the last selected
        /// terrain, does nothing.
        /// </param>
        /// <param name="isInsertion">were prototypes added or removed?</param>
        /// <param name="index">index prototypes were inserted at or removed from.</param>
        /// <param name="count">number of prototypes added or removed.</param>
        public static void AdjustDetailIndex(TerrainData terrainData, bool isInsertion, int index, int count)
        {
            if (m_terrain == null || m_terrain.terrainData != terrainData)
            {
                return;
            }
            int detailIndex = DetailIndex;
            if (isInsertion)
            {
                if (detailIndex >= index)
                {
                    DetailIndex = detailIndex + count;
                }
            }
            else if (detailIndex > index + count - 1)
            {
                DetailIndex = detailIndex - count;
            }
            else if (detailIndex >= index)
            {
                DetailIndex = -1;
            }
        }

        /// <summary>
        /// Draws a terrain brush. Must be called from SceneView.duringSceneGUI during a repaint event.
        /// </summary>
        /// <param name="brush">brush to draw.</param>
        /// <param name="material">material to draw the brush with.</param>
        public static void DrawBrush(sfTerrainBrush brush, Material material)
        {
            if (Event.current.type != EventType.Repaint || brush == null || material == null || brush.Terrain == null)
            {
                return;
            }
            Texture2D texture = GetBrushTexture(brush.Index);
            if (texture == null)
            {
                return;
            }
            BrushTransform brushTransform = TerrainPaintUtility.CalculateBrushTransform(brush.Terrain, brush.Position,
                brush.Size, brush.Rotation);
            PaintContext paintContext = TerrainPaintUtility.BeginPaintHeightmap(brush.Terrain,
                brushTransform.GetBrushXYBounds(), 1);
            TerrainPaintUtilityEditor.DrawBrushPreview(paintContext, TerrainBrushPreviewMode.SourceRenderTexture,
                texture, brushTransform, material, 0);
            TerrainPaintUtility.ReleaseContextResources(paintContext);
        }

        /// <summary>
        /// Load the Unity Editor as the base editor when this editor is enabled.
        /// </summary>
        protected override void OnEnable()
        {
            m_instance = this;
            LoadBaseEditor("TerrainInspector");

            // Unity normally sets the active terrain inspector to this inspector in OnEnable, except it won't do it for
            // us because the terrain inspector isn't actually active--instead the override inspector is. We do this to
            // make Unity think the terrain inspector is the active inspector.
            if (m_roActiveTerrainInspectorField == null)
            {
                m_roActiveTerrainInspectorField = ReflectionEditor.GetField("s_activeTerrainInspector");
                m_roActiveTerrainInspectorInstanceField = ReflectionEditor.GetField("s_activeTerrainInspectorInstance");
            }
            m_roActiveTerrainInspectorField.SetValue(BaseEditor.GetInstanceID());
            m_roActiveTerrainInspectorInstanceField.SetValue(BaseEditor);

            if (m_state == States.OVERRIDE)
            {
                Initialize();
            }
        }

        /// <summary>Called when the editor is disabled.</summary>
        protected override void OnDisable()
        {
            if (m_state == States.OVERRIDE)
            {
                CleanUp();
            }
            if (m_instance == this)
            {
                m_instance = null;
            }
            base.OnDisable();
        }

        /// <summary>
        /// Initialization that runs when the editor is enabled during a session with terrain editing enabled.
        /// </summary>
        private void Initialize()
        {
            if (m_roSelectedTool == null)
            {
                LoadReflectionData();
            }
            m_terrain = target as Terrain;
            m_brushList = ReflectionEditor.GetProperty("brushList").GetValue();
            m_isPlaceTreeWizardOpen = false;
            sfUnityEventDispatcher.Get().OnUpdate += CheckTerrain;
            SceneView.duringSceneGui += OnSceneGUI;
            SceneView.beforeSceneGui += BeforeSceneGUI;
        }

        /// <summary>Clean up.</summary>
        private void CleanUp()
        {
            sfUnityEventDispatcher.Get().OnUpdate -= CheckTerrain;
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.beforeSceneGui -= BeforeSceneGUI;
            CheckTerrain(TERRAIN_CHECK_INTERVAL);

            EditorWindow treePlaceWizard = GetPlaceTreeWizard();
            if (treePlaceWizard != null)
            {
                treePlaceWizard.Close();
            }
            m_isPlaceTreeWizardOpen = false;
            m_brush.Terrain = null;
        }

        /// <summary>Loads reflection data.</summary>
        private void LoadReflectionData()
        {
#if UNITY_6000_0_OR_NEWER
            m_roSelectedTool = ReflectionEditor.GetProperty("selectedCategory");
            m_roGetActiveToolMethod = ReflectionEditor.GetMethod("GetActiveTerrainTool");
            m_roSelectCategory = ReflectionEditor.GetMethod("SelectCategory");
#else
            m_roSelectedTool = ReflectionEditor.GetProperty("selectedTool");
            m_roGetActiveToolMethod = ReflectionEditor.GetMethod("GetActiveTool");
#endif
            m_roTerrainColliderRaycastMethod = new ksReflectionObject(typeof(TerrainCollider)).GetMethod(
                "Raycast",
                BindingFlags.NonPublic | BindingFlags.Instance,
                new Type[] { typeof(Ray), typeof(RaycastHit).MakeByRefType(), typeof(float), typeof(bool) }
            );
            m_roCalcPixelRectFromBoundsMethod = new ksReflectionObject(typeof(TerrainPaintUtility)).GetMethod("CalcPixelRectFromBounds");
            ksReflectionObject roTerrainWizard = new ksReflectionObject(m_editorAssembly, "UnityEditor.TerrainWizard");
            // This field has a different name in some versions so try both names.
            m_roGetWindowTerrain = roTerrainWizard.GetField("terrain", true);
            if (m_roGetWindowTerrain.IsVoid)
            {
                m_roGetWindowTerrain = roTerrainWizard.GetField("m_Terrain");
            }
            m_onInspectorGUIEditContext = ReflectionEditor.GetField("onInspectorGUIEditContext").GetValue();
            m_roSelectedBrush = ReflectionEditor.GetProperty("brushList").GetField("m_SelectedBrush");
            m_roBrushSize = ReflectionEditor.GetProperty("brushSize");
        }

        /// <summary>Draws the inspector GUI.</summary>
        public override void OnInspectorGUI()
        {
            switch (m_state)
            {
                default: base.OnInspectorGUI(); break;
                case States.OVERRIDE: DrawOverridenInspectorGUI(); break;
                case States.DISABLED: DrawDisabledInspectorGUI(); break;
            }
        }

        /// <summary>Draws the overriden inspector GUI.</summary>
        private void DrawOverridenInspectorGUI()
        {
            // Terrain is always editable.
            target.hideFlags &= ~HideFlags.NotEditable;

            if (m_toolCategory == ToolCategories.TERRAIN_SETTINGS && IsTerrainLocked())
            {
                DrawDisabledSettings();
            }
            else if (m_toolCategory == ToolCategories.PLACE_TREE && IsTerrainLocked())
            {
                DrawDisabledTrees();
            }
            else
            {
                base.OnInspectorGUI();
            }

            // Update tool and brush
            GetTool(out m_toolCategory, out m_tool);
            UpdateBrush();
        }

        /// <summary>Draws a message saying terrain editing is disabled and displays an upgrade link.</summary>
        private void DrawDisabledInspectorGUI()
        {
            ksStyle.HelpBox(MessageType.Info, "Terrain editing is not enabled for your account. ", null,
                "Upgrade to enable terrain editing.", sfConfig.Get().Urls.Upgrade);
        }

        /// <summary>Records the event type before it is changed to USED in the OnSceneGUI event handlers.</summary>
        private void BeforeSceneGUI(SceneView sceneView)
        {
            m_sceneGuiEventType = Event.current.type;
            if (PreSceneGUI != null)
            {
                PreSceneGUI();
            }
        }

        /// <summary>Called when the scene GUI is drawn. Invokes terrain edit events based on user input.</summary>
        private void OnSceneGUI(SceneView sceneView)
        {
            if (m_terrain == null || m_terrain.gameObject.GetComponent<TerrainCollider>() == null)
            {
                return;
            }

            m_sceneGuiMouseButton = Event.current.button;
            m_sceneGuiMousePosition = Event.current.mousePosition;
            m_sceneGuiShift = Event.current.shift;
            m_sceneGuiCtrl = Event.current.control;
            UpdateBrushHit(sceneView);
            CheckTerrainEdits();
            m_sceneGuiMouseButton = -1;

            if (PostSceneGUI != null)
            {
                PostSceneGUI();
            }
        }

        /// <summary>Checks if the inspected terrain component is locked by another user.</summary>
        /// <returns>true if the terrain component is locked.</returns>
        private bool IsTerrainLocked()
        {
            if (SceneFusion.Get().Service.Session == null)
            {
                return false;
            }
            sfObject obj = sfObjectMap.Get().GetSFObject(target);
            return obj != null && obj.IsLocked;
        }

        /// <summary>
        /// Draws the terrain tool bar. Unity does not have a function to draw the toolbar without drawing the rest of
        /// the GUI, so we use this when we want to draw the toolbar and change the GUI that comes after.
        /// </summary>
        /// <returns>true if the tool bar was successfully drawn.</returns>
        private bool DrawToolBar()
        {
            if (m_commandStyle == null || m_toolIcons == null)
            {
                ksReflectionObject roStyles = ReflectionEditor.GetField("styles");
                if (roStyles.GetValue() == null)
                {
                    roStyles.SetValue(roStyles.GetConstructor().Invoke());
                }
                m_toolIcons = roStyles.GetField("toolIcons").GetValue() as GUIContent[];
                m_commandStyle = roStyles.GetField("command").GetValue() as GUIStyle;
                if (m_commandStyle == null || m_toolIcons == null)
                {
                    return false;
                }
            }

            if (m_roInitializeMethod == null)
            {
                m_roInitializeMethod = ReflectionEditor.GetMethod("Initialize", paramTypes: new Type[] { });
            }
            m_roInitializeMethod.InstanceInvoke(BaseEditor);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.changed = false;
            int tool = GUILayout.Toolbar(m_toolCategory, m_toolIcons, m_commandStyle, new GUILayoutOption[0]);
            if (tool != m_toolCategory)
            {
#if UNITY_6000_0_OR_NEWER
                m_roSelectCategory.Invoke(tool, true);
#else
                m_roSelectedTool.SetValue(BaseEditor, tool);
                Repaint();
#endif
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            return true;
        }

        /// <summary>Draws the disabled tree GUI.</summary>
        private void DrawDisabledTrees()
        {
            if (!DrawToolBar())
            {
                base.OnInspectorGUI();
                return;
            }
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Paint Trees");
            GUILayout.Label("Cannot paint trees while the terrain is locked.",
                EditorStyles.wordWrappedMiniLabel, new GUILayoutOption[0]);
            GUILayout.EndVertical();
            EditorGUILayout.Space();
            EditorGUI.BeginDisabledGroup(true);
            new ksReflectionObject(m_activeTool).Call("OnInspectorGUI", m_terrain, m_onInspectorGUIEditContext);
            EditorGUI.EndDisabledGroup();
        }

        /// <summary>Draws the GUI with the settings showing and disabled.</summary>
        private void DrawDisabledSettings()
        {
            if (!DrawToolBar())
            {
                base.OnInspectorGUI();
                return;
            }
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Terrain Settings");
            GUILayout.Label("Cannot edit terrain settings while the terrain is locked.",
                EditorStyles.wordWrappedMiniLabel, new GUILayoutOption[0]);
            GUILayout.EndVertical();
            EditorGUILayout.Space();
            EditorGUI.BeginDisabledGroup(true);
            if (m_roShowSettingsMethod == null)
            {
                m_roShowSettingsMethod = ReflectionEditor.GetMethod("ShowSettings");
            }
            m_roShowSettingsMethod.InstanceInvoke(BaseEditor);
            EditorGUI.EndDisabledGroup();
        }

        /// <summary>
        /// Periodically invokes a check for terrain data changes which do not have unity events. These include changes to:
        /// terrain layers, detail prototypes, tree prototypes, and detail resolution
        /// </summary>
        public void CheckTerrain(float deltaTime)
        {
            if (m_terrain == null)
            {
                return;
            }
            m_timeToPaletteCheck -= deltaTime;
            if (m_timeToPaletteCheck <= 0f)
            {
                m_timeToPaletteCheck = TERRAIN_CHECK_INTERVAL;
                sfUnityEventDispatcher.Get().InvokeTerrainCheck(m_terrain);
                SyncMassPlaceTrees();
            }
        }

        /// <summary>
        /// Checks for edits to details and trees by checking the brush state and mouse events and invokes events when
        /// the editing is finished.
        /// </summary>
        private void CheckTerrainEdits()
        {
            // Trigger change events once painting finishes (mouse is released or leaves the window).
            if (m_sceneGuiEventType == EventType.MouseUp || m_sceneGuiEventType == EventType.MouseLeaveWindow)
            {
                switch (m_tool)
                {
                    case Tools.PLACE_DETAIL:
                    {
                        InvokeDetailEdit();
                        break;
                    }
                    case Tools.PLACE_TREE:
                    {
                        InvokeTreeEdit();
                        sfTerrainTranslator translator = sfObjectEventDispatcher.Get()
                            .GetTranslator<sfTerrainTranslator>(sfType.Terrain);
                        if (translator != null)
                        {
                            translator.SendTerrainChanges(sfTerrainTranslator.TerrainType.TREES);
                        }
                        ReleaseTempLocks();
                        break;
                    }
                }
            }

            if ((m_tool != Tools.PLACE_DETAIL && m_tool != Tools.PLACE_TREE) || 
                m_sceneGuiMouseButton != 0 || m_brush.Terrain == null || 
                (m_sceneGuiEventType != EventType.MouseDown && m_sceneGuiEventType != EventType.MouseDrag))
            {
                return;
            }

            switch (m_tool)
            {
                case Tools.PLACE_DETAIL:
                {
                    ForEachPaintedTerrain(TrackDetailEdit);
                    break;
                }
                case Tools.PLACE_TREE:
                {
                    ForEachPaintedTerrain(TrackTreeEdit);
                    break;
                }
            }
        }

        /// <summary>
        /// Tracks detail edit info to invoke a details change event with once the user stops editing details.
        /// </summary>
        /// <param name="terrain">terrain being edited</param>
        /// <param name="bounds">bounds of the edit.</param>
        private void TrackDetailEdit(Terrain terrain, RectInt bounds)
        {
            // Change area and bool indicating if shift was held.
            KeyValuePair<RectInt, bool> editInfo;
            if (m_detailEdits.TryGetValue(terrain, out editInfo))
            {
                // Update the existing change area to include the new bounds.
                RectInt changeArea = new RectInt();
                changeArea.min = Vector2Int.Min(bounds.min, editInfo.Key.min);
                changeArea.max = Vector2Int.Max(bounds.max, editInfo.Key.max);
                m_detailEdits[terrain] = new KeyValuePair<RectInt, bool>(
                    changeArea, m_sceneGuiShift | editInfo.Value);
            }
            else
            {
                m_detailEdits[terrain] = new KeyValuePair<RectInt, bool>(bounds, m_sceneGuiShift);
            }
        }

        /// <summary>Invokes detail change events from stored detail edits infos.</summary>
        private void InvokeDetailEdit()
        {
            m_activeTool = m_roGetActiveToolMethod.InstanceInvoke(BaseEditor);
            foreach (KeyValuePair<Terrain, KeyValuePair<RectInt, bool>> edit in m_detailEdits)
            {
                Terrain terrain = edit.Key;
                RectInt changeArea = edit.Value.Key;
                // -1 is used to indicate all layers are dirty.
                int detailLayer = edit.Value.Value ? -1 : DetailIndex;
                if (terrain.terrainData != m_terrain.terrainData && detailLayer != -1)
                {
                    if (terrain.terrainData != null && m_terrain.terrainData != null && 
                        detailLayer < m_terrain.terrainData.detailPrototypes.Length)
                    {
                        DetailPrototype prototype = m_terrain.terrainData.detailPrototypes[detailLayer];
                        detailLayer = Array.IndexOf(terrain.terrainData.detailPrototypes, prototype);
                    }
                    else
                    {
                        detailLayer = -1;
                    }
                }
                sfUnityEventDispatcher.Get().InvokeOnTerrainDetailChange(terrain, changeArea, detailLayer);
            }
            m_detailEdits.Clear();
        }

        /// <summary>
        /// Tracks tree edit info to invoke a details change event with once the user stops editing trees. Requests a
        /// lock on the terrain if it isn't already locked.
        /// </summary>
        /// <param name="terrain">terrain being edited</param>
        /// <param name="bounds">bounds of the edit.</param>
        private void TrackTreeEdit(Terrain terrain, RectInt bounds)
        {
            bool removedTrees;
            if ((m_treeEdits.TryGetValue(terrain, out removedTrees) && removedTrees) ||
                terrain.terrainData == null)
            {
                return;
            }
            sfObject obj = sfObjectMap.Get().GetSFObject(terrain.terrainData);
            if (obj == null)
            {
                return;
            }
            sfListProperty treesProp = (sfListProperty)((sfDictionaryProperty)obj.Property)[sfProp.Trees];
            int numTrees = ((sfValueProperty)treesProp[0]).Value;
            if (numTrees == terrain.terrainData.treeInstanceCount)
            {
                return;
            }

            obj = sfObjectMap.Get().GetSFObject(terrain.gameObject);
            if (obj != null)
            {
                if (obj.IsLocked)
                {
                    // Call the property change handler to revert the property.
                    sfObjectEventDispatcher.Get().OnPropertyChange(treesProp);
                    return;
                }
                // If we haven't locked the terrain object, lock it until we finish the current tree paint action.
                if (obj.GetLockOwnerId() == 0u && !obj.IsLockPending)
                {
                    obj.RequestLock();
                    m_tempLockedObjects.Add(obj);
                }
            }

            if (numTrees > terrain.terrainData.treeInstanceCount)
            {
                m_treeEdits[terrain] = true;
            }
            else
            {
                m_treeEdits[terrain] = false;
            }
        }

        /// <summary>
        /// Invokes tree change events from stored tree edits infos. Reverts tree changes for locked terrain components.
        /// </summary>
        private void InvokeTreeEdit()
        {
            foreach (KeyValuePair<Terrain, bool> edit in m_treeEdits)
            {
                sfObject obj = sfObjectMap.Get().GetSFObject(edit.Key);
                if (obj != null && obj.IsLocked)
                {
                    // Revert the trees.
                    obj = sfObjectMap.Get().GetSFObject(edit.Key.terrainData);
                    if (obj != null)
                    {
                        sfListProperty treesProp = (sfListProperty)((sfDictionaryProperty)obj.Property)[sfProp.Trees];
                        sfObjectEventDispatcher.Get().OnPropertyChange(treesProp);
                    }
                }
                else
                {
                    sfUnityEventDispatcher.Get().InvokeOnTerrainTreeChange(edit.Key, edit.Value);
                }
            }
            m_treeEdits.Clear();
        }

        /// <summary>Gets a brush texture by index.</summary>
        /// <param name="brushIndex">brushIndex in brush list.</param>
        /// <returns>
        /// brush texture. If the index was greater than or equal to the size of the textures list,
        /// returns the first brush texture.
        /// </returns>
        private static Texture2D GetBrushTexture(int brushIndex)
        {
            if (brushIndex < 0)
            {
                return null;
            }
            // If we haven't selected a terrain yet, we won't have the brush list and need to load it.
            if (m_brushList == null)
            {
                m_brushList = new ksReflectionObject(m_editorAssembly, "UnityEditor.BrushList")
                    .Construct().GetValue();
            }
            if (m_roBrushList == null)
            {
                m_roBrushList = new ksReflectionObject(m_brushList).GetField("m_BrushList");
            }
            object[] brushes = m_roBrushList.GetValue(m_brushList) as object[];
            if (brushes == null || brushes.Length == 0)
            {
                return null;
            }
            object brush;
            if (brushIndex < brushes.Length)
            {
                brush = brushes[brushIndex];
            }
            else
            {
                brush = brushes[0];
                if (!m_missingBrushWarningShown)
                {
                    m_missingBrushWarningShown = true;
                    ksLog.Warning(LOG_CHANNEL, "Cannot get terrain brush " + brushIndex +
                        ". Terrain brush list size: " + brushes.Length + ". Using brush 0 instead.");
                }
            }
            if (m_roTexture == null)
            {
                m_roTexture = new ksReflectionObject(brush).GetProperty("texture");
            }
            return m_roTexture.GetValue(brush) as Texture2D;
        }

        /// <summary>
        /// Updates the brush hit position by raycasting from the mouse to see if any terrains are hit. If no terrains
        /// are hit or the selected tool doesn't use a brush, sets Brush.Terrain to null.
        /// </summary>
        private void UpdateBrushHit(SceneView sceneView)
        {
            m_brush.Terrain = null;
            if (m_tool == Tools.NONE || m_tool == Tools.TERRAIN_SETTINGS ||
                // Check if the mouse is in the scene view.
                !sfUI.Get().GetViewport(sceneView).Contains(m_sceneGuiMousePosition))
            {
                return;
            }
            float distance = float.MaxValue;
            Ray ray = HandleUtility.GUIPointToWorldRay(m_sceneGuiMousePosition);
            foreach (Terrain activeTerrain in Terrain.activeTerrains)
            {
                TerrainCollider collider = activeTerrain.GetComponent<TerrainCollider>();
                object[] parameters = new object[] { ray, null, distance, true };
                bool hit = (bool)m_roTerrainColliderRaycastMethod.InstanceInvoke(collider, parameters);
                RaycastHit hitInfo = (RaycastHit)parameters[1];

                if (hit && hitInfo.distance < distance)
                {
                    distance = hitInfo.distance;
                    m_brush.Terrain = activeTerrain;
                    m_brush.Position = hitInfo.textureCoord;
                }
            }
        }

        /// <summary>Updates the brush index, rotation, and size.</summary>
        private void UpdateBrush()
        {
            m_brush.Rotation = 0f;
            if (m_tool == Tools.PLACE_TREE)
            {
                m_brush.Index = TREE_BRUSH_INDEX;
                m_brush.Size = (float)new ksReflectionObject(m_activeTool).GetProperty("brushSize").GetValue();
            }
            else
            {
                m_brush.Index = (int)m_roSelectedBrush.GetValue(m_brushList);
                m_brush.Size = (float)m_roBrushSize.GetValue(BaseEditor);
                if (m_activeTool != null)
                {
                    // Common UI is part of the terrain tools package.
                    ksReflectionObject commonUI = new ksReflectionObject(m_activeTool).GetProperty("commonUI", true);
                    if (commonUI != ksReflectionObject.Void)
                    {
                        m_brush.Size = (float)commonUI.GetProperty("brushSize").GetValue();
                        m_brush.Rotation = (float)commonUI.GetProperty("brushRotation").GetValue();
                    }
                }
            }
        }

        /// <summary>Releases temporary locks.</summary>
        private void ReleaseTempLocks()
        {
            foreach (sfObject obj in m_tempLockedObjects)
            {
                UObject uobj = sfObjectMap.Get().GetUObject(obj);
                if (uobj == null || !Selection.Contains(uobj))
                {
                    obj.ReleaseLock();
                }
            }
            m_tempLockedObjects.Clear();
        }

        /// <summary>Iterates all terrains the brush overlaps.</summary>
        /// <param name="callback">callback to call for each terrain the brush is over.</param>
        private void ForEachPaintedTerrain(ForEachPaintedTerrainCallback callback)
        {
            if (m_brush.Terrain == null)
            {
                return;
            }
            int width, height;
            GetTerrainDataDimensions(m_brush.Terrain.terrainData, out width, out height);
            RectInt fullBounds = GetBrushBounds(width, height);

            // Clamp the bounds to the terrain
            RectInt bounds = fullBounds;
            bounds.min = Vector2Int.Max(bounds.min, Vector2Int.zero);
            bounds.max = Vector2Int.Min(bounds.max, new Vector2Int(width - 1, height - 1));
            callback(m_brush.Terrain, bounds);

            if (bounds.Equals(fullBounds))
            {
                // The brush bounds are within the terrain.
                return;
            }

            // Unity has a requirement that all terrains be the same size and aligned perfectly in a grid, otherwise
            // painting doesn't work properly. We calculate the grid coordinates of all terrains the brush overlaps
            // relative to the terrain the brush origin is on. Eg. (-2, 1) means the terrain 2 grid-cells to the left
            // and one above.
            List<Vector2Int> dirtyNeighbours = new List<Vector2Int>();
            int xMin = fullBounds.xMin >= 0 ? 0 : -1 -((-fullBounds.xMin - 1) / width);
            int xMax = (fullBounds.xMax - 1) / width;
            int yMin = fullBounds.yMin >= 0 ? 0 : -1 -((-fullBounds.yMin - 1) / height);
            int yMax = (fullBounds.yMax - 1) / height;
            for (int x = xMin; x <= xMax; x++)
            {
                for (int y = yMin; y <= yMax; y++)
                {
                    if (x != 0 || y != 0)
                    {
                        dirtyNeighbours.Add(new Vector2Int(x, y));
                    }
                }
            }

            TerrainMap terrainMap = TerrainMap.CreateFromPlacement(m_brush.Terrain, null, true);
            RectInt neighborBounds;
            foreach (Vector2Int coord in dirtyNeighbours)
            {
                Terrain neighborTerrain = terrainMap.GetTerrain(coord.x, coord.y);
                if (neighborTerrain != null)
                {
                    neighborBounds = CalculateNeighborEditBounds(coord, fullBounds, width, height);
                    callback(neighborTerrain, neighborBounds);
                }
            }
        }

        /// <summary>Gets the brush bounds.</summary>
        /// <param name="width">width of the terrain data.</param>
        /// <param name="height">height of the terrain data.</param>
        private RectInt GetBrushBounds(int width, int height)
        {
            BrushTransform brushTransform = TerrainPaintUtility.CalculateBrushTransform(
                m_brush.Terrain,
                m_brush.Position,
                m_brush.Size,
                m_brush.Rotation
            );

            return (RectInt)m_roCalcPixelRectFromBoundsMethod.Invoke(
                m_brush.Terrain,
                brushTransform.GetBrushXYBounds(),
                Math.Max(1, width),
                Math.Max(1, height),
                0,
                false
            );
        }

        /// <summary>Gets the terrain data dimensions for the current terrain tool.</summary>
        /// <param name="terrainData">terrainData to get size from.</param>
        /// <param name="width">width of the data for the current tool. -1 if the tool has no data.</param>
        /// <param name="height">height of the data forthe current tool. -1 if the tool has no data.</param>
        private void GetTerrainDataDimensions(TerrainData terrainData, out int width, out int height)
        {
            width = -1;
            height = -1;
            switch (m_tool)
            {
                case Tools.PAINT_HEIGHT:
                case Tools.SET_HEIGHT:
                case Tools.SMOOTH_HEIGHT:
                {
                    width = terrainData.heightmapResolution;
                    height = terrainData.heightmapResolution;
                    break;
                }
                case Tools.PAINT_TEXTURE:
                {
                    width = terrainData.alphamapWidth;
                    height = terrainData.alphamapHeight;
                    break;
                }
                case Tools.PLACE_DETAIL:
                {
                    width = terrainData.detailWidth;
                    height = terrainData.detailHeight;
                    break;
                }
                case Tools.PAINT_HOLE:
                {
                    width = terrainData.holesResolution;
                    height = terrainData.holesResolution;
                    break;
                }
                case Tools.PLACE_TREE:
                {
                    width = Math.Max(2, (int)terrainData.size.x);
                    height = Math.Max(2, (int)terrainData.size.z);
                    break;
                }
            }
        }

        /// <summary>
        /// Calculates the bounds of possible changes on the neighbor terrain at the given coordinate.
        /// </summary>
        /// <param name="coord">coord for neighbour terrain relative to the original terrain.</param>
        /// <param name="bounds">bounds relative to the original terrain.</param>
        /// <param name="width">width of the terrain</param>
        /// <param name="height">height of the terrain</param>
        /// <returns>neightbour edit bounds.</returns>
        private RectInt CalculateNeighborEditBounds(
            Vector2Int coord,
            RectInt bounds,
            int width,
            int height)
        {
            bounds.x -= coord.x * width;
            bounds.y -= coord.y * height;
            bounds.min = Vector2Int.Max(bounds.min, Vector2Int.zero);
            bounds.max = Vector2Int.Min(bounds.max, new Vector2Int(width - 1, height - 1));
            return bounds;
        }

        /// <summary>Gets selected tool index.</summary>
        /// <returns></returns>
        private void GetTool(out int toolCategory, out int tool)
        {
            toolCategory = (int)m_roSelectedTool.GetValue(BaseEditor);

            m_activeTool = null;
            switch (toolCategory)
            {
                case ToolCategories.CREATE_NEIGHBOR:
                {
                    m_activeTool = m_roGetActiveToolMethod.InstanceInvoke(BaseEditor);
                    tool = Tools.NONE;
                    break;
                }
                case ToolCategories.PAINT:
                {
                    m_activeTool = m_roGetActiveToolMethod.InstanceInvoke(BaseEditor);
                    string toolName = m_activeTool.ToString();
                    toolName = toolName.Substring((" (" + TERRAIN_TOOLS_NAMESPACE + ".").Length);
                    toolName = toolName.TrimEnd(')');

                    if (toolName == "PaintTextureTool")
                    {
                        tool = Tools.PAINT_TEXTURE;
                    }
                    else if (m_heightPaintingTools.Contains(toolName))
                    {
                        tool = Tools.PAINT_HEIGHT;
                    }
                    else if (m_transformTools.Contains(toolName))
                    {
                        tool = Tools.TRANSFORM;
                    }
                    else if (toolName == "PaintHolesTool")
                    {
                        tool = Tools.PAINT_HOLE;
                    }
                    else { 
                        tool = Tools.NONE;
                    }
                    break;
                }
                case ToolCategories.PLACE_TREE:
                {
                    m_activeTool = m_roGetActiveToolMethod.InstanceInvoke(BaseEditor);
                    tool = Tools.PLACE_TREE;
                    break;
                }
                case ToolCategories.PAINT_DETAIL:
                {
                    m_activeTool = m_roGetActiveToolMethod.InstanceInvoke(BaseEditor);
                    tool = Tools.PLACE_DETAIL;
                    break;
                }
                case ToolCategories.TERRAIN_SETTINGS:
                default:
                {
                    tool = Tools.NONE;
                    break;
                }
            }
        }

        /// <summary>
        /// Detect if the mass place trees window was closed since the last time we checked. If it was closed
        /// then resync all trees.
        /// </summary>
        private void SyncMassPlaceTrees()
        {
            // If the PlaceTreeWizard window was closed then check if trees were added to the terrain.
            bool isTreePlaceWizardOpen = GetPlaceTreeWizard() != null;
            if (m_isPlaceTreeWizardOpen != isTreePlaceWizardOpen)
            {
                m_isPlaceTreeWizardOpen = isTreePlaceWizardOpen;
                if (!m_isPlaceTreeWizardOpen)
                {
                    sfUnityEventDispatcher.Get().InvokeOnTerrainTreeChange(m_terrain, true);
                }
            }
        }

        /// <summary>Check if the current selected terrain has an active PlaceTreeWizard window open.</summary>
        /// <returns>PlaceTreeWizard associated with the current terrain.</returns>
        private EditorWindow GetPlaceTreeWizard()
        {
            UnityEngine.Object[] windows = ksEditorUtils.FindWindows("PlaceTreeWizard", "UnityEditor");
            if (windows.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < windows.Length; ++i)
            {
                if (windows[i] == null)
                {
                    continue;
                }

                Terrain component = m_roGetWindowTerrain.GetValue(windows[i]) as Terrain;
                if (component == target)
                {
                    return windows[i] as EditorWindow;
                }
            }
            return null;
        }
    }
}
