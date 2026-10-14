#nullable enable

using System;
using System.Numerics;
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
        private Complex _v0 = 200;
        private Complex _i = Complex.Zero;

        public override Complex Current => _i;

        public Complex SourcePower { get; set; }

        public double MinimumVoltage { get; set; } = 100;
        public double MaximumVoltage { get; set; } = 250;

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
            _i = SourcePower / _v0;
            if (_i.Magnitude < 0.0001)
            {
                _i = 0.0001;
            }

            // // If estimate is sufficiently far from our current estimate, reset it
            if (_appliedCurrent == null || (_appliedCurrent.Value - _i).Magnitude > 0.1)
            {
                RemoveState();

                Circuit.Solver.AddCurrent(NodeB?.Value, NodeA?.Value, _i);
                _appliedCurrent = _i;
            }
        }

        public override void ApplyState()
        {
            base.ApplyState();
        }

        public void UpdateDifferentialState()
        {
            var vA = VoltageA;
            var vB = VoltageB;
            var vDelta = (vA - vB).Magnitude;
            
            if (double.IsNaN(vDelta) || vDelta < 1)
            {
                vDelta = 1;
            }
            _v0 = vDelta;

            var v2 = vDelta * vDelta;

            Complex didva, didvb;

            // UVL; I(V) = 0
            if (vDelta < MinimumVoltage)
            {
                var m = SourcePower / (MinimumVoltage * MinimumVoltage);
                _i = m * vDelta;
                didva = -m;
                didvb = m;
            }
            // High voltage
            else if (vDelta > MaximumVoltage)
            {
                var m = -SourcePower / (MaximumVoltage * 10);
                _i = (m * vDelta) - (m * MaximumVoltage) + (SourcePower / MaximumVoltage);
                didva = -m;
                didvb = m;
            }
            // Nominal voltage; I(V) = P/V
            else
            {
                _i = SourcePower / vDelta;
                didva = didvb = -SourcePower / v2;
            }

            if (_i.Magnitude < 0.0001)
            {
                _i = 0.0001;
            }

            var di = _i - (_appliedCurrent ?? Complex.Zero);
            Circuit?.Solver.AddNonlinearCurrent(NodeB?.Value, NodeA?.Value, di, didva, didvb);
        }

        private void RemoveState()
        {
            if (_appliedCurrent != null)
            {
                Circuit.Solver.AddCurrent(NodeB?.Value, NodeA?.Value, -_appliedCurrent.Value);
                _appliedCurrent = null;
            }
        }
    }
}
