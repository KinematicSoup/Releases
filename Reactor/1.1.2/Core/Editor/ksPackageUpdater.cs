using System;
using KS.Unity.Editor;
using UnityEditor;
using UnityEngine.Networking;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Checks for Reactor package updates and prompts users to apply the update.</summary>
    public class ksPackageUpdater : ksGitPackageUpdater<ksPackageUpdater>
    {
        /// <summary>Url to get releases from.</summary>
        protected override string Url => ksReactorConfig.Instance.Urls.Downloads + "/ReactorReleases.json";

        /// <summary>Package name.</summary>
        protected override string PackageName => "Reactor";

        /// <summary>Package version</summary>
        protected override ksVersion Version => ksReactor.Version;
    }
}