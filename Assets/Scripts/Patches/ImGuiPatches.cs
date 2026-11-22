#nullable enable

using System.Collections.Generic;
using Assets.Scripts.Networks;
using Assets.Scripts.Objects.Items;
using Assets.Scripts.UI.ImGuiUi;
using Hardwired.Networks;
using HarmonyLib;

namespace Hardwired.Patches
{
    [HarmonyPatch]
    public static class ImGuiPatches
    {
        [HarmonyPostfix, HarmonyPatch(typeof(ImGuiInWorldManager), nameof(ImGuiInWorldManager.DrawDebugActions))]
        public static void Postfix_DrawDebugActions()
        {
            if (Hardwired.RenderDebugOverlay)
            {
                RenderCircuitDebugOverlays();
            }
        }

        private static void RenderCircuitDebugOverlays()
        {
            HashSet<CircuitTick> allCircuits = new();
            CableNetwork.AllCableNetworks.ForEach(cableNetwork =>
            {
                var circuitTick = (cableNetwork.PowerTick as HardwiredPowerTick)?.CircuitTick;

                if (circuitTick != null)
                {
                    allCircuits.Add(circuitTick);
                }
            });

            foreach (var circuitTick in allCircuits)
            {
                circuitTick.DrawDebugOverlay();
            }
        }
    }
}