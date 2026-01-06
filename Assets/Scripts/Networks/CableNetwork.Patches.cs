#nullable enable

using System;
using System.Reflection;
using Assets.Scripts.Networks;
using Assets.Scripts.Objects.Electrical;
using HarmonyLib;

namespace Hardwired.Networks
{
    /// <summary>
    /// Patch base game CableNetwork class to override PowerTick.
    /// 
    /// Credit to Sukasa from Re-Volt
    /// https://github.com/Sukasa/ReVolt/blob/main/Assets/Scripts/patches/CableNetworkPatches.cs
    /// </summary>
    [HarmonyPatch(typeof(CableNetwork))]
    public static class CableNetworkPatches
    {
        // private static readonly FieldInfo TickSetter = typeof(CableNetwork).GetField(nameof(CableNetwork.PowerTick));
        private static readonly FieldInfo _powerTickField = AccessTools.Field(typeof(CableNetwork), nameof(CableNetwork.PowerTick));

        [HarmonyPostfix, HarmonyPatch(MethodType.Constructor, new Type[0])]
        private static void __Postfix_Constructor_None(CableNetwork __instance) => Inject(__instance);

        [HarmonyPostfix, HarmonyPatch(MethodType.Constructor, new Type[1] { typeof(Cable) })]
        private static void __Postfix_Constructor_Cable(CableNetwork __instance) => Inject(__instance);

        [HarmonyPostfix, HarmonyPatch(MethodType.Constructor, new Type[1] { typeof(long) })]
        private static void __Postfix_Constructor_Long(CableNetwork __instance) => Inject(__instance);

        private static void Inject(CableNetwork instance) => _powerTickField.SetValue(instance, new HardwiredPowerTick());
    }
}