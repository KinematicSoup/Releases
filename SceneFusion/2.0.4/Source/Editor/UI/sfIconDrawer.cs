using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using KS.SceneFusion;
using KS.SceneFusion2.Client;
using KS.SF.Reactor;
using KS.SF.Unity.Editor;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>
    /// Draws icons in the hierarchy and project browser indicating the sync/lock/notification status of objects.
    /// </summary>
    public class sfIconDrawer
    {
        /// <summary></summary>
        /// <returns>singleton instance</returns>
        public static sfIconDrawer Get()
        {
            return m_instance;
        }
        private static sfIconDrawer m_instance = new sfIconDrawer();

        // The icons are actually 16x16, but for some reason Unity shrinks them by 1.
        private const float ICON_SIZE = 17f;

        /// <summary>Singleton constructor</summary>
        private sfIconDrawer()
        {
        }

        /// <summary>Starts drawing icons.</summary>
        public void Start()
        {
            EditorApplication.hierarchyWindowItemOnGUI += DrawHierarchyItem;
            // ProjectWindowItemInstanceOnGUI is preferrable to projectWindowItemOnGUI because it distinguishes between
            // assets and subassets. It does not exist prior to to 2022.1, so, in older versions on Unity, all
            // subassets will have the same icon as the main asset since we cannot tell if we are drawing the main
            // asset or a subasset.
#if UNITY_2022_1_OR_NEWER
            EditorApplication.projectWindowItemInstanceOnGUI += DrawProjectItem;
#else
            EditorApplication.projectWindowItemOnGUI += DrawProjectItem;
#endif
            sfConfig.Get().UI.OnHierarchyIconOffsetChange += HandleHierarchyOffsetChange;
            sfConfig.Get().UI.OnProjectBrowserIconOffsetChange += HandleProjectBrowserOffsetChange;
        }

        /// <summary>Stops drawing icons.</summary>
        public void Stop()
        {
            EditorApplication.hierarchyWindowItemOnGUI -= DrawHierarchyItem;
#if UNITY_2022_1_OR_NEWER
            EditorApplication.projectWindowItemInstanceOnGUI -= DrawProjectItem;
#else
            EditorApplication.projectWindowItemOnGUI -= DrawProjectItem;
#endif
            sfConfig.Get().UI.OnHierarchyIconOffsetChange -= HandleHierarchyOffsetChange;
            sfConfig.Get().UI.OnProjectBrowserIconOffsetChange -= HandleProjectBrowserOffsetChange;
            sfUI.Get().MarkProjectBrowserStale();
        }

        /// <summary>Draws icons for a game object in the hierarchy window.</summary>
        /// <param name="instanceId">instanceId of game object to draw icons for.</param>
        /// <param name="area">area the game object label was drawn in.</param>
        private void DrawHierarchyItem(int instanceId, Rect area)
        {
            UObject uobj = EditorUtility.InstanceIDToObject(instanceId);
            if (uobj == null)
            {
                return;
            }
            area.x += area.width + 1f - ICON_SIZE - sfConfig.Get().UI.HierarchyIconOffset;
            area.y -= 1f;
            area.width = ICON_SIZE;
            DrawIcons(uobj, area);
        }

#if UNITY_2022_1_OR_NEWER
        /// <summary>Draws icons for an asset in the project browser.</summary>
        /// <param name="instanceId">instanceId of asset to draw icons for.</param>
        /// <param name="area">area the asset was drawn in.</param>
        private void DrawProjectItem(int instanceId, Rect area)
        {
            UObject asset = EditorUtility.InstanceIDToObject(instanceId);
            DrawProjectItem(asset, area);
        }
#else
        /// <summary>Draws icons for an asset in the project browser.</summary>
        /// <param name="guid">guid of the asset to draw icons for.</param>
        /// <param name="area">area the asset was drawn in.</param>
        private void DrawProjectItem(string guid, Rect area)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            UObject asset = AssetDatabase.LoadMainAssetAtPath(path);
            DrawProjectItem(asset, area);
        }
#endif
        /// <summary>Draws icons for an asset in the project browser.</summary>
        /// <param name="asset">asset to draw icons for.</param>
        /// <param name="area">area the asset was drawn in.</param>
        private void DrawProjectItem(UObject asset, Rect area)
        {
            if (asset == null)
            {
                return;
            }
            area.x += Math.Clamp(area.width - ICON_SIZE - 1f - sfConfig.Get().UI.ProjectBrowserIconOffset.x,
                0, Mathf.Max(0, area.width - ICON_SIZE + 2f));
            area.y += Math.Clamp(area.height - 32f - sfConfig.Get().UI.ProjectBrowserIconOffset.y,
                0, Mathf.Max(0f, area.height - ICON_SIZE));
            area.width = ICON_SIZE;
            area.height = ICON_SIZE;
            DrawIcons(asset, area);
        }

        /// <summary>Draws icons for an object.</summary>
        /// <param name="uobj">uobj to draw icons for.</param>
        /// <param name="area">area to draw in.</param>
        private void DrawIcons(UObject uobj, Rect area)
        {
            sfObject obj = sfObjectMap.Get().GetSFObject(uobj);
            if (sfConfig.Get().SyncPrefabs && obj == null)
            {
                // If the object is a stage object, get the prefab object instead.
                UObject prefab = sfPrefabStageMap.Get().GetPrefabObject(uobj);
                if (prefab != null)
                {
                    obj = sfObjectMap.Get().GetSFObject(prefab);
                    uobj = prefab;
                }
            }
            if (obj != null && obj.IsCreated)
            {
                DrawSyncedIcon(uobj, obj, area);
                area.x -= area.width - 1f;
            }

            ksLinkedList<sfNotification> notifications = sfNotificationManager.Get().GetNotifications(uobj, true);
            if (notifications != null && notifications.Count > 0)
            {
                // The notification icon has an empty border around it. We need to increase the size by 1 to make it
                // display the same size as our other icons.
                area.width += 1f;
                area.height += 1f;
                DrawNotificationIcon(notifications, area);
            }
        }

        /// <summary>Draws either a green checkmark icon or a lock icon for an object based on its lock state.</summary>
        /// <param name="uobj">uobj to draw icon for.</param>
        /// <param name="obj">obj for the uobject.</param>
        /// <param name="area">area to draw icon in.</param>
        private void DrawSyncedIcon(UObject uobj, sfObject obj, Rect area)
        {
            Texture2D icon;
            string tooltip;
            if (obj.CanEdit)
            {
                icon = sfTextures.Check;
                tooltip = "Synced and unlocked";
            }
            else if (obj.IsFullyLocked)
            {
                icon = sfTextures.Lock;
                tooltip = "Fully locked by " + obj.LockOwner.Name + ". Property and child editing disabled.";
                GUI.color = obj.LockOwner.Color;
            }
            else
            {
                icon = sfTextures.Lock;
                tooltip = "Partially Locked. Property editing disabled.";
                if (Event.current.type == EventType.ContextClick)
                {
                    GameObject gameObject = uobj as GameObject;
                    if (gameObject != null)
                    {
                        // Temporarly make the object editable so we can add children to it via the context menu.
                        sfGameObjectTranslator translator = sfObjectEventDispatcher.Get()
                            .GetTranslator<sfGameObjectTranslator>(sfType.GameObject);
                        if (translator != null)
                        {
                            translator.TempUnlock(gameObject);
                        }
                    }
                }
            }

            GUI.Label(area, new GUIContent(icon, tooltip));
            if (obj.IsFullyLocked)
            {
                GUI.color = Color.white;
            }
        }

        /// <summary>Draws a notification icon for an object.</summary>
        /// <param name="uobj">uobj to draw icon for.</param>
        /// <param name="obj">obj for the uobject.</param>
        /// <param name="area">area to draw icon in.</param>
        private void DrawNotificationIcon(ksLinkedList<sfNotification> notifications, Rect area)
        {
            string tooltip = null;
            Texture2D icon = ksStyle.GetHelpBoxIcon(MessageType.Warning);
            HashSet<sfNotificationCategory> categories = new HashSet<sfNotificationCategory>();
            foreach (sfNotification notification in notifications)
            {
                if (categories.Add(notification.Category))
                {
                    if (tooltip == null)
                    {
                        tooltip = notification.Category.Name;
                    }
                    else
                    {
                        tooltip += "\n" + notification.Category.Name;
                    }
                }
            }
            GUI.Label(area, new GUIContent(icon, tooltip));
        }

        /// <summary>Called when the hierarchy icon offset changes. Refreshes the hierarchy window.</summary>
        /// <param name="offset"></param>
        private void HandleHierarchyOffsetChange(float offset)
        {
            sfHierarchyWatcher.Get().MarkHierarchyStale();
        }

        /// <summary>Called when the project browser icon offset changes. Refreshes the project browser.</summary>
        /// <param name="offset"></param>
        private void HandleProjectBrowserOffsetChange(Vector2 offset)
        {
            sfUI.Get().MarkProjectBrowserStale();
        }
    }
}
