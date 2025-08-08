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

namespace KS.Reactor.Client.Unity
{
    /// <summary>Extend <see cref="ksPlayerAPI"/> with helper functions specific to Unity.</summary>
    public static class ksPlayerAPIExtensions
    {
        private const string DEVICE_KEY = "com.kinematicsoup.reactor.device_id";
        private const string SESSION_KEY = "com.kinematicsoup.reactor.session_id";

        /// <summary>Detect the platform type and login using the appropriate device ID and device type.</summary>
        /// <param name="playerAPI">Extended object.</param>
        /// <param name="callback">Login callback.</param>
        /// <param name="asyncState">Async state object.</param>
        public static void LoginWithDevice(
            this ksPlayerAPI playerAPI, 
            ksPlayerAPI.LoginCallback callback = null, 
            object asyncState = null)
        {
            ksPlayerAPI.DeviceTypes deviceType = ksPlayerAPI.DeviceTypes.Other;
            string deviceId = null;
            switch (Application.platform)
            {
                case RuntimePlatform.Android:
                    deviceType = ksPlayerAPI.DeviceTypes.Android;
                    deviceId = SystemInfo.deviceUniqueIdentifier;
                    break;
                case RuntimePlatform.IPhonePlayer:
                    deviceType = ksPlayerAPI.DeviceTypes.IOS;
                    deviceId = SystemInfo.deviceUniqueIdentifier;
                    break;
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                    deviceType = ksPlayerAPI.DeviceTypes.Windows;
                    deviceId = SystemInfo.deviceUniqueIdentifier;
                    break;
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor:
                    deviceType = ksPlayerAPI.DeviceTypes.Linux;
                    deviceId = SystemInfo.deviceUniqueIdentifier;
                    break;
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXEditor:
                    deviceType = ksPlayerAPI.DeviceTypes.OSX;
                    deviceId = SystemInfo.deviceUniqueIdentifier;
                    break;
                case RuntimePlatform.WebGLPlayer:
                    deviceType = ksPlayerAPI.DeviceTypes.WebGL;
                    deviceId = GetCustomDeviceId();
                    return;
                default:
                    deviceType = ksPlayerAPI.DeviceTypes.Other;
                    deviceId = GetCustomDeviceId();
                    return;
            }
            playerAPI.LoginWithDeviceID(deviceId, deviceType, true, callback, asyncState);
        }

        /// <summary>Save a session in the player preferences.</summary>
        /// <param name="playerAPI">Extended object.</param>
        /// <param name="session">Session to save.</param>
        public static void SaveSession(this ksPlayerAPI playerAPI, ksPlayerAPI.Session session)
        {
            PlayerPrefs.SetString(SESSION_KEY, session.ToBase64());
        }

        /// <summary>Delete a session in the the player preferences.</summary>
        /// <param name="playerAPI">Extended object.</param>
        public static void DeleteSession(this ksPlayerAPI playerAPI)
        {
            if (PlayerPrefs.HasKey(SESSION_KEY))
            {
                PlayerPrefs.DeleteKey(SESSION_KEY);
            }
        }

        /// <summary>Load a session from the player preferences.</summary>
        /// <param name="playerAPI">Extended object.</param>
        /// <param name="callback">Login callback.</param>
        /// <param name="asyncState">Async state object.</param>
        public static void LoadSession(this ksPlayerAPI playerAPI, ksPlayerAPI.LoginCallback callback = null, object asyncState = null)
        {
            if (PlayerPrefs.HasKey(SESSION_KEY))
            {
                ksPlayerAPI.Session session = playerAPI.CreateSession(PlayerPrefs.GetString(SESSION_KEY));
                session.Authenticate(callback, asyncState);
            }
            else if (callback != null) {
                callback("Session not found.", null, asyncState);
            }
        }

        /// <summary>
        /// Fetch the custom device ID from the players preferences. If one does not exist then generate a GUID to 
        /// be used as a device ID and store it in the player preferences.
        /// </summary>
        /// <returns>A custom device ID.</returns>
        private static string GetCustomDeviceId()
        {
            if (!PlayerPrefs.HasKey(DEVICE_KEY))
            {
                PlayerPrefs.SetString(DEVICE_KEY, System.Guid.NewGuid().ToString());
            }
            return PlayerPrefs.GetString(DEVICE_KEY);
        }
    }
}
