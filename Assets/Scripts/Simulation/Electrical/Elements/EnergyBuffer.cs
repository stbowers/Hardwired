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
    /// Depending on the voltage curve
    /// 
    /// Linear: DC Capacitor
    /// Tangent: Battery
    /// </summary>
    public class EnergyBuffer : ICircuitElement, IDipoleCircuitElement
    {
        public abstract class VoltageCurveFunction
        {
            public static readonly VoltageCurveFunction Linear = new LinearCurve();
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

        public Circuit Circuit { get; }

        public RefCounted<MNASolver.Unknown>? NodeA => _nortonEquivalent.NodeA;

        public RefCounted<MNASolver.Unknown>? NodeB => _nortonEquivalent.NodeB;

        public Complex Current => _nortonEquivalent.Current;

        public double Charge { get; set; }

        public double ChargeMaximum { get; set; }

        public double VoltageMaximum { get; set; }

        public double CurrentMaximum { get; set; }

        public double ChargeRatio => ChargeMaximum != 0 ? Math.Clamp(Charge / ChargeMaximum, 0f, 1f) : 0;

        public double StateOfCharge => ChargeRatio;

        public VoltageCurveFunction VoltageCurve { get; set; } = VoltageCurveFunction.Linear;

        public EnergyBuffer(Circuit circuit, RefCounted<MNASolver.Unknown>? nodeA, RefCounted<MNASolver.Unknown>? nodeB)
        {
            Circuit = circuit;
            _nortonEquivalent = new(circuit, nodeA, nodeB);
        }

        public void Dispose()
        {
            _nortonEquivalent.Dispose();
        }

        public void UpdateState()
        {
            Charge = Math.Clamp(Charge, 0f, ChargeMaximum);

            _nortonEquivalent.VoltageOpen = StateOfCharge * VoltageMaximum;
            _nortonEquivalent.Resistance = VoltageMaximum / CurrentMaximum;
            _nortonEquivalent.UpdateState();
        }

        public void ApplyState()
        {
            Charge = Math.Clamp(Charge - (this as IDipoleCircuitElement).Power.Real, 0f, ChargeMaximum);
        }
    }
}
