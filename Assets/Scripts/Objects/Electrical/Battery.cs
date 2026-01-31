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
        private Assets.Scripts.Objects.Pipes.Device? _device;
        private List<Assets.Scripts.Objects.Items.IChargable> _batteries = new();
        private EnergyBuffer? _energyBuffer;

        public ConnectionRole Connection = ConnectionRole.Input;

        /// <summary>
        /// The ratio of power that should be equalized between multiple batteries per tick
        /// </summary>
        public double BalanceRatio = 0.005;
        
        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            // stringBuilder.AppendLine($"-- Battery --");
            // stringBuilder.AppendLine($"Charge: {Charge.ToStringPrefix("Wt", "yellow")} / {MaxCharge.ToStringPrefix("Wt", "yellow")}");
            // stringBuilder.AppendLine($"Voltage: {Voltage.ToStringPrefix("V", "yellow")}");
            // stringBuilder.AppendLine($"Current: {Current.ToStringPrefix("A", "yellow")}");
            // stringBuilder.AppendLine($"Power: {Power.ToStringPrefix("W", "yellow")}");
            // stringBuilder.AppendLine($"Resistance: {Resistance.ToStringPrefix("Ω", "yellow")}");
        }

        public override void AddTo(Circuit circuit)
        {
            base.AddTo(circuit);

            if (_device == null)
            {
                TryGetComponent(out _device);
            }

            var nodeA = GetNode(circuit, PowerInput, WireType.Line1);
            var nodeB = GetNode(circuit, PowerInput, WireType.Neutral);

            _energyBuffer?.Dispose();
            _energyBuffer = new(circuit, nodeA, nodeB);
        }

        public override void UpdateState(Circuit circuit)
        {
            base.UpdateState(circuit);

            if (_energyBuffer == null) {return;}

            if (_device is AreaPowerControl apc)
            {
                _batteries.Clear();
                if (apc.Battery != null)
                {
                    _batteries.Add(apc.Battery);
                }

                _energyBuffer.Charge = apc.Battery?.PowerStored ?? 0f;
                _energyBuffer.ChargeMaximum = apc.Battery?.PowerMaximum ?? 0f;
            }

            _energyBuffer.UpdateState();
        }

        public override void ApplyState(Circuit circuit)
        {
            base.ApplyState(circuit);

            var previousCharge = _energyBuffer?.Charge ?? 0f;

            _energyBuffer?.ApplyState();

            var charge = _energyBuffer?.Charge ?? 0f;
            var power = _energyBuffer?.Power.Real ?? 0f;
            var chargeMax = _energyBuffer?.ChargeMaximum ?? 0f;

            // Get total amount of charge "headroom" (if charging), or charge available (if discharging)
            var w = (power >= 0)
                ? chargeMax - previousCharge
                : previousCharge;

            // Get average charge ratio
            var r_average = charge / chargeMax;

            // Update the charge in each battery
            foreach (var battery in _batteries)
            {
                // Calculate how much of the power this battery cell should take/provide (as ratio of this battery's headroom/available to the total)
                var wi = (power >= 0)
                    ? (battery.GetPowerMaximum() - battery.PowerStored) / w
                    : battery.PowerStored / w;
                
                // Add new charge to battery
                battery.PowerStored += (float)(wi * power);

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
