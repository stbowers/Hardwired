#nullable enable

using System;
using System.Collections.Generic;
using Assets.Scripts.Objects;
using LaunchPadBooster.Utils;
using UnityEngine;

namespace Hardwired.Patches
{
    public class PatchBuildStateTools : MonoBehaviour
    {
        // Add prefab patch setup
        static PatchBuildStateTools()
        {
            Hardwired.LogDebug("Adding PatchBuildStateTools prefab setup...");

            var prefabSetup = Hardwired.MOD.SetupPrefabs();
            prefabSetup.RunFunc(PatchPrefab);
        }

        public int BuildState;

        public string? SetEntryTool;

        public string? SetEntry2Tool;

        public string? SetExitTool;

        private static void PatchPrefab(Thing prefab)
        {
            if (prefab.GetComponent<Structure>() is not Structure structure)
            {
                // Can only patch structures
                return;
            }

            foreach (var patch in prefab.GetComponents<PatchBuildStateTools>())
            {
                Hardwired.LogDebug($"Patching {prefab.PrefabName} -- {patch.BuildState} - {patch.SetExitTool}");

                if (!string.IsNullOrWhiteSpace(patch.SetEntryTool))
                {
                    PrefabUtils.SetEntryTool(structure, patch.SetEntryTool, patch.BuildState);
                }

                if (!string.IsNullOrWhiteSpace(patch.SetEntry2Tool))
                {
                    PrefabUtils.SetEntry2Tool(structure, patch.SetEntry2Tool, patch.BuildState);
                }

                if (!string.IsNullOrWhiteSpace(patch.SetExitTool))
                {
                    PrefabUtils.SetExitTool(structure, patch.SetExitTool, patch.BuildState);
                }
            }
        }
    }
}