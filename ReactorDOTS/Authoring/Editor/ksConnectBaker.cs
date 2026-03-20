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
using Unity.Entities;

namespace KS.Reactor.Client.Unity.DOTS.Editor
{
    /// <summary>
    /// Baker for <see cref="ksConnect"/> component that converts to a <see cref="ksConnectComponent"/>.
    /// </summary>
    public class ksConnectBaker : Baker<ksConnect>
    {
        /// <summary>
        /// Bakes a <see cref="ksConnectComponent"/> from a <see cref="ksConnect"/>.  If a <see cref="ksRoomType"/> is
        /// on the same game object, bakes the scene and room type from it, and the
        /// <see cref="ksRoomType.LocalServerPort"/> if <see cref="ksConnect.ConnectModes"/> is
        /// <see cref="ksConnect.ConnectModes.LOCAL"/>.
        /// </summary>
        /// <param name="authoring"></param>
        public override void Bake(ksConnect authoring)
        {
            Entity ent = GetEntity(TransformUsageFlags.None);
            ksConnectComponent component = new ksConnectComponent()
            {
                Protocol = (ksConnectionProtocols)authoring.ConnectProtocol,
                Mode = authoring.ConnectMode
            };
            ksRoomType roomType = authoring.GetComponent<ksRoomType>();
            if (roomType != null)
            {
                component.Scene = roomType.gameObject.scene.name;
                component.RoomType = roomType.gameObject.name;
            }
            if (authoring.ConnectMode == ksConnect.ConnectModes.REMOTE)
            {
                component.RemoteHost = authoring.RemoteHost;
                component.Port = authoring.RemotePort;
            }
            else
            {
                component.RemoteHost = "localhost";
                component.Port = roomType == null ? (ushort)8000 : roomType.LocalServerPort;
                component.SecureLocalWebSockets = authoring.SecureLocalWebSockets;
            }
            AddComponentObject(ent, component);
            if (authoring.ConnectOnStart)
            {
                AddComponent<ksAutoConnectTag>(ent);
            }
        }
    }
}
