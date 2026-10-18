#nullable enable

using System;
using System.Numerics;
using Assets.Scripts.Util;
using Hardwired.Utility;
using MathNet.Numerics;

namespace Hardwired.Simulation.Electrical.Elements
{
    /// <summary>
    /// Represents a constant power source/draw in the circuit, represented by a non-linear current dependent on voltage.
    /// 
    /// Not to be confused for `Objects.Electrical.PowerSource`, which is a component added to devices that produce power
    /// (generators, batteries, etc).
    /// </summary>
    public class ConstantPowerSource : DipoleCircuitElementBase, ICircuitElement, IFrequencySource, INonlinearCircuitElement
    {
        private Complex? _appliedCurrent;
        private double? _frequency;
        private Complex? _v0;
        private Complex _i = Complex.Zero;

        public override Complex Current => _i;

        public Complex SourcePower { get; set; }

        public double MinimumVoltage { get; set; } = 100;

        public double MaximumVoltage { get; set; } = 250;

        public double NominalVoltage { get; set; } = 200;

        public double? Frequency
        {
            get => _frequency;
            set
            {
                if (_frequency == value) { return; }

                _frequency = value;
                Circuit.InvalidateFrequency();
            }
        }

        public ConstantPowerSource(Circuit circuit, RefCounted<MNASolver.Unknown>? nodeA, RefCounted<MNASolver.Unknown>? nodeB) : base(circuit, nodeA, nodeB)
        {
        }

        public override void Dispose()
        {
            RemoveState();

            base.Dispose();
        }

        public override void UpdateState()
        {
            base.UpdateState();

            // Estimate current draw given a guess at the voltage
            // _v0 ??= NominalVoltage;
            // _v0 = _v0.Value.Magnitude.Clamp(MinimumVoltage, MaximumVoltage);
            _v0 = NominalVoltage;
            _i = SourcePower / _v0.Value;
            if (_i.Magnitude < 0.0001)
            {
                _i = 0.0001;
            }

            // // If estimate is sufficiently far from our current estimate, reset it
            // if (_appliedCurrent == null || (_appliedCurrent.Value - _i).Magnitude > 0.1)
            // {
                RemoveState();

                Circuit.Solver.AddCurrent(NodeA?.Value, NodeB?.Value, _i);
                _appliedCurrent = _i;
            // }
        }

        public override void ApplyState()
        {
            base.ApplyState();
        }

        public void UpdateDifferentialState()
        {
            var vA = VoltageA;
            var vB = VoltageB;
            var vDelta = vA - vB;
            
            if (double.IsNaN(vDelta.Magnitude) || vDelta.Magnitude < 1)
            {
                vDelta = 1;
            }
            _v0 = vDelta;

            var v2 = vDelta * vDelta;

            Complex didva, didvb;

            // https://www.desmos.com/calculator/l0bdnvaaaw

            // V <= MinimumVoltage
            // Low voltage dropoff; I(V) = ...
            if (vDelta.Magnitude <= MinimumVoltage)
            {
                var vMin2 = MinimumVoltage * MinimumVoltage;
                var vMin3 = vMin2 * MinimumVoltage;
                var k = Math.Sqrt((2 * SourcePower.Magnitude) / vMin3);
                var k2 = k * k;
                var a = (2 * k2 * vMin3 - SourcePower.Magnitude) / (2 * k * vMin2);
                var b = SourcePower.Magnitude / MinimumVoltage + Math.Pow(k * MinimumVoltage - a, 2);

                _i = -Math.Pow(k * vDelta.Magnitude - a, 2) + b;
                didva = -2 * (vDelta.Magnitude - a);
                didvb = -didva;
            }
            // MinimumVoltage < V <= MaximumVoltage
            // Nominal voltage; I(V) = P/V
            else if (vDelta.Magnitude <= MaximumVoltage)
            {
                _i = SourcePower / vDelta;
                didva = -SourcePower / v2;
                didvb = -didva;
            }
            // MaximumVoltage < V
            // Low voltage dropoff; I(V) = ...
            else
            {
                // TODO - for now, I've just left it hardcoded to draw 0 power past the max voltage, otherwise if t
                // This produces a discontinuity that will definately cause issues with the NR solver near these voltages...
                // But most devices shouldn't really be close to the max voltage normally...

                // I a potentially good function to use here is x^3-x, with coefficients set such that
                // 1) It is contiguous and smooth at V = V_max, and
                // 2) The local minimum is at I(V)=0, so that we eventually get to 0 power draw

                // var vMax2 = MaximumVoltage * MaximumVoltage;
                // var vMax3 = vMax2 * MaximumVoltage;
                // var c = (2 * vMax3 - SourcePower.Magnitude) / (2 * vMax2);
                // var d = SourcePower.Magnitude / MaximumVoltage + Math.Pow(MaximumVoltage - c, 2);

                // _i = -Math.Pow(vDelta.Magnitude - c, 2) + d;
                // didva = -2 * (vDelta.Magnitude - c);
                // didvb = -didva;

                _i = 0;
                didva = 0;
                didvb = 0;
            }

            if (_i.Magnitude < 0.0001)
            {
                _i = 0.0001;
            }

            var di = _i - (_appliedCurrent ?? Complex.Zero);
            Circuit?.Solver.AddNonlinearCurrent(NodeA?.Value, NodeB?.Value, di, didva, didvb);
        }

        private void RemoveState()
        {
            if (_appliedCurrent != null)
            {
                Circuit.Solver.AddCurrent(NodeA?.Value, NodeB?.Value, -_appliedCurrent.Value);
                _appliedCurrent = null;
            }
        }
    }
}
