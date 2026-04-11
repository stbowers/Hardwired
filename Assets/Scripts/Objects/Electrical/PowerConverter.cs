#nullable enable

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Assets.Scripts.Networks;
using Assets.Scripts.Objects.Electrical;
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

        public double TargetOutputVoltage => (Device as Assets.Scripts.Objects.Electrical.Transformer)?.Setting ?? 200.0;

        public Complex InputVoltage { get; private set; }

        public Complex OutputVoltage { get; private set; }

        public Complex InputCurrent { get; private set; }

        public Complex OutputCurrent { get; private set; }

        public double InputPower { get; private set; }

        public double OutputPower { get; private set; }

        public double EnergyBuffer { get; private set; }

        public StringBuilder ToolTipInfo { get; } = new();

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            // If this is the only component, add a description (otherwise, only show debug values so we don't take up too much space...)
            if (GetComponents<ElectricalComponent>().Length == 1)
            {
                stringBuilder.AppendLine($"Produces a stable AC output voltage, provided the input voltage remains within its operating range.");
                stringBuilder.AppendLine($"Conceptually similar to a multi-tap transformer, variable transformer, or rectifier + inverter.");
                stringBuilder.AppendLine($"Modeled internally as a power sink on the input and a power source on the output, connected by an energy buffer.");
                stringBuilder.AppendLine($"The input stage charges the internal buffer, and the output stage draws power from it.");
                stringBuilder.AppendLine($"The buffer settles into equilibrium when input power equals output power.");
                stringBuilder.AppendLine($"Electrically isolates the input and output networks; unlike a fixed transformer, power (including reactive power) cannot flow from output back to input.");

                stringBuilder.AppendLine($"\n---\n");
            }

            stringBuilder.AppendLine($"ΔV(In): {InputVoltage.ToStringPrefix(InputCircuit?.Frequency, "V", "yellow")} | ΔV(Out): {OutputVoltage.ToStringPrefix("V", "yellow")}");
            stringBuilder.AppendLine($"I(In): {InputCurrent.ToStringPrefix(InputCircuit?.Frequency, "A", "yellow")} | I(Out): {OutputCurrent.ToStringPrefix(OutputCircuit?.Frequency, "A", "yellow")}");
            stringBuilder.AppendLine($"P(In): {InputPower.ToStringPrefix("VA", "yellow")} | P(Out): {OutputPower.ToStringPrefix("VA", "yellow")}");
            stringBuilder.AppendLine($"Energy Buffer: {EnergyBuffer.ToStringPrefix("VAt", "yellow")} / {(_powerSink?.EnergyBuffer.ChargeMaximum ?? 0).ToStringPrefix("VAt", "yellow")}");

            stringBuilder.AppendLine(ToolTipInfo.ToString());
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

            ToolTipInfo.Clear();

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
                if (Device is AreaPowerControl apc)
                {
                    _powerSource.PowerAvailable = apc.Battery?.PowerStored ?? 0f;
                }
                else
                {
                    _powerSource.PowerAvailable = _powerSink?.EnergyBuffer.Charge ?? 0;
                }

                _powerSource.VoltageNominal = TargetOutputVoltage;
                _powerSource.UpdateState();
            }
        }

        public override void ApplyState(Circuit circuit)
        {
            base.ApplyState(circuit);

            ToolTipInfo.AppendLine($"ApplyState({circuit.Id})");

            if (circuit == _powerSink?.Circuit)
            {
                _powerSink.ApplyState();

                InputVoltage = _powerSink.VoltageDelta;
                InputCurrent = _powerSink.Current;
                InputPower = _powerSink.Power.Real;
                EnergyBuffer = _powerSink.EnergyBuffer.Charge;

                if (Device is AreaPowerControl apc && apc.Battery != null)
                {
                    var powerUsed = Math.Clamp(apc.Battery.PowerMaximum - apc.Battery.PowerStored, 0, _powerSink.PowerAvailable);
                    apc.Battery.PowerStored += (float)powerUsed;
                    _powerSink.UsePower(powerUsed);

                    ToolTipInfo.AppendLine($"  - powerInput: {powerUsed}");
                }
            }

            if (circuit == _powerSource?.Circuit)
            {
                _powerSource.ApplyState();

                OutputPower = _powerSource.Power.Real;
                OutputVoltage = _powerSource.VoltageDelta;
                OutputCurrent = _powerSource.Current;

                if (Device is AreaPowerControl apc && apc.Battery != null)
                {
                    apc.Battery.PowerStored += (float)(_powerSink?.Power.Real ?? 0);
                }
                else
                {
                    _powerSink?.UsePower(-OutputPower);
                }

                ToolTipInfo.AppendLine($"  - powerOutput: {OutputPower}");

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
