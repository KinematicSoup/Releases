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
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace KS.Reactor.Client.Unity
{
    /// <summary>
    /// Calculates and optionally logs bandwidth and other network counts per second at regular intervals. Which stats
    /// are calculated is configurable using <see cref="CounterTypes"/>. If attached to a room object, will calculate
    /// the stats for just that room from <see cref="ksBaseRoom{Player, Entity}.NetStats"/>. Otherwise it will calculate
    /// the total stats for all rooms from <see cref="ksService.NetStats"/>. If you have other code that clears the
    /// <see cref="ksNetStats"/> counters, the calculate stats will be inaccurate.
    /// </summary>
    [DisallowMultipleComponent]
    public class ksNetStatsMonitor : MonoBehaviour
    {
        /// <summary>Counter type flags</summary>
        public enum CounterType
        {
            /// <summary>Number of received frames</summary>
            RX_FRAMES = 1,
            /// <summary>Receive total</summary>
            RX_TOTAL = 1 << 1,
            /// <summary>Receive events (RPCs, Properties, Destroy, Connect/Disconnect)</summary>
            RX_EVENTS = 1 << 2,
            /// <summary>Number of received RPCs.</summary>
            RX_RPC_COUNT = 1 << 3,
            /// <summary>Receive transform updates</summary>
            RX_TRANSFORM_UPDATE = 1 << 4,
            /// <summary>Receive full transforms</summary>
            RX_TRANSFORM_FULL = 1 << 5,
            /// <summary>Receive input</summary>
            RX_INPUT = 1 << 6,
            /// <summary>Receive room properties</summary>
            RX_ROOM_PROPS = 1 << 7,
            /// <summary>Receive player properties</summary>
            RX_PLAYER_PROPS = 1 << 8,
            /// <summary>Receive entity properties</summary>
            RX_ENTITY_PROPS = 1 << 9,
            /// <summary>Receive model updates</summary>
            RX_MODEL_UPDATES = 1 << 10,
            /// <summary>Receive owner delta times</summary>
            RX_OWNER_DELTA_TIME = 1 << 11,

            /// <summary>Number of transmitted frames</summary>
            TX_FRAMES = 1 << 16,
            /// <summary>Transmit total</summary>
            TX_TOTAL = 1 << 17,
            /// <summary>Transmit events (RPCs)</summary>
            TX_EVENTS = 1 << 18,
            /// <summary>Number of transmitted RPCs.</summary>
            TX_RPC_COUNT = 1 << 19,
            /// <summary>
            /// Transmit transform updates for owned entities. This in included in <see cref="TX_ENTITY"/>.
            /// </summary>
            TX_TRANSFORM_UPDATE = 1 << 20,
            /// <summary>Transmit input</summary>
            TX_INPUT = 1 << 22,
            /// <summary>
            /// Transmit property updates for owned entities, This is included in <see cref="TX_ENTITY"/>.
            /// </summary>
            TX_ENTITY_PROPS = 1 << 25,
            /// <summary>
            /// Transmit updates for owned entities. Includes <see cref="TX_TRANSFORM_UPDATE"/> and
            /// <see cref="TX_ENTITY_PROPS"/>.
            /// </summary>
            TX_ENTITY = 1 << 28,
            /// <summary>Number of transmitted updates for owned entities.</summary>
            TX_ENTITY_COUNT = 1 << 29
        }

        /// <summary>Flags indicating which net stats to calculate.</summary>
        [ksFlags]
        [Tooltip("Which net stats to calculate.Rx = Receive, Tx = Total.")]
        public CounterType CounterTypes = CounterType.RX_TOTAL;

        /// <summary>
        /// Interval in seconds to calculate net stats. The calculated bandwidth/rate will be the average over this
        /// interval. If zero or less, will calculate every frame. If changed to a value less than or equal to the
        /// amount of time since the last calculation, new stats will be calculated on the next update.
        /// </summary>
        [Tooltip("Interval in seconds to calculate net stats. The calculated bandwidth/rate will be the average over " +
            "this interval. If zero or less, will calculate every frame.")]
        public float Interval = 5f;

        /// <summary>Should stats be logged? Changing this value at runtime does not reset the interval timer.</summary>
        [Tooltip("Should stats be logged?")]
        public bool LogStats = true;

        /// <summary>
        /// Should each stat be logged on a different line, or should all stats be on one line? Not used if
        /// <see cref="LogStats"/> is false.
        /// </summary>
        [Tooltip("Should each stat be logged on a different line, or should all stats be on one line?")]
        public bool MultiLine = false;

        /// <summary>
        /// Invoked when stats are recalculated. You can use this to update network stats in your UI. The parameter is
        /// a dictionary containing the calculated counters and their values. For bandwidth types the value is in kB/s
        /// where 1 kB = 1024 bytes. For other types the value is in count/s.
        /// </summary>
        [Tooltip("Invoked when stats are recalculated. You can use this to update network stats in your UI. The " +
            "parameter is a dictionary containing the calculated counters and their values. For bandwidth types the " +
            "value is in kB/s where 1 kB = 1024 bytes. For other types the value is in count/s.")]
        public UnityEvent<Dictionary<CounterType, float>> OnUpdate;

        /// <summary>Is this script calculating the total statistics for all rooms?</summary>
        public bool IsGlobal
        {
            get { return m_netStats == ksReactor.Service.NetStats; }
        }

        private Dictionary<CounterType, float> m_stats = new Dictionary<CounterType, float>();
        private ksNetStats m_netStats;
        private ksNetStats m_lastStats = new ksNetStats();
        private ksRoom m_room;
        private float m_timer;

        /// <summary>ToString format for logged values.</summary>
        private const string FORMAT = "F2";

        // Counters for non-byte data.
        private static HashSet<CounterType> m_nonByteCounters = new HashSet<CounterType>()
        {
            CounterType.RX_FRAMES, CounterType.RX_RPC_COUNT,
            CounterType.TX_RPC_COUNT, CounterType.TX_ENTITY_COUNT
        };

        /// <summary>
        /// Called when the script is enabled. Determines if it will monitor net stats for one room by checking for a
        /// <see cref="ksRoomComponent"/> or <see cref="ksRoomType"/>. If it does not find one, monitors net stats for
        /// all rooms using <see cref="ksNetStats.Global"/>.
        /// </summary>
        private void OnEnable()
        {
            ksRoomComponent roomComponent = GetComponent<ksRoomComponent>();
            bool isGlobal = roomComponent == null && GetComponent<ksRoomType>() == null;
            if (roomComponent != null && roomComponent.Room != null)
            {
                m_room = roomComponent.Room;
            }
            else if (!isGlobal)
            {
                // We are on a room object but the room is not connected. Register an OnRoomConnect event so we can be
                // notified when the room connects and begin monitoring stats then.
                ksReactor.OnRoomConnect += HandleRoomConnect;
                return;
            }

            m_netStats = isGlobal ? ksReactor.Service.NetStats : m_room.NetStats;
            m_netStats.CopyTo(m_lastStats);
        }

        /// <summary>Clean up when the script becomes disabled.</summary>
        private void OnDisable()
        {
            if (!IsGlobal)
            {
                ksReactor.OnRoomConnect -= HandleRoomConnect;
                m_room = null;
            }
            m_netStats = null;
            m_timer = 0;
        }

        /// <summary>
        /// Called when a room becomes connected. If the room is the room for this game object, begins monitoring its
        /// net stats.
        /// </summary>
        /// <param name="room">Room that became connected.</param>
        private void HandleRoomConnect(ksRoom room)
        {
            if (room.GameObject == gameObject)
            {
                m_room = room;
                m_netStats = room.NetStats;
                ksReactor.OnRoomConnect -= HandleRoomConnect;
            }
        }

        /// <summary>
        /// Called every frame. Calculates updated stats if enough time has passed since the last time.
        /// </summary>
        private void Update()
        {
            if (!IsGlobal)
            {
                if (m_room == null)
                {
                    return;
                }
                if (!m_room.IsConnected && !m_room.IsConnecting)
                {
                    // Register an OnRoomConnect event so we can be notified if a new room connection is made and begin
                    // monitoring stats again.
                    ksReactor.OnRoomConnect += HandleRoomConnect;
                    m_room = null;
                    m_timer = 0f;
                    return;
                }
            }

            m_timer += Time.deltaTime;
            if (m_timer >= Interval)
            {
                UpdateStats();
            }
        }

        /// <summary>
        /// Calculates updated stats, logs them if <see cref="LogStats"/> is true, and invokes <see cref="OnUpdate"/>.
        /// </summary>
        public void UpdateStats()
        {
            if (m_timer <= 0f || m_netStats == null)
            {
                return;
            }

            // Clear the last calculated stats.
            m_stats.Clear();

            // Calculate new stats.
            if (CounterTypes != 0)
            {
                List<string> strValues = LogStats ? new List<string>() : null;
                foreach (CounterType counter in Enum.GetValues(typeof(CounterType)))
                {
                    if ((CounterTypes & counter) != 0)
                    {
                        ksNetStats.CounterType netCounter = GetNetStatsCounterType(counter);
                        ulong count = m_netStats.Get(netCounter) - m_lastStats.Get(netCounter);
                        float value;
                        if (m_nonByteCounters.Contains(counter))
                        {
                            value = count / m_timer;
                            if (LogStats)
                            {
                                strValues.Add(counter + " = " + value.ToString(FORMAT) + "/s");
                            }
                        }
                        else
                        {
                            value = count / (1024 * m_timer);
                            if (LogStats)
                            {
                                strValues.Add(counter + " = " + value.ToString(FORMAT) + " kB/s");
                            }
                        }
                        m_stats[counter] = value;
                    }
                }

                // Log the stats
                if (LogStats && strValues.Count > 0)
                {
                    string logChannel = typeof(ksNetStatsMonitor).Name + "." + gameObject.name;
                    ksLog.Debug(logChannel, string.Join(MultiLine ? "\n" : ", ", strValues));
                }
            }
            m_netStats.CopyTo(m_lastStats);
            m_timer = 0f;

            // Invoke OnUpdate
            if (OnUpdate != null)
            {
                OnUpdate.Invoke(m_stats);
            }
        }

        private ksNetStats.CounterType GetNetStatsCounterType(CounterType type)
        {
            switch (type)
            {
                case CounterType.RX_FRAMES: return ksNetStats.CounterType.RX_FRAMES;
                case CounterType.RX_TOTAL: return ksNetStats.CounterType.RX_TOTAL;
                case CounterType.RX_EVENTS: return ksNetStats.CounterType.RX_EVENTS;
                case CounterType.RX_RPC_COUNT: return ksNetStats.CounterType.RX_RPC_COUNT;
                case CounterType.RX_TRANSFORM_UPDATE: return ksNetStats.CounterType.RX_TRANSFORM_UPDATE;
                case CounterType.RX_TRANSFORM_FULL: return ksNetStats.CounterType.RX_TRANSFORM_FULL;
                case CounterType.RX_INPUT: return ksNetStats.CounterType.RX_INPUT;
                case CounterType.RX_ROOM_PROPS: return ksNetStats.CounterType.RX_ROOM_PROPS;
                case CounterType.RX_PLAYER_PROPS: return ksNetStats.CounterType.RX_PLAYER_PROPS;
                case CounterType.RX_ENTITY_PROPS: return ksNetStats.CounterType.RX_ENTITY_PROPS;
                case CounterType.RX_MODEL_UPDATES: return ksNetStats.CounterType.RX_MODEL_UPDATES;
                case CounterType.RX_OWNER_DELTA_TIME: return ksNetStats.CounterType.RX_OWNER_DELTA_TIME;
                case CounterType.TX_FRAMES: return ksNetStats.CounterType.TX_FRAMES;
                case CounterType.TX_TOTAL: return ksNetStats.CounterType.TX_TOTAL;
                case CounterType.TX_EVENTS: return ksNetStats.CounterType.TX_EVENTS;
                case CounterType.TX_RPC_COUNT: return ksNetStats.CounterType.TX_RPC_COUNT;
                case CounterType.TX_TRANSFORM_UPDATE: return ksNetStats.CounterType.TX_TRANSFORM_UPDATE;
                case CounterType.TX_INPUT: return ksNetStats.CounterType.TX_INPUT;
                case CounterType.TX_ENTITY_PROPS: return ksNetStats.CounterType.TX_ENTITY_PROPS;
                case CounterType.TX_ENTITY: return ksNetStats.CounterType.TX_ENTITY;
                case CounterType.TX_ENTITY_COUNT: return ksNetStats.CounterType.TX_ENTITY_COUNT;
                default: throw new ArgumentException("Unknown counter type: " + type);
            }
        }
    }
}