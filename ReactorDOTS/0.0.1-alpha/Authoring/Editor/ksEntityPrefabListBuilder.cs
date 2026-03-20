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
using UnityEngine;
using UnityEditor;
using KS.Reactor.Client.Unity.Editor;

namespace KS.Reactor.Client.Unity.DOTS.Editor
{
    /// <summary>
    /// Generates a <see cref="ksEntityPrefabList"/> asset in the ReactorScripts folder containing all published entity
    /// prefabs each time configs are built.
    /// </summary>
    [InitializeOnLoad]
    public class ksEntityPrefabListBuilder
    {
        /// <summary>Path to save <see cref="ksEntityPrefabList"/> asset to.</summary>
        public static string PrefabListPath
        {
            get { return ksPaths.ReactorScripts + "ksEntityPrefabList.asset"; }
        }

        /// <summary>Static initialization. Registers a post build event to build the prefab list asset.</summary>
        static ksEntityPrefabListBuilder()
        {
            ksBuildEvents.PostBuild += BuildPrefabList;
        }

        /// <summary>
        /// Generates a <see cref="ksEntityPrefabList"/> asset in ReactorScripts containing all published entity
        /// prefabs if <paramref name="success"/> is true.
        /// </summary>
        /// <param name="success">True if the config build was successful.</param>
        private static void BuildPrefabList(bool success)
        {
            if (!success)
            {
                return;
            }

            string path = PrefabListPath;
            ksEntityPrefabList asset = AssetDatabase.LoadAssetAtPath<ksEntityPrefabList>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ksEntityPrefabList>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.Prefabs.Clear();
            foreach (ksBuildUtils.PrefabInfo info in ksBuildUtils.IterateResourceAndAssetBundlePrefabs())
            {
                ksEntityComponent entity = info.GameObject.GetComponent<ksEntityComponent>();
                if (entity != null && entity.AssetId != 0)
                {
                    asset.Prefabs.Add(entity);
                }
            }
            EditorUtility.SetDirty(asset);
        }
    }
}