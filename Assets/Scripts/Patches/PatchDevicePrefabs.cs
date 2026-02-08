#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Items;
using Assets.Scripts.Objects.Pipes;
using Assets.Scripts.UI;
using Hardwired.Objects.Electrical;
using Hardwired.Simulation.Electrical.Elements;
using HarmonyLib;
using Objects.Pipes;

using HardwiredBattery = Hardwired.Objects.Electrical.Battery;

namespace Hardwired.Patches
{
    [HarmonyPatch]
    public class PatchDevicePrefabs
    {
        /// <summary>
        /// Set of custom patches to apply to devices of a specific type, when normal heuristics don't work
        /// </summary>
        public static readonly Dictionary<Type, Action<Device>> CustomPatches = new()
        {
            [typeof(VolumePump)] = d => {
                var load = d.GetOrAddComponent<DeviceLoad>();
                load.PowerProfile = PowerSink.PowerProfile.SmallMotor;
            },
            [typeof(Transformer)] = d => {
                d.GetOrAddComponent<FixedTransformer>();
                (d as Transformer)!.OutputMaximum = 100;
                (d as Transformer)!.Setting = 1;
                (d as Transformer)!.StepNormal = 1;
                (d as Transformer)!.StepSmall = 0.1f;
            },
            [typeof(BatteryCellCharger)] = d => {
                d.GetOrAddComponent<HardwiredBattery>();
            },
            [typeof(AreaPowerControl)] = d => {
                d.GetOrAddComponent<HardwiredBattery>();
            }
        };

        [HarmonyPrefix, HarmonyPatch(typeof(Prefab), nameof(Prefab.LoadAll))]
        private static bool Prefab_LoadAll()
        {
            foreach (var cable in WorldManager.Instance.SourcePrefabs.OfType<Cable>())
            {
                PatchCable(cable);
            }

            foreach (var device in WorldManager.Instance.SourcePrefabs.OfType<Device>())
            {
                if (device is CableFuse cableFuse)
                {
                    PatchCableFuse(cableFuse);
                }
                else
                {
                    PatchDevice(device);
                }
            }

            return true;
        }

        private static void PatchCable(Cable cable)
        {
            var cableComponent = cable.GetOrAddComponent<HardwiredCable>();

            // Physical properties of the cable
            double resistance, specificHeat, currentCapacity, temperatureCapacity, dissipationCapacity, voltageRating;

            // TODO: probably need some better balancing here...

            // Heavy cable == base game power limit: 100 kW => 500A @ 200V ~= 100 kW
            if (cable.CableType == Cable.Type.heavy)
            {
                // Partially based on physical properties of coppwer wire ~4mm diameter
                resistance = 0.0007;
                specificHeat = 0.1;
                currentCapacity = 500;
                temperatureCapacity = 90;
                voltageRating = 10_000.0;
            }
            // Normal cable == base game power limit: 5kW => 25A @ 200V ~= 5 kW
            else
            {
                // Partially based on physical properties of coppwer wire ~2mm diameter
                resistance = 0.003;
                specificHeat = 0.025;
                currentCapacity = 25;
                temperatureCapacity = 90;
                voltageRating = 500;
            }

            // I^2 * R = D * T_c
            // note - PowerSink starts dissipating heat at 20 C, so subtract 20 from temperatureCapacity
            dissipationCapacity = currentCapacity * currentCapacity * resistance / (temperatureCapacity - 20f);

            cableComponent.Resistance = resistance;
            cableComponent.SpecificHeat = specificHeat;
            cableComponent.DissipationCapacity = dissipationCapacity;
            cableComponent.Resistance = resistance;
            cableComponent.MaximumVoltageRating = voltageRating;

            Hardwired.LogDebug($"patched cable {cable.PrefabName} ({cable.CableType}) -- resistance: {resistance} Ohm, max current: {currentCapacity} A, max voltage: {voltageRating}");
        }

        private static void PatchCableFuse(CableFuse cableFuse)
        {
            var vNominal = 1000f;
            var iLimit = cableFuse.PowerBreak / vNominal;

            cableFuse.CustomName = $"Cable Fuse ({iLimit} A)";
        }

        private static void PatchDevice(Device device)
        {
            Connection? powerInput, powerOutput;

            // Device has custom patch
            if (CustomPatches.TryGetValue(device.GetType(), out var customPatch))
            {
                customPatch(device);
            }
            // Device already has a component, so assume it's already set up as needed
            else if (device.TryGetComponent<ElectricalComponent>(out _))
            {
            }
            // Generic power input/output (batteries, APC, etc)
            else if (TryGetPowerInput(device, out powerInput) && TryGetPowerOutput(device, out powerOutput) && powerInput != powerOutput)
            {
                device.GetOrAddComponent<HardwiredBattery>();

                Hardwired.LogDebug($"patching device {device.PrefabName} -- Battery");
            }
            // Generic power sink
            else if (TryGetPowerInput(device, out powerInput) && device.UsedPower > 0f)
            {
                device.GetOrAddComponent<DeviceLoad>();

                Hardwired.LogDebug($"patching device {device.PrefabName} -- Generic power sink, P_nom: {device.UsedPower} W");
            }
            // Generic power source
            else if (TryGetPowerOutput(device, out powerOutput))
            {
                device.GetOrAddComponent<Generator>();

                Hardwired.LogDebug($"patching device {device.PrefabName} -- Generic power source, P_nom: 500 W");
            }
            else
            {
                Hardwired.LogDebug($"did not patch device {device.PrefabName}");
                // foreach (var connection in device.OpenEnds)
                // {
                //     Hardwired.LogDebug($"{connection.ConnectionType} | {connection.ConnectionRole}");
                // }
            }
        }

        private static bool TryGetPowerInput(Device device, out Connection? connection)
        {
            connection = device.OpenEnds.FirstOrDefault(IsConnectionPowerInput);
            return connection != null;
        }

        private static bool TryGetPowerOutput(Device device, out Connection? connection)
        {
            connection = device.OpenEnds.FirstOrDefault(IsConnectionPowerOutput);
            return connection != null;
        }

        private static bool IsConnectionPowerInput(Connection connection)
            => (connection.ConnectionRole != ConnectionRole.Output) && (connection.ConnectionType & NetworkType.Power) != NetworkType.None;

        private static bool IsConnectionPowerOutput(Connection connection)
            => (connection.ConnectionRole != ConnectionRole.Input) && (connection.ConnectionType & NetworkType.Power) != NetworkType.None;
    }
}