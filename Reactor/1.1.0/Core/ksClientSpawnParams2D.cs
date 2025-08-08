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
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KS.Reactor.Client.Unity
{
    /// <summary>Parameters for spawning entities on the client with 2D transform values.</summary>
    public class ksClientSpawnParams2D : ksClientSpawnParams
    {
        /// <summary>2D-representation of the transform to spawn at.</summary>
        public new ksTransformState2D Transform
        {
            get { return m_transform2D; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException("Transform cannot be null.");
                }
                m_transform2D = value;
                base.Transform = value.Transform3D;
            }
        }
        private ksTransformState2D m_transform2D;

        /// <summary>
        /// Called when the 3D transform is assigned. Creates a new 2D transform to wrap the 3D transform.
        /// </summary>
        protected override void TransformAssigned()
        {
            if (m_transform2D.Transform3D != base.Transform)
            {
                m_transform2D = new ksTransformState2D(base.Transform);
            }
        }

        /// <summary>Constructor</summary>
        public ksClientSpawnParams2D()
        {
            m_transform2D = new ksTransformState2D(base.Transform);
        }

        /// <summary>Constructor</summary>
        /// <param name="prefab">Entity prefab to spawn.</param>
        public ksClientSpawnParams2D(GameObject prefab) : this()
        {
            GameObject = prefab;
        }

        /// <summary>
        /// Fetches a <see cref="ksClientSpawnParams2D"/> from the pool, or creates one if the pool is empty.
        /// </summary>
        /// <returns>Spawn parameters</returns>
        public new static ksClientSpawnParams2D Create()
        {
            return Create<ksClientSpawnParams2D>();
        }

        /// <summary>
        /// Fetches a <see cref="ksClientSpawnParams2D"/> from the pool, or creates one if the pool is empty.
        /// </summary>
        /// <param name="prefab">Prefab to spawn</param>
        /// <returns>Spawn parameters</returns>
        public static new ksClientSpawnParams2D Create(GameObject prefab)
        {
            ksClientSpawnParams2D spawnParams = Create();
            spawnParams.GameObject = prefab;
            return spawnParams;
        }

        /// <summary>Returns the spawn parameters object to the pool.</summary>
        public override void Release()
        {
            Release<ksClientSpawnParams2D>();
        }
    }
}
