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
namespace KS.SceneFusion.Client.Unity.Editor
{
    // Implementation specific to the cloud package.
    public partial class SceneFusion
    {
        /// <summary>Creates the Scene Fusion service.</summary>
        /// <returns>Scene Fusion service.</returns>
        private sfService CreateService()
        {
            return new sfService(sfConfig.Get().Urls.WebAPI, sfConfig.Get().Version,
                sfConfig.Get().ServerVersion);
        }

        /// <summary>
        /// Sets <see cref="sfService.OverrideServerPort"/> to
        /// <see cref="sfConfig.NetworkSettings.OverrideServerPort"/>.
        /// </summary>
        partial void SetSessionSettings()
        {
            m_service.OverrideServerPort = sfConfig.Get().Network.OverrideServerPort;
        }
    }
}
