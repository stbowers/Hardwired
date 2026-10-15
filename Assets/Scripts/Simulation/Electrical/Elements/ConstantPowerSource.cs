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

        public void UpdateDifferentialState(int n, out bool discontinuityHint)
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

            double s = 1.0;

            discontinuityHint = false;
            // discontinuityHint = Math.Abs(vDelta.Magnitude - MinimumVoltage) < 40
            //                  || Math.Abs(vDelta.Magnitude - MaximumVoltage) < 40;

            if (n > 30)
            {
                // // Assume it won't converge...
                // _i = 0;
                // didva = 0;
                // didvb = 0;

                Hardwired.LogDebug($"NR iteration {n}: V = {vDelta.Magnitude} (near discontinuity: {discontinuityHint}) -- (v_min: {MinimumVoltage}, v_max: {MaximumVoltage})");
            }

            // V <= MinimumVoltage
            // Low voltage dropoff; I(V) = e^( s*(V-V_min) + ln(P_target/V_min))
            if (vDelta.Magnitude <= MinimumVoltage)
            {
                _i = Math.Exp(s * (vDelta.Magnitude - MinimumVoltage) + Math.Log(SourcePower.Magnitude / MinimumVoltage));
                didva = _i;
                didvb = -_i;
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
            // Low voltage dropoff; I(V) = e^( s*(V_max-V) + ln(P_target/V_max))
            else
            {
                _i = Math.Exp(s * (MaximumVoltage - vDelta.Magnitude) + Math.Log(SourcePower.Magnitude / MaximumVoltage));
                didva = -_i;
                didvb = _i;
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
