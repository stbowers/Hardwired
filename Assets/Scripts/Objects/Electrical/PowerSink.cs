#nullable enable

using System;
using System.Linq;
using System.Numerics;
using System.Text;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Pipes;
using Assets.Scripts.Util;
using Hardwired.Simulation.Electrical;
using Hardwired.Simulation.Electrical.Elements;
using Hardwired.Utility.Extensions;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    public class PowerSink : ElectricalComponent
    {
        private Device? _device;
        private EnergyBuffer? _energyBuffer;

        /// <summary>
        /// The maximum operational voltage a device can accept. If the input voltage is above this, the device will enter an overvoltage protection
        /// state and stop drawing power.
        /// </summary>
        public double VoltageMax = 200f;

        /// <summary>
        /// The nominal operational voltage for a device. If voltage is at this value or above (up to V_max), the device will draw the target power.
        /// If voltage is below this value (down to V_min), the device enters a "brownout" state where it draws less power in proportion to voltage.
        /// </summary>
        public double VoltageNominal = 100f;

        /// <summary>
        /// The preferred AC frequency for the load.
        /// Not currently used; but will likely cause a small efficiency loss if not matched.
        /// </summary>
        public double Frequency = 60f;

        /// <summary>
        /// The nominal inductance of the load in Henrys
        /// </summary>
        public double Inductance = 0f;

        /// <summary>
        /// The nominal capacitance of the load in Farads
        /// </summary>
        public double Capacitance = 0f;

        /// <summary>
        /// The minimum power that this device can draw, as a ratio of `PowerTarget`, and still function.
        /// 
        /// By default this will be `1.0` for most devices, meaning the device must have the actual power required available in order to function at all.
        /// Certain devices that have "brownout" behavior implemented may set this to less than 1.0 in order to draw less power as it's available.
        /// </summary>
        public double MinimumPowerDrawRatio = 1f;

        public double PowerTarget { get; private set; }

        public double PowerDraw { get; private set; }

        public double PowerFactor { get; private set; }

        public Complex VoltageDelta { get; private set; }

        public Complex CurrentDraw { get; private set; }

        public double BufferCharge { get; private set; }

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            bool altKey = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            if (altKey)
            {
                stringBuilder.AppendLine($"Consumes electrical power from the circuit.");
                stringBuilder.AppendLine($"Modeled as a constant impedance (resistive or resistive + reactive) in series with a controllable voltage source, and an internal energy buffer.");
                stringBuilder.AppendLine($"As the energy buffer charges, the internal voltage source increases, pushing back on the circuit and reducing current draw on the next tick.");
                stringBuilder.AppendLine($"The system settles into equilibrium when input power equals the device's power consumption.");
                stringBuilder.AppendLine($"Each device has a nominal, minimum, and maximum design voltage; if voltage is above maximum or below minimum, it will not draw power.");
                stringBuilder.AppendLine($"If input voltage is above nominal (but below maximum), excess power charges the buffer until equilibrium is reached.");
                stringBuilder.AppendLine($"If input voltage is below nominal (but above minimum), the device enters a brownout state where available power is limited.");

                stringBuilder.AppendLine($"\n");
            }
            else
            {
                stringBuilder.AppendLine($"Press [alt] for description");
            }

            stringBuilder.AppendLine($"Power Target: {PowerTarget.ToStringPrefix("W", "yellow")}");
            stringBuilder.AppendLine($"Power Draw: {PowerDraw.ToStringPrefix("W", "yellow")} | PF: {PowerFactor}");
            stringBuilder.AppendLine($"ΔV: {VoltageDelta.ToStringPrefix(InputCircuit?.Frequency, "V", "yellow")} | Current Draw: {CurrentDraw.ToStringPrefix(InputCircuit?.Frequency, "A", "yellow")}");
            stringBuilder.AppendLine($"Buffer charge: {BufferCharge.ToStringPrefix("Wt", "yellow")} / {(_energyBuffer?.ChargeMaximum ?? 0).ToStringPrefix("Wt", "yellow")}");
        }

        public override void AddTo(Circuit circuit)
        {
            base.AddTo(circuit);

            _device ??= GetComponent<Device>();

            if (InputCircuit == circuit)
            {
                if (_energyBuffer != null)
                {
                    RemoveFrom(_energyBuffer.Circuit);
                }

                var nodeA = GetNode(circuit, PowerInput, WireType.Line1);

                _energyBuffer = new(circuit, nodeA, null);
            }
        }

        public override void UpdateState(Circuit circuit)
        {
            base.UpdateState(circuit );

            if (_energyBuffer != null)
            {
                PowerTarget = _device?.GetUsedPower(InputCableNetwork) ?? 0f;

                // Use a minimum power target of 10 W, to avoid dividing by zero
                var powerTarget = Math.Max(10, PowerTarget);

                _energyBuffer.Resistance = VoltageNominal * VoltageNominal / powerTarget;
                _energyBuffer.ChargeMaximum = Math.Max(4 * powerTarget, _energyBuffer.ChargeMaximum);

                _energyBuffer.VoltageMaximum = 2 * VoltageMax;
                _energyBuffer.VoltageCurve = EnergyBuffer.VoltageCurveFunction.Linear;

                _energyBuffer.UpdateState();
            }
        }

        public override void ApplyState(Circuit circuit)
        {
            base.ApplyState(circuit);

            _energyBuffer?.ApplyState();

            BufferCharge = _energyBuffer?.Charge ?? 0;
            PowerFactor = _energyBuffer?.PowerFactor ?? 0;
            VoltageDelta = _energyBuffer?.VoltageDelta ?? 0;
            CurrentDraw = _energyBuffer?.Current ?? 0;

            PowerDraw = Math.Min(BufferCharge, PowerTarget);

            if (_energyBuffer != null)
            {
                _energyBuffer.Charge -= PowerDraw;
            }

            var minPower = MinimumPowerDrawRatio * PowerTarget;

            _device?.ReceivePower(_device.PowerCableNetwork, (float)PowerDraw);
            _device?.SetPowerFromThread(_device.PowerCableNetwork, PowerDraw >= minPower).Forget();
        }

        public override void RemoveFrom(Circuit circuit)
        {
            base.RemoveFrom(circuit);

            _energyBuffer?.Dispose();
            _energyBuffer = null;
        }
    }
}
