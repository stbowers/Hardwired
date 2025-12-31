using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.GridSystem;
using Assets.Scripts.Inventory;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.UI;
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
    }
}
