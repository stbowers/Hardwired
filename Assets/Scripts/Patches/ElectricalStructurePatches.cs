#nullable enable

using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
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

        /// <summary>
        /// Patches ElectricalInputOutput.GetPassiveTooltip() to call PassiveTooltip.Populate(connection) for input/output connections
        /// (which is what most other devices do, but ElectricalInputOutput specifically overrides the normal connection tooltip...)
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="hitCollider"></param>
        /// <param name="__result"></param>
        [HarmonyPostfix, HarmonyPatch(typeof(ElectricalInputOutput), nameof(ElectricalInputOutput.GetPassiveTooltip))]
        public static void Postfix_ElectricalInputOutput_GetPassiveTooltip(ElectricalInputOutput __instance, Collider? hitCollider, ref PassiveTooltip __result)
        {
            if (hitCollider == null)
            {
                return;
            }
            else if (hitCollider == __instance.InputConnection?.Collider)
            {
                __result = new PassiveTooltip().Populate(__instance.InputConnection);
            }
            else if (hitCollider == __instance.OutputConnection?.Collider)
            {
                __result = new PassiveTooltip().Populate(__instance.OutputConnection);
            }
        }
    }
}
