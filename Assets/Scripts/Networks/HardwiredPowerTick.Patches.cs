#nullable enable

using System;
using Assets.Scripts.Networks;
using HarmonyLib;

namespace Hardwired.Networks
{
    [HarmonyPatch(typeof(PowerTick))]
    public partial class HardwiredPowerTick : PowerTick
    {
        [HarmonyPrefix, HarmonyPatch(nameof(PowerTick.Initialise))]
        private static bool __Prefix_Initialise(PowerTick __instance, CableNetwork cableNetwork)
        {
            // If injection failed, run original code
            if (__instance is not HardwiredPowerTick hardwiredPowerTick)
            {
                return true;
            }

            hardwiredPowerTick.Initialise(cableNetwork);
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(nameof(PowerTick.CalculateState))]
        private static bool __Prefix_CalculateState(PowerTick __instance)
        {
            // If injection failed, run original code
            if (__instance is not HardwiredPowerTick hardwiredPowerTick)
            {
                return true;
            }

            hardwiredPowerTick.CalculateState();
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(nameof(PowerTick.ApplyState))]
        private static bool __Prefix_ApplyState(PowerTick __instance)
        {
            // If injection failed, run original code
            if (__instance is not HardwiredPowerTick hardwiredPowerTick)
            {
                return true;
            }

            hardwiredPowerTick.ApplyState();
            return false;
        }
    }
}