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
    /// <summary>Feedback renderer</summary>
    public class sfFeedback
    {
        private const float H_PADDING = 5.0f;
        private const float V_PADDING = 13.0f;
        private const float BTN_WIDTH = 75.0f;

        /// <summary>Feedback question</summary>
        public class Question {
            public string Set = null;
            public string Group = null;
            public int Id = -1;
            public string Type = "text";
            public string Text = "Do you have any feedback or questions about Scene Fusion?";
            public List<string> Answers = new List<string>(new string[] {"Yes"});
            public string Answer = "";
            public bool Repeat = false;
        }

        /// <summary>sfFeedback Instance</summary>
        public static sfFeedback Instance
        {
            get
            {
                if (m_instance == null)
                {
                    m_instance = new sfFeedback();
                }
                return m_instance;
            }
        }
        private static sfFeedback m_instance = null;

        /// <summary>Scene Fusion service</summary>
        private static sfService Service
        {
            get { return SceneFusion.Get().Service; }
        }

        private Queue<Question> m_questions = new Queue<Question>();

        /// <summary>Get the last question in the list of questions.</summary>
        /// <returns>last question in a list or null if empty</returns>
        public Question GetLastQuestion()
        {
            return (m_questions.Count == 0) ? null : m_questions.Last();
        }

        /// <summary>Parse a list of questions from a JSON object</summary>
        /// <param name="jsonQuestions"></param>
        public void SetQuestions(ksJSON jsonQuestions)
        {
            if (jsonQuestions == null)
            {
                return;
            }

            m_questions.Clear();
            string set = null;
            string group = null;
            ksJSON questions = null;
            
            if ( jsonQuestions.GetField("question_set", ref set) &&
                 jsonQuestions.GetField("question_group", ref group) &&
                 jsonQuestions.GetField("questions", ref questions))
            {
                foreach(ksJSON question in questions.Array)
                {
                    Question q = new Question();
                    q.Set = set;
                    q.Group = group;

                    bool success = true;
                    success &= question.GetField("id", ref q.Id);
                    success &= question.GetField("type", ref q.Type);
                    success &= question.GetField("question", ref q.Text);
                    success &= question.GetField("repeat", ref q.Repeat);

                    string answers = null;
                    if (question.GetField("answers", ref answers))
                    {
                        if (answers != null)
                        {
                            q.Answers = answers.Split(';').ToList<string>();
                        }
                    }
                    else
                    {
                        success = false;
                    }

                    if (success)
                    {
                        q.Text = "<b>Feedback:</b> " + q.Text;
                        m_questions.Enqueue(q);
                    }
                    else
                    {
                        ksLog.Debug("Unable to parse json question. \n"+question.Print());
                    }
                }
            }
            else
            {
                ksLog.Debug("Unable to parse json question. \n" + jsonQuestions.Print());
            }
        }

        /// <summary>Render the feedback UI</summary>
        public void Draw()
        {
            if (m_questions.Count == 0)
            {
                return;
            }

            Question question = m_questions.Peek();

            // Style
            GUIStyle questionStyle = new GUIStyle(GUI.skin.label);
            questionStyle.richText = true;
            questionStyle.wordWrap = true;
            questionStyle.normal.textColor = Color.black;

            GUIStyle feedbackStyle = new GUIStyle();
            feedbackStyle.padding = new RectOffset(5, 5, 5, 8);

            // Calculate bounds
            float questionWidth = EditorGUIUtility.currentViewWidth - 2.0f * H_PADDING;
            questionWidth -= question.Answers.Count * (BTN_WIDTH + H_PADDING);

            GUIContent questionText = new GUIContent(question.Text);
            float height = questionStyle.CalcHeight(questionText, questionWidth);

            Rect bounds = GUILayoutUtility.GetLastRect();
            bounds.yMin = bounds.yMax;
            bounds.yMax = bounds.yMin + height + V_PADDING;

            // Render components
            EditorGUI.DrawRect(bounds, new Color(1.0f, 0.7f, 0.0f));

            EditorGUILayout.BeginHorizontal(feedbackStyle);
            EditorGUILayout.LabelField(questionText, questionStyle);
            foreach(string answer in question.Answers)
            {
                if (GUILayout.Button(answer, GUILayout.Width(BTN_WIDTH)))
                {
                    question.Answer = answer;
                    EditorApplication.delayCall += SendAnswer;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>Send an answer or display another feedback form in response to a feedback answer.</summary>
        /// <param name="answered">answered question</param>
        private void SendAnswer()
        {
            Question question = m_questions.Peek();
            if (question.Type != "text")
            {
                ksJSON feedback = new ksJSON();
                feedback["set"] = question.Set;
                feedback["group"] = question.Group;
                feedback["id"] = question.Id;
                feedback["answer"] = question.Answer;
                Service.WebService.SendFeedback(feedback);
            }
            else
            {
                question.Answer = "";
                sfFeedbackMenu.Get().Question = question;
                ksWindow.Open(
                    ksWindow.SCENE_FUSION_FEEDBACK,
                    delegate (ksWindow window)
                    {
                        window.titleContent = new GUIContent(Product.NAME + "Feedback");
                        window.minSize = new Vector2(380f, 100f);
                        window.Menu = sfFeedbackMenu.Get();
                    },
                    ksWindow.WindowStyle.UTILITY
                );
            }

            // Cycle repeat questions
            if (question.Repeat)
            {
                m_questions.Enqueue(question);
            }
            m_questions.Dequeue();
        }
    }
}
