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
using System.Net;
using UnityEngine;
using UnityEditor;
using KS.SF.Unity.Editor;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>
    /// Singleton for drawing the footer with, version info, upgrade and feedback links at the bottom of the
    /// <see cref="sfSessionsMenu"/> and <see cref="sfOnlineMenu"/>.
    /// </summary>
    public class sfSessionFooterUI
    {
        /// <summary>Gets the singleton instance</summary>
        /// <returns></returns>
        public static sfSessionFooterUI Get()
        {
            return m_instance;
        }
        private static sfSessionFooterUI m_instance = new sfSessionFooterUI();

        /// <summary>Should the upgrade link be shown in the footer?</summary>
        public bool ShowUpgradeLink
        {
            get { return m_showUpgradeLink; }
            set { m_showUpgradeLink = value; }
        }
        private bool m_showUpgradeLink = false;

        // Width of the pipe seperator for links.
        private const float PIPE_WIDTH = 8f;

        /// <summary>Draws the footer with version info and links.</summary>
        public void DrawFooter()
        {
            DrawLinks();
            GUILayout.Label("Version " + sfConfig.Get().FullVersion);
        }

        /// <summary>Draws web links.</summary>
        private void DrawLinks()
        {
            EditorGUILayout.BeginHorizontal();
            ksStyle.Link("Docs", sfConfig.Get().Urls.Documentation);
            GUILayout.Label("|", GUILayout.MaxWidth(PIPE_WIDTH));
            ksStyle.Link("Discord", sfConfig.Get().Urls.Discord);
            GUILayout.Label("|", GUILayout.MaxWidth(PIPE_WIDTH));
            ksStyle.Link("Workflow", "https://kinematicsoup.com/scene-fusion/workflow");
            GUILayout.Label("|", GUILayout.MaxWidth(PIPE_WIDTH));
            ksStyle.Link("Feedback", "https://www.kinematicsoup.com/scene-fusion/feedback?email=" +
                WebUtility.UrlEncode(ksEditorWebService.Email));
            if (m_showUpgradeLink)
            {
                GUILayout.Label("|", GUILayout.MaxWidth(PIPE_WIDTH));
                ksStyle.Link("Upgrade", sfConfig.Get().Urls.Upgrade);
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
