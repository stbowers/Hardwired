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

        public List<BuildStatePatch> BuildStatePatches = new();

        private static void PatchPrefab(Thing prefab)
        {
            // Check for components required for the patch
            if (prefab.GetComponent<Structure>() is not Structure structure
                || prefab.GetComponent<PatchBuildStateTools>() is not PatchBuildStateTools patch)
            {
                return;
            }

            foreach (var buildStatePatch in patch.BuildStatePatches)
            {
                if (!string.IsNullOrWhiteSpace(buildStatePatch.SetEntryTool))
                {
                    PrefabUtils.SetEntryTool(structure, buildStatePatch.SetEntryTool, buildStatePatch.BuildState);
                }

                if (!string.IsNullOrWhiteSpace(buildStatePatch.SetEntry2Tool))
                {
                    PrefabUtils.SetEntry2Tool(structure, buildStatePatch.SetEntry2Tool, buildStatePatch.BuildState);
                }

                if (!string.IsNullOrWhiteSpace(buildStatePatch.SetExitTool))
                {
                    PrefabUtils.SetExitTool(structure, buildStatePatch.SetExitTool, buildStatePatch.BuildState);
                }
            }
        }

        [Serializable]
        public class BuildStatePatch
        {
            public int BuildState;

            public string? SetEntryTool;

            public string? SetEntry2Tool;

            public string? SetExitTool;
        }
    }
    
}