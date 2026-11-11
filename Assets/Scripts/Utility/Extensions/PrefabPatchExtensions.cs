#nullable enable

using System.Collections.Generic;
using Assets.Scripts.Objects;

namespace Hardwired.Utility.Extensions
{
    public static class PrefabPatchExtensions
    {
        public static void AddToMultiConstructor(this Structure structure, string multiConstructorPrefabName)
        {
            MultiConstructor? multiConstructor = WorldManager.Instance.SourcePrefabs.Find(p => p.PrefabName == multiConstructorPrefabName) as MultiConstructor;
            if (multiConstructor == null)
            {
                Hardwired.LogDebug($"MultiConstructor '{multiConstructorPrefabName}' not found...");
                return;
            }

            multiConstructor.Constructables.Add(structure);
        }
    }
}