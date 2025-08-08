using System;
using KS.Unity.Editor;
using UnityEditor;
using UnityEngine.Networking;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>
    /// Checks for Reactor package updates and prompts users to apply the update.
    /// </summary>
    public class ksPackageUpdater : ksSingleton<ksPackageUpdater>
    {
        /// <summary>Callback to invoke after an update check completes.</summary>
        /// <param name="updated">True if the package was updated.</param>
        public delegate void UpdateCallback(bool updated);

        private static string LOG_CHANNEL = typeof(ksPaths).ToString();

        private bool m_checked = false;
        [NonSerialized] private bool m_isCheckingUpdates = false;
        [NonSerialized] private UpdateCallback m_updateCallback = null;

        /// <summary>Check for Reactor updates.</summary>
        /// <param name="callback">Callback which returns the updated state.</param>
        /// <param name="forceCheck">Force an update check even if a check was already perfomed.</param>
        public void Check(UpdateCallback callback, bool forceCheck = false)
        {
            if (!m_isCheckingUpdates && (forceCheck || !m_checked))
            {
                m_isCheckingUpdates = true;
                m_updateCallback = callback;
                ksEditorWebService.Get(
                    $"{ksReactorConfig.Instance.Urls.Downloads}/ReactorReleases.json",
                    OnGetReleases
                );
            }
            else if (callback != null)
            {
                callback(false);
            }
        }

        /// <summary>Handle the response from the get Reactor release web request.</summary>
        /// <param name="result">Web request JSON result.</param>
        /// <param name="error">Web request error.</param>
        private void OnGetReleases(ksJSON result, string error)
        {
            bool updated = false;
            if (!string.IsNullOrEmpty(error))
            {
                ksLog.Warning(LOG_CHANNEL, "Unable to retrieve Reactor release data. " + error);
            }
            else if (result == null || !result.IsArray)
            {
                ksLog.Warning(LOG_CHANNEL, "Unable to parse Reactor release data.");
            }
            else
            {
                // Releases are always ordered from newest to oldest.  We iterate from the first release to the last and 
                // ask users to apply the first release which is greater than their current release.
                foreach (ksJSON release in result.Array)
                {
                    ksVersion releaseVersion;
                    try
                    {
                        releaseVersion = ksVersion.FromString(release["version"]);
                    }
                    catch (Exception ex)
                    {
                        ksLog.Warning(LOG_CHANNEL, "Error parsing Reactor release version", ex);
                        continue;
                    }
                    if (releaseVersion > ksVersion.Current)
                    {
                        updated = UpdatePackage(releaseVersion, release["git"]);
                        break;
                    }
                }
            }

            m_checked = true;
            m_isCheckingUpdates = false;
            if (m_updateCallback != null)
            {
                m_updateCallback(updated);
            }
        }

        /// <summary>Prompt the user to install a new Reactor package.</summary>
        /// <param name="newVersion">New Reactor version</param>
        /// <param name="gitIdentifier">Git package identifier.</param>
        /// <returns>True if the package was updated.</returns>
        private static bool UpdatePackage(ksVersion newVersion, string gitIdentifier)
        {
            bool update = EditorUtility.DisplayDialog(
                "Reactor Package Update", 
                $"A new Reactor version {newVersion.ToString()} is available. Do you want to update now?",
                "Yes", "No"
            );
#if !KS_DEVELOPMENT
            if (update)
            {
                try
                {
                    AddRequest addRequest = UnityEditor.PackageManager.Client.Add(gitIdentifier);
                    while (!addRequest.IsCompleted) { }
                    if (addRequest.Status == StatusCode.Failure)
                    {
                        ksLog.Error(LOG_CHANNEL, $"Reactor package update failed.");
                        update = false;
                    }
                }
                catch (Exception ex)
                {
                    ksLog.Error(LOG_CHANNEL, "Error updating Reactor Package.", ex);
                    update = false;
                }
            }
#endif
            return update;
        }
    }
}