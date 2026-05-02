#nullable enable

using System;
using System.Collections.Generic;
using Assets.Scripts.Util;
using Hardwired.Utility.Extensions;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    [Serializable]
    public class PowerProfile
    {
        public static readonly PowerProfile Default = new();

        /// <summary>
        /// The efficiency of this power profile.
        /// Power used will be multiplied by this value before being sent to the device.
        /// </summary>
        public double Efficiency = 1.0;

        /// <summary>
        /// The preferred AC frequency for the load, or 0 for DC.
        /// </summary>
        public double Frequency = 60.0;

        /// <summary>
        /// The maximum operational voltage a device can accept. If the input voltage is above this, the device will enter an overvoltage protection
        /// state and stop drawing power.
        /// </summary>
        public double VoltageMax = 250.0;

        /// <summary>
        /// The nominal operational voltage for a device. If voltage is at this value or above (up to V_max), the device will draw the target power.
        /// If voltage is below this value (down to V_min), the device enters a "brownout" state where it draws less power in proportion to voltage.
        /// </summary>
        public double VoltageNominal = 150.0;

        /// <summary>
        /// The maximum power draw that this power sink should target (i.e. PowerTarget will be capped to this value).
        /// 
        /// Most normal devices should already draw less than the default value of 2 kW in most circumstances, but this
        /// is mostly here for battery chargers which would otherwise draw too much power.
        /// 
        /// It could be possible to add an inline (series) resistor that players could add to limit power to devices, so
        /// that this value isn't needed (similar to base game), but at the moment without that component working around
        /// the high power draw of the APC or battery charger is kinda frustrating...
        /// </summary>
        public double MaximumPower = 2000;

        /// <summary>
        /// The nominal inductance of the load in Henrys
        /// </summary>
        public double Inductance = 0.0;

        /// <summary>
        /// The nominal capacitance of the load in Farads
        /// </summary>
        public double Capacitance = 0.0;

        public override string ToString()
            => $"{VoltageNominal}-{VoltageMax.ToStringPrefix("V")} {CurrentTypeString()} (Efficiency: {(int)(Efficiency * 100)}%)";

        private string CurrentTypeString()
            => Frequency switch
            {
                0.0f => $"DC",
                _ => $"~ {Frequency.ToStringPrefix("Hz")}"
            };
    }
}
