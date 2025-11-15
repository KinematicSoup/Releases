using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AnimatedValues;
using KS.SF.Reactor;
using KS.SceneFusion;
using KS.SceneFusion2.Client;
using KS.SF.Unity.Editor;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>Renders the terrain brushes from other users and sends terrain brush data for the local user.</summary>
    public class sfTerrainBrushTranslator : sfBaseTranslator
    {
        private sfObject m_localBrushObject;
        private sfTerrainBrush m_lastBrushState;
        private List<sfObject> m_brushObjects = new List<sfObject>();
        private Dictionary<uint, Material> m_userMaterials = new Dictionary<uint, Material>();

        /// <summary>Called after connecting to a session.</summary>
        public override void OnSessionConnect()
        {
            sfUnityEventDispatcher.Get().OnUpdate += Update;
            SceneFusion.Get().Service.Session.OnUserColorChange += OnUserColorChange;
            SceneFusion.Get().Service.Session.OnUserLeave += OnUserLeave;
            sfConfig.Get().UI.OnShowUserTerrainBrushesChange += OnShowBrushesChange;
            SceneView.duringSceneGui += DrawBrushes;
            sfTerrainEditor.MissingBrushWarningShown = false;
            m_lastBrushState = new sfTerrainBrush();
        }

        /// <summary>Called after disconnecting from a session.</summary>
        public override void OnSessionDisconnect()
        {
            sfUnityEventDispatcher.Get().OnUpdate -= Update;
            SceneFusion.Get().Service.Session.OnUserColorChange -= OnUserColorChange;
            SceneFusion.Get().Service.Session.OnUserLeave -= OnUserLeave;
            sfConfig.Get().UI.OnShowUserTerrainBrushesChange -= OnShowBrushesChange;
            SceneView.duringSceneGui -= DrawBrushes;
            m_localBrushObject = null;
            foreach (Material material in m_userMaterials.Values)
            {
                UObject.DestroyImmediate(material);
            }
            m_userMaterials.Clear();
        }

        /// <summary>Called when an object is created by another user.</summary>
        /// <param name="obj">obj that was created.</param>
        /// <param name="childIndex">childIndex of new object. -1 if object is a root.</param>
        public override void OnCreate(sfObject obj, int childIndex)
        {
            sfUI.Get().MarkSceneViewStale();
            m_brushObjects.Add(obj);
        }

        /// <summary>Called when an object is deleted by another user.</summary>
        /// <param name="obj">obj that was deleted.</param>
        public override void OnDelete(sfObject obj)
        {
            sfUI.Get().MarkSceneViewStale();
            m_brushObjects.Remove(obj);
        }

        /// <summary>Called when an object property changes.</summary>
        /// <param name="property">property that changed.</param>
        public override void OnPropertyChange(sfBaseProperty property)
        {
            sfUI.Get().MarkSceneViewStale();
        }

        /// <summary>Called when the show user terrain brushes config setting changes.</summary>
        /// <param name="value"></param>
        private void OnShowBrushesChange(sfConfig.ShowTerrainBrushOptions value)
        {
            sfUI.Get().MarkSceneViewStale();
        }

        /// <summary>Called when a user's color changes. Updates the user's terrain brush material color.</summary>
        /// <param name="user"></param>
        private void OnUserColorChange(sfUser user)
        {
            Material material = GetUserMaterial(user.Id);
            if (material != null)
            {
                material.SetColor("m_colour", user.Color);
            }
        }

        /// <summary>Called when a user leaves the session. Destroys the user's terrain brush material.</summary>
        /// <param name="user"></param>
        private void OnUserLeave(sfUser user)
        {
            Material material;
            if (m_userMaterials.Remove(user.Id, out material))
            {
                UObject.DestroyImmediate(material);
            }
        }

        /// <summary>Gets the terrain brush material for a user. Creates one if it does not already exist.</summary>
        /// <param name="userId">userId of user to get material for.</param>
        /// <returns>for the user's terrain brush.</returns>
        private Material GetUserMaterial(uint userId)
        {
            Material material;
            if (!m_userMaterials.TryGetValue(userId, out material))
            {
                material = UObject.Instantiate(sfUserMaterials.BrushPreviewMaterial);
                material.hideFlags = HideFlags.HideAndDontSave;
                material.name = "User" + userId + "BrushMaterial";
                m_userMaterials[userId] = material;

                sfUser user = SceneFusion.Get().Service.Session.GetUser(userId);
                if (user != null)
                {
                    OnUserColorChange(user);
                }
            }
            return material;
        }

        /// <summary>Draws the terrain brushes for other users.</summary>
        /// <param name="sceneView"></param>
        private void DrawBrushes(SceneView sceneView)
        {
            if (Event.current.type != EventType.Repaint || m_brushObjects.Count == 0 || 
                sfConfig.Get().UI.ShowUserTerrainBrushes == sfConfig.ShowTerrainBrushOptions.NEVER ||
                (sfConfig.Get().UI.ShowUserTerrainBrushes == sfConfig.ShowTerrainBrushOptions.WHEN_TERRAIN_SELECTED &&
                !sfTerrainEditor.IsOpen))
            {
                return;
            }
            sfTerrainBrush brush = new sfTerrainBrush();
            for (int i = 0; i < m_brushObjects.Count; i++)
            {
                sfDictionaryProperty properties = (sfDictionaryProperty)m_brushObjects[i].Property;
                brush.Terrain = sfObjectMap.Get().Get<Terrain>(SceneFusion.Get().Service.Session.GetObject(
                    ((sfReferenceProperty)properties[sfProp.Terrain]).ObjectId));
                if (brush.Terrain == null)
                {
                    continue;
                }
                brush.Index = (int)properties[sfProp.Index];
                brush.Position = properties[sfProp.Position].As<Vector2>();
                brush.Rotation = (float)properties[sfProp.Rotation];
                brush.Size = (float)properties[sfProp.Scale];
                sfTerrainEditor.DrawBrush(brush, GetUserMaterial((uint)properties[sfProp.UserId]));
            }
        }

        /// <summary>Called every update. Sends updates for the local user's terrain brush.</summary>
        /// <param name="deltaTime">deltaTime in seconds since the last update.</param>
        private void Update(float deltaTime)
        {
            sfTerrainBrush brush = sfTerrainEditor.Brush;
            if (brush.Terrain == null)
            {
                if (m_localBrushObject != null && m_localBrushObject.IsSyncing)
                {
                    SceneFusion.Get().Service.Session.Delete(m_localBrushObject);
                }
            }
            else
            {
                if (m_localBrushObject == null)
                {
                    sfDictionaryProperty dict = new sfDictionaryProperty();
                    dict[sfProp.UserId] = SceneFusion.Get().Service.Session.LocalUserId;
                    m_localBrushObject = new sfObject(sfType.TerrainBrush, dict, sfObject.ObjectFlags.TRANSIENT);
                }
                if (m_localBrushObject.IsDeletePending)
                {
                    return;
                }

                sfDictionaryProperty properties = (sfDictionaryProperty)m_localBrushObject.Property;
                if (!m_localBrushObject.IsSyncing || m_lastBrushState.Terrain != brush.Terrain)
                {
                    properties[sfProp.Terrain] = sfPropertyUtils.FromReference(brush.Terrain);
                    m_lastBrushState.Terrain = brush.Terrain;
                }
                if (!m_localBrushObject.IsSyncing || m_lastBrushState.Index != brush.Index)
                {
                    properties[sfProp.Index] = brush.Index;
                    m_lastBrushState.Index = brush.Index;
                }
                if (!m_localBrushObject.IsSyncing || m_lastBrushState.Position != brush.Position)
                {
                    properties[sfProp.Position] = new sfValueProperty(brush.Position);
                    m_lastBrushState.Position = brush.Position;
                }
                if (!m_localBrushObject.IsSyncing || m_lastBrushState.Rotation != brush.Rotation)
                {
                    properties[sfProp.Rotation] = brush.Rotation;
                    m_lastBrushState.Rotation = brush.Rotation;
                }
                if (!m_localBrushObject.IsSyncing || m_lastBrushState.Size != brush.Size)
                {
                    properties[sfProp.Scale] = brush.Size;
                    m_lastBrushState.Size = brush.Size;
                }

                if (!m_localBrushObject.IsSyncing)
                {
                    SceneFusion.Get().Service.Session.Create(m_localBrushObject);
                }
            }
        }
    }
}
