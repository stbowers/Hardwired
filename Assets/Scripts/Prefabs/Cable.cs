using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.GridSystem;
using Assets.Scripts.Inventory;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.UI;
using LaunchPadBooster.Utils;
using UnityEngine;

namespace Hardwired.Prefabs
{
    public class Cable : SmallSingleGrid
    {
        Assets.Scripts.Objects.Electrical.Cable _foo;
        // Patch prefab in static constructor to add references to base game content
        static Cable()
        {
            Hardwired.LogDebug("Adding Cable prefab setup...");

            // Set exit tool to wire cutters (can't be done direclty in unity, since we don't have access to the base game prefabs)
            var prefabSetup = Hardwired.MOD.SetupPrefabs<Cable>();
            prefabSetup.SetExitTool(PrefabNames.WireCutters);
        }

        /// <summary>
        /// Resistance value in ohms
        /// </summary>
        [Header("Cables")]
        public double Resistance;

        public override void OnRegistered(Cell cell)
        {
            base.OnRegistered(cell);

            var connected = Connected();
            Hardwired.LogDebug($"Cable registered - {connected.Count} connections.");
        }
    }
}
