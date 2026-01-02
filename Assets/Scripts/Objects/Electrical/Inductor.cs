#nullable enable

using System;
using System.Numerics;
using System.Text;
using Assets.Scripts.Util;
using Hardwired.Utility;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    public class Inductor : ElectricalComponent
    {
        /// <summary>
        /// Inductance value in Henrys
        /// </summary>
        public double Inductance;

        /// <summary>
        /// Impedence value in ohms (depends on AC circuit frequency)
        /// </summary>
        [HideInInspector]
        public double Impedence;

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            stringBuilder.AppendLine($"Inductance: {Inductance.ToStringPrefix("H", "yellow")}");
            stringBuilder.AppendLine($"Impedence: {Impedence.ToStringPrefix("Ω", "yellow") ?? "N/A"}");
        }

        public override void InitializeSolver(MNASolver solver)
        {
            base.InitializeSolver(solver);

            // If circuit has AC current, add impedence based on the frequency
            if (solver.Frequency != 0)
            {
                var w = 2f * Math.PI * solver.Frequency;
                Impedence = w * Inductance;

                int? n = GetNodeIndex(PinA);
                int? m = GetNodeIndex(PinB);

                solver.AddImpedence(n, m, Impedence);
            }
        }
    }
}
