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
        /// Postfix patch for Connection.Populate() that adds info about the required power input profile to power connection tooltips.
        /// 
        /// Also handles the interaction logic for changing power profiles with a screwdriver (note - doing this in the tooltip handler
        /// is a bit of a code smell... "normally" something like this would be done in the InteractWith() method, but power connections
        /// aren't normally interactable, so this was the easiest place to do this...)
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="__result"></param>
        // [HarmonyPostfix, HarmonyPatch(typeof(Connection), nameof(Connection.Populate))]
        // public static void Postfix_Populate_PowerSinkProfile(Connection __instance, ref PassiveTooltip __result)
        // {
        //     // Only modify if the parent device has a power sink component, and this is the input connection
        //     if (__instance.Parent?.TryGetComponent(out PowerSink powerSink) != true
        //         || __instance != powerSink.PowerInput)
        //     {
        //         return;
        //     }

        //     StringBuilder stringBuilder = new();
        //     stringBuilder.AppendLine(__result.Extended);

        //     bool hasScrewdriver = InventoryManager.ActiveHandSlot.Get()?.PrefabName == "ItemScrewdriver";
        //     bool primaryButtonDown = KeyManager.GetButtonDown(KeyMap.PrimaryAction);

        //     // On click with screwdriver, cycle active profile index
        //     if (hasScrewdriver && primaryButtonDown)
        //     {
        //         powerSink.ActiveProfileIndex = (powerSink.ActiveProfileIndex + 1) % powerSink.PowerProfiles.Count;
        //     }

        //     stringBuilder.AppendLine($"[[ Power Input ]]");

        //     // If player is holding a screwdriver, show a list of all available power profiles
        //     if (hasScrewdriver)
        //     {
        //         for (int i = 0; i < powerSink.PowerProfiles.Count; i++)
        //         {
        //             var powerProfile = powerSink.PowerProfiles[i];
        //             var isActive = i == powerSink.ActiveProfileIndex;

        //             stringBuilder.AppendLine($"[{(isActive ? "*".AsColor("green") : " ")}] {powerProfile}");
        //         }
        //     }
        //     // Otherwise, only show currently active profile
        //     else
        //     {
        //         stringBuilder.AppendLine($"Use screwdriver + left click to change".AsColor("yellow"));
        //         stringBuilder.AppendLine($"{powerSink.ActivePowerProfile}");
        //     }

        //     __result.Extended = stringBuilder.ToString();
        // }
    }
}
