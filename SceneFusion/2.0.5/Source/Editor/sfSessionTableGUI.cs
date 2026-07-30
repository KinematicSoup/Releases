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
using System;
using UnityEngine;
using UnityEditor;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>GUI utility for displaying a session table.</summary>
    internal class sfSessionTableGUI
    {
        /// <summary>Singleton instance</summary>
        public static sfSessionTableGUI Instance
        {
            get { return m_instance; }
        }
        private static sfSessionTableGUI m_instance = new sfSessionTableGUI();

        // small label column width in pixels
        private const float SMALL_LABEL_WIDTH = 60f;
        // button width in pixels
        private const float BUTTON_WIDTH = 35f;
        private const float BUTTON_HEIGHT = 16f;

        /// <summary>Callback for when a button is clicked.</summary>
        public delegate void OnClickCallback();

        /// <summary>Singleton constructor</summary>
        private sfSessionTableGUI()
        {

        }

        /// <summary>Draws the table header.</summary>
        /// <param name="headers"></param>
        public void DrawHeader(params string[] headers)
        {
            EditorGUILayout.BeginHorizontal();
            foreach (string header in headers)
            {
                EditorGUILayout.LabelField(header, EditorStyles.boldLabel, GUILayout.MinWidth(SMALL_LABEL_WIDTH));
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.LabelField("", // required for alignment
                GUILayout.MaxWidth(2 * BUTTON_WIDTH + GUI.skin.button.margin.left));
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>Draws a row with one button in the last column.</summary>
        /// <param name="buttonLabel"></param>
        /// <param name="labels"></param>
        /// <returns>true if the button was clicked.</returns>
        public bool DrawRow(string buttonLabel, params string[] labels)
        {
            BeginRow(null, labels);
            bool value = LargeButton(buttonLabel);
            EndRow();
            return value;
        }

        /// <summary>Draws a row with a Join button.</summary>
        /// <param name="-">session information</param>
        /// <param name="joinError">reason the user cannot join the session. If null then join is enabled</param>
        /// <param name="joinSessionCallback">action to take when the user clicks the join button</param>
        public void DrawRow(sfSessionInfo session, string joinError, OnClickCallback joinSessionCallback)
        {
            DrawRow(session, joinError, null, joinSessionCallback, null);
        }

        /// <summary>Draws a row with Join and Stop buttons.</summary>
        /// <param name="-">session information</param>
        /// <param name="joinError">reason the user cannot join the session. If null then join is enabled</param>
        /// <param name="stopError">reason the user cannot stop the session. If null then stop is enabled</param>
        /// <param name="joinSessionCallback">action to take when the user clicks the join button</param>
        /// <param name="stopSessionCallback">action to take when the user clicks the stop button</param>
        public void DrawRow(
            sfSessionInfo session,
            string joinError,
            string stopError,
            OnClickCallback joinSessionCallback,
            OnClickCallback stopSessionCallback)
        {
            string tooltip = "ID: " + session.RoomInfo.Id +
                "\n-Start time: " + session.StartTime.ToString("yyyy-MM-dd HH:mm:ss") +
                "\n-Scene Fusion Version: " + session.RequiredVersion +
                "\n-Unity Version: " + session.LaunchApplication;

            if (!string.IsNullOrEmpty(joinError))
            {
                tooltip += "\n\n" + joinError;
            }

            if (!string.IsNullOrEmpty(stopError))
            {
                tooltip += "\n\n" + stopError;
            }

            BeginRow(tooltip, session.EditorProjectName, session.SceneName, session.Creator);

            if (stopSessionCallback == null)
            {
                // Draw a button to join sessions
                if (LargeButton("Join", joinError))
                {
                    joinSessionCallback();
                }
            }
            else
            {
                // Draw a button to join sessions
                if (SmallButton("Join", joinError))
                {
                    joinSessionCallback();
                }

                // Draw a button to stop sessions
                if (SmallButton("Stop", stopError))
                {
                    stopSessionCallback();
                }
            }
            EndRow();
        }

        /// <summary>Begins the GUI for a row.</summary>
        /// <param name="tooltip"></param>
        /// <param name="labels"></param>
        private void BeginRow(string tooltip, params string[] labels)
        {
            EditorGUILayout.BeginHorizontal();
            foreach (string label in labels)
            {
                EditorGUILayout.LabelField(new GUIContent(label, tooltip), GUILayout.MinWidth(SMALL_LABEL_WIDTH));
                GUILayout.FlexibleSpace();
            }
        }

        /// <summary>Ends the GUI for a row.</summary>
        private void EndRow()
        {
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>Creates a large button that fills the same space as a small buton + X button.</summary>
        /// <param name="label">label for the button.</param>
        /// <param name="disabledMessage">if non-empty, the button is disabled and this is the tooltip.</param>
        /// <returns>true if the button was clicked.</returns>
        private bool LargeButton(string label, string disabledMessage = null)
        {
            bool clicked;
            EditorGUI.BeginDisabledGroup(!string.IsNullOrEmpty(disabledMessage));
            clicked = GUILayout.Button(label,
                GUILayout.Width(2 * BUTTON_WIDTH + GUI.skin.button.margin.left),
                GUILayout.Height(BUTTON_HEIGHT));
            EditorGUI.EndDisabledGroup();
            return clicked;
        }

        /// <summary>Creates a small button.</summary>
        /// <param name="label">label for the button.</param>
        /// <param name="disabledMessage">if non-empty, the button is disabled and this is the tooltip.</param>
        /// <returns>true if the button was clicked.</returns>
        private static bool SmallButton(string label, string disabledMessage = null)
        {
            bool clicked;
            EditorGUI.BeginDisabledGroup(!string.IsNullOrEmpty(disabledMessage));
            clicked = GUILayout.Button(new GUIContent(label, disabledMessage), GUILayout.Width(BUTTON_WIDTH));
            EditorGUI.EndDisabledGroup();
            return clicked;
        }

        /// <summary>Creates an empty space the same size as the X button.</summary>
        private void SmallButtonSpace()
        {
            GUILayout.Space(BUTTON_WIDTH + GUI.skin.button.margin.left);
        }
    }
}
