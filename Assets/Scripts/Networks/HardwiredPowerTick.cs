#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Assets.Scripts.Networks;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Pipes;
using Hardwired.Objects.Electrical;
using Hardwired.Simulation.Electrical;
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
                Circuit.RemoveComponent(component);
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
                    powerSink.MaxPower = 500;

                    powerSink.PinA = device.OpenEnds.FindIndex(c => (c.ConnectionType & NetworkType.Power) != NetworkType.None);
                    powerSink.PinB = -1;

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
                    powerSink.PowerTarget = generatedPower;
                }
            }

            // Update cables
            foreach (var cable in CableNetwork.CableList)
            {
                if (cable.GetComponent<Resistor>() is Resistor resistor) { continue; }

                for (int i = 0; i < cable.OpenEnds.Count; i++)
                {
                    for (int j = i + 1; j < cable.OpenEnds.Count; j++)
                    {
                        resistor = cable.gameObject.AddComponent<Resistor>();
                        resistor.Resistance = 0.001;

                        resistor.PinA = i;
                        resistor.PinB = j;

                        Hardwired.LogDebug($"Added resistor to cable between connection {i} and {j}");

                        Circuit.AddComponent(resistor);
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
            // TODO: Check cable temperatures; burn out if too hot
            // TODO: Apply power to devices

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