#nullable enable

using System;
using System.Linq;
using System.Numerics;
using System.Text;
using Assets.Scripts.Util;
using Hardwired.Simulation.Electrical;
using Hardwired.Utility.Extensions;
using MathNet.Numerics;

namespace Hardwired.Objects.Electrical
{
    public class Battery : ElectricalComponent
    {
        private Assets.Scripts.Objects.Electrical.AreaPowerControl? _apcStructure;
        private Assets.Scripts.Objects.Electrical.Battery? _batteryStructure;
        private Assets.Scripts.Objects.Electrical.BatteryCellCharger? _batteryChargerStructure;

        public double MaxCharge;

        /// <summary>
        /// The nominal voltage for the battery. The battery will produce a voltage equal to r * VoltageNominal, where r is the charge ratio.
        /// If the circuit has a higher voltage outside of the battery, current will flow into the battery and charge it.
        /// If the circuit has a lower voltage outside of the battery, current will flow out of the battery and discharge it.
        /// </summary>
        public double VoltageNominal;

        public Complex Voltage;

        public Complex Current;

        public double Resistance;
        
        public double Power;

        /// <summary>
        /// The ratio of power that should be equalized between multiple batteries per tick
        /// </summary>
        public double BalanceRatio = 0.005;

        /// <summary>
        /// Current charge level, in Wt (Watt-ticks, i.e 1 Wt = 0.5 Ws = 0.5 J)
        /// </summary>
        public double Charge { get; protected set; }

        /// <summary>
        /// The current through the battery (positive if charging, negative if discharging)
        /// </summary>
        private MNASolver.Unknown? _i;

        /// <summary>
        /// Voltage at internal node "X"
        /// </summary>
        private MNASolver.Unknown? _vX;
        
        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            stringBuilder.AppendLine($"-- Battery --");
            stringBuilder.AppendLine($"Charge: {Charge.ToStringPrefix("Wt", "yellow")} / {MaxCharge.ToStringPrefix("Wt", "yellow")}");
            stringBuilder.AppendLine($"Voltage: {Voltage.ToStringPrefix("V", "yellow")}");
            stringBuilder.AppendLine($"Current: {Current.ToStringPrefix("A", "yellow")}");
            stringBuilder.AppendLine($"Power: {Power.ToStringPrefix("W", "yellow")}");
            stringBuilder.AppendLine($"Resistance: {Resistance.ToStringPrefix("Ω", "yellow")}");
        }

        public override string DebugInfo()
        {
            return $"{base.DebugInfo()} | pinX: {_vX?.Index ?? -1} | i: {_i?.Index ?? -1}";
        }

        public override void AddTo(Circuit circuit)
        {
            base.AddTo(circuit);

            // Look for attached structure which has a battery slot or internal battery
            bool foundStructure = false;
            foundStructure |= TryGetComponent(out _apcStructure);
            foundStructure |= TryGetComponent(out _batteryStructure);
            foundStructure |= TryGetComponent(out _batteryChargerStructure);

            if (!foundStructure)
            {
                Hardwired.LogDebug($"WARNING - no compatible structure found for Battery");
            }
        }

        public override void Initialize()
        {
            base.Initialize();

            // TODO
            Resistance = 40;

            _vX = Circuit?.Solver.AddUnknown();
            Circuit?.Solver.AddVoltageSource(_vB, _vX, ref _i);
            Circuit?.Solver.AddResistance(_vX, _vA, Resistance);
        }

        public override void Deinitialize()
        {
            base.Deinitialize();

            Circuit?.Solver.AddResistance(_vX, _vA, -Resistance);
            Circuit?.Solver.RemoveUnknown(_i);
            Circuit?.Solver.RemoveUnknown(_vX);

            _i = null;
            _vX = null;
        }

        public override void UpdateState()
        {
            base.UpdateState();

            // Update charge from structure
            if (_apcStructure != null)
            {
                Charge = _apcStructure.Battery?.PowerStored ?? 0;
                MaxCharge = _apcStructure.Battery?.PowerMaximum ?? 0;
            }
            else if (_batteryStructure != null)
            {
                Charge = _batteryStructure.PowerStored;
                MaxCharge = _batteryStructure.PowerMaximum;
            }
            else if (_batteryChargerStructure != null)
            {
                Charge = _batteryChargerStructure.Batteries.Sum(b => b.PowerStored);
                MaxCharge = _batteryChargerStructure.Batteries.Sum(b => b.PowerMaximum);
            }

            Complex vUnit = (Voltage.Magnitude > 0f) ? Voltage / Voltage.Magnitude : 1f;

            var r = Math.Clamp(Charge / MaxCharge, 0f, 1f);
            var v = r * VoltageNominal * vUnit;

            Circuit?.Solver.SetVoltage(_i, v);
        }

        public override void ApplyState()
        {
            base.ApplyState();

            var vA = Circuit?.Solver.GetValue(_vA) ?? Complex.Zero;
            var vB = Circuit?.Solver.GetValue(_vB) ?? Complex.Zero;
            Voltage = vA - vB;

            Current = Circuit?.Solver.GetValue(_i) ?? Complex.Zero;

            Power = (Voltage * Current.Conjugate()).Real;

            var previousCharge = Charge;
            Charge = Math.Clamp(Charge + Power, 0f, MaxCharge);

            // Update charge from structure
            if (_apcStructure != null && _apcStructure.Battery != null)
            {
                _apcStructure.Battery.PowerStored = (float)Charge;
            }
            else if (_batteryStructure != null)
            {
                _batteryStructure.PowerStored = (float)Charge;
            }
            else if (_batteryChargerStructure != null && _batteryChargerStructure.Batteries.Count > 0)
            {
                // Get total amount of charge "headroom" (if charging), or charge available (if discharging)
                var w = (Power >= 0)
                    ? MaxCharge - previousCharge
                    : previousCharge;

                // Get average charge ratio
                var r_average = Charge / MaxCharge;

                // Update the charge in each battery
                foreach (var battery in _batteryChargerStructure.Batteries)
                {
                    // Calculate how much of the power this battery cell should take/provide (as ratio of this battery's headroom/available to the total)
                    var wi = (Power >= 0)
                        ? (battery.GetPowerMaximum() - battery.PowerStored) / w
                        : battery.PowerStored / w;
                    
                    // Add new charge to battery
                    battery.PowerStored += (float)(wi * Power);

                    // Balance battery charges
                    battery.PowerStored += (float)(BalanceRatio * battery.PowerMaximum * (r_average - battery.PowerRatio));
                }
            }
        }
    }
}
