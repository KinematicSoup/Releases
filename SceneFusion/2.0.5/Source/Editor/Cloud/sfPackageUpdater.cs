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
using KS.SF.Unity.Editor;
using KS.SF.Reactor.Client;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>Checks for Scene Fusion package updates and prompts users to apply the update.</summary>
    public class sfPackageUpdater : ksGitPackageUpdater<sfPackageUpdater>
    {
        /// <summary>Url to get releases from.</summary>
        protected override string Url => sfConfig.Get().Urls.Downloads + "/SceneFusionReleases.json";

        /// <summary>Package name.</summary>
        protected override string PackageName => "Scene Fusion";

        /// <summary>Package version</summary>
        protected override ksVersion Version => sfConfig.Get().Version;
    }
}
