#nullable enable

using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Assets.Scripts.Networks;
using Assets.Scripts.Util;
using Hardwired.Simulation.Electrical;
using Hardwired.Simulation.Electrical.Elements;
using Hardwired.Utility.Extensions;

namespace Hardwired.Objects.Electrical
{
    /// <summary>
    /// Component for fixed ratio (i.e. passive) transformer devices.
    /// 
    /// note - "fixed" as in the ratio does not dynamically update each tick; however the ratio can be manually changed via the device's Setting value
    /// </summary>
    public class FixedTransformer : ElectricalComponent
    {
        MutualInductor? _mutualInductor;

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

            stringBuilder.AppendLine($"N: {Ratio}");
            stringBuilder.AppendLine($"ΔV(1): {PrimaryVoltage.ToStringPrefix(InputCircuit?.Frequency, "V", "yellow")} | ΔV(N): {SecondaryVoltage.ToStringPrefix(InputCircuit?.Frequency, "V", "yellow")}");
            stringBuilder.AppendLine($"I(1): {PrimaryCurrent.ToStringPrefix(InputCircuit?.Frequency, "A", "yellow")} | I(N): {SecondaryCurrent.ToStringPrefix(InputCircuit?.Frequency, "A", "yellow")}");
            stringBuilder.AppendLine($"P(1): {PrimaryApparentPower.ToStringPrefix("VA", "yellow")} | P(N): {SecondaryApparentPower.ToStringPrefix("VA", "yellow")}");
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
            var nodeC = GetNode(circuit, PowerOutput, WireType.Line1);

            // Nodes B & D are shared ground
            _mutualInductor = new(circuit, nodeA, null, nodeC, null);
            _mutualInductor.N = 2;
        }

        public override void UpdateState(Circuit circuit)
        {
            base.UpdateState(circuit);
        }

        public override void ApplyState(Circuit circuit)
        {
            base.ApplyState(circuit);

            PrimaryVoltage = _mutualInductor?.PrimaryVoltageDelta ?? 0f;
            SecondaryVoltage = _mutualInductor?.SecondaryVoltageDelta ?? 0f;
            PrimaryCurrent = _mutualInductor?.PrimaryCurrent ?? 0f;
            SecondaryCurrent = _mutualInductor?.SecondaryCurrent ?? 0f;
            PrimaryApparentPower = _mutualInductor?.PrimaryPower.Magnitude ?? 0f;
            SecondaryApparentPower = _mutualInductor?.SecondaryPower.Magnitude ?? 0f;
        }

        public override void RemoveFrom(Circuit circuit)
        {
            base.RemoveFrom(circuit);

            _mutualInductor?.Dispose();
            _mutualInductor = null;
        }
    }
}
