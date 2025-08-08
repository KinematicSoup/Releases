/*
KINEMATICSOUP CONFIDENTIAL
 Copyright(c) 2014-2025 KinematicSoup Technologies Incorporated 
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

using UnityEngine;
using KS.Unity.Editor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Kinematicsoup Service Login GUI.</summary>
    public class ksLoginMenu : ksBaseLoginMenu<ksLoginMenu>, ksIMenu
    {
        /// <summary>Icon to display.</summary>
        public Texture Icon
        {
            get { return ksTextures.Logo; }
        }

        /// <summary>Return the KS console URL.</summary>
        protected override string ConsoleURL
        {
            get { return ksReactorConfig.Instance.Urls.WebConsole; }
        }

        /// <summary>Send a login request.</summary>
        protected override void Login()
        {
            if (!ksEditorWebService.IsLoggingIn)
            {
                ksEditorWebService.OnLogin += OnLogin;
                ksEditorWebService.Login(ksReactorConfig.Instance.Urls.Publishing + "/v3/login", m_email, m_password);
            }
        }

        /// <summary>Get the menu to show when the user is logged in.</summary>
        /// <param name="window">Window to render the next menu in.</param>
        /// <returns>Menu to show when the user is logged in.</returns>
        protected override ksAuthenticatedMenu GetNextMenu(ksWindow window)
        {
            if (window.Id == ksWindow.REACTOR_SERVERS)
            {
                return ScriptableObject.CreateInstance<ksServersMenu>();
            }
            return ScriptableObject.CreateInstance<ksPublishMenu>();
        }
    }
}
