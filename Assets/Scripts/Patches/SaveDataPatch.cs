using System;
using System.Collections.Generic;
using Assets.Scripts.Serialization;
using Hardwired.Prefabs.JunctionBox;
using HarmonyLib;

namespace Hardwired.Patches
{
    [HarmonyPatch]
    public static class SaveDataPatch
    {
        [HarmonyPatch(typeof(XmlSaveLoad), nameof(XmlSaveLoad.AddExtraTypes))]
        public static void Prefix(ref List<Type> extraTypes)
        {
            extraTypes.Add(typeof(JunctionBoxSaveData));
            extraTypes.Add(typeof(JunctionBoxTerminalBlockSaveData));
        }
    }
    
}