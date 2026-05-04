#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Assets.Scripts.Inventory;
using Assets.Scripts.Objects;
using Assets.Scripts.Util;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Triggers;
using Hardwired.Objects.Electrical;
using HarmonyLib;
using UnityEngine;

namespace Hardwired.Patches
{
    [HarmonyPatch]
    public static class ConnectionPatches
    {
        /// <summary>
        /// Add power profile tooltip to input connections if the device has a power sink component
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="__result"></param>
        [HarmonyPostfix, HarmonyPatch(typeof(Connection), nameof(Connection.Populate))]
        public static void Postfix_Populate(Connection __instance, ref PassiveTooltip __result)
        {
            // Only modify if the parent device has a power sink component, and this is the input connection
            if (__instance.Parent?.TryGetComponent(out PowerSink powerSink) != true
                || __instance != powerSink.PowerInput)
            {
                return;
            }

            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine(__result.Extended);

            powerSink.BuildPowerProfileTooltip(stringBuilder, true);

            __result.Extended = stringBuilder.ToString();
        }
    }
}
