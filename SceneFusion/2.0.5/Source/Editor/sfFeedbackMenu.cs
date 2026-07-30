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
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using KS.SF.Unity.Editor;
using KS.SF.Reactor;

namespace KS.SceneFusion.Client.Unity.Editor
{
    /// <summary>GUI that displays a list of feedback questions</summary>
    public class sfFeedbackMenu : ksSingleton<sfFeedbackMenu>, ksIMenu
    {
        // Rendering
        private const int indent = 20;
        private Vector2 m_scrollPosition;

        /// <summary>Icon</summary>
        public Texture Icon
        {
            get { return sfTextures.Logo; }
        }

        /// <summary>Question details</summary>
        public sfFeedback.Question Question
        {
            set { m_question = value; }
        }
        private sfFeedback.Question m_question;

        /// <summary>Scene Fusion service</summary>
        private static sfService Service
        {
            get { return SceneFusion.Get().Service; }
        }

        /// <summary>Destroy this menu on close.</summary>
        public bool DestroyOnClose
        {
            get { return true; }
        }

        /// <summary>Called when the menu is opened.</summary>
        /// <param name="window">window that opened the menu.</param>
        public void OnOpen(ksWindow window)
        {
            
        }

        /// <summary>Called when the menu is closed. Does nothing.</summary>
        /// <param name="window">window that closed the menu.</param>
        public void OnClose(ksWindow window)
        {

        }

        /// <summary>Draw the feedback window content</summary>
        /// <param name="window">window the GUI is for.</param>
        public void Draw(ksWindow window)
        {
            // Styles
            GUIStyle questionStyle = new GUIStyle(EditorStyles.label);
            questionStyle.richText = true;
            questionStyle.wordWrap = true;
            GUIStyle styleText = new GUIStyle(EditorStyles.textField);
            styleText.wordWrap = true;

            // Render
            m_scrollPosition = EditorGUILayout.BeginScrollView(m_scrollPosition);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("<b>"+m_question.Text+"</b>", questionStyle);
            m_question.Answer = EditorGUILayout.TextArea(m_question.Answer, styleText, GUILayout.Height(150));

            EditorGUI.BeginDisabledGroup(m_question.Answer.Trim().Length == 0);
            if (ksStyle.Button("Submit"))
            {
                SendAnswer(m_question);
                m_question.Answer = "";
                window.Close();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndScrollView();
            window.Repaint();
        }


        /// <summary>Send an answer to a feedback question.</summary>
        /// <param name="answered">answered question</param>
        private void SendAnswer(sfFeedback.Question question)
        {
            ksJSON feedback = new ksJSON();
            feedback["set"] = question.Set;
            feedback["group"] = question.Group;
            feedback["id"] = question.Id;
            feedback["answer"] = question.Answer;
            Service.WebService.SendFeedback(feedback);
        }
    }
}
