#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Util;
using Hardwired.Simulation.Electrical;
using Hardwired.Simulation.Electrical.Elements;
using Hardwired.Utility.Extensions;
using MathNet.Numerics;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    public class Battery : ElectricalComponent
    {
        private List<Assets.Scripts.Objects.Items.IChargable> _batteries = new();
        private EnergyBuffer? _energyBuffer;

        /// <summary>
        /// The ratio of power that should be equalized between multiple batteries per tick
        /// </summary>
        public double BalanceRatio { get; set; } = 0.01;

        public double MaximumVoltage { get; set; } = 200;

        public double Charge { get; private set; }

        public double MaxCharge { get; private set; }

        public Complex VoltageDelta { get; private set; }

        public Complex Current { get; private set; }

        public double Power { get; private set; }

        public double Resistance { get; private set; }

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            // If this is the only component, add a description (otherwise, only show debug values so we don't take up too much space...)
            if (GetComponents<ElectricalComponent>().Length == 1)
            {
                stringBuilder.AppendLine($"Stores electrical energy in the circuit.");
                stringBuilder.AppendLine($"Modeled as a non-ideal voltage source with a series resistance.");
                stringBuilder.AppendLine($"The internal voltage is set each tick by the state of charge (SoC).");
                stringBuilder.AppendLine($"The voltage is 0 V when fully discharged and V_max when fully charged.");
                stringBuilder.AppendLine($"If the internal voltage is below the circuit voltage, current flows into the battery and it charges.");
                stringBuilder.AppendLine($"If the internal voltage is above the circuit voltage, current flows out of the battery and it discharges.");

                stringBuilder.AppendLine($"\n---\n");
            }

            stringBuilder.AppendLine($"Charge: {Charge.ToStringPrefix("Wt", "yellow")} / {MaxCharge.ToStringPrefix("Wt", "yellow")}");
            stringBuilder.AppendLine($"ΔV: {VoltageDelta.ToStringPrefix(InputCircuit?.Frequency, "V", "yellow")} | V_max: {MaximumVoltage.ToStringPrefix("V", "yellow")}");
            stringBuilder.AppendLine($"Current: {Current.ToStringPrefix(InputCircuit?.Frequency, "A", "yellow")} | Power: {Power.ToStringPrefix("W", "yellow")}");
            stringBuilder.AppendLine($"Internal Resistance: {Resistance.ToStringPrefix("Ω", "yellow")}");
        }

        public override void AddTo(Circuit circuit)
        {
            base.AddTo(circuit);

            if (InputCircuit == circuit)
            {
                var nodeA = GetNode(circuit, PowerInput, WireType.Line1);

                _energyBuffer?.Dispose();
                _energyBuffer = new(circuit, nodeA, null);
                _energyBuffer.VoltageMaximum = MaximumVoltage;
                _energyBuffer.CurrentMaximum = 40f;
                _energyBuffer.VoltageCurve = EnergyBuffer.VoltageCurveFunction.Tangent;

                if (Device is Assets.Scripts.Objects.Electrical.Battery battery)
                {
                    _batteries.Clear();
                    _batteries.Add(battery);
                }
            }
        }

        public override void UpdateState(Circuit circuit)
        {
            base.UpdateState(circuit);

            if (InputCircuit == circuit)
            {
                if (Device is AreaPowerControl apc)
                {
                    _batteries.Clear();
                    if (apc.Battery != null)
                    {
                        _batteries.Add(apc.Battery);
                    }
                }
                else if (Device is BatteryCellCharger batteryCellCharger)
                {
                    _batteries.Clear();
                    _batteries.AddRange(batteryCellCharger.Batteries);
                }

                if (_energyBuffer == null) { return; }

                _energyBuffer.ChargeMaximum = _batteries.Sum(b => b.GetPowerMaximum());
                _energyBuffer.Charge = _batteries.Sum(b => b.PowerStored);

                _energyBuffer.UpdateState();
            }
        }

        public override void ApplyState(Circuit circuit)
        {
            base.ApplyState(circuit);

            if (InputCircuit == circuit)
            {
                var previousCharge = _energyBuffer?.Charge ?? 0f;

                _energyBuffer?.ApplyState();

                Charge = _energyBuffer?.Charge ?? 0f;
                MaxCharge = _energyBuffer?.ChargeMaximum ?? 0f;
                Power = _energyBuffer?.Power.Real ?? 0f;

                VoltageDelta = _energyBuffer?.VoltageDelta ?? 0f;
                Current = _energyBuffer?.Current ?? 0f;
                Resistance = _energyBuffer?.Resistance ?? -1f;

                // Get total amount of charge "headroom" (if charging), or charge available (if discharging)
                var dCharge = Charge - previousCharge;
                var w = (dCharge >= 0)
                    ? MaxCharge - previousCharge
                    : previousCharge;

                // Get average charge ratio
                var r_average = Charge / MaxCharge;

                // Update the charge in each battery
                foreach (var battery in _batteries)
                {
                    // Calculate how much of the power this battery cell should take/provide (as ratio of this battery's headroom/available to the total)
                    var wi = (dCharge >= 0)
                        ? (battery.GetPowerMaximum() - battery.PowerStored) / w
                        : battery.PowerStored / w;
                    
                    // Add new charge to battery
                    battery.PowerStored += (float)(wi * dCharge);

                    // Balance battery charges
                    battery.PowerStored += (float)(BalanceRatio * battery.GetPowerMaximum() * (r_average - battery.PowerRatio));
                }
            }
        }

        public override void RemoveFrom(Circuit circuit)
        {
            base.RemoveFrom(circuit);

            if (circuit == _energyBuffer?.Circuit)
            {
                _energyBuffer?.Dispose();
                _energyBuffer = null;
            }
        }
    }
}
