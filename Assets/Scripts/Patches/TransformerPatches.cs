#nullable enable

using System;
using Assets.Scripts.Localization2;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Pipes;
using Assets.Scripts.Util;
using Hardwired.Objects.Electrical;
using HarmonyLib;
using static Assets.Scripts.Objects.Thing;

namespace Hardwired.Patches
{
    [HarmonyPatch]
    public static class TransformerPatches
    {
        public static readonly GameString TransformerRatio = GameString.Create("TransformerRatio", "Ratio <color=green>{0:D1}</color>");

        [HarmonyPostfix, HarmonyPatch(typeof(Transformer), nameof(Transformer.InteractWith))]
        private static void InteractWith__Postfix(Transformer __instance, DelayedActionInstance __result, Interactable interactable, Interaction interaction, bool doAction)
        {
            if (!__instance.TryGetComponent<FixedTransformer>(out var transformer))
            {
                return;
            }

            if (interactable.Action == InteractableType.Button1 || interactable.Action == InteractableType.Button2)
            {
                __result.ClearStateMessage();
                __result.AppendStateMessage(TransformerRatio, StringManager.Get(transformer.Ratio));
                __result.AppendStateMessage(GameStrings.HoldForSmallIncrements, Assets.Scripts.Localization.QuantityModifierKey);
                __result.AppendStateMessage(GameStrings.UseLabelerToSet);
            }
        }
    }
}