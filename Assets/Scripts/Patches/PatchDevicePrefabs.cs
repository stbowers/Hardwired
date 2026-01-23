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
using HarmonyLib;
using Objects.Pipes;

using GameTransformer = Assets.Scripts.Objects.Electrical.Transformer;
using HardwiredTransformer = Hardwired.Objects.Electrical.Transformer;

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
            [typeof(VolumePump)] = d => AddPowerSink(d, inductance: 2f),
            [typeof(GameTransformer)] = d => {
                var transformer = d.GetOrAddComponent<HardwiredTransformer>();

                transformer.PinA = 0;
                transformer.PinB = -1;
                transformer.PinC = 1;
                transformer.PinD = -1;
            },
            // [typeof(BatteryCellCharger)] = d => AddPowerSink(d, vNom: 400f, vMax: 400f),
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
                PatchDevice(device);
            }

            return true;
        }

        private static void PatchCable(Cable cable)
        {
            // Physical properties of the cable
            double resistance, specificHeat, currentCapacity, temperatureCapacity, dissipationCapacity;

            // TODO: probably need some better balancing here...

            // Heavy cable == base game power limit: 100 kW => 500A @ 200V ~= 100 kW
            if (cable.CableType == Cable.Type.heavy)
            {
                // Partially based on physical properties of coppwer wire ~4mm diameter
                resistance = 0.0007;
                specificHeat = 0.1;
                currentCapacity = 500;
                temperatureCapacity = 90;
            }
            // Normal cable == base game power limit: 5kW => 25A @ 200V ~= 5 kW
            else
            {
                // Partially based on physical properties of coppwer wire ~2mm diameter
                resistance = 0.003;
                specificHeat = 0.025;
                currentCapacity = 25;
                temperatureCapacity = 90;
            }

            // I^2 * R = D * T_c
            // note - PowerSink starts dissipating heat at 20 C, so subtract 20 from temperatureCapacity
            dissipationCapacity = currentCapacity * currentCapacity * resistance / (temperatureCapacity - 20f);

            // Add a "line" component between each connection
            for (int i = 0; i < cable.OpenEnds.Count; i++)
            {
                for (int j = i + 1; j < cable.OpenEnds.Count; j++)
                {
                    Line line = cable.gameObject.AddComponent<Line>();

                    line.PinA = i;
                    line.PinB = j;

                    line.Resistance = resistance;
                    line.SpecificHeat = specificHeat;
                    line.DissipationCapacity = dissipationCapacity;
                    line.Temperature = 293.15;
                }
            }

            Hardwired.LogDebug($"patched cable {cable.PrefabName} -- resistance: {resistance} Ohm, max current: {currentCapacity} A");
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
                // Add power sink that will draw up to 1000 W depending on input voltage (fully resistive load, no current limiting)
                AddPowerSink(device, powerInput, vNom: 400, vMax: 400);

                // Add power source that supplies up to 1000 W
                AddPowerSource(device, powerOutput, pNom: 1000);

                Hardwired.LogDebug($"patching device {device.PrefabName} -- Input/Output, Power sink, P_nom: 1 kW, Power source: P_nom: 1 kW");
            }
            // Generic power sink
            else if (TryGetPowerInput(device, out powerInput) && device.UsedPower > 0f)
            {
                AddPowerSink(device, powerInput);

                Hardwired.LogDebug($"patching device {device.PrefabName} -- Generic power sink, P_nom: {device.UsedPower} W");
            }
            // Generic power source
            else if (TryGetPowerOutput(device, out powerOutput))
            {
                AddPowerSource(device, powerOutput);

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

        private static void AddPowerSink(Device device, Connection? powerInput = null, double vMin = 100f, double vNom = 200f, double vMax = 400f, double inductance = 0f)
        {
            PowerSink powerSink = device.GetOrAddComponent<PowerSink>();

            powerInput ??= device.OpenEnds.First(IsConnectionPowerInput);

            powerSink.PinA = device.OpenEnds.IndexOf(powerInput);
            powerSink.PinB = -1;

            powerSink.VoltageMin = vMin;
            powerSink.VoltageNominal = vNom;
            powerSink.VoltageMax = vMax;
            powerSink.Inductance = inductance;
        }

        private static void AddPowerSource(Device device, Connection? powerOutput = null, double vNom = 200f, double pNom = 500f, double frequency = 60f)
        {
            PowerSource powerSource = device.GetOrAddComponent<PowerSource>();

            powerOutput ??= device.OpenEnds.First(IsConnectionPowerOutput);

            powerSource.PinA = -1;
            powerSource.PinB = device.OpenEnds.IndexOf(powerOutput);

            powerSource.NominalPower = pNom;
            powerSource.VoltageNominal = vNom;
            powerSource.Frequency = frequency;
            powerSource.IsFrequencyDriver = frequency != 0;
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