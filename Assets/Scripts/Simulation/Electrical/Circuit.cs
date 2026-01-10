#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using Assets.Scripts.Networks;
using Assets.Scripts.Objects;
using Hardwired.Objects;
using Hardwired.Objects.Electrical;
using UnityEngine;

namespace Hardwired.Simulation.Electrical
{
    public class Circuit
    {
        private static int _nextId = 0;

        private Dictionary<(GameObject parent, int pin), MNASolver.Unknown> _nodes = new();
        private List<ElectricalComponent> _components = new();
        private List<PowerSink> _powerSinks = new();
        private List<PowerSource> _powerSources = new();
        private bool _frequencyInitialized;
        private bool _initialized;

        public int Id { get; } = _nextId++;

        public MNASolver Solver { get; } = new();

        public IReadOnlyList<ElectricalComponent> Components => _components.AsReadOnly();
        public IReadOnlyList<PowerSink> PowerSinks => _powerSinks.AsReadOnly();
        public IReadOnlyList<PowerSource> PowerSources => _powerSources.AsReadOnly();

        /// <summary>
        /// The frequency of any AC voltages or currents in the circuit.
        /// </summary>
        public double Frequency { get; private set; }

        /// <summary>
        /// The time delta (dt) to use between each tick.
        /// </summary>
        public double TimeDelta { get; private set; } = 0.5;

        public void AddComponent(ElectricalComponent component)
        {
            Hardwired.LogDebug($"Adding component: {component.GetType()}");

            _components.Add(component);

            if (component is PowerSink powerSink)
            {
                _powerSinks.Add(powerSink);
            }

            // If the component is a frequency "driver", re-evaluate the frequency on the next power tick
            if (component is VoltageSource vs && vs.IsFrequencyDriver)
            {
                _frequencyInitialized = false;
            }
            else if (component is CurrentSource cs && cs.IsFrequencyDriver)
            {
                _frequencyInitialized = false;
            }

            // Initialize component
            component.AddTo(this);
            if (_initialized)
            {
                component.Initialize();
            }
        }

        public void RemoveComponent(ElectricalComponent component)
        {
            Hardwired.LogDebug($"Removing component: {component.GetType()}");

            // Remove from components list
            if (!_components.Remove(component))
            {
                // If the component wasn't in this circuit to begin with, we don't have anything else to clean up...
                return;
            }

            if (component is PowerSink powerSink)
            {
                _powerSinks.Remove(powerSink);
            }

            // If the component was a frequency "driver", re-evaluate the frequency on the next power tick
            if (component is VoltageSource vs && vs.IsFrequencyDriver)
            {
                _frequencyInitialized = false;
            }
            else if (component is CurrentSource cs && cs.IsFrequencyDriver)
            {
                _frequencyInitialized = false;
            }

            // Deinitialize
            component.RemoveFrom(this);
        }

        /// <summary>
        /// Returns the MNASolver.Unknown object representing the voltage at the node referenced by the given pin on the component.
        /// 
        /// If the component is part of a SmallGrid object, the pin is taken to be the index of 
        /// </summary>
        /// <param name="component"></param>
        /// <param name="pin"></param>
        /// <returns></returns>
        public MNASolver.Unknown? GetNode(ElectricalComponent component, int pin)
        {
            // pin -1 (or any negative pin) is the common ground
            if (pin < 0) { return null; }

            MNASolver.Unknown? node;
            GameObject gameObject = component.gameObject;

            if (_nodes.TryGetValue((gameObject, pin), out node))
            {
                return node;
            }

            var peer = TryGetPeer(gameObject, pin);
            if (peer != null)
            {
                node = _nodes.GetValueOrDefault(peer.Value);
            }

            node ??= Solver.AddUnknown();
            _nodes.Add((gameObject, pin), node);

            return node;
        }

        /// <summary>
        /// Removes the the given component's reference to the given node.
        /// If all references to a node are gone, the node will be removed.
        /// </summary>
        /// <param name="component"></param>
        /// <param name="pin"></param>
        /// <param name="node"></param>
        public void RemoveNodeReference(ElectricalComponent component, int pin)
        {
            // pin -1 (or any negative pin) is the common ground
            if (pin < 0) { return; }

            GameObject gameObject = component.gameObject;

            // Get the node registered for this connection, if one exists
            if (!_nodes.TryGetValue((gameObject, pin), out MNASolver.Unknown node))
            {
                // No node registered for this connection, nothing to do...
                return;
            }

            // Remove the reference for this connection
            _nodes.Remove((gameObject, pin));

            Hardwired.LogDebug($"Removing node reference for node {node.Index} - remaining references: {_nodes.Count(e => e.Value == node)}");

            // Check if there are any other references left to this node
            bool stillAlive = _nodes.Any(entry => entry.Value == node);
            if (!stillAlive)
            {
                Hardwired.LogDebug($"Removing node {node.Index}");
                // If no more references, remove the node from the MNA solver
                Solver.RemoveUnknown(node);
            }
        }

        private (GameObject component, int pin)? TryGetPeer(GameObject component, int pin)
        {
            if (pin < 0) { return null; }

            if (component.TryGetComponent(out SmallGrid smallGrid))
            {
                var connection = smallGrid.OpenEnds[pin];
                var peerIndex = connection.GetPeerIndex();
                var peer = connection.GetOther(false)?.GetComponents<ElectricalComponent>().FirstOrDefault(c => c.UsesConnection(peerIndex));
                if (peer != null)
                {
                    return (peer.gameObject, peerIndex);
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return _nodes.Keys.FirstOrDefault(k => k.pin == pin);
            }
        }

        public void ProcessTick()
        {
            try
            {
                InitializeFrequency();
                Initialize();
                Solver.Z.Clear();
                UpdateState();
                Solver.Solve();
            }
            catch (Exception e)
            {
                Hardwired.LogDebug($"Error processing tick! {e}");

                Solver?.Z?.Clear();
                Solver?.X?.Clear();
            }

            ApplyState();
        }

        /// <summary>
        /// Marks the circuit as uninitialized so it will be re-initialized on the next power tick
        /// </summary>
        public void Invalidate()
        {
            _initialized = false;
        }

        private void Initialize()
        {
            if (_initialized) { return; }

            Hardwired.LogDebug($"Circuit network {Id} - Initializing circuit with {Components.Count} components at {Frequency} Hz");

            // Clear MNA matrix
            Solver.A.Clear();

            foreach (var component in Components)
            {
                component.Initialize();
            }

            _initialized = true;
        }

        // Determine the AC frequency to use
        private void InitializeFrequency()
        {
            // Check if frequency is already initialized
            if (_frequencyInitialized) { return; }

            // Set to true if the frequncy is changed and thus the circuit needs to be reinitialized
            bool frequencyChanged = false;

            // Set to true if any components are "driving" the frequency, and thus it must stay the same
            // (two components can't "drive" the circuit at different frequencies)
            bool frequencyDriven = false;

            foreach (var component in Components)
            {
                double componentFrequency;

                if (component is VoltageSource voltageSource && voltageSource.IsFrequencyDriver)
                {
                    componentFrequency = voltageSource.Frequency;
                }
                else if (component is CurrentSource currentSource && currentSource.IsFrequencyDriver)
                {
                    componentFrequency = currentSource.Frequency;
                }
                else
                {
                    continue;
                }

                if (frequencyDriven && componentFrequency != Frequency)
                {
                    throw new InvalidOperationException($"Circuit network {Id} invalid -- cannot have multiple AC sources at different frequencies");
                }

                frequencyDriven = true;
                frequencyChanged = componentFrequency != Frequency;
                Frequency = componentFrequency;
            }

            // If the circuit freqency was changed, re-initialize the circuit
            if (frequencyChanged)
            {
                _initialized = false;
            }

            _frequencyInitialized = true;
        }

        private void UpdateState()
        {
            // Update solver inputs (voltage source voltages, current source currents, etc) from each component
            foreach (var component in Components)
            {
                component.UpdateState();
            }
        }

        private void ApplyState()
        {
            // Update components with solver output
            foreach (var component in Components)
            {
                component.ApplyState();
            }
        }

        public static Circuit? Merge(Circuit? a, Circuit? b)
        {
            // If a or b is null, nothing to merge (just return the other)
            if (a is null) { return b; }
            if (b is null) { return a; }

            // If a == b, nothing to merge (same network)
            if (a == b) { return a; }

            foreach (var component in b.Components.ToList())
            {
                b.RemoveComponent(component);

                component.Circuit = a;
                a.AddComponent(component);
            }

            // Clear circuit B
            b._components.Clear();
            b._nodes.Clear();

            return a;
        }
    }
}