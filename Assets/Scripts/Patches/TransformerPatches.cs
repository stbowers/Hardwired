#nullable enable

using System;
using Assets.Scripts.Localization2;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Pipes;
using Assets.Scripts.Util;
using HarmonyLib;
using static Assets.Scripts.Objects.Thing;

using TransformerComponent = Hardwired.Objects.Electrical.Transformer;

namespace Hardwired.Patches
{
    [HarmonyPatch]
    public static class TransformerPatches
    {
        public static readonly GameString OutputVolts = GameString.Create("OutputVolts", "Output <color=green>{0} V</color>");

        [HarmonyPostfix, HarmonyPatch(typeof(Transformer), nameof(Transformer.InteractWith))]
        private static void InteractWith__Postfix(Transformer __instance, DelayedActionInstance __result, Interactable interactable, Interaction interaction, bool doAction)
        {
            if (interactable.Action == InteractableType.Button1 || interactable.Action == InteractableType.Button2)
            {
                __result.ClearStateMessage();
                __result.AppendStateMessage(OutputVolts, StringManager.Get((int)__instance.Setting));
                __result.AppendStateMessage(GameStrings.HoldForSmallIncrements, Assets.Scripts.Localization.QuantityModifierKey);
                __result.AppendStateMessage(GameStrings.UseLabelerToSet);
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Device), nameof(Device.OnPowerTick))]
        public static void OnPowerTick__Postfix(Device __instance)
        {
            if (__instance is not Transformer self) { return; }

            var transformer = __instance.GetOrAddComponent<TransformerComponent>();

            self.StepNormal = 50f;
            self.StepSmall = 1f;

            var inputVoltage = transformer.PrimaryVoltage.Magnitude;
            var outputVoltage = transformer.SecondaryVoltage.Magnitude;

            var minStep = 0.06 * inputVoltage;

            var dV = Math.Abs(self.Setting - outputVoltage);

            // If output voltage is out of range, change the ratio of the transformer
            if (dV > minStep)
            {
                // Determine nearest "tap point", and winding ratio
                var tap = Math.Round(20 * (self.Setting / inputVoltage));
                var n = tap / 20;

                n = Math.Clamp(n, 0.1, 10);

                transformer.Deinitialize();
                transformer.N = n;
                transformer.Initialize();
            }
        }
    }
}