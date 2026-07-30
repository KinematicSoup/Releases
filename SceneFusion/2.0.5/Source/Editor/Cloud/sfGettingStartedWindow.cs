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
using UnityEditor;
using UnityEngine;
using KS.SF.Unity.Editor;

namespace KS.SceneFusion.Client.Unity.Editor
{
    // Implementation specific to the cloud package.
    public partial class sfGettingStartedWindow
    {
        /// <summary>Draws signup instructions.</summary>
        partial void DrawSignupInstructions()
        {
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sign Up", m_heading);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scene Fusion requires user accounts.  If you have not signed up for an account click the register link and follow the registration instructions.", m_paragraph);
            EditorGUILayout.Space();
            Rect rect = EditorGUILayout.GetControlRect();
            rect.x += INDENT;
            ksStyle.Link(new Vector2(rect.x, rect.y), "Register", sfConfig.Get().Urls.WebConsole + "/ksauthentication/login?register=1");
        }

        /// <summary>Draws editing instructions.</summary>
        partial void DrawEditingInstructions()
        {
            EditorGUILayout.LabelField("1. Open the session window by going to <b>Window > Scene Fusion > Session</b>.", m_paragraph);
            EditorGUILayout.LabelField("2. Login with the your account email and password.", m_paragraph);
            EditorGUILayout.LabelField("3. Select the project you want to work on from the top right dropdown menu.", m_paragraph);
            EditorGUILayout.LabelField("4. Editing Sessions", m_paragraph);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("a. To start a new editing session click the <b>New Session</b> button.", m_paragraph);
            EditorGUILayout.LabelField("b. To join an existing editing session check the list of started sessions and click the <b>Join</b> button.", m_paragraph);
            EditorGUI.indentLevel--;
            EditorGUILayout.LabelField("5. A busy spinner will appear in the lower left corner of the scene view while the scene is being prepared. Once the session is joined, green checkmarks will appear next to synced objects in the hierarchy window.", m_paragraph);
        }

        //// <summary>Draws a web console link.</summary>
        partial void DrawWebConsoleLink()
        {
            ksStyle.Link("- Web Console", sfConfig.Get().Urls.WebConsole);
        }
    }
}
