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
    /// <summary>Parameters for spawning entities on the client.</summary>
    public class ksClientSpawnParams : ksSpawnParams
    {
        /// <summary>
        /// The game object to spawn an entity for. The game object must have a <see cref="ksEntityComponent"/> and a
        /// non-zero asset id (which all entity prefabs in Resources or asset bundles should have). If the game object
        /// is a prefab, and instance will be instantiated for the spawned entity.
        /// </summary>
        public GameObject GameObject
        {
            get { return m_gameObject; }
            set
            {
                m_gameObject = value;
                ksEntityComponent entityComponent = value == null ? null : value.GetComponent<ksEntityComponent>();
                if (entityComponent == null)
                {
                    m_entityType = null;
                    m_assetId = 0;
                }
                else
                {
                    m_entityType = entityComponent.Type;
                    m_assetId = entityComponent.AssetId;
                }
            }
        }
        private GameObject m_gameObject;

        /// <summary>Constructor</summary>
        public ksClientSpawnParams()
        {

        }

        /// <summary>Constructor</summary>
        /// <param name="prefab">Entity prefab to spawn.</param>
        public ksClientSpawnParams(GameObject prefab)
        {
            GameObject = prefab;
        }

        /// <summary>Validates the parameters.</summary>
        /// <param name="log">True to log warnings</param>
        /// <returns>True if the spawn parameters are valid.</returns>
        public override bool Validate(bool log = true)
        {
            if (m_gameObject == null)
            {
                if (log)
                {
                    ksLog.Warning(this, "Invalid spawn parameters. GameObject cannot be null.");
                }
                return false;
            }
            ksEntityComponent entityComponent = m_gameObject.GetComponent<ksEntityComponent>();
            if (entityComponent == null)
            {
                if (log)
                {
                    ksLog.Warning(this, "Invalid spawn parameters. GameObject must have a " +
                        typeof(ksEntityComponent).Name + ".");
                }
                return false;
            }
            if (m_assetId == 0)
            {
                if (log)
                {
                    ksLog.Warning(this, "Invalid spawn parameters. Asset id is invalid. Make sure you are " +
                        "spawning entity prefabs from Resources folders or asset bundles.");
                }
                return false;
            }
            if (entityComponent.Entity != null && !entityComponent.Entity.IsDestroyed)
            {
                if (log)
                {
                    ksLog.Warning(this, "Invalid spawn parameters. When Instantiate is false, the GameObject must " +
                        "not already belong to a non-destroyed entity.");
                }
                return false;
            }
            //TODO: verify the entity prefab is client-spawnable.
            return true;
        }

        /// <summary>
        /// Fetches a <see cref="ksClientSpawnParams"/> from the pool, or creates one if the pool is empty.
        /// </summary>
        /// <returns>Spawn parameters</returns>
        public new static ksClientSpawnParams Create()
        {
            return Create<ksClientSpawnParams>();
        }

        /// <summary>
        /// Fetches a <see cref="ksClientSpawnParams"/> from the pool, or creates one if the pool is empty.
        /// </summary>
        /// <param name="prefab">Prefab to spawn</param>
        /// <returns>Spawn parameters</returns>
        public static ksClientSpawnParams Create(GameObject prefab)
        {
            ksClientSpawnParams spawnParams = Create();
            spawnParams.GameObject = prefab;
            return spawnParams;
        }

        /// <summary>Returns the spawn parameters object to the pool.</summary>
        public override void Release()
        {
            Release<ksClientSpawnParams>();
        }

        /// <summary>Resets parameters to their default values.</summary>
        protected override void Reset()
        {
            base.Reset();
            m_gameObject = null;
        }
    }
}
