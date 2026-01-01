#nullable enable

using UnityEngine;
using LaunchPadBooster;
using System.Collections.Generic;
using LaunchPadBooster.Utils;

namespace Hardwired
{
    public class Hardwired : MonoBehaviour
    {
        public static readonly Mod MOD = new("Hardwired", "0.1.0");

        public void OnLoaded(List<GameObject> prefabs)
        {
            LogDebug("Loading Hardwired...");

            MOD.AddPrefabs(prefabs);
        }

        public static void LogDebug(object msg)
        {
            Debug.Log($"[Hardwired]: {msg}");
        }
    }
}
