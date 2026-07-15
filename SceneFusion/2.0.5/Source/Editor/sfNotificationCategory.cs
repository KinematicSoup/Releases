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
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>A notification category.</summary>
    public class sfNotificationCategory
    {
        /// <summary>Missing component category</summary>
        public static sfNotificationCategory MissingScript = new sfNotificationCategory(
            "Missing Script",
            "https://docs.kinematicsoup.com/SceneFusion/unity_legacy/TroubleshootingPages/missing_script.html");

        /// <summary>Missing component category</summary>
        public static sfNotificationCategory MissingComponent = new sfNotificationCategory(
            "Missing Component",
            "https://docs.kinematicsoup.com/SceneFusion/unity/TroubleshootingPages/missing_components.html");

        /// <summary>Missing prefab category</summary>
        public static sfNotificationCategory MissingPrefab = new sfNotificationCategory(
            "Missing Prefab",
            "https://docs.kinematicsoup.com/SceneFusion/unity/TroubleshootingPages/missing_prefabs.html");

        /// <summary>Missing asset category</summary>
        public static sfNotificationCategory MissingAsset = new sfNotificationCategory(
            "Missing Asset",
            "https://docs.kinematicsoup.com/SceneFusion/unity/TroubleshootingPages/missing_assets.html");

        /// <summary>Property mismatch category</summary>
        public static sfNotificationCategory PropertyMismatch = new sfNotificationCategory(
            "Property Mismatch",
            "https://docs.kinematicsoup.com/SceneFusion/unity_legacy/TroubleshootingPages/property_mismatch.html");

        /// <summary>Unsupported component category</summary>
        public static sfNotificationCategory UnsupportedComponent = new sfNotificationCategory(
            "Unsupported Component",
            "");

        // SF2-only notifications

        public static sfNotificationCategory AssetConflict = new sfNotificationCategory(
            "Asset Conflict",
            "https://docs.kinematicsoup.com/SceneFusion/unity/TroubleshootingPages/asset_out_of_sync.html");

        /// <summary>Category name</summary>
        public string Name
        {
            get { return m_name; }
        }
        private string m_name;

        /// <summary>Url to help page for this notification category.</summary>
        public string HelpUrl
        {
            get { return m_helpUrl; }
        }
        private string m_helpUrl;

        /// <summary>Constructor</summary>
        /// <param name="name">name of the category</param>
        /// <error>string helpUrl for the category</error>
        public sfNotificationCategory(string name, string helpUrl)
        {
            m_name = name;
            m_helpUrl = helpUrl;
        }
    }
}
