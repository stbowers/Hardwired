#nullable enable

using System;
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
    /// Component for a power converter (i.e. draws power from one circuit, and supplies it to another, potentially converting one or more parameters such as voltage, frequency, etc, in the process)
    /// </summary>
    public class PowerConverter : ElectricalComponent
    {
        private PowerSink? _powerSink;
        private PowerSource? _powerSource;

        public double TargetOutputVoltage => (Device as Assets.Scripts.Objects.Electrical.Transformer)?.Setting ?? 0.0;

        public Complex InputVoltage { get; private set; }

        public Complex OutputVoltage { get; private set; }

        public Complex InputCurrent { get; private set; }

        public Complex OutputCurrent { get; private set; }

        public double InputPower { get; private set; }

        public double OutputPower { get; private set; }

        public double EnergyBuffer { get; private set; }

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            stringBuilder.AppendLine($"ΔV(In): {InputVoltage.ToStringPrefix(InputCircuit?.Frequency, "V", "yellow")} | ΔV(Out): {OutputVoltage.ToStringPrefix("V", "yellow")}");
            stringBuilder.AppendLine($"I(In): {InputCurrent.ToStringPrefix(InputCircuit?.Frequency, "A", "yellow")} | I(Out): {OutputCurrent.ToStringPrefix(OutputCircuit?.Frequency, "A", "yellow")}");
            stringBuilder.AppendLine($"P(In): {InputPower.ToStringPrefix("VA", "yellow")} | P(Out): {OutputPower.ToStringPrefix("VA", "yellow")}");
            stringBuilder.AppendLine($"Energy Buffer: {EnergyBuffer.ToStringPrefix("VAt", "yellow")}");

            stringBuilder.AppendLine($"src.Resistance: {_powerSource?.NortonEquivalent.Resistance.ToStringPrefix("Ω", "yellow")}");
            stringBuilder.AppendLine($"src.Current: {_powerSource?.NortonEquivalent.CurrentShort.ToStringPrefix("A", "yellow")}");
            stringBuilder.AppendLine($"sink.Resistance: {_powerSink?.EnergyBuffer.Resistance.ToStringPrefix("Ω", "yellow")}");
            stringBuilder.AppendLine($"sink.Charge: {_powerSink?.EnergyBuffer.Charge.ToStringPrefix("Wt", "yellow")} / {_powerSink?.EnergyBuffer.ChargeMaximum.ToStringPrefix("Wt", "yellow")}");
        }

        public override void AddTo(Circuit circuit)
        {
            base.AddTo(circuit);

            if (circuit == InputCircuit)
            {
                var nodeA = GetNode(circuit, PowerInput, WireType.Line1);

                _powerSink?.Dispose();
                _powerSink = new(circuit, nodeA, null) { PowerTarget = 5000 };
            }

            if (circuit == OutputCircuit)
            {
                var nodeC = GetNode(circuit, PowerOutput, WireType.Line1);

                _powerSource?.Dispose();
                _powerSource = new(circuit, nodeC, null) { Frequency = 60 };
            }
        }

        public override void UpdateState(Circuit circuit)
        {
            base.UpdateState(circuit);

            if (_powerSink != null && InputCircuit != _powerSink.Circuit)
            {
                RemoveFrom(_powerSink.Circuit);
            }

            if (_powerSource != null && OutputCircuit != _powerSource.Circuit)
            {
                RemoveFrom(_powerSource.Circuit);
            }

            if (circuit == _powerSink?.Circuit)
            {
                _powerSink.UpdateState();
            }

            if (circuit == _powerSource?.Circuit)
            {
                _powerSource.PowerAvailable = _powerSink?.EnergyBuffer.Charge ?? 0;
                _powerSource.VoltageNominal = TargetOutputVoltage;

                _powerSource.UpdateState();
            }
        }

        public override void ApplyState(Circuit circuit)
        {
            base.ApplyState(circuit);

            InputVoltage = _powerSink?.VoltageDelta ?? 0f;
            OutputVoltage = _powerSource?.VoltageDelta ?? 0f;
            InputCurrent = _powerSink?.Current ?? 0f;
            OutputCurrent = _powerSource?.Current ?? 0f;
            InputPower = _powerSink?.Power.Real ?? 0f;
            OutputPower = _powerSource?.Power.Real ?? 0f;
            EnergyBuffer = _powerSink?.EnergyBuffer.Charge ?? 0f;

            if (circuit == _powerSink?.Circuit)
            {
                _powerSink.ApplyState();
                _powerSink.UsePower(-OutputPower);
            }

            if (circuit == _powerSource?.Circuit)
            {
                _powerSource.ApplyState();
            }

        }

        public override void RemoveFrom(Circuit circuit)
        {
            base.RemoveFrom(circuit);

            if (circuit == _powerSink?.Circuit)
            {
                _powerSink?.Dispose();
                _powerSink = null;
            }

            if (circuit == _powerSource?.Circuit)
            {
                _powerSource?.Dispose();
                _powerSource = null;
            }
        }
    }
}
