#nullable enable

using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Objects;
using Hardwired.Objects.Electrical;
using HarmonyLib;
using UnityEngine;

namespace Hardwired.Patches
{
    /// <summary>
    /// Patches `Thing.DeserializeSave()` and `Thing.InitialiseSaveData()` to call `ElectricalComponent.DeserializeSave()` and `ElectricalComponent.SerializeSave()` for each
    /// electrical component attached to the game object, providing electrical components with the opportunity to save/load data.
    /// </summary>
    [HarmonyPatch]
    public class ComponentSaveDataPatch : Thing
    {
        [HarmonyPostfix, HarmonyPatch(typeof(Thing), methodName: nameof(DeserializeSave))]
        public static void Postfix_DeserializeSave(Thing __instance, ThingSaveData saveData)
        {
            foreach (var component in __instance.GetComponents<IComponentSaveData>())
            {
                component.DeserializeSave(saveData);
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Thing), methodName: nameof(InitialiseSaveData))]
        public static void Postfix_InitialiseSaveData(Thing __instance, ref ThingSaveData savedData)
        {
            foreach (var component in __instance.GetComponents<IComponentSaveData>())
            {
                component.SerializeSave(savedData);
            }
        }
    }

    /// <summary>
    /// Interface for components which can save/load data to their root object's ThingSaveData.
    /// 
    /// These methods will be called in postfix patches to `Thing.DeserializeSave()` and `Thing.SerializeSave()`, and implementors are intended to
    /// add extra data to `ThingSaveData.States` instead of creating a whole new type to serialize.
    /// 
    /// This is less fragile than creating a custom implementation of ThingSaveData because:
    /// 1. The base game's ThingSaveData system is primarily set up to save data only for one script/component per "thing" (the component that
    ///    derrives from `Thing`). Saving data for other attached scripts (such as our `ElectricalComponent` scripts) is not supported by default.
    /// 2. Even if we were to use the base game's save data system as designed, this would involve creating new types derrived from `ThingSaveData`
    ///    to serialize, which we would then need to register with the game's XML serializer
    ///    - This can cause issues since we're being loaded as a plugin, so our types aren't garunteed to be loaded yet when the XML serializer is
    ///      first initialized
    ///    - This may also cause issues if Hardwired is uninstalled and the user tries to load a save with our custom data (maybe it would be ok if
    ///      all that failed to load were Hardwired structures that no longer exist... But it could be a big problem if we replaced all the normal
    ///      save data with a custom type that could no longer be deserialized).
    /// 
    /// Note, Sationeers' save system is fairly rigid, and doesn't provide many places to add custom data to the save file...
    /// The best method I have found so far is to add custom "interactable states" to `ThingSaveData.States`.
    /// Each state has a name and int value, meaning we can use `ThingSaveData.States` as a budget `Dictionary<string, int>` :')
    /// 
    /// When loading save data, `Thing.DeserialiseSave()` ignores any interactable states with an unknown name, so generally hardwired
    /// components will add states with names like "Hardwired.Objects.Electrical.PowerSink:ActivePowerProfileIndex" to ensure there are
    /// no conflicts.
    /// 
    /// Note that using this system, hardwired components are limited to only saving/loading integer values (or other values that can be
    /// converted to a 4-byte representation).
    /// In theory if more complex data was really needed, and it couldn't be converted into a series of integer/4-byte properties, more
    /// complex data could be stored in the `StateName` string, serialized as JSON or some other format...
    /// 
    /// See extension methods in `ThingSaveDataExtensions.cs` for helper methods for storing values in the `ThingSaveData.States` list.
    /// </summary>
    public interface IComponentSaveData
    {
        /// <summary>
        /// Called by a postfix patch on `Thing.DeserializeSave()`, allowing any components to load save data.
        /// </summary>
        /// <param name="saveData"></param>
        public void DeserializeSave(ThingSaveData saveData);

        /// <summary>
        /// Called by a postfix patch on `Thing.SerializeSave()`, allowing any components to write data to be saved.
        /// </summary>
        /// <param name="saveData"></param>
        public void SerializeSave(ThingSaveData saveData);
    }
}