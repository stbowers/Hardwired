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
    public class VoltageSource : Component
    {
        [Header("Voltage Source")]
        public double Voltage;
    }
}
