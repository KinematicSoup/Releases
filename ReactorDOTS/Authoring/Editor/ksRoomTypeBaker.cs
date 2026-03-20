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
using Unity.Entities;

namespace KS.Reactor.Client.Unity.DOTS.Editor
{
    /// <summary>Baker for <see cref="ksRoomType"/> that converts to a <see cref="ksRoomData"/>.</summary>
    public class ksRoomTypeBaker : Baker<ksRoomType>
    {
        /// <summary>Bakes a <see cref="ksRoomData"/> from a <see cref="ksRoomType"/>.</summary>
        /// <param name="authoring"></param>
        public override void Bake(ksRoomType authoring)
        {
            Entity ent = GetEntity(TransformUsageFlags.None);
            DependsOn(authoring.gameObject);
            AddComponent(ent, new ksRoomData()
            {
                Scene = authoring.gameObject.scene.name,
                RoomType = authoring.gameObject.name,
                SendRate = authoring.ClientSendRate,
                AllowPlayerSpawning = authoring.AllowPlayerSpawning,
                DefaultEntityPredictor = authoring.DefaultEntityPredictor,
                DefaultEntityControllerPredictor = authoring.DefaultEntityControllerPredictor,
                DefaultRoomPredictor = authoring.DefaultRoomPredictor,
                DefaulPlayerPredictor = authoring.DefaultPlayerPredictor
            });
        }
    }
}