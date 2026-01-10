#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Pipes;
using HarmonyLib;
using Objects.Pipes;

namespace Hardwired.Patches
{
    [HarmonyPatch]
    public class PatchDevicePrefabs
    {
        [HarmonyPrefix, HarmonyPatch(typeof(Prefab), nameof(Prefab.LoadAll))]
        private static bool Prefab_LoadAll()
        {
            foreach (var device in WorldManager.Instance.SourcePrefabs.OfType<Device>())
            {
                PatchDevice(device);
            }

            return true;
        }

        private static void PatchDevice(Device device)
        {
            Hardwired.LogDebug($"patching device {device.PrefabName} -- IsPowerProvider: {device.IsPowerProvider} IsPowerInputOutput: {device.IsPowerInputOutput}");
        }
    }
}