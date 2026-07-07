#nullable enable

using System;
using System.Numerics;
using Hardwired.Utility;
using MathNet.Numerics;

namespace Hardwired.Simulation.Electrical.Elements
{
    public class _PowerSource : DipoleCircuitElementBase, ICircuitElement, IFrequencySource, INonlinearCircuitElement
    {
        private Complex? _appliedCurrent;
        private double? _frequency;
        private Complex _v0 = 200;

        public override Complex Current => SourcePower / VoltageDelta;

        public Complex SourcePower { get; set; }

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

        public _PowerSource(Circuit circuit, RefCounted<MNASolver.Unknown>? nodeA, RefCounted<MNASolver.Unknown>? nodeB) : base(circuit, nodeA, nodeB)
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
            // if (_v0.IsNaN() || _v0.Magnitude < 1)
            // {
            //     _v0 = 1;
            // }
            var i = SourcePower / _v0;

            // // If estimate is sufficiently far from our current estimate, reset it
            // if (_appliedCurrent == null || (_appliedCurrent.Value - i).Magnitude > 0.1)
            // {
                RemoveState();

                Circuit.Solver.AddCurrent(NodeB?.Value, NodeA?.Value, i);
                _appliedCurrent = i;
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
            var vDelta = (vA - vB).Magnitude;
            
            if (double.IsNaN(vDelta) || vDelta < 1)
            {
                vDelta = 1;
            }
            //_v0 = vDelta;

            var v2 = vDelta * vDelta;

            Complex didva, didvb, i;

            // UVL; I(V) = 0
            if (vDelta < 100)
            {
                var m = SourcePower / 10000;
                i = m * vDelta;
                didva = -m;
                didvb = m;
            }
            // Nominal voltage; I(V) = P/V
            else
            {
                i = SourcePower / vDelta;
                didva = didvb = -SourcePower / v2;
            }

            var di = i - (_appliedCurrent ?? Complex.Zero);
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
