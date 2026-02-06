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

        public bool Closed => Device?.OnOff ?? false;

        public Complex VoltageA { get; private set;}

        public Complex VoltageB { get; private set;}

        public Complex VoltageDelta { get; private set; }

        public double Current { get; private set; }

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            stringBuilder.AppendLine($"Closed: {Closed}");
            stringBuilder.AppendLine($"ΔV: {VoltageDelta.ToStringPrefix(InputCircuit?.Frequency, "V", "yellow")} | VA: {VoltageA.ToStringPrefix(InputCircuit?.Frequency, "V", "yellow")} | VB: {VoltageB.ToStringPrefix(InputCircuit?.Frequency, "V", "yellow")}");
            stringBuilder.AppendLine($"Current: {Current.ToStringPrefix("A", "yellow")}");
        }

        public override void AddTo(Circuit circuit)
        {
            base.AddTo(circuit);

            var nodeA = GetNode(circuit, PowerInput, WireType.Line1);

            _switch?.Dispose();
            _switch = new(circuit, nodeA, null);
        }

        public override void UpdateState(Circuit circuit)
        {
            base.UpdateState(circuit);

            if (_switch != null)
            {
                _switch.Closed = Closed;
            }
        }

        public override void ApplyState(Circuit circuit)
        {
            base.ApplyState(circuit);

            VoltageA = _switch?.VoltageA.Magnitude ?? 0f;
            VoltageB = _switch?.VoltageB.Magnitude ?? 0f;
            VoltageDelta = _switch?.VoltageDelta ?? 0f;

            Current = _switch?.Current.Magnitude ?? 0f;
        }

        public override void RemoveFrom(Circuit circuit)
        {
            base.RemoveFrom(circuit);

            _switch?.Dispose();
            _switch = null;
        }
    }
}
