#nullable enable

using UnityEngine;
using LaunchPadBooster;
using System.Collections.Generic;
using LaunchPadBooster.Utils;
using HarmonyLib;
using Hardwired.Patches;

namespace Hardwired
{
    public class Hardwired : MonoBehaviour
    {
        public static readonly Mod MOD = new("Hardwired", "0.1.0");
        public static readonly Harmony HARMONY = new("Hardwired");

        public void OnLoaded(List<GameObject> prefabs)
        {
            LogDebug("Loading Hardwired...");

            PatchBuildStateTools.ApplyPatch();
            PatchElectricalPrefabs.ApplyPatch();

            MOD.AddPrefabs(prefabs);
            HARMONY.PatchAll();
        }

        public static void LogDebug(object msg)
        {
            Debug.Log($"[Hardwired]: {msg}");
        }
    }
}
