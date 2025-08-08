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
using UnityEngine;

namespace KS.Reactor.Client.Unity
{
    /// <summary>
    /// Base class for developers to write custom predictors for smoothing object movement and property data.
    /// Predictors are scriptable objects. You must create an asset from a predictor (Create > Reactor > 
    /// PredictorClassName) to use it, then assign it to an entity component or the room type to make it the default
    /// predictor.
    /// </summary>
    public abstract class ksPredictor : ScriptableObject, ksIPredictor
    {
        /// <summary>The room.</summary>
        public ksRoom Room
        {
            get { return m_room; }
        }
        private ksRoom m_room;

        /// <summary>The entity we are predicting movement for. Null if predicting room or player properties.</summary>
        public ksEntity Entity
        {
            get { return m_entity; }
        }
        private ksEntity m_entity;

        /// <summary>The player we are predicting properties for. Null if not predicting player properties.</summary>
        public ksPlayer Player
        {
            get { return m_player; }
        }
        private ksPlayer m_player;

        /// <summary>Server and local time data.</summary>
        public ksClientTime Time
        {
            get { return m_room == null ? ksClientTime.Zero : m_room.Time; }
        }

        /// <summary>
        /// The player controller for the <see cref="Entity"/>. Null if the entity has no controller or the controller
        /// has <see cref="ksPlayerController.UseInputPrediction"/> set to false.
        /// </summary>
        public ksPlayerController Controller
        {
            get
            {
                return m_entity == null || m_entity.PlayerController == null ||
                    !m_entity.PlayerController.UseInputPrediction ? null : m_entity.PlayerController;
            }
        }

        /// <summary>Does the predictor require a player controller?</summary>
        public virtual bool RequiresController
        {
            get { return false; }
        }

        /// <summary>Constructor</summary>
        public ksPredictor()
        {
            
        }

        /// <summary>Creates an instance of the predictor. Return null to disable prediction.</summary>
        /// <returns>Predictor instance.</returns>
        public virtual ksIPredictor CreateInstance()
        {
            return Instantiate(this);
        }

        /// <summary>Initializes the predictor.</summary>
        /// <param name="room">The room object.</param>
        /// <param name="entity">
        /// Entity the predictor is smoothing. Null if the predictor is smoothing room or player properties.
        /// </param>
        /// /// <param name="player">
        /// Player the predictor is smoothing properties for. Null if the predictor is not smoothing player properties.
        /// </param>
        /// <returns>False if the predictor could not be initialized and needs to be removed.</returns>
        public bool Initialize(ksBaseRoom room, ksBaseEntity entity, ksBasePlayer player)
        {
            m_room = (ksRoom)room;
            m_entity = (ksEntity)entity;
            m_player = (ksPlayer)player;
            return Initialize();
        }

        /// <summary>Initializes the predictor.</summary>
        /// <returns>False if the predictor could not be initialized and needs to be removed.</returns>
        public virtual bool Initialize()
        {
            return true;
        }

        /// <summary>
        /// Invoked when the Entity parent changes. This method should correct predicted values by converting the positions back to world positions
        /// for the removal of the old parent and then to the local position for the new parent.
        /// </summary>
        /// <param name="oldParent">Parent entity being removed.</param>
        /// <param name="newParent">Parent entity being added.</param>
        public virtual void ParentChange(ksBaseEntity oldParent, ksBaseEntity newParent)
        {
        }

        /// <summary>
        /// Called when the predictor is enabled. May be called zero or more times. Called after 
        /// <see cref="Initialize(ksBaseRoom, ksBaseEntity)"/>. When an entity switches between the
        /// <see cref="ksBaseEntity.NonInputPredictor"/> and <see cref="ksBaseEntity.InputPredictor"/>, this is called
        /// on the one that was switched to.
        /// </summary>
        public virtual void Enabled()
        {

        }

        /// <summary>
        /// Called when the predictor is disabled. May be called zero or more times. Called before <see cref="Detached"/>.
        /// When an entity switches between the <see cref="ksBaseEntity.NonInputPredictor"/> and 
        /// <see cref="ksBaseEntity.InputPredictor"/>, this is called on the one that was switched off.
        /// </summary>
        public virtual void Disabled()
        {

        }

        /// <summary>Called when the predictor is removed. Clears the entity/room reference.</summary>
        public virtual void Detached()
        {
            m_entity = null;
            m_room = null;
        }

        /// <summary>
        /// Does the predictor predict the property value with the given <paramref name="propertyId"/>?
        /// </summary>
        /// <param name="propertyId">Property id</param>
        /// <returns>True if the predictor predicts the property value.</returns>
        public virtual bool IsPredictedProperty(uint propertyId)
        {
            return false;
        }

        /// <summary>
        /// Called once per server frame. Perform any relevant calculations with server frame data here, and/or store
        /// the relevant server frame data to be used in calculating future client state in
        /// <see cref="ClientUpdate(ksTransformState, Dictionary{uint, ksMultiType})"/>.
        /// </summary>
        /// <param name="state">
        /// Server transform data. Null if not smoothing a transform.
        /// </param>
        /// <param name="properties">
        /// Smoothed properties whose values changed since the last frame. Null if no properties have changed.
        /// </param>
        /// <param name="teleport">Did the entity teleport?</param>
        /// <param name="idle">True if there were no transform or property changes since the last frame.</param>
        /// <returns>
        /// If false and <paramref name="idle"/> is true, this function will not be called again until the server
        /// transform or a server property changes.
        /// </returns>
        public virtual bool ServerUpdate(ksReadOnlyTransformState state, 
            Dictionary<uint, ksMultiType> properties, 
            bool teleport, 
            bool idle)
        {
            return false;
        }

        /// <summary>
        /// Called once per client render frame to update the client transform and smoothed properties.
        /// </summary>
        /// <param name="state">Client transform to update with new values. Null if not smoothing a transform.</param>
        /// <param name="properties">
        /// Smoothed client properties to update with new values. Null if there are no smoothed properties.
        /// </param>
        /// <returns>
        /// If false, this function will not be called again until the server transform or a server property changes.
        /// </returns>
        public virtual bool ClientUpdate(ksTransformState state, Dictionary<uint, ksMultiType> properties)
        {
            return false;
        }

        /// <summary>
        /// Called when a new frame of input is generated. Only called for entities with player controllers.
        /// </summary>
        /// <param name="input">Input</param>
        public virtual void InputUpdate(ksInput input)
        {
            input.CleanUp();
        }
    }
}