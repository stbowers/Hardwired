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

            stringBuilder.AppendLine($"Power Target: {PowerTarget.ToStringPrefix("W", "yellow")} | PF: {PowerFactor}");
            stringBuilder.AppendLine($"Power Draw: {PowerDraw.ToStringPrefix("W", "yellow")} | PF: {PowerFactor}");
            stringBuilder.AppendLine($"ΔV: {VoltageDelta.ToStringPrefix(InputCircuit?.Frequency, "V", "yellow")} | Current Draw: {CurrentDraw.ToStringPrefix(InputCircuit?.Frequency, "A", "yellow")}");
            stringBuilder.AppendLine($"Energy Buffer: {EnergyBuffer.ToStringPrefix("Wt", "yellow")}");
        }

        public override void AddTo(Circuit circuit)
        {
            base.AddTo(circuit);

            _device ??= GetComponent<Device>();

            var nodeA = GetNode(circuit, PowerInput, WireType.Line1);

            _powerSink = new(circuit, nodeA, null);
        }

        public override void UpdateState(Circuit circuit)
        {
            base.UpdateState(circuit );

            if (_powerSink != null)
            {
                PowerTarget = _device?.GetUsedPower(_device.PowerCableNetwork) ?? 0f;

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
