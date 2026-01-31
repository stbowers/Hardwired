#nullable enable

using System;
using System.Numerics;
using Hardwired.Utility;

namespace Hardwired.Simulation.Electrical.Elements
{
    public class PowerSink : DipoleCircuitElementBase, ICircuitElement
    {
        private EnergyBuffer _energyBuffer;

        /// <summary>
        /// The power profile (i.e. nominal voltage, voltage tolerance, etc)
        /// </summary>
        public PowerProfile Profile { get; set; } = PowerProfile.Default;

        /// <summary>
        /// The target power in Watts.
        /// </summary>
        public double PowerTarget { get; set; }

        /// <summary>
        /// The minimum power draw in Watts - if available power is below this value, no power will be drawn this tick.
        /// </summary>
        public double MinimumPowerDraw { get; set; }

        /// <summary>
        /// The calculated power draw in Watts this tick.
        /// </summary>
        public double PowerDraw { get; private set; }

        /// <summary>
        /// The total power available to be drawn this tick.
        /// </summary>
        public double PowerAvailable { get; private set; }

        public override Complex Current => _energyBuffer.Current;

        public PowerSink(Circuit circuit, RefCounted<MNASolver.Unknown>? nodeA, RefCounted<MNASolver.Unknown>? nodeB) : base(circuit, nodeA, nodeB)
        {
            _energyBuffer = new(circuit, nodeA, nodeB);
        }

        public override void Dispose()
        {
            _energyBuffer.Dispose();
        }

        public override void UpdateState()
        {
            _energyBuffer.UpdateState();
        }

        public override void ApplyState()
        {
            _energyBuffer.ApplyState();

            PowerAvailable = _energyBuffer.Charge;
            PowerDraw = Math.Clamp(PowerAvailable, MinimumPowerDraw, PowerTarget);

            _energyBuffer.Charge -= PowerDraw;
        }

        public struct PowerProfile
        {
            public static readonly PowerProfile Default = new() { Frequency = 60f, VoltageMin = 50f, VoltageMax = 200f, VoltageNominal = 100f, Inductance = 0f, Capacitance = 0f };

            public static readonly PowerProfile SmallMotor = new() { Frequency = 60f, VoltageMin = 50f, VoltageMax = 200f, VoltageNominal = 100f, Inductance = 0.2f, Capacitance = 0f };

            public static readonly PowerProfile LargeMotor = new() { Frequency = 60f, VoltageMin = 50f, VoltageMax = 200f, VoltageNominal = 100f, Inductance = 0.5f, Capacitance = 0f };

            public static readonly PowerProfile LogicDevice = new() { Frequency = 0f, VoltageMin = 50f, VoltageMax = 200f, VoltageNominal = 100f, Inductance = 0f, Capacitance = 0f };

            /// <summary>
            /// The minimum operational voltage a device can accept. If the input voltage is below this, the device will enter an undervoltage protection
            /// state and stop drawing power.
            /// </summary>
            public double VoltageMin;

            /// <summary>
            /// The maximum operational voltage a device can accept. If the input voltage is above this, the device will enter an overvoltage protection
            /// state and stop drawing power.
            /// </summary>
            public double VoltageMax;

            /// <summary>
            /// The nominal operational voltage for a device. If voltage is at this value or above (up to V_max), the device will draw the target power.
            /// If voltage is below this value (down to V_min), the device enters a "brownout" state where it draws less power in proportion to voltage.
            /// </summary>
            public double VoltageNominal;

            /// <summary>
            /// The preferred AC frequency for the load.
            /// Not currently used; but will likely cause a small efficiency loss if not matched.
            /// </summary>
            public double Frequency;

            /// <summary>
            /// The nominal inductance of the load in Henrys
            /// </summary>
            public double Inductance;

            /// <summary>
            /// The nominal capacitance of the load in Farads
            /// </summary>
            public double Capacitance;
        }
    }
}
