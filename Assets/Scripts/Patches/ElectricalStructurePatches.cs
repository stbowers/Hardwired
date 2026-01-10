#nullable enable

using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Objects;
using Assets.Scripts.UI;
using Hardwired.Objects.Electrical;
using HarmonyLib;
using UnityEngine;

namespace Hardwired.Objects
{
    [HarmonyPatch]
    public static class ElectricalStructurePatches
    {

        [HarmonyPostfix, HarmonyPatch(typeof(Structure), nameof(Structure.GetPassiveTooltip))]
        public static void Postfix_GetPassiveTooltip(Structure __instance, Collider? hitCollider, ref PassiveTooltip __result)
        {
            ElectricalComponent[] electricalComponents = __instance.GetComponents<ElectricalComponent>();
            if (electricalComponents.Length == 0) { return; }

            Tooltip.ToolTipStringBuilder.Clear();

            if (Tooltip.ToolTipStringBuilder.Length > 0)
            {
                Tooltip.ToolTipStringBuilder.AppendLine();
            }

            foreach (var component in __instance.GetComponents<ElectricalComponent>())
            {
                component.BuildPassiveToolTip(Tooltip.ToolTipStringBuilder);
            }

            __result = new PassiveTooltip()
            {
                Title = __instance.PrefabName,
                Extended = Tooltip.ToolTipStringBuilder.ToString(),
            };
        }
    }
}
