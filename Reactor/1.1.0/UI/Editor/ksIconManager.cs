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
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using KS.Unity.Editor;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Load icons and associates them with Reactor scripts.</summary>
    public class ksIconManager
    {
        // Search for scripts to apply icons to in assemblies that reference this assembly.
#if KS_DEVELOPMENT
        private string REACTOR_ASSEMBLY = "Reactor";
#else
        private string REACTOR_ASSEMBLY = "KinematicSoup.Reactor";
#endif

        private const string ENTITY = "KSEntity";
        private const string ROOM_TYPE = "KSRoomType";
        private const string PREDICTOR = "KSPredictor";
        private const string CONE_COLLIDER = "ConeCollider";
        private const string CYLINDER_COLLIDER = "CylinderCollider";
        private const string CONNECT_SCRIPT = "KSConnectScript";
        private const string CLIENT_SCRIPT = "KSClientScript";
        private const string CLIENT_ENTITY_SCRIPT = "KSClientEntityScript";
        private const string CLIENT_ROOM_SCRIPT = "KSClientRoomScript";
        private const string CLIENT_PLAYER_SCRIPT = "KSClientPlayerScript";
        private const string SERVER_ENTITY_SCRIPT = "KSServerEntityScript";
        private const string SERVER_ROOM_SCRIPT = "KSServerRoomScript";
        private const string SERVER_PLAYER_SCRIPT = "KSServerPlayerScript";
        private const string SCRIPT_ASSET = "KSScriptAsset";
        private const string COLLISION_FILTER = "KSCollisionFilter";
        private const string PLAYER_CONTROLLER = "KSPlayerController";
        
        /// <summary>Iterates all Reactor scripts using reflection and sets their icons.</summary>
        public void SetScriptIcons()
        {
            if (EditorApplication.isPlaying)
            {
                // We'll get a errors doing this in play mode, because we need to attach a script to game object to set
                // it's icon, and in play mode the script's awake method will be called which can cause unwanted side
                // effects.
                return;
            }

            ksIconUtility.Get().SetDisabledIcon<ksEntityComponent>(LoadIcon(ENTITY));
            ksIconUtility.Get().SetDisabledIcon<ksRoomType>(LoadIcon(ROOM_TYPE));
            ksIconUtility.Get().SetDisabledIcon<ksPhysicsSettings>(LoadIcon(ROOM_TYPE));
            ksIconUtility.Get().SetDisabledIcon<ksConeCollider>(LoadIcon(CONE_COLLIDER));
            ksIconUtility.Get().SetDisabledIcon<ksCylinderCollider>(LoadIcon(CYLINDER_COLLIDER));
            ksIconUtility.Get().SetDisabledIcon<ksOwnershipScriptManager>(LoadIcon(CLIENT_SCRIPT));
            ksIconUtility.Get().SetDisabledIcon<ksAutoSpawn>(LoadIcon(CLIENT_ENTITY_SCRIPT));
            ksIconUtility.Get().SetDisabledIcon<ksNewClientEntityScript>(LoadIcon(CLIENT_ENTITY_SCRIPT));
            ksIconUtility.Get().SetDisabledIcon<ksNewClientRoomScript>(LoadIcon(CLIENT_ROOM_SCRIPT));
            ksIconUtility.Get().SetDisabledIcon<ksNewClientPlayerScript>(LoadIcon(CLIENT_PLAYER_SCRIPT));
            ksIconUtility.Get().SetDisabledIcon<ksNewServerEntityScript>(LoadIcon(SERVER_ENTITY_SCRIPT));
            ksIconUtility.Get().SetDisabledIcon<ksNewServerRoomScript>(LoadIcon(SERVER_ROOM_SCRIPT));
            ksIconUtility.Get().SetDisabledIcon<ksNewServerPlayerScript>(LoadIcon(SERVER_PLAYER_SCRIPT));
            ksIconUtility.Get().SetIcon<ksCollisionFilterAsset>(LoadIcon(COLLISION_FILTER));

            ksIconUtility.Get().SetInheritedIcon<ksConnect>(LoadIcon(CONNECT_SCRIPT));
            ksIconUtility.Get().SetInheritedIcon<ksConnectionHUD>(LoadIcon(CONNECT_SCRIPT));
            ksIconUtility.Get().SetInheritedIcon<ksEntityScript>(LoadIcon(CLIENT_ENTITY_SCRIPT));
            ksIconUtility.Get().SetInheritedIcon<ksRoomScript>(LoadIcon(CLIENT_ROOM_SCRIPT));
            ksIconUtility.Get().SetInheritedIcon<ksPlayerScript>(LoadIcon(CLIENT_PLAYER_SCRIPT));
            ksIconUtility.Get().SetInheritedIcon<ksProxyEntityScript>(LoadIcon(SERVER_ENTITY_SCRIPT));
            ksIconUtility.Get().SetInheritedIcon<ksProxyRoomScript>(LoadIcon(SERVER_ROOM_SCRIPT));
            ksIconUtility.Get().SetInheritedIcon<ksProxyPlayerScript>(LoadIcon(SERVER_PLAYER_SCRIPT));
            ksIconUtility.Get().SetInheritedIcon<ksProxyScriptAsset>(LoadIcon(SCRIPT_ASSET));
            ksIconUtility.Get().SetInheritedIcon<ksPlayerControllerAsset>(LoadIcon(PLAYER_CONTROLLER));
            ksIconUtility.Get().SetInheritedIcon<ksPredictor>(LoadIcon(PREDICTOR));

            // Apply the inherited icons to scripts in assemblies that reference the Reactor assembly.
            ksIconUtility.Get().ApplyInheritedIcons(REACTOR_ASSEMBLY);
        }

        /// <summary>Loads an icon as a <see cref="Texture2D"/>.</summary>
        /// <param name="icon">Icon name.</param>
        /// <returns>2D Texture</returns>
        private Texture2D LoadIcon(string icon)
        {
            string path = ksPaths.Textures + icon + ".png";
            Texture2D texture = (Texture2D)AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D));
            if (texture == null)
            {
                ksLog.Error(this, "Unable to load icon at " + path);
            }
            return texture;
        }
    }
}
