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
    /// Acts like a resistor during the "initial solve", where I(v) = V/R, with R set such that I(V_min)*V_min = P_target
    /// (i.e. to draw the target power at minimum nominal voltage).
    /// The voltage across the resistor after the "initial solve" then determines the `OperatingState`.
    /// If V < V_min, there is not enough power in the circuit to meet the power target, so the element will continue to
    /// act as a purely resistive load (no non-linear current component), drawing less than P_target.
    /// If V_min < V < V_max, the circuit has enough power to supply the device, so it will try to draw a constant power
    /// by adding a non-linear current component to the circut such that I = P/V (solved via Newton method).
    /// If V_max < V, the source voltage is too high for this device, so it will draw 0 current.
    /// 
    /// Not to be confused for `Objects.Electrical.PowerSource`, which is a component added to devices that produce power
    /// (generators, batteries, etc).
    /// </summary>
    public class ConstantPowerSource : DipoleCircuitElementBase, ICircuitElement, IFrequencySource, INonlinearCircuitElement
    {
        public enum State
        {
            // Unknown state (not yet calculated)
            Unknown,

            /// <summary>
            /// V < MinimumNominalVoltage
            /// Device will draw less than TargetPower (will act like a resistive load)
            /// </summary>
            LowVoltage,

            /// <summary>
            /// Nominal voltage; device will draw constant power, depending on voltage (non-linear current draw)
            /// </summary>
            Nominal,

            /// <summary>
            /// V > MaximumVoltage
            /// Device will draw 0 power
            /// </summary>
            HighVoltage,
        }

        private Resistor _resistor;
        private double? _frequency;
        private Complex? _v0;
        private Complex _i = Complex.Zero;

        public State OperatingState { get; private set;}

        public override Complex Current => _i;

        public Complex TargetPower { get; set; }

        public double MinimumNominalVoltage { get; set; } = 100;

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
            _resistor = new(circuit, nodeA, nodeB);
        }

        public override void Dispose()
        {
            _resistor.Dispose();

            base.Dispose();
        }

        public override void UpdateState()
        {
            base.UpdateState();

            // Update resistor
            _resistor.Resistance = (MinimumNominalVoltage * MinimumNominalVoltage) / TargetPower.Magnitude;
            _resistor.UpdateState();

            // Reset state
            OperatingState = State.Unknown;
        }

        public void UpdateDifferentialState()
        {
            // Update state based on initial solve with just resistor
            if (OperatingState == State.Unknown)
            {
                // Update resistor voltage readings
                _resistor.ApplyState();

                // Determine state for this tick
                OperatingState = _resistor.VoltageDelta.Magnitude switch {
                    var x when x <= MinimumNominalVoltage => State.LowVoltage,
                    var x when x <= MaximumVoltage => State.Nominal,
                    _ => State.HighVoltage,
                };
            }

            var vA = VoltageA;
            var vB = VoltageB;
            var vDelta = vA - vB;
            _v0 = vDelta;

            var vDeltaSquared = vDelta * vDelta;

            Complex dIdV, dIdVConjugate;

            // Determine non-linear behavior based on `OperatingState` (which will be constant throught any given
            // tick, and can only change between ticks, so we don't have to worry about discontinuities in how we
            // calculate the current).

            // Low voltage; no non-linear current (just resistor current)
            if (OperatingState == State.LowVoltage)
            {
                _i = _resistor.Current;
                return;
            }
            // Nominal voltage; I(V) = P/V (non-linear)
            else if (OperatingState == State.Nominal)
            {
                _i = (TargetPower / vDelta).Conjugate();
                dIdV = 0;
                dIdVConjugate = (-TargetPower / vDeltaSquared).Conjugate();
            }
            // High voltage; I = 0
            // note - this branch is "linear", but differs from the initial conditions set up by the resistor, so
            // we "counteract" the resistor by adding a "non-linear" current that is equal but opposite.
            else
            {
                _i = 0;
                dIdV = 0;
                dIdVConjugate = 0;
            }

            // Calculate current to "add" to the resistor to get the target non-linear current
            var di = _i - _resistor.Current;
            Circuit?.Solver.AddNonlinearCurrent(NodeA?.Value, NodeB?.Value, di, dIdV, dIdVConjugate);
        }
    }
}
