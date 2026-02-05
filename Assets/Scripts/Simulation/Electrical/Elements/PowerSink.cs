#nullable enable

using System;
using System.Numerics;
using Hardwired.Utility;

namespace Hardwired.Simulation.Electrical.Elements
{
    /// <summary>
    /// Represents a device in the circuit which draws some constant amount of power per tick
    /// </summary>
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
        /// The real power being drawn this tick.
        /// 
        /// Note that this is distinct from `Power`, which represents the power transferred through the circuit this tick, which may be different from the power draw since there is an internal energy buffer
        /// to dampen changes in voltage/current.
        /// </summary>
        public double PowerDraw { get; private set; }

        public override Complex Current => _energyBuffer.Current;

        public PowerSink(Circuit circuit, RefCounted<MNASolver.Unknown>? nodeA, RefCounted<MNASolver.Unknown>? nodeB) : base(circuit, nodeA, nodeB)
        {
            _energyBuffer = new(circuit, nodeA, nodeB);
        }

        public override void Dispose()
        {
            base.Dispose();

            _energyBuffer.Dispose();
        }

        public override void UpdateState()
        {
            base.UpdateState();

            _energyBuffer.Charge -= PowerDraw;

            _energyBuffer.CurrentMaximum = Profile.VoltageMax * PowerTarget / (Profile.VoltageNominal * Profile.VoltageNominal);
            _energyBuffer.ChargeMaximum = 1.25 * PowerTarget;
            _energyBuffer.VoltageMaximum = Profile.VoltageMax;
            _energyBuffer.UpdateState();
        }

        public override void ApplyState()
        {
            base.ApplyState();

            _energyBuffer.ApplyState();

            if ((VoltageDelta.Magnitude - Profile.VoltageMin) < -0.01
                || (VoltageDelta.Magnitude - Profile.VoltageMax) > 0.01
                || _energyBuffer.Charge < Profile.MinimumPowerDrawRatio * PowerTarget)
            {
                PowerDraw = 0;
            }
            else
            {
                PowerDraw = Math.Clamp(_energyBuffer.Charge, 0f, PowerTarget);
            }
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

            /// <summary>
            /// The minimum power that this device can draw, as a ratio of `PowerTarget`, and still function.
            /// 
            /// By default this will be `1.0` for most devices, meaning the device must have the actual power required available in order to function at all.
            /// Certain devices that have "brownout" behavior implemented may set this to less than 1.0 in order to draw less power as it's available.
            /// </summary>
            public double MinimumPowerDrawRatio;
        }
    }
}
