#nullable enable

using System;
using System.Numerics;
using Assets.Scripts.Util;
using Hardwired.Utility;
using MathNet.Numerics;
using MathNet.Numerics.Providers.LinearAlgebra;

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

        public double MinimumNominalVoltage { get; set; } = 100;

        public double MaximumNominalVoltage { get; set; } = 200;

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
            // _v0 ??= NominalVoltage;
            // _v0 = _v0.Value.Magnitude.Clamp(MinimumVoltage, MaximumVoltage);
            _v0 ??= (MinimumNominalVoltage + MaximumNominalVoltage) / 2;
            if (_v0.Value.Magnitude < 0.0001)
            {
                _v0 = 0.0001;
            }

            _i = (SourcePower / _v0.Value).Conjugate();
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
            
            if (double.IsNaN(vDelta.Magnitude) || vDelta.Magnitude < 0.001)
            {
                vDelta = 0.001 * (vDelta / vDelta.Magnitude);
            }
            _v0 = vDelta;

            var v2 = vDelta * vDelta;

            Complex didva, dIdConjugateVa;

            // Calculate I(V), and the partial derivatives for V_a and V_b for the current state.
            // I(V) is a non-linear piecewise function, but must be "smooth" (i.e. both I(V) and its derivative must be
            // continuous) in order for the NR iterative solver to be able to converge.
            // https://www.desmos.com/calculator/l0bdnvaaaw

            // V <= MinimumVoltage
            // Low voltage dropoff
            if (vDelta.Magnitude <= MinimumNominalVoltage)
            {
                // l(v)
                var normalizedUnderVoltage = vDelta.Magnitude / MinimumNominalVoltage;

                // g_min(v)
                var taper = normalizedUnderVoltage * normalizedUnderVoltage * (3 - 2 * normalizedUnderVoltage);
                // partial derivative of g_min(v) w.r.t. l(v)
                var taperDerivative = 6 * normalizedUnderVoltage * (1 - normalizedUnderVoltage);

                // partial derivatives of l(v) w.r.t v and v*
                var dldv = vDelta.Conjugate() / (2 * vDelta.Magnitude * MinimumNominalVoltage);
                var dldvConjugate = vDelta / (2 * vDelta.Magnitude * MinimumNominalVoltage);

                _i = (SourcePower / vDelta).Conjugate() * taper;
                didva = (SourcePower / vDelta).Conjugate() * taperDerivative * dldv;
                dIdConjugateVa = (-SourcePower / v2).Conjugate() * taper + (SourcePower / vDelta).Conjugate() * taperDerivative * dldvConjugate;
            }
            // MinimumVoltage < V <= MaximumNominalVoltage
            // Nominal voltage; I(V) = P/V
            else if (vDelta.Magnitude <= MaximumNominalVoltage)
            {
                _i = (SourcePower / vDelta).Conjugate();
                didva = 0;
                dIdConjugateVa = (-SourcePower / v2).Conjugate();
            }
            // MaximumNominalVoltage < V < MaximumVoltage
            // High voltage dropoff
            else if (vDelta.Magnitude <= MaximumVoltage)
            {
                // V_max - V_nmax
                var vBuffer = MaximumVoltage - MaximumNominalVoltage;

                // u(v)
                var normalizedOverVoltage = (vDelta.Magnitude - MaximumNominalVoltage) / vBuffer;
                var oneMinusUSquared = 1 - normalizedOverVoltage * normalizedOverVoltage;

                // g_max(v)
                var taper = oneMinusUSquared * oneMinusUSquared;
                // partial derivative of g_max(v) w.r.t. u(v)
                var taperDerivative = -4 * normalizedOverVoltage * oneMinusUSquared;

                // partial derivatives of u(v) w.r.t v and v*
                var dudv = vDelta.Conjugate() / (2 * vDelta.Magnitude * vBuffer);
                var dudvConjugate = vDelta / (2 * vDelta.Magnitude * vBuffer);

                _i = (SourcePower / vDelta).Conjugate() * taper;
                didva = (SourcePower / vDelta).Conjugate() * taperDerivative * dudv;
                dIdConjugateVa = (-SourcePower / v2).Conjugate() * taper + (SourcePower / vDelta).Conjugate() * taperDerivative * dudvConjugate;
            }
            // MaximumVoltage < V
            // Above max voltage; 0 power draw
            else
            {
                _i = 0;
                didva = 0;
                dIdConjugateVa = 0;
            }

            // Ensure current is always non-zero, to avoid NaN when solving...
            if (_i.Magnitude < 1e-10)
            {
                _i = 1e-10;
            }

            var di = _i - (_appliedCurrent ?? Complex.Zero);
            Circuit?.Solver.AddNonlinearCurrent(NodeA?.Value, NodeB?.Value, di, didva, dIdConjugateVa);
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
