#nullable enable

using UnityEngine;
using LaunchPadBooster;
using System.Collections.Generic;
using LaunchPadBooster.Utils;
using HarmonyLib;
using Hardwired.Patches;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.UI;
using System.Linq;

namespace Hardwired
{
    public partial class Hardwired : MonoBehaviour
    {
        public static readonly Mod MOD = new("Hardwired", "0.1.0");
        public static readonly Harmony HARMONY = new("Hardwired");

        public void OnLoaded(List<GameObject> prefabs)
        {
            LogDebug("Applying patches");

            PatchBuildStateTools.ApplyPatch();

            MOD.AddPrefabs(prefabs);
            HARMONY.PatchAll();

            LogDebug("Registering commands");

            RegisterCommands();

            LogDebug("Done loading!");
        }

        public static void LogDebug(object msg)
        {
            Debug.Log($"[Hardwired]: {msg}");
        }
    }
}
