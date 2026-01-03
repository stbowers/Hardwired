#nullable enable

using System.Numerics;
using System.Text;
using Assets.Scripts.Util;
using Hardwired.Utility;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    public class Resistor : ElectricalComponent
    {
        /// <summary>
        /// Resistance value in ohms
        /// </summary>
        public double Resistance;

        /// <summary>
        /// The momentary voltage across this cable calculated by the circuit solver.
        /// </summary>
        [HideInInspector]
        public double? DeltaVoltage;

        /// <summary>
        /// The momentary current across this voltage source calculated by the circuit solver.
        /// </summary>
        [HideInInspector]
        public double? Current;

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            stringBuilder.AppendLine($"Resistance: {Resistance.ToStringPrefix("Ω", "yellow")}");
            stringBuilder.AppendLine($"ΔV: {DeltaVoltage?.ToStringPrefix("V", "yellow") ?? "N/A"}");
            stringBuilder.AppendLine($"Current: {Current?.ToStringPrefix("A", "yellow") ?? "N/A"}");
        }


        public override void InitializeSolver(MNASolver solver)
        {
            base.InitializeSolver(solver);

            int? n = GetNodeIndex(PinA);
            int? m = GetNodeIndex(PinB);
            solver.AddResistance(n, m, Resistance);
        }

        public override void GetSolverOutputs(MNASolver solver)
        {
            base.GetSolverOutputs(solver);

            int? n = GetNodeIndex(PinA);
            int? m = GetNodeIndex(PinB);

            Complex vA = solver.GetVoltage(n);
            Complex vB = solver.GetVoltage(m);

            // TODO: This needs to take in to account the resistance of the entire cable segment, not just this individual component... May need to re-think this approach...
            // Maybe CableSegment will be the actual Component added to the network, and that will in turn hold a reference to each individual cable in the segment, so
            // cables themselves won't actually have this function called?

            // I = V / R
            // var totalResistance = segment.Cables.Sum(c => c.Resistance);
            var current = (vB - vA) / Resistance;

            DeltaVoltage = (vB - vA).Magnitude;
            Current = current.Magnitude;
        }

    }
}
