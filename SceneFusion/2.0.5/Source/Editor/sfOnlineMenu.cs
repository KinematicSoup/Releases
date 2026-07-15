/*
KINEMATICSOUP CONFIDENTIAL
 Copyright(c) 2014-2026 KinematicSoup Technologies Incorporated 
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
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using KS.SF.Unity.Editor;
using KS.SF.Reactor;
using System;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>GUI that shows while connected to a session.</summary>
    public partial class sfOnlineMenu
    {
        // Player icon size. Unity actually renders it 4 pixels smaller than this because Unity.
        private const float ICON_SIZE = 19f;
        // Player icon y offset in pixels.
        private const float ICON_OFFSET_Y = 0f;
        // Minimum width reserved for fields after the field label. This number was chosen to give enough space to
        // display the Go To and Follow buttons and have the text be visible.
        private const float MIN_FIELD_WIDTH = 95f;
        // Padding between Go To and Follow buttons.
        private const float BUTTON_PADDING = 2f;

        public static sfUI.Delegates.AllowLeaveSession AllowLeaveSession = null;
        public static sfUI.Delegates.DrawSettings DrawSettings = null;

        private static uint m_followedUserId;

        private bool m_disconnect = false;
        private bool m_usersExpanded = true;
        private Vector2 m_scrollPosition;
        private sfSessionTableGUI m_sessionTable = sfSessionTableGUI.Instance;
        
        private sfUndoableColor m_undoableColor;
        private Rect m_colorFieldPosition;
        
        private string m_sceneName = "";
        private string m_sceneCreator = "";
        private DateTime m_sceneStart = DateTime.Now;

        private sfUIUserInfo m_localUserInfo;
        private Dictionary<uint, sfUIUserInfo> m_nonLocalUserInfos = new Dictionary<uint, sfUIUserInfo>();

        /// <summary>Service</summary>
        private static sfService Service
        {
            get { return SceneFusion.Get().Service; }
        }

        /// <summary>Icon</summary>
        override public Texture Icon
        {
            get { return sfTextures.Logo; }
        }

        /// <summary>Unity on disable. Cleans up.</summary>
        private void OnDisable()
        {
            m_nonLocalUserInfos.Clear();
            if (Service != null)
            {
                Service.UnregisterUserInfoChangeCallback(OnUserInfoChanged);
            }
        }

        /// <summary>Called when the menu is opened. Runs initialization.</summary>
        /// <param name="window">window that shows this menu.</param>
        public override void OnOpen(ksWindow window)
        {
            sfUI.Get().OnUnfollowUserCamera = OnUnfollow;
            List<sfUIUserInfo> infos = Service.GetAllUserInfos();
            if (infos != null)
            {
                foreach (sfUIUserInfo info in infos)
                {
                    if (info.IsLocal)
                    {
                        m_localUserInfo = info;
                        continue;
                    }
                    m_nonLocalUserInfos[info.Id] = info;
                }
            }
            Service.RegisterUserInfoChangeCallback(OnUserInfoChanged);
        }

        /// <summary>Destroy this object when the menu is closed.</summary>
        /// <param name="window">window that closed the menu.</param>
        public override void OnClose(ksWindow window)
        {
            m_followedUserId = 0;
            if (m_undoableColor != null)
            {
                DestroyImmediate(m_undoableColor);
            }
        }

        /// <summary>Creates the GUI.</summary>
        /// <param name="window">window the GUI is for.</param>
        public override void OnDraw(ksWindow window)
        {
            if (Event.current.type == EventType.Layout)
            {
                // changing the layout is only safe during layout events.
                if (m_disconnect && Service != null)
                {
                    Service.LeaveSession();
                }
                if (Service != null && !Service.IsConnected)
                {
                    if (window.Id == ksWindow.SCENE_FUSION_MAIN)
                    {
                        window.Menu = ScriptableObject.CreateInstance<sfSessionsMenu>();
                    }
                    window.Menu.Draw(window);
                    return;
                }
                if (Service != null && Service.SessionInfo != null)
                {
                    m_sceneName = Service.SessionInfo.SceneName;
                    m_sceneCreator = Service.SessionInfo.Creator;
                    m_sceneStart = Service.SessionInfo.StartTime;
                }
            }

            // Set the label width and minimum field width.
            float oldLabelWidth = EditorGUIUtility.labelWidth;
            float oldFieldWidth = EditorGUIUtility.fieldWidth;
            EditorGUIUtility.labelWidth = sfUI.Get().GetInspectorLabelWidth();
            EditorGUIUtility.fieldWidth = MIN_FIELD_WIDTH;

            try
            {
                m_scrollPosition = EditorGUILayout.BeginScrollView(m_scrollPosition);
                EditorGUILayout.Space();
                m_sessionTable.DrawHeader("Creator", "Start Time");
                if (m_sessionTable.DrawRow("Leave", m_sceneCreator, m_sceneStart.ToString()))
                {
                    if (AllowLeaveSession == null || AllowLeaveSession())
                    {
                        m_disconnect = true;
                    }
                }

                if (DrawSettings != null)
                {
                    DrawSettings();
                }

                m_usersExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(m_usersExpanded,
                    "Users (" + (m_nonLocalUserInfos.Count + 1) + ")");
                if (m_usersExpanded)
                {
                    EditorGUI.indentLevel++;
                    DrawPlayer(m_localUserInfo);
                    foreach (sfUIUserInfo info in m_nonLocalUserInfos.Values)
                    {
                        DrawPlayer(info);
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndScrollView();

                sfSessionFooterUI.Get().DrawFooter();

                window.Repaint();
            }
            finally
            {
                EditorGUIUtility.labelWidth = oldLabelWidth;
                EditorGUIUtility.fieldWidth = oldFieldWidth;
            }
        }


        /// <summary>
        /// Draws the given user's name and icon. If the user is the local user, draws a color picker, otherwise draws
        /// Go To and Follow buttons.
        /// </summary>
        /// <param name="userInfo"></param>
        private void DrawPlayer(sfUIUserInfo userInfo)
        {
            if (userInfo == null)
            {
                // User info is set via RPC instead of during authentication when we use mock web service,
                // so it may not be set until a few frames after the user connects, in which case we display nothing.
                return;
            }

            Rect rect = EditorGUILayout.GetControlRect();
            float width = rect.width + rect.x;
            float fieldX = rect.x + EditorGUIUtility.labelWidth + 2f;
            rect = EditorGUI.IndentedRect(rect);

            // Draw the user icon.
            Rect iconRect = rect;
            iconRect.y += ICON_OFFSET_Y;
            iconRect.width = ICON_SIZE;
            iconRect.height = ICON_SIZE;
            sfDrawUtils.Instance.DrawUserIcon(iconRect, userInfo.Color);

            // Draw the user name.
            rect.x += ICON_SIZE;
            rect.width = EditorGUIUtility.labelWidth - ICON_SIZE;
            GUI.Label(rect, userInfo.Name);
            rect.x = fieldX;
            rect.width = Mathf.Max(width - rect.x, EditorGUIUtility.fieldWidth);

            // Draw a color field for the local user.
            if (userInfo.IsLocal)
            {
                if (m_undoableColor == null)
                {
                    m_undoableColor = ScriptableObject.CreateInstance<sfUndoableColor>();
                    m_undoableColor.hideFlags = HideFlags.DontSave;
                    m_undoableColor.Color = userInfo.Color;
                }

                m_undoableColor.Draw(rect, 
                    "Your user color. Other users will see objects you select shaded with this color.");

                string focus = "";
                if (EditorWindow.focusedWindow != null)
                {
                    focus = EditorWindow.focusedWindow.ToString();
                }
                // If color changed, set user color on server.
                if (m_undoableColor.Color != userInfo.Color
                    && focus != " (UnityEditor.ColorPicker)")
                {
                    userInfo.Color = m_undoableColor.Color;
                    SetUserColor(userInfo.Color);
                }
            }
            else
            {
                // Draw the Go To and Follow buttons.
                rect.width = (rect.width - BUTTON_PADDING) / 2f;
                SceneView sceneView = SceneView.lastActiveSceneView;
                if (GUI.Button(rect, new GUIContent("Go To",
                    "Move your camera to the location of this user's camera.")) && sceneView != null)
                {
                    sfUI.Get().GotoUserCamera(userInfo.Id);
                    m_followedUserId = 0;
                }

                rect.x += rect.width + BUTTON_PADDING;
                bool following = userInfo.Id == m_followedUserId;
                bool willFollow = GUI.Toggle(rect, following, new GUIContent("Follow",
                    "Keep your camera following this user's camera."), "Button");
                if (willFollow != following)
                {
                    m_followedUserId = sfUI.Get().FollowUserCamera(userInfo.Id, willFollow);
                }
            }
        }

        /// <summary>Called when the user info changed. Updates the user info dictionary.</summary>
        /// <param name="info"></param>
        /// <param name="type"></param>
        private void OnUserInfoChanged(sfUIUserInfo info, sfBaseService.UserInfoChangeType type)
        {
            switch (type)
            {
                case sfBaseService.UserInfoChangeType.Join:
                {
                    if (info.IsLocal)
                    {
                        m_localUserInfo = info;
                    }
                    else
                    {
                        m_nonLocalUserInfos[info.Id] = info;
                    }
                    break;
                }
                case sfBaseService.UserInfoChangeType.Leave:
                {
                    m_nonLocalUserInfos.Remove(info.Id);
                    break;
                }
                case sfBaseService.UserInfoChangeType.Change:
                {
                    if (info.IsLocal)
                    {
                        m_localUserInfo = info;
                    }
                    else
                    {
                        m_nonLocalUserInfos[info.Id] = info;
                    }
                    break;
                }
            }
        }

        /// <summary>Called when the user stopped following another user's camera.</summary>
        public static void OnUnfollow()
        {
            m_followedUserId = 0;
        }
    }
}
