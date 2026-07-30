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
using System.Diagnostics;

namespace KS.SceneFusion.Client.Unity.Editor
{
    // Implementation specific to the cloud package.
    public partial class sfLocalSession
    {
        /// <summary>Checks if the correct local server version is installed.</summary>
        /// <param name="serverPath">Path to local server executable</param>
        /// <returns>True if the server version is correct.</returns>
        private static bool VersionCheck(string serverPath)
        {
            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(serverPath);
            return sfConfig.Get().ServerVersion.ToString() == versionInfo.ProductVersion;
        }
    }
}
