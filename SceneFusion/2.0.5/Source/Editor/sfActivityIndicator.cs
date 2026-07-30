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
    /// <summary>Spinning activity indicator for the scene view.</summary>
    public class sfActivityIndicator : sfIActivityIndicator
    {
        // Spinner size in pixels
        private const float SPINNER_SIZE = 32f;
        // Size in pixels of rounded box behind spinner.
        private const float BOX_SIZE = 40f;
        // Corner radius of box behind spinner.
        private const float CORNER_RADIUS = 8f;
        // Number of columns in spinner sprite sheet.
        private const int NUM_COLUMNS = 4;
        // Number of rows in spinner sprite sheet.
        private const int NUM_ROWS = 3;
        // Number of frames.
        private const int NUM_FRAMES = NUM_COLUMNS * NUM_ROWS;
        // Time between frames in ticks
        private const long FRAME_INTERVAL = 1000 * TimeSpan.TicksPerMillisecond / NUM_FRAMES;
        // Colour of box behind spinner.
        private static readonly Color BOX_COLOUR = new Color(0, 0, 0, .67f);

        private sfDrawUtils m_drawUtils = sfDrawUtils.Instance;
        private int m_frame = 0;
        private long m_lastFrameTime = 0;
        private GameObject m_gameObject;

        /// <summary>Is the activity indicator showing?</summary>
        public bool Showing
        {
            get { return m_showing; }

            private set
            {
                if (m_showing != value && sfTextures.Spinner != null)
                {
                    m_showing = value;
                    m_lastFrameTime = DateTime.Now.Ticks;
                    m_frame = 0;
                    if (value)
                    {
                        SceneView.duringSceneGui += DrawInScene;
                        if (EditorApplication.isPlaying)
                        {
                            m_gameObject = new GameObject("__ksActivityIndicator");
                            m_gameObject.hideFlags = HideFlags.HideAndDontSave;
                            GUIHook indicator = m_gameObject.AddComponent<GUIHook>();
                            indicator.OnDraw += DrawInGame;
                        }
                    }
                    else
                    {
                        SceneView.duringSceneGui -= DrawInScene;
                        if (m_gameObject != null)
                        {
                            GameObject.DestroyImmediate(m_gameObject);
                        }
                    }
                    SceneView.RepaintAll();
                }
            }
        }
        private bool m_showing = false;

        private int m_taskCount = 0;

        /// <summary>
        /// Increments the task count. The spinner is visible while the task count is greater than zero.
        /// </summary>
        public void AddTask()
        {
            if (m_taskCount == 0)
            {
                Showing = true;
            }
            m_taskCount++;
        }

        /// <summary>
        /// Decrements the task count if it is greater than zero. The spinner is visible while the task count is
        /// greater than zero.
        /// </summary>
        public void RemoveTask()
        {
            if (m_taskCount <= 0)
            {
                return;
            }
            m_taskCount--;
            if (m_taskCount == 0)
            {
                Showing = false;
            }
        }

        /// <summary>Sets the task count to zero and hides the spinner.</summary>
        public void ClearTasks()
        {
            m_taskCount = 0;
            Showing = false;
        }

        /// <summary>Shows a progress bar with a message.</summary>
        /// <param name="message">Message to show with progress bar</param>
        /// <param name="progress">Progress from 0 to 1.</param>
        public void ShowProgressBar(string message, float progress)
        {
            EditorUtility.DisplayProgressBar(Product.NAME, message, progress);
        }

        /// <summary>Hides the progress bar.</summary>
        public void ClearProgressBar()
        {
            EditorUtility.ClearProgressBar();
        }

        /// <summary>Draws the activity indicator in the scene view.</summary>
        /// <param name="sceneView"></param>
        private void DrawInScene(SceneView sceneView)
        {
            Rect viewport = sfUI.Get().GetViewport(sceneView);
            Draw(viewport.width, viewport.height);
            SceneView.RepaintAll();
        }

        /// <summary>Draws the activity indicator in the game view.</summary>
        private void DrawInGame()
        {
            Draw(Screen.width, Screen.height);
        }

        /// <summary>Draws the activity indicator.</summary>
        /// <param name="screenWidth">screenWidth in pixels.</param>
        /// <param name="screenHeight">screenHeight in pixels.</param>
        private void Draw(float screenWidth, float screenHeight)
        {
            Handles.BeginGUI();
            Rect rect = new Rect(20, screenHeight - 20 - BOX_SIZE, BOX_SIZE, BOX_SIZE);

            m_drawUtils.DrawRoundedBox(rect, BOX_COLOUR, CORNER_RADIUS);
            rect = new Rect(24, screenHeight - 24 - SPINNER_SIZE, SPINNER_SIZE, SPINNER_SIZE);
            if (DateTime.Now.Ticks - m_lastFrameTime >= FRAME_INTERVAL)
            {
                m_frame++;
                m_frame %= NUM_FRAMES;
                m_lastFrameTime = DateTime.Now.Ticks;
            }
            float gridSize = Mathf.Max(NUM_COLUMNS, NUM_ROWS);
            float cellSize = 1f / gridSize;
            int x = m_frame % NUM_COLUMNS;
            int y = NUM_COLUMNS - m_frame / NUM_COLUMNS - 1;
            Rect srcRect = new Rect(x / gridSize, y / gridSize, cellSize, cellSize);
            GUI.DrawTextureWithTexCoords(rect, sfTextures.Spinner, srcRect);
            Handles.EndGUI();
        }
    }
}
