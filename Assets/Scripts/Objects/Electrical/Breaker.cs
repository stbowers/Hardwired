#nullable enable

using System;
using System.Numerics;
using System.Text;
using Assets.Scripts.Objects;
using Assets.Scripts.Util;
using Hardwired.Simulation.Electrical;
using Hardwired.Simulation.Electrical.Elements;
using Hardwired.Utility.Extensions;
using MathNet.Numerics;
using Objects.Pipes;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    public class Breaker : ElectricalComponent
    {
        private Assets.Scripts.Objects.Pipes.Device? _device;
        private Switch? _switch;

        public ConnectionRole Connection = ConnectionRole.Input;

        public double MaximumNodeVoltage { get; private set; }

        public double Current { get; private set; }

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            // stringBuilder.AppendLine($"-- Breaker --");
            // stringBuilder.AppendLine($"Closed: {Closed}");
            // stringBuilder.AppendLine($"Vcc: {VoltageGround.ToStringPrefix("V", "yellow")}");
            // stringBuilder.AppendLine($"ΔV: {VoltageDrop.ToStringPrefix("V", "yellow")}");
            // stringBuilder.AppendLine($"Current: {Current.ToStringPrefix("A", "yellow")}");
        }

        public override void AddTo(Circuit circuit)
        {
            base.AddTo(circuit);

            if (_device == null)
            {
                TryGetComponent(out _device);
            }

            var nodeA = GetNode(circuit, PowerInput, WireType.Line1);
            var nodeB = GetNode(circuit, PowerInput, WireType.Line1);

            _switch?.Dispose();
            _switch = new(circuit, nodeA, nodeB);
        }

        public override void UpdateState(Circuit circuit)
        {
            base.UpdateState(circuit);

            if (_switch != null)
            {
                _switch.Closed = _device?.OnOff ?? false;
                _switch.UpdateState();
            }
        }

        public override void ApplyState(Circuit circuit)
        {
            base.ApplyState(circuit);

            var nodeVoltageA = _switch?.VoltageA.Magnitude ?? 0f;
            var nodeVoltageB = _switch?.VoltageB.Magnitude ?? 0f;
            MaximumNodeVoltage = Math.Max(nodeVoltageA, nodeVoltageB);

            Current = _switch?.Current.Magnitude ?? 0f;
        }

        public override void RemoveFrom(Circuit circuit)
        {
            base.RemoveFrom(circuit);
        }
    }
}
