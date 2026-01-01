#nullable enable

using System.Text;
using Assets.Scripts.Util;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    public class CurrentSource : Component
    {
        [Header("Current Source")]
        public double Current;

        /// <summary>
        /// The momentary voltage across this current source calculated by the circuit solver.
        /// </summary>
        [HideInInspector]
        public double? DeltaVoltage;

        protected override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            stringBuilder.AppendLine($"Current: {Current.ToStringPrefix("A", "yellow")}");
            stringBuilder.AppendLine($"Voltage: {DeltaVoltage?.ToStringPrefix("V", "yellow") ?? "N/A"}");
        }
    }
}
