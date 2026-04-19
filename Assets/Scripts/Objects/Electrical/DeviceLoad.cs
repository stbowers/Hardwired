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
    public class DeviceLoad : ElectricalComponent
    {
        private Device? _device;
        private PowerSink? _powerSink;

        public PowerSink.PowerProfile PowerProfile { get; set; } = PowerSink.PowerProfile.Default;

        public double PowerTarget { get; private set; }

        public double PowerDraw { get; private set; }

        public double PowerFactor { get; private set; }

        public Complex VoltageDelta { get; private set; }

        public Complex CurrentDraw { get; private set; }

        public double EnergyBuffer { get; private set; }

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
            stringBuilder.AppendLine($"Energy Buffer: {EnergyBuffer.ToStringPrefix("Wt", "yellow")} / {(_powerSink?.EnergyBuffer.ChargeMaximum ?? 0).ToStringPrefix("Wt", "yellow")}");
        }

        public override void AddTo(Circuit circuit)
        {
            base.AddTo(circuit);

            _device ??= GetComponent<Device>();

            if (InputCircuit == circuit)
            {
                if (_powerSink != null)
                {
                    RemoveFrom(_powerSink.Circuit);
                }

                var nodeA = GetNode(circuit, PowerInput, WireType.Line1);

                _powerSink = new(circuit, nodeA, null);
            }
        }

        public override void UpdateState(Circuit circuit)
        {
            base.UpdateState(circuit );

            if (_powerSink != null)
            {
                PowerTarget = _device?.GetUsedPower(InputCableNetwork) ?? 0f;

                _powerSink.Profile = PowerProfile;
                _powerSink.PowerTarget = PowerTarget;

                _powerSink.UpdateState();
            }
        }

        public override void ApplyState(Circuit circuit)
        {
            base.ApplyState(circuit);

            _powerSink?.ApplyState();

            EnergyBuffer = _powerSink?.EnergyBuffer.Charge ?? 0;
            PowerDraw = Math.Min(_powerSink?.PowerAvailable ?? 0, PowerTarget);
            PowerFactor = _powerSink?.PowerFactor ?? 0;
            VoltageDelta = _powerSink?.VoltageDelta ?? 0;
            CurrentDraw = _powerSink?.Current ?? 0;

            if (PowerDraw > 0f)
            {
                _powerSink?.UsePower(PowerDraw);
                _device?.ReceivePower(_device.PowerCableNetwork, (float)PowerDraw);
                _device?.SetPowerFromThread(_device.PowerCableNetwork, true).Forget();
            }
            else
            {
                _device?.SetPowerFromThread(_device.PowerCableNetwork, false).Forget();
            }
        }

        public override void RemoveFrom(Circuit circuit)
        {
            base.RemoveFrom(circuit);

            _powerSink?.Dispose();
            _powerSink = null;
        }
    }
}
