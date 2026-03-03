#nullable enable

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Assets.Scripts.Networks;
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

            // If this is the only component, add a description (otherwise, only show debug values so we don't take up too much space...)
            if (GetComponents<ElectricalComponent>().Length == 1)
            {
                stringBuilder.AppendLine($"Interrupts or allows power flow in the circuit.");
                stringBuilder.AppendLine($"When closed, it is modeled as a small series resistance between input and output.");
                stringBuilder.AppendLine($"When open, it is modeled as a very large resistance to prevent current flow.");
                stringBuilder.AppendLine($"A large but finite open resistance improves numerical stability in the circuit solver.");
                stringBuilder.AppendLine($"The over-current varient will automatically trip open if current exceeds a configurable threshold.");
                stringBuilder.AppendLine($"The under-voltage varient will automatically trip open if voltage to ground falls below a set threshold.");

                stringBuilder.AppendLine($"\n---\n");
            }

            stringBuilder.AppendLine($"Closed: {Closed}");
            stringBuilder.AppendLine($"ΔV: {VoltageDelta.ToStringPrefix(InputCircuit?.Frequency, "V", "yellow")} | VA: {VoltageA.ToStringPrefix(InputCircuit?.Frequency, "V", "yellow")} | VB: {VoltageB.ToStringPrefix(InputCircuit?.Frequency, "V", "yellow")}");
            stringBuilder.AppendLine($"Current: {Current.ToStringPrefix("A", "yellow")}");
        }

        public override IEnumerable<CableNetwork> GetBridgedNetworks(CableNetwork network)
        {
            CableNetwork? inputNetwork = PowerInput?.GetCable()?.CableNetwork;
            CableNetwork? outputNetwork = PowerOutput?.GetCable()?.CableNetwork;

            if (network == inputNetwork && outputNetwork != null)
            {
                yield return outputNetwork;
            }
            else if (network == outputNetwork && inputNetwork != null)
            {
                yield return inputNetwork;
            }
        }

        public override void AddTo(Circuit circuit)
        {
            base.AddTo(circuit);

            var nodeA = GetNode(circuit, PowerInput, WireType.Line1);
            var nodeB = GetNode(circuit, PowerOutput, WireType.Line1);
            
            if (_switch?.NodeA != nodeA || _switch?.NodeB != nodeB)
            {
                _switch?.Dispose();
                _switch = new(circuit, nodeA, nodeB);
            }
        }

        public override void UpdateState(Circuit circuit)
        {
            base.UpdateState(circuit);

            if (_switch != null)
            {
                _switch.Closed = Closed;
                _switch.UpdateState();
            }
        }

        public override void ApplyState(Circuit circuit)
        {
            base.ApplyState(circuit);

            _switch?.ApplyState();

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
