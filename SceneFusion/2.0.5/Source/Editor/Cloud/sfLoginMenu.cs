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
using KS.SF.Reactor;
using UnityEngine;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>Kinematicsoup Service Login GUI</summary>
    public class sfLoginMenu : ksBaseLoginMenu<sfLoginMenu>, ksIMenu
    {
        /// <summary>Service</summary>
        private static sfService Service
        {
            get { return SceneFusion.Get().Service; }
        }

        /// <summary>Icon</summary>
        public Texture Icon
        {
            get { return sfTextures.Logo; }
        }

        /// <summary>Return the KS console URL</summary>
        protected override string ConsoleURL
        {
            get { return sfConfig.Get().Urls.WebConsole; }
        }

        /// <summary>Send a login request</summary>
        protected override void Login()
        {
            Service.Login(m_email, m_password,
                delegate (bool success, string username, string response)
            {
                if (success)
                {
                    // Fetch feedback
                    Service.WebService.GetFeedbackQuestions(delegate (ksJSON questions)
                    {
                        sfFeedback.Instance.SetQuestions(questions);
                    });
                }
                OnLogin(null, response);
            });
        }

        /// <summary>Get the menu to show when the user is logged in.</summary>
        /// <param name="window"></param>
        /// <returns></returns>
        protected override ksAuthenticatedMenu GetNextMenu(ksWindow window)
        {
            return ScriptableObject.CreateInstance<sfSessionsMenu>();
        }

        /// <summary>Draws the footer with version info and links.</summary>
        protected override void DrawFooter()
        {
            sfSessionFooterUI.Get().DrawFooter();
        }
    }
}
