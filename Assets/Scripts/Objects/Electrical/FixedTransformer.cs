#nullable enable

using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Assets.Scripts.Networks;
using Assets.Scripts.Util;
using Hardwired.Simulation.Electrical;
using Hardwired.Simulation.Electrical.Elements;
using Hardwired.Utility;
using Hardwired.Utility.Extensions;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    /// <summary>
    /// Component for fixed ratio (i.e. passive) transformer devices.
    /// 
    /// note - "fixed" as in the ratio does not dynamically update each tick; however the ratio can be manually changed via the device's Setting value
    /// </summary>
    public class FixedTransformer : ElectricalComponent
    {
        private Switch? _switch;
        private MutualInductor? _mutualInductor;
        private RefCounted<MNASolver.Unknown>? _internalPin;

        public double Ratio => (Device as Assets.Scripts.Objects.Electrical.Transformer)?.Setting ?? 0.0;

        public Complex PrimaryVoltage { get; private set; }

        public Complex SecondaryVoltage { get; private set; }

        public Complex PrimaryCurrent { get; private set; }

        public Complex SecondaryCurrent { get; private set; }

        public double PrimaryApparentPower { get; private set; }

        public double SecondaryApparentPower { get; private set; }

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            bool altKey = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            if (altKey)
            {
                stringBuilder.AppendLine($"Transforms AC power from one voltage level to another.");
                stringBuilder.AppendLine($"Modeled as two coupled inductors with a fixed winding ratio of 1:N.");
                stringBuilder.AppendLine($"The output voltage is N times the input voltage.");
                stringBuilder.AppendLine($"The output current is 1/N times the input current.");
                stringBuilder.AppendLine($"Power is conserved (ignoring losses), so increasing voltage reduces current proportionally.");
                stringBuilder.AppendLine($"Higher voltages are ideal for long transmission lines, since lower current reduces resistive losses in cables.");

                stringBuilder.AppendLine($"\n");
            }
            else
            {
                stringBuilder.AppendLine($"Press [alt] for description");
            }

            stringBuilder.AppendLine($"N: {Ratio}");
            stringBuilder.AppendLine($"ΔV(In): {PrimaryVoltage.ToStringPrefix(InputCircuit?.Frequency, "V", "yellow")} | ΔV(Out): {SecondaryVoltage.ToStringPrefix(InputCircuit?.Frequency, "V", "yellow")}");
            stringBuilder.AppendLine($"I(In): {PrimaryCurrent.ToStringPrefix(InputCircuit?.Frequency, "A", "yellow")} | I(Out): {SecondaryCurrent.ToStringPrefix(InputCircuit?.Frequency, "A", "yellow")}");
            stringBuilder.AppendLine($"P(In): {PrimaryApparentPower.ToStringPrefix("VA", "yellow")} | P(Out): {SecondaryApparentPower.ToStringPrefix("VA", "yellow")}");
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
            
            if (_mutualInductor != null)
            {
                RemoveFrom(_mutualInductor.Circuit);
            }

            var nodeA = GetNode(circuit, PowerInput, WireType.Line1);
            var nodeC = GetNode(circuit, PowerOutput, WireType.Line1);
            _internalPin = new(circuit.Solver.AddUnknown());

            _switch = new(circuit, nodeA, _internalPin);

            // Nodes B & D are shared ground
            _mutualInductor = new(circuit, _internalPin, null, nodeC, null);
            _mutualInductor.N = 2;
        }

        public override void UpdateState(Circuit circuit)
        {
            base.UpdateState(circuit);

            if (_switch != null)
            {
                _switch.Closed = Device?.OnOff == true;
            }

            _switch?.UpdateState();
            _mutualInductor?.UpdateState();
        }

        public override void ApplyState(Circuit circuit)
        {
            base.ApplyState(circuit);

            _switch?.ApplyState();
            _mutualInductor?.ApplyState();

            PrimaryVoltage = _mutualInductor?.PrimaryVoltageDelta ?? 0f;
            SecondaryVoltage = _mutualInductor?.SecondaryVoltageDelta ?? 0f;
            PrimaryCurrent = _mutualInductor?.PrimaryCurrent ?? 0f;
            SecondaryCurrent = _mutualInductor?.SecondaryCurrent ?? 0f;
            PrimaryApparentPower = _mutualInductor?.PrimaryPower.Magnitude ?? 0f;
            SecondaryApparentPower = _mutualInductor?.SecondaryPower.Magnitude ?? 0f;

            Device?.SetPowerFromThread(InputCableNetwork, Device?.OnOff == true);
        }

        public override void RemoveFrom(Circuit circuit)
        {
            base.RemoveFrom(circuit);

            _mutualInductor?.Dispose();
            _mutualInductor = null;

            _switch?.Dispose();
            _switch = null;

            _internalPin?.Dispose();
            _internalPin = null;
        }
    }
}
