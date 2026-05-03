#nullable enable

using System;
using System.Buffers.Binary;
using System.Linq;
using Assets.Scripts.Objects;

namespace Hardwired.Utility.Extensions
{
    /// <summary>
    /// Provides extension methods for adding custom data to `ThingSaveData` to allow other components to save/load data.
    /// </summary>
    public static class ThingSaveDataExtensions
    {
        public static void AddCustomData(this ThingSaveData saveData, string key, int value)
        {
            saveData.States.Add(new InteractableState { StateName = key, State = value });
        }

        public static void AddCustomData(this ThingSaveData saveData, string key, float value)
        {
            int intValue = BitConverter.SingleToInt32Bits(value);
            AddCustomData(saveData, key, intValue);
        }

        public static bool TryGetCustomData(this ThingSaveData saveData, string key, out int value)
        {
            if (saveData.States.FirstOrDefault(s => s.StateName == key) is InteractableState state)
            {
                value = state.State;
                return true;
            }

            value = 0;
            return false;
        }

        public static bool TryGetCustomData(this ThingSaveData saveData, string key, out float value)
        {
            if (TryGetCustomData(saveData, key, out int intValue))
            {
                value = BitConverter.Int32BitsToSingle(intValue);
                return true;
            }

            value = 0;
            return false;
        }
    }
}