#nullable enable

using System;
using System.Linq;
using System.Numerics;
using System.Text;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Pipes;
using Assets.Scripts.Util;
using Hardwired.Networks;
using Hardwired.Simulation.Electrical;
using Hardwired.Simulation.Electrical.Elements;
using Hardwired.Utility.Extensions;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    public class Generator : ElectricalComponent
    {
        private NortonEquivalent? _nortonEquivalent;

        [SerializeReference]
        public PowerProfile PowerProfile = new ();

        public double PowerGenerated { get; private set; }

        public double PowerDraw { get; private set; }

        public double PowerFactor { get; private set; }

        public Complex VoltageDelta { get; private set; }

        public Complex CurrentDraw { get; private set; }

        public double Charge { get; private set; }

        public double ChargeMaximum { get; set; } = 4500;

        public double VoltageMaximum { get; set; } = 200;

        public override Connection? PowerOutput => base.PowerOutput ?? base.PowerInput;

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            bool altKey = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            if (altKey)
            {
                stringBuilder.AppendLine($"Supplies electrical power to the circuit.");
                stringBuilder.AppendLine($"Modeled as a non-ideal voltage source with a maximum output voltage and a series power-limiting resistor.");
                stringBuilder.AppendLine($"Includes an internal energy buffer that is charged each tick by generated power and discharged by the load.");
                stringBuilder.AppendLine($"As current increases, the series resistance causes the output voltage to droop.");
                stringBuilder.AppendLine($"Maximum power draw occurs at 1/2 · V_max and is limited by the available energy in the buffer.");
                stringBuilder.AppendLine($"The internal buffer smooths grid transients and is computationally cheaper to simulate than a fully non-linear power source/sink.");

                stringBuilder.AppendLine($"\n");
            }
            else
            {
                stringBuilder.AppendLine($"Press [alt] for description");
            }

            stringBuilder.AppendLine($"Power Generated: {PowerGenerated.ToStringPrefix("W", "yellow")}");
            stringBuilder.AppendLine($"Current Draw: {CurrentDraw.ToStringPrefix(InputCircuit?.Frequency, "A", "yellow")}");
            stringBuilder.AppendLine($"Power Draw: {PowerDraw.ToStringPrefix("W", "yellow")} | PF: {PowerFactor}");
            stringBuilder.AppendLine($"ΔV: {VoltageDelta.ToStringPrefix(InputCircuit?.Frequency, "V", "yellow")} | ΔV_max: {VoltageMaximum.ToStringPrefix("V", "yellow")}");
            stringBuilder.AppendLine($"Internal resistance: {_nortonEquivalent?.Resistance.ToStringPrefix("Ω", "yellow")}");
            stringBuilder.AppendLine($"Internal Buffer: {Charge.ToStringPrefix("Wt", "yellow")} / {ChargeMaximum.ToStringPrefix("Wt", "yellow")}");
        }

        public override void AddTo(Circuit circuit)
        {
            base.AddTo(circuit);

            if (OutputCircuit == circuit)
            {
                if (_nortonEquivalent != null)
                {
                    RemoveFrom(_nortonEquivalent.Circuit);
                }

                var nodeA = GetNode(circuit, PowerOutput, WireType.Line1);

                _nortonEquivalent = new(circuit, nodeA, null);
            }
        }

        public override void UpdateState(Circuit circuit)
        {
            base.UpdateState(circuit);

            if (circuit != OutputCircuit) { return; }

            // Get generated power
            PowerGenerated = Device?.GetGeneratedPower(OutputCableNetwork) ?? 0;

            // Note - the following line was commented out because it was causing really weird behavior with
            // the APC... Basically the APC returns the full charge of the attached battery for GetGeneratedPower(),
            // which is way to large for this internal buffer...
            // There might be a cleaner way to handle this, but for now I've just set ChargeMaximum to 4.5 kWt by default,
            // which should be large enough for most generators...
            // ChargeMaximum = Math.Max(4.5 * PowerGenerated, ChargeMaximum);

            // Update internal charge
            var powerUsed = Math.Clamp(PowerGenerated, 0, ChargeMaximum - Charge);
            Charge = Math.Clamp(Charge + powerUsed, 0f, ChargeMaximum);

            Device?.UsePower(OutputCableNetwork, (float)powerUsed);

            if (_nortonEquivalent != null)
            {
                _nortonEquivalent.Frequency = PowerProfile.Frequency;

                // Set minimum charge to avoid dividing by zero
                var charge = Math.Max(1e-5, Charge);

                PowerProfile.VoltageMax = VoltageMaximum;
                _nortonEquivalent.Resistance = PowerProfile.VoltageMax * PowerProfile.VoltageMax / charge;
                _nortonEquivalent.CurrentShort = charge / PowerProfile.VoltageMax;

                _nortonEquivalent.UpdateState();
            }
        }

        public override void ApplyState(Circuit circuit)
        {
            base.ApplyState(circuit);

            if (OutputCircuit != circuit) {return;}

            _nortonEquivalent?.ApplyState();

            PowerDraw = _nortonEquivalent?.Power.Real ?? 0;
            PowerFactor = _nortonEquivalent?.PowerFactor ?? 0;
            VoltageDelta = _nortonEquivalent?.VoltageDelta ?? 0;
            CurrentDraw = _nortonEquivalent?.Current ?? 0;

            // Update internal charge
            Charge = Math.Clamp(Charge + PowerDraw, 0f, ChargeMaximum);
        }

        public override void RemoveFrom(Circuit circuit)
        {
            base.RemoveFrom(circuit);

            if (circuit == _nortonEquivalent?.Circuit)
            {
                _nortonEquivalent?.Dispose();
                _nortonEquivalent = null;
            }
        }
    }
}
