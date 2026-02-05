#nullable enable

using System;
using System.Numerics;
using Hardwired.Utility;
using UnityEngine.PlayerLoop;

namespace Hardwired.Simulation.Electrical.Elements
{
    /// <summary>
    /// Represents any component that stores energy over time, and produces a voltage/current based on the state of charge.
    /// 
    /// Depending on the voltage curve, this element can be used to approximate different time-domain 
    /// 
    /// Linear: DC Capacitor
    /// Tangent: Battery
    /// </summary>
    public class EnergyBuffer : DipoleCircuitElementBase, ICircuitElement
    {
        /// <summary>
        /// Representation of a voltage curve function (i.e. V(r) = U(r) * V_max; U(r) = curve from 0 to 1 given charge ratio r from 0 to 1).
        /// </summary>
        public abstract class VoltageCurveFunction
        {
            /// <summary>
            /// Linear - `U(r) = r`
            /// </summary>
            public static readonly VoltageCurveFunction Linear = new LinearCurve();

            /// <summary>
            /// Tangent - `U(r) = A * tan(B*r + C)`, where A, B, and C are set such that U(r) is between 0 and 1
            /// </summary>
            public static readonly VoltageCurveFunction Tangent = new TangentCurve();

            public abstract double U(double r);

            private class LinearCurve : VoltageCurveFunction
            {
                public override double U(double r) => r;
            }

            private class TangentCurve : VoltageCurveFunction
            {
                /// <summary>
                /// lim B -> 0: Curve will approach linear
                /// lim B -> pi/2: Curve will approach constant
                /// </summary>
                private static readonly double B = 1.5;
                private static readonly double TAN_B = Math.Tan(B);

                public override double U(double r) => (Math.Tan(2 * B * (r - 0.5)) + TAN_B) / (2 * TAN_B);
            }
        }

        private NortonEquivalent _nortonEquivalent { get; }

        /// <summary>
        /// The voltage curve to use when evaluating the voltage this energy buffer will output based on the current state of charge.
        /// </summary>
        public VoltageCurveFunction VoltageCurve { get; set; } = VoltageCurveFunction.Linear;

        /// <summary>
        /// The current charge level of the energy buffer, in Watt-ticks.
        /// </summary>
        public double Charge { get; set; }

        /// <summary>
        /// The maximum charge level of the energy buffer, in Watt-ticks.
        /// </summary>
        public double ChargeMaximum { get; set; }

        /// <summary>
        /// The maximum voltage that this energy buffer will produce when Charge == ChargeMaximum
        /// </summary>
        public double VoltageMaximum { get; set; }

        /// <summary>
        /// The maximum current that this energy buffer is designed to draw.
        /// Used to size the internal resistor such that Current == CurrentMaximum when Charge == 0 and VoltageDelta == VoltageMaximum.
        /// </summary>
        public double CurrentMaximum { get; set; }

        public override Complex Current => _nortonEquivalent.Current;

        /// <summary>
        /// The ratio of Charge to ChargeMaximum, clamped to be between 0 and 1
        /// </summary>
        public double ChargeRatio => ChargeMaximum != 0 ? Math.Clamp(Charge / ChargeMaximum, 0f, 1f) : 0;

        public EnergyBuffer(Circuit circuit, RefCounted<MNASolver.Unknown>? nodeA, RefCounted<MNASolver.Unknown>? nodeB) : base(circuit, nodeA, nodeB)
        {
            _nortonEquivalent = new(circuit, nodeA, nodeB);
        }

        public override void Dispose()
        {
            _nortonEquivalent.Dispose();
        }

        public override void UpdateState()
        {
            Charge = Math.Clamp(Charge, 0f, ChargeMaximum);

            _nortonEquivalent.Resistance = VoltageMaximum / CurrentMaximum;
            _nortonEquivalent.VoltageOpen = VoltageCurve.U(ChargeRatio) * VoltageMaximum;

            _nortonEquivalent.UpdateState();
        }

        public override void ApplyState()
        {
            Charge = Math.Clamp(Charge + Power.Real, 0f, ChargeMaximum);
        }
    }
}
