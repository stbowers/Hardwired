#nullable enable

using System;
using System.Collections.Generic;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using LaunchPadBooster.Utils;
using UnityEngine;

namespace Hardwired.Patches
{
    public class PatchElectricalPrefabs
    {
        // Add prefab patch setup
        public static void ApplyPatch()
        {
            Hardwired.LogDebug("Adding PatchElectricalPrefabs prefab setup...");

            var prefabSetup = Hardwired.MOD.SetupPrefabs<Electrical>();
            prefabSetup.RunFunc(PatchPrefab);
        }

        private static void PatchPrefab(Electrical electrical)
        {
            // TODO

        }
    }
}