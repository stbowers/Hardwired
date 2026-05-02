#nullable enable

using System;
using System.Collections.Generic;
using Assets.Scripts.Util;
using Hardwired.Utility.Extensions;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    /// <summary>
    /// Describes a set of design parameters (voltage range, frequency, etc) for power sinks and power sources.
    /// </summary>
    [Serializable]
    public class PowerProfile
    {
        /// <summary>
        /// Default power profile for devices that directly connect to the "grid" (~200V AC, default for most consumers)
        /// </summary>
        public static readonly PowerProfile DefaultGrid = new() { Frequency = 60, VoltageMaximum = 250, VoltageNominal = 200, VoltageMinimum = 150 };

        /// <summary>
        /// Default profile for generators, and other power production/infrastructure related devices (~500V DC)
        /// </summary>
        public static readonly PowerProfile DefaultGenerator = new() { Frequency = 0, VoltageMaximum = 550, VoltageNominal = 500, VoltageMinimum = 450 };

        /// <summary>
        /// The efficiency of this power profile.
        /// Power used will be multiplied by this value before being sent to the device.
        /// </summary>
        public double Efficiency = 1.0;

        /// <summary>
        /// The design circuit AC frequency, or 0 for DC.
        /// </summary>
        public double Frequency = 60.0;

        /// <summary>
        /// The maximum design voltage for this profile.
        /// 
        /// Power sinks: This will be the maximum voltage the device can accept (it will not draw power if voltage is higher). This is also
        ///   the voltage at which the buffer will have a full charge.
        /// Power sources: This voltage is not generally used.
        /// </summary>
        public double VoltageMaximum = 250.0;

        /// <summary>
        /// The nominal design voltage.
        /// 
        /// Power sinks: This voltage is not generally used.
        /// Power sources: This will be the target output voltage (i.e. output voltage under no load)
        /// </summary>
        public double VoltageNominal = 200.0;

        /// <summary>
        /// The minimum design voltage for this profile.
        /// 
        /// Power sinks: This will be the voltage at which the sink is designed to draw the power target (lower voltage = brownout, higher voltage = charge buffer)
        /// Power sources: This will be the voltage the source is designed to "droop" to when at maximum load
        /// </summary>
        public double VoltageMinimum = 150.0;

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

        public PowerProfile() { }

        public PowerProfile(PowerProfile other)
        {
            Efficiency = other.Efficiency;
            Frequency = other.Frequency;
            VoltageMaximum = other.VoltageMaximum;
            VoltageNominal = other.VoltageNominal;
            VoltageMinimum = other.VoltageMinimum;
            MaximumPower = other.MaximumPower;
            Inductance = other.Inductance;
            Capacitance = other.Capacitance;
        }

        public override string ToString()
            => $"{VoltageMinimum}-{VoltageMaximum.ToStringPrefix("V")} {CurrentTypeString()} (Efficiency: {(int)(Efficiency * 100)}%)";

        private string CurrentTypeString()
            => Frequency switch
            {
                0.0f => $"DC",
                _ => $"~ {Frequency.ToStringPrefix("Hz")}"
            };
    }
}
