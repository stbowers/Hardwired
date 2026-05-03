#nullable enable

using System.Collections.Generic;
using Assets.Scripts.Objects;
using Hardwired.Objects.Electrical;
using HarmonyLib;

namespace Hardwired.Patches
{
    /// <summary>
    /// Patches `Thing.DeserializeSave()` and `Thing.InitialiseSaveData()` to call `ElectricalComponent.DeserializeSave()` and `ElectricalComponent.SerializeSave()` for each
    /// electrical component attached to the game object, providing electrical components with the opportunity to save/load data.
    /// </summary>
    [HarmonyPatch]
    public class ElectricalComponentSaveDataPatch : Thing
    {
        [HarmonyPostfix, HarmonyPatch(typeof(Thing), methodName: nameof(DeserializeSave))]
        public static void Postfix_DeserializeSave(Thing __instance, ThingSaveData saveData)
        {
            foreach (var electricalComponent in __instance.GetComponents<ElectricalComponent>())
            {
                electricalComponent.DeserializeSave(saveData);
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Thing), methodName: nameof(InitialiseSaveData))]
        public static void Postfix_InitialiseSaveData(Thing __instance, ref ThingSaveData savedData)
        {
            foreach (var electricalComponent in __instance.GetComponents<ElectricalComponent>())
            {
                electricalComponent.SerializeSave(savedData);
            }
        }
    }
}