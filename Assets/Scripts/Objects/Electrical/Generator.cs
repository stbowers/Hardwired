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

namespace Hardwired.Objects.Electrical
{
    public class Generator : ElectricalComponent
    {
        private PowerSource? _powerSource;

        public double PowerGenerated { get; private set; }

        public double PowerDraw { get; private set; }

        public double PowerFactor { get; private set; }

        public Complex VoltageDelta { get; private set; }

        public Complex CurrentDraw { get; private set; }

        public double Charge { get; private set; }

        public double ChargeMaximum { get; set; } = 2500;

        public double VoltageMaximum { get; set; } = 200;

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            stringBuilder.AppendLine($"Supplies electrical power to the circuit.");
            stringBuilder.AppendLine($"Modeled as a non-ideal voltage source with a maximum output voltage and a series power-limiting resistor.");
            stringBuilder.AppendLine($"Includes an internal energy buffer that is charged each tick by generated power and discharged by the load.");
            stringBuilder.AppendLine($"As current increases, the series resistance causes the output voltage to droop.");
            stringBuilder.AppendLine($"Maximum power draw occurs at 1/2 · V_max and is limited by the available energy in the buffer.");
            stringBuilder.AppendLine($"The internal buffer smooths grid transients and is computationally cheaper to simulate than a fully non-linear power source/sink.");

            stringBuilder.AppendLine($"\n---\n");

            stringBuilder.AppendLine($"Power Generated: {PowerGenerated.ToStringPrefix("W", "yellow")}");
            stringBuilder.AppendLine($"Current Draw: {CurrentDraw.ToStringPrefix(InputCircuit?.Frequency, "A", "yellow")}");
            stringBuilder.AppendLine($"Power Draw: {PowerDraw.ToStringPrefix("W", "yellow")} | PF: {PowerFactor}");
            stringBuilder.AppendLine($"ΔV: {VoltageDelta.ToStringPrefix(InputCircuit?.Frequency, "V", "yellow")} | ΔV_max: {VoltageMaximum.ToStringPrefix("V", "yellow")}");
            stringBuilder.AppendLine($"Internal resistance: {_powerSource?.NortonEquivalent.Resistance.ToStringPrefix("Ω", "yellow")}");
            stringBuilder.AppendLine($"Internal Buffer: {Charge.ToStringPrefix("Wt", "yellow")} / {ChargeMaximum.ToStringPrefix("Wt", "yellow")}");
        }

        public override void AddTo(Circuit circuit)
        {
            base.AddTo(circuit);

            var nodeA = GetNode(circuit, PowerInput, WireType.Line1);
            _powerSource = new(circuit, nodeA, null) { Frequency = 60, VoltageNominal = VoltageMaximum };
        }

        public override void UpdateState(Circuit circuit)
        {
            base.UpdateState(circuit);

            if (_powerSource != null)
            {
                _powerSource.PowerAvailable = Charge;
                _powerSource.UpdateState();
            }
        }

        public override void ApplyState(Circuit circuit)
        {
            base.ApplyState(circuit);

            _powerSource?.ApplyState();

            PowerGenerated = Device?.GetGeneratedPower(Device.PowerCableNetwork) ?? 0;
            PowerDraw = _powerSource?.Power.Real ?? 0;
            PowerFactor = _powerSource?.PowerFactor ?? 0;
            VoltageDelta = _powerSource?.VoltageDelta ?? 0;
            CurrentDraw = _powerSource?.Current ?? 0;

            Charge += PowerGenerated + PowerDraw;
            Charge = Math.Clamp(Charge, 0f, ChargeMaximum);

            Device?.UsePower(Device.PowerCableNetwork, (float)PowerDraw);
        }

        public override void RemoveFrom(Circuit circuit)
        {
            base.RemoveFrom(circuit);

            _powerSource?.Dispose();
            _powerSource = null;
        }
    }
}
