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

            // Determine settings based on type of transformer
            double nMin, nMax, step;

            // Small transformer
            if (self.PrefabName == "StructureTransformerSmall")
            {
                self.StepNormal = 50f;
                self.StepSmall = 1f;
                self.OutputMaximum = 500;

                nMin = 0.5;
                nMax = 2.0;
                step = 0.01;
            }
            // Medium transformer
            else if (self.OutputMaximum < 55_000)
            {
                self.StepNormal = 100f;
                self.StepSmall = 10f;
                self.OutputMaximum = 5_000;

                nMin = 0.1;
                nMax = 10.0;
                step = 0.05;
            }
            // Large transformer
            else
            {
                self.StepNormal = 1000f;
                self.StepSmall = 100f;
                self.OutputMaximum = 100_000;

                nMin = 1;
                nMax = 100.0;
                step = 0.1;
            }

            // Calculate input & output voltages, and error between output voltage and desired setting
            var inputVoltage = transformer.PrimaryVoltage.Magnitude;
            var outputVoltage = transformer.SecondaryVoltage.Magnitude;
            var dV = Math.Abs(self.Setting - outputVoltage);

            // Minimum value of dV before we'll switch transformer ratio
            var deadzone = 3 * step * inputVoltage;

            // If output voltage is out of range, change the ratio of the transformer
            if (dV > deadzone)
            {
                // Determine nearest "tap point", and winding ratio (only set ratio in 1% increments)
                var tap = Math.Round((self.Setting / inputVoltage) / step);
                var n = tap * step;

                // Clamp 
                n = Math.Clamp(n, nMin, nMax);

                if (n != transformer.N)
                {
                    transformer.Deinitialize();
                    transformer.N = n;
                    transformer.Initialize();
                }
            }
        }
    }
}