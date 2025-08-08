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
using UnityEngine;

namespace KS.Reactor.Client.Unity
{
    /// <summary>
    /// Unity implementation of <see cref="ksIInputManager"/>. Translates Unity inputs to <see cref="ksInput"/> 
    /// and integrates it with direct input.
    /// </summary>
    public class ksInputManager : ksIInputManager
    {
        /// <summary>Input validation handler.</summary>
        /// <returns>False to ignore the input.</returns>
        public delegate bool Validator();

        /// <summary>Callback to get an input value.</summary>
        /// <returns>Value</returns>
        public delegate ksMultiType ValueGetter();

        private Dictionary<uint, Button> m_buttonMap = new Dictionary<uint, Button>();
        private Dictionary<uint, Axis> m_axisMap = new Dictionary<uint, Axis>();
        private Dictionary<uint, Value> m_valueMap = new Dictionary<uint, Value>();

        /// <summary>
        /// If false, all axes will be zero, all buttons will be up, and all values will be the null multitype.
        /// </summary>
        public bool Enabled
        {
            get { return m_enabled; }
            set { m_enabled = value; }
        }
        private bool m_enabled = true;

        /// <summary>Class for retrieving button input from Unity or direct input.</summary>
        private class Button
        {
            /// <summary>Name of a Unity input button.</summary>
            public string Name;

            /// <summary>Value directly assigned to the input.</summary>
            public bool DirectValue;

            /// <summary>Input validation handler.</summary>
            public Validator Validator;

            /// <summary>Ignore input.</summary>
            public bool Ignored = false;

            /// <summary>Default constructor</summary>
            public Button()
            {
                Name = null;
                DirectValue = false;
            }

            /// <summary>Constructor</summary>
            /// <param name="name">Name of a Unity input button.</param>
            /// <param name="value">Value directly assigned to the input.</param>
            /// <param name="validator">Input validation handler.</param>
            public Button(string name, bool value, Validator validator)
            {
                Name = name;
                DirectValue = value;
                Validator = validator;
            }

            /// <summary>
            /// Gets the button state. If the button state was set to true using 
            /// <see cref="ksInput.SetButton(uint, bool)"/>, then return true. Otherwise gets the button state from
            /// the bound Unity input. If a validator was given and the validator returned false when the Unity button
            /// was first pressed, the entire button press will be ignored.
            /// </summary>
            /// <returns>Input value</returns>
            public bool GetValue()
            {
                if (Name != null)
                {
                    if (Ignored)
                    {
                        if (!Input.GetButton(Name))
                        {
                            Ignored = false;
                        }
                    }
                    else if (Validator != null && Input.GetButton(Name) && !Validator())
                    {
                        Ignored = true;
                    }
                }
                return DirectValue || (!Ignored && Name != null && Input.GetButton(Name));
            }
        }

        /// <summary>Class for retrieving axis input from Unity or direct input.</summary>
        private class Axis
        {
            /// <summary>Name of the Unity input axis.</summary>
            public string Name;

            /// <summary>Use the raw Unity axis input.</summary>
            public bool IsRaw;

            /// <summary>Value directly assigned to the input.</summary>
            public float DirectValue;

            /// <summary>Default constructor</summary>
            public Axis()
            {
                Name = null;
                IsRaw = false;
                DirectValue = 0;
            }

            /// <summary>Constructor</summary>
            /// <param name="name">Name of the Unity input axis.</param>
            /// <param name="isRaw">Use the raw Unity axis input.</param>
            /// <param name="value">Value directly assigned to the input.</param>
            public Axis(string name, bool isRaw, float value)
            {
                Name = name;
                IsRaw = isRaw;
                DirectValue = value;
            }

            /// <summary>Get the axis value.</summary>
            /// <returns>Axis value</returns>
            public float GetValue()
            {
                if (Name == null)
                {
                    return DirectValue;
                }
                float unityValue = IsRaw ? Input.GetAxisRaw(Name) : Input.GetAxis(Name);
                return Math.Abs(unityValue) > Math.Abs(DirectValue) ? unityValue : DirectValue;
            }
        }

        /// <summary>Class for retrieving value input from a callback or direct input.</summary>
        private class Value
        {
            /// <summary>Callback to get the value.</summary>
            public ValueGetter Getter;

            /// <summary>Value directly assigned to the input.</summary>
            public ksMultiType DirectValue;

            /// <summary>Default constructor</summary>
            public Value()
            {

            }

            /// <summary>Constructor</summary>
            /// <param name="getter">Callback to get the value.</param>
            public Value(ValueGetter getter)
            {
                Getter = getter;
            }

            /// <summary>
            /// Gets the value. Returns <see cref="DirectValue"/> if it is not a null type. Otherwise calls the
            /// <see cref="Getter"/> if it is set, or returns int 0.
            /// </summary>
            /// <returns>Value</returns>
            public ksMultiType GetValue()
            {
                if (!DirectValue.IsNull)
                {
                    return DirectValue;
                }
                return Getter == null ? 0 : Getter();
            }
        }

        /// <summary>Constructor</summary>
        internal ksInputManager()
        {

        }

        /// <summary>Binds a button ID to a Unity button.</summary>
        /// <param name="button">Button ID</param>
        /// <param name="name">Unity button name</param>
        /// <param name="validator">Input validation handler.</param>
        public void BindButton(uint button, string name, Validator validator = null)
        {
            bool value = false;
            if (m_buttonMap.ContainsKey(button))
            {
                Button bu = m_buttonMap[button];
                value = bu.DirectValue;
                ksLog.Warning(this, "Button " + button + " is already bound with " + bu.Name + ". Rebinding with " + name);
            }
            m_buttonMap[button] = new Button(name, value, validator);
        }

        /// <summary>Binds an axis ID to a Unity axis.</summary>
        /// <param name="axis">Axis ID</param>
        /// <param name="name">Unity axis name</param>
        public void BindAxis(uint axis, string name)
        {
            float value = 0;
            if (m_axisMap.ContainsKey(axis))
            {
                Axis ax = m_axisMap[axis];
                value = ax.DirectValue;
                string oldName = ax.IsRaw ? "raw " + ax.Name : ax.Name;
                ksLog.Warning(this, "Axis " + axis + " is already bound with " + oldName + ". Rebinding with " + name);
            }
            m_axisMap[axis] = new Axis(name, false, value);
        }

        /// <summary>Binds an axis ID to a raw Unity axis.</summary>
        /// <param name="axis">Axis ID</param>
        /// <param name="name">Unity axis name</param>
        public void BindRawAxis(uint axis, string name)
        {
            float value = 0;
            if (m_axisMap.ContainsKey(axis))
            {
                Axis ax = m_axisMap[axis];
                value = ax.DirectValue;
                string oldName = ax.IsRaw ? "raw " + ax.Name : ax.Name;
                ksLog.Warning(this, "Axis " + axis + " is already bound with " + oldName + ". Rebinding with raw " + name);
            }
            m_axisMap[axis] = new Axis(name, true, value);
        }

        /// <summary>Binds a value id to a callback to get the input value.</summary>
        /// <param name="valueId">Id of value to bind.</param>
        /// <param name="getter">Callback for getting the value to bind to.</param>
        public void BindValue(uint valueId, ValueGetter getter)
        {
            Value val;
            if (m_valueMap.TryGetValue(valueId, out val))
            {
                val.Getter = getter;
                ksLog.Warning(this, "Value " + valueId + " is already bound to a callback. Rebinding.");
            }
            else
            {
                m_valueMap[valueId] = new Value(getter);
            }
        }

        /// <summary>Sets a button down state.</summary>
        /// <param name="button">Button ID</param>
        /// <param name="down">True if the button is down.</param>
        public void SetButton(uint button, bool down)
        {
            Button bu;
            if (!m_buttonMap.TryGetValue(button, out bu))
            {
                bu = new Button();
                m_buttonMap[button] = bu;
            }
            bu.DirectValue = down;
        }

        /// <summary>Sets an axis value.</summary>
        /// <param name="axis">Axis ID</param>
        /// <param name="value">Axis value</param>
        public void SetAxis(uint axis, float value)
        {
            if (float.IsNaN(value))
            {
                ksLog.Warning(this, "Invalid axis value NaN for axis " + axis + ". Setting to 0.");
                value = 0;
            }
            else if (value < -1 || value > 1)
            {
                ksLog.Warning(this, "Axis value " + value + " for axis " + axis +
                    " will be clamped to the range [-1, 1].");
                value = Math.Max(-1, value);
                value = Math.Min(1, value);
            }

            Axis ax;
            if (!m_axisMap.TryGetValue(axis, out ax))
            {
                ax = new Axis();
                m_axisMap[axis] = ax;
            }
            ax.DirectValue = value;
        }

        /// <summary>Sets a value.</summary>
        /// <param name="valueId">Id of value to set.</param>
        /// <param name="value">Value to set.</param>
        public void SetValue(uint valueId, ksMultiType value)
        {
            Value val;
            if (!m_valueMap.TryGetValue(valueId, out val))
            {
                val = new Value();
                m_valueMap[valueId] = val;
            }
            val.DirectValue = value;
        }

        /// <summary>Checks if a button is up or down.</summary>
        /// <param name="buttonId">Id of button to check.</param>
        /// <returns>True if the button is down.</returns>
        public bool GetButton(uint buttonId)
        {
            Button button;
            if (m_enabled && m_buttonMap.TryGetValue(buttonId, out button))
            {
                return button.GetValue();
            }
            return false;
        }

        /// <summary>Gets an axis value.</summary>
        /// <param name="axisId">Id of the axis value to get.</param>
        /// <returns>Axis value</returns>
        public float GetAxis(uint axisId)
        {
            Axis axis;
            if (m_enabled && m_axisMap.TryGetValue(axisId, out axis))
            {
                return axis.GetValue();
            }
            return 0f;
        }

        /// <summary>Gets a value.</summary>
        /// <param name="valueId">Id of value to get.</param>
        /// <returns>Value</returns>
        public ksMultiType GetValue(uint valueId)
        {
            Value value;
            if (m_enabled && m_valueMap.TryGetValue(valueId, out value))
            {
                return value.GetValue();
            }
            return new ksMultiType();  
        }
    }
}
