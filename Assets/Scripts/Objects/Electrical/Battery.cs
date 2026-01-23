#nullable enable

using System;
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
        public double MaxCharge;

        public double MaxVoltage;

        public Complex Voltage;

        public Complex Current;

        public double Resistance;
        
        public double Power;

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
            stringBuilder.AppendLine($"Charge: {Charge.ToStringPrefix("J", "yellow")} / {MaxCharge.ToStringPrefix("J", "yellow")}");
            stringBuilder.AppendLine($"Voltage: {Voltage.ToStringPrefix("V", "yellow")}");
            stringBuilder.AppendLine($"Current: {Current.ToStringPrefix("A", "yellow")}");
            stringBuilder.AppendLine($"Power: {Power.ToStringPrefix("W", "yellow")}");
            stringBuilder.AppendLine($"Resistance: {Resistance.ToStringPrefix("Ω", "yellow")}");
        }

        public override void AddTo(Circuit circuit)
        {
            base.AddTo(circuit);

            _vX = circuit.Solver.AddUnknown();
            circuit.Solver.AddVoltageSource(_vB, _vX, ref _i);
        }

        public override void RemoveFrom(Circuit circuit)
        {
            base.RemoveFrom(circuit);

            circuit.Solver.RemoveUnknown(_i);
            circuit.Solver.RemoveUnknown(_vX);

            _i = null;
            _vX = null;
        }

        public override void Initialize()
        {
            base.Initialize();

            // TODO
            var r = 10;

            Circuit?.Solver.AddResistance(_vX, _vA, r);
        }

        public override void Deinitialize()
        {
            base.Deinitialize();

            // TODO
            var r = 10;

            Circuit?.Solver.AddResistance(_vX, _vA, -r);
        }

        public override void UpdateState()
        {
            base.UpdateState();

            Complex vUnit = (Voltage.Magnitude > 0f) ? Voltage / Voltage.Magnitude : 1f;

            var r = Math.Clamp(Charge / MaxCharge, 0f, 1f);
            var v = r * MaxVoltage * vUnit;

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

            var dT = Circuit?.TimeDelta ?? 0f;
            var dE = Power * dT;

            Charge = Math.Clamp(Charge + dE, 0f, MaxCharge);
        }
    }
}
