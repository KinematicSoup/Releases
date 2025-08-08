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

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using UnityEngine.Networking;

namespace KS.Reactor.Client.Unity
{
    /// <summary>Handles web requests using the Unity <see cref="UnityWebRequest"/> object.</summary>
    public class ksUnityWebRequest : ksIWebRequest
    {
        // Timeout in seconds
        private const int TIMEOUT = 5;

        /// <summary>Method</summary>
        public string Method
        {
            get { return m_method; }
        }
        private string m_method = "GET";

        /// <summary>URL</summary>
        public string URL
        {
            get { return m_url; }
        }
        private string m_url = null;

        /// <summary>JSON request data.</summary>
        public ksJSON JSON
        {
            get { return m_json; }
        }
        private ksJSON m_json = null;

        /// <summary>Headers</summary>
        public WebHeaderCollection Headers
        {
            get { return null; }
        }

        /// <summary>Is the request completed?</summary>
        public bool IsDone
        {
            get {
                if (!m_isDone && m_unityWebRequest.isDone)
                {
                    CompleteRequest();
                }
                return m_isDone;
            }
        }
        private bool m_isDone = false;

        /// <summary>Response</summary>
        public ksWebResponse Response
        {
            get { return m_response; }
        }
        private ksWebResponse m_response = new ksWebResponse();


        /// <summary>Time in ticks when the request was sent.</summary>
        public long SendTime
        {
            get { return m_sendTime; }
        }
        private long m_sendTime = 0;

        private UnityWebRequest m_unityWebRequest;

        /// <summary><see cref="ksUnityWebRequest"/> factory.</summary>
        /// <param name="url">URL for the request.</param>
        /// <param name="headers">Header collection.</param>
        /// <param name="method">Request method with a default of null resolving to GET.</param>
        /// <param name="request">JSON request data.</param>
        /// <param name="asyncState">User defined state tracking object.</param>
        /// <returns>A <see cref="ksIWebRequest"> object.</returns>
        public static ksIWebRequest Create(
            string url, 
            WebHeaderCollection headers = null, 
            string method = null, 
            ksJSON request = null, 
            object asyncState = null)
        {
            if (string.IsNullOrEmpty(method))
            {
                method = request == null ? "GET" : "POST";
            }
            return new ksUnityWebRequest(url, headers, method, request, asyncState);
        }

        /// <summary>Constructor</summary>
        /// <param name="url">URL for the request.</param>
        /// <param name="headers">Header collection with a default of null.</param>
        /// <param name="method">Request method with a default of null resolving to GET.</param>
        /// <param name="jsonRequest">JSON request data.</param>
        /// <param name="asyncState">User defined state tracking object.</param>
        public ksUnityWebRequest(
            string url,
            WebHeaderCollection headers = null,
            string method = "GET",
            ksJSON jsonRequest = null,
            object asyncState = null)
        {
            Send(url, headers, method, jsonRequest, asyncState);
        }

        /// <summary>Sends an asynchronous web request.</summary>
        /// <param name="url">URL for the request.</param>
        /// <param name="headers">Header collection with a default of null.</param>
        /// <param name="method">Request method with a default of null resolving to GET.</param>
        /// <param name="jsonRequest">JSON request data.</param>
        /// <param name="asyncState">User defined state tracking object.</param>
        private void Send(string url,
           WebHeaderCollection headers = null,
           string method = "GET",
           ksJSON jsonRequest = null,
           object asyncState = null)
        {
            // Store request data
            m_url = url;
            m_method = method;
            m_json = jsonRequest;
            m_response.AsyncState = asyncState;
            m_sendTime = DateTime.UtcNow.Ticks;

            try
            {
                m_unityWebRequest = new UnityWebRequest(url, method);
                m_unityWebRequest.downloadHandler = new DownloadHandlerBuffer();
                m_unityWebRequest.SetRequestHeader("content-type", "application/json");
                m_unityWebRequest.timeout = TIMEOUT;
                if (headers != null && headers.Count > 0)
                {
                    for (int i = 0; i < headers.Count; i++)
                    {
                        try
                        {
                            m_unityWebRequest.SetRequestHeader(headers.GetKey(i), headers.GetValues(i)[0]);
                        }
                        catch (Exception ex)
                        {
                            ksLog.Warning(this, "ksWebRequest invalid header: " + ex.Message);
                        }
                    }
                }
                if (m_json != null)
                {
                    byte[] data = Encoding.UTF8.GetBytes(m_json.Print());
                    UploadHandlerRaw uploader = new UploadHandlerRaw(data);
                    uploader.contentType = "application/json";
                    m_unityWebRequest.uploadHandler = uploader;
                }
                m_unityWebRequest.SendWebRequest();
            }
            catch (Exception ex)
            {
                m_response.Error = ex.Message;
                m_isDone = true;
            }
        }

        /// <summary>Complete the request and fill the response object.</summary>
        public void CompleteRequest()
        {
            m_response.ReplyTime = (int)((DateTime.UtcNow.Ticks - m_sendTime) / TimeSpan.TicksPerMillisecond);
            m_response.StatusCode = m_unityWebRequest.responseCode;
            m_response.Error = m_unityWebRequest.error;
            m_response.Headers = new WebHeaderCollection();
            foreach (KeyValuePair<string, string> header in m_unityWebRequest.GetResponseHeaders())
            {
                m_response.Headers.Add(header.Key, header.Value);
            }
            if (m_response.StatusCode != 0)
            {
                try
                {
                    m_response.Data = m_unityWebRequest.downloadHandler.data;
                }
                catch (Exception ex)
                {
                    m_response.Error = ex.Message;
                }
            }
            m_isDone = true;
        }

        /// <summary>Cleans up JSON responses. Replaces any back-slash quotes with plain quotes.</summary>
        /// <param name="json"> JSON string to clean.</param>
        /// <returns>Cleaned up JSON.</returns>
        private string CleanJson(string json)
        {
            if (json == "" || json == null)
            {
                return "";
            }

            // Replace //" with "
            json = json.Replace("//\"", "\"");

            return json;
        }
    }
}
