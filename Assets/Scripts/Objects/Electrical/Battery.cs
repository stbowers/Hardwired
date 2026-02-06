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
        public double BalanceRatio { get; set; }= 0.005;

        public double Charge { get; private set; }

        public double MaxCharge { get; private set; }

        public Complex VoltageDelta { get; private set; }

        public Complex Current { get; private set; }

        public double Power { get; private set; }

        public double Resistance { get; private set; }

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            stringBuilder.AppendLine($"Charge: {Charge.ToStringPrefix("Wt", "yellow")} / {MaxCharge.ToStringPrefix("Wt", "yellow")}");
            stringBuilder.AppendLine($"ΔV: {VoltageDelta.ToStringPrefix(OutputCircuit?.Frequency, "V", "yellow")} | Current: {Current.ToStringPrefix(OutputCircuit?.Frequency, "A", "yellow")}");
            stringBuilder.AppendLine($"Power: {Power.ToStringPrefix("W", "yellow")}");
            stringBuilder.AppendLine($"Resistance: {Resistance.ToStringPrefix("Ω", "yellow")}");
        }

        public override void AddTo(Circuit circuit)
        {
            base.AddTo(circuit);

            var nodeA = GetNode(circuit, PowerInput, WireType.Line1);

            _energyBuffer?.Dispose();
            _energyBuffer = new(circuit, nodeA, null);
        }

        public override void UpdateState(Circuit circuit)
        {
            base.UpdateState(circuit);

            if (_energyBuffer == null) {return;}

            if (Device is AreaPowerControl apc)
            {
                _batteries.Clear();
                if (apc.Battery != null)
                {
                    _batteries.Add(apc.Battery);
                }

                _energyBuffer.Charge = apc.Battery?.PowerStored ?? 0f;
                _energyBuffer.ChargeMaximum = apc.Battery?.PowerMaximum ?? 0f;
            }
            else if (Device is Assets.Scripts.Objects.Electrical.Battery battery)
            {
                _batteries.Clear();
                _batteries.Add(battery);
            }
        }

        public override void ApplyState(Circuit circuit)
        {
            base.ApplyState(circuit);

            var previousCharge = _energyBuffer?.Charge ?? 0f;

            Charge = _energyBuffer?.Charge ?? 0f;
            MaxCharge = _energyBuffer?.ChargeMaximum ?? 0f;
            Power = _energyBuffer?.Power.Real ?? 0f;

            VoltageDelta = _energyBuffer?.VoltageDelta ?? 0f;
            Current = _energyBuffer?.Current ?? 0f;

            // Get total amount of charge "headroom" (if charging), or charge available (if discharging)
            var w = (Power >= 0)
                ? MaxCharge - previousCharge
                : previousCharge;

            // Get average charge ratio
            var r_average = Charge / MaxCharge;

            // Update the charge in each battery
            foreach (var battery in _batteries)
            {
                // Calculate how much of the power this battery cell should take/provide (as ratio of this battery's headroom/available to the total)
                var wi = (Power >= 0)
                    ? (battery.GetPowerMaximum() - battery.PowerStored) / w
                    : battery.PowerStored / w;
                
                // Add new charge to battery
                battery.PowerStored += (float)(wi * Power);

                // Balance battery charges
                battery.PowerStored += (float)(BalanceRatio * battery.GetPowerMaximum() * (r_average - battery.PowerRatio));
            }
        }

        public override void RemoveFrom(Circuit circuit)
        {
            base.RemoveFrom(circuit);

            _energyBuffer?.Dispose();
            _energyBuffer = null;
        }
    }
}
