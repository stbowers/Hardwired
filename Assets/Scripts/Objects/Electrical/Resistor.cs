using System.Collections;
using System.Collections.Generic;
using System.Text;
using Assets.Scripts.GridSystem;
using Assets.Scripts.Inventory;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.UI;
using Assets.Scripts.Util;
using LaunchPadBooster.Utils;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    public class Resistor : Component
    {
        /// <summary>
        /// Resistance value in ohms
        /// </summary>
        [Header("Resistor")]
        public double Resistance;

        /// <summary>
        /// The momentary current across this voltage source calculated by the circuit solver.
        /// </summary>
        [HideInInspector]
        public double? Current;

        protected override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            stringBuilder.AppendLine($"Voltage: {Resistance.ToStringPrefix("Ω", "yellow")}");
            stringBuilder.AppendLine($"Current: {Current?.ToStringPrefix("A", "yellow") ?? "N/A"}");
        }
    }
}
