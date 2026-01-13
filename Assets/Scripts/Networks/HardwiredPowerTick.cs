#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Assets.Scripts;
using Assets.Scripts.Atmospherics;
using Assets.Scripts.Networks;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Pipes;
using Hardwired.Objects.Electrical;
using Hardwired.Simulation.Electrical;
using Objects.Pipes;
using Objects.Rockets;

namespace Hardwired.Networks
{
    /// <summary>
    /// Hardwired's replacement for the base game PowerTick class.
    /// </summary>
    public partial class HardwiredPowerTick : PowerTick
    {
        public Circuit Circuit = new();

        public long TicksProcessed;

        public Stopwatch TimeProcessingTick = new();

        public Stopwatch TimeInitializing = new();
        
        public Stopwatch TimeSolving = new();

        public Stopwatch TimeApplying = new();

        public new void Initialise(CableNetwork cableNetwork)
        {
            TimeProcessingTick.Start();
            TimeInitializing.Start();

            CableNetwork = cableNetwork;

            // TODO: it'd be better if CableNetwork called a method in PowerTick when it added/removed devices, so we could
            // handle them immediately (or at the very least, save a list of components that would need to be added/removed 
            // from the circuit on the next power tick), rather than iterate over the lists every time...

            // Get list of components already added to the circuit
            List<ElectricalComponent> knownComponents = Circuit.Components.ToList();

            // Iterate over all devices in the cable network
            foreach (var component in cableNetwork.PowerDeviceList.SelectMany(d => d.GetComponents<ElectricalComponent>()))
            {
                // Skip components that are already in the circuit; but remove them from "knownComponents" list
                if (knownComponents.Remove(component)) { continue; }

                // If a component wasn't already in the circuit, add it now
                Circuit.AddComponent(component);
            }

            // Iterate over all cables in the cable network
            foreach (var component in cableNetwork.CableList.SelectMany(d => d.GetComponents<ElectricalComponent>()))
            {
                // Skip components that are already in the circuit; but remove them from "knownComponents" list
                if (knownComponents.Remove(component)) { continue; }

                // If a component wasn't already in the circuit, add it now
                Circuit.AddComponent(component);
            }

            // Now "knownComponents" will only contain components that were in the circuit last tick but have since been removed
            foreach (var component in knownComponents)
            {
                component.RemoveFrom(Circuit);
            }

            // Update power sources/sinks
            foreach (var device in CableNetwork.PowerDeviceList)
            {
                var generatedPower = device.GetGeneratedPower(CableNetwork);
                var usedPower = device.GetUsedPower(CableNetwork);

                PowerSource? powerSource = device.GetComponent<PowerSource>();
                PowerSink? powerSink = device.GetComponent<PowerSink>();

                if (generatedPower > 0.1 && powerSource == null)
                {
                    powerSource = device.gameObject.AddComponent<PowerSource>();

                    powerSource.NominalPower = 500;
                    powerSource.VoltageNominal = 200;
                    powerSource.Frequency = 60;
                    powerSource.IsFrequencyDriver = true;

                    powerSource.PinA = -1;
                    powerSource.PinB = device.OpenEnds.FindIndex(c => (c.ConnectionType & NetworkType.Power) != NetworkType.None);

                    Circuit.AddComponent(powerSource);
                }

                if (usedPower > 0.1 && powerSink == null)
                {
                    powerSink = device.gameObject.AddComponent<PowerSink>();

                    powerSink.VoltageMin = 100;
                    powerSink.VoltageNominal = 200;
                    powerSink.VoltageMax = 400;

                    powerSink.PinA = device.OpenEnds.FindIndex(c => (c.ConnectionType & NetworkType.Power) != NetworkType.None);
                    powerSink.PinB = -1;

                    // Make volume pumps inductive (this is just an example - eventually I'd like to have data-driven power profiles for devices)
                    if (device is VolumePump volPump)
                    {
                        powerSink.Inductance = 2;
                    }

                    Circuit.AddComponent(powerSink);
                }

                // Update power source
                if (powerSource != null)
                {
                    powerSource.PowerSetting = generatedPower;
                }

                // Update power sink
                if (powerSink != null)
                {
                    if (powerSink.MaxPower < usedPower)
                    {
                        powerSink.MaxPower = usedPower;
                        powerSink.Deinitialize();
                        powerSink.Initialize();
                    }

                    powerSink.PowerTarget = generatedPower;
                }
            }

            // Update cables
            foreach (var cable in CableNetwork.CableList)
            {
                if (cable.GetComponent<Line>() is Line line) { continue; }

                for (int i = 0; i < cable.OpenEnds.Count; i++)
                {
                    for (int j = i + 1; j < cable.OpenEnds.Count; j++)
                    {
                        line = cable.gameObject.AddComponent<Line>();

                        // Partially based on physical properties of copper wire ~2mm diameter
                        // Partially balanced around 25A max for normal cables (~5 kW @ 200V)
                        line.Resistance = 0.002;
                        line.SpecificHeat = 0.025;
                        line.Temperature = 293.15;
                        line.DissipationCapacity = 0.0138;

                        line.PinA = i;
                        line.PinB = j;

                        Hardwired.LogDebug($"Added resistor to cable between connection {i} and {j}");

                        Circuit.AddComponent(line);
                    }
                }
                
            }

            // Update power sources based on how much power they will generate this tick
            foreach (var source in Circuit.PowerSources)
            {
                if (source.GetComponent<Device>() is not Device device){ continue; }

                source.PowerSetting = device.GetGeneratedPower(CableNetwork);
            }

            // Update power sinks based on how much power they want to draw this tick
            foreach (var sink in Circuit.PowerSinks)
            {
                if (sink.GetComponent<Device>() is not Device device){ continue; }

                sink.PowerTarget = device.GetUsedPower(CableNetwork);
            }

            TimeInitializing.Stop();
        }

        public new void CalculateState()
        {
            TimeSolving.Start();

            Circuit.ProcessTick();

            TimeSolving.Stop();
        }

        public new void ApplyState()
        {
            TimeApplying.Start();

            // TODO: Check fuses/breakers/etc

            // Use power from sources
            foreach (var source in Circuit.PowerSources)
            {
                if (source.GetComponent<Device>() is not Device device){ continue; }

                // Note - Device.UsePower()/ReceivePower() expect power in Watts, but for easier energy calculations the power source/sink
                // calculate the actual energy used in this tick, so we need to convert back when sending to the device.
                device.UsePower(CableNetwork, (float)(source.EnergyOutput / Circuit.TimeDelta));
            }

            // Apply power to sinks
            foreach (var sink in Circuit.PowerSinks)
            {
                // Get device this sink is attached to
                if (sink.GetComponent<Device>() is not Device device){ continue; }

                // Note - Device.UsePower()/ReceivePower() expect power in Watts, but for easier energy calculations the power source/sink
                // calculate the actual energy used in this tick, so we need to convert back when sending to the device.
                device.ReceivePower(CableNetwork, (float)(sink.EnergyInput / Circuit.TimeDelta));

                if (sink.EnergyInput > 0)
                {
                    device.SetPowerFromThread(CableNetwork, true).Forget();
                }
                else
                {
                    device.SetPowerFromThread(CableNetwork, false).Forget();
                }
            }

            // Update cable heat
            foreach (var cable in CableNetwork.CableList)
            {
                Line[] lines = cable.GetComponents<Line>();
                if (lines.Length == 0) { continue; }

                // Set all cables to average temp (in case more current is going down one path)
                var cableTemp = lines.Average(l => l.Temperature);
                foreach (var line in lines)
                {
                    line.Temperature = cableTemp;
                }

                // Randomly break cables that are over temp, with a higher chance the hotter they are
                // 100 C ~ 0%
                // 150 C ~ 100%
                double breakChance = (cableTemp - 373.15) / 50f;
                breakChance = Math.Clamp(breakChance, 0f, 1f);

                bool shouldBreak = breakChance >= UnityEngine.Random.Range(0f, 1f);
                if (shouldBreak)
                {
                    cable.Break();
                }
            }

            Required = 0;
            Consumed = 0;
            Potential = 0;

            TimeApplying.Stop();
            TimeProcessingTick.Stop();

            TicksProcessed += 1;

            LogMetrics();
        }

        private void LogMetrics()
        {
            // Log metrics every minute ~= 120 ticks
            if ((TicksProcessed % 120) != 0) { return; }

            TimeSpan averageTimeInitializing = TimeInitializing.Elapsed / 120;
            TimeSpan averageTimeSolving = TimeSolving.Elapsed / 120;
            TimeSpan averageTimeApplying = TimeApplying.Elapsed / 120;
            TimeSpan averageTickProcessingTime = TimeProcessingTick.Elapsed / 120;

            Hardwired.LogDebug($"Network {CableNetwork.ReferenceId} performance -- components: {Circuit.Components.Count}");
            Hardwired.LogDebug($"  Initialise(): {averageTimeInitializing.TotalMilliseconds} ms");
            Hardwired.LogDebug($"  CalculateState(): {averageTimeSolving.TotalMilliseconds} ms");
            Hardwired.LogDebug($"  ApplyState(): {averageTimeApplying.TotalMilliseconds} ms");
            Hardwired.LogDebug($"  Total: {averageTickProcessingTime.TotalMilliseconds} ms");

            TimeInitializing.Reset();
            TimeSolving.Reset();
            TimeApplying.Reset();
            TimeProcessingTick.Reset();
        }

    }
}