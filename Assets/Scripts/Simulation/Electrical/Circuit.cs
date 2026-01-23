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
        internal static List<WeakReference<Circuit>> _allCircuits = new();

        private Dictionary<(ElectricalComponent component, int pin), MNASolver.Unknown> _nodes = new();
        private List<ElectricalComponent> _components = new();
        private List<PowerSink> _powerSinks = new();
        private List<PowerSource> _powerSources = new();
        private List<INonlinearComponent> _nonlinearComponents = new();
        private bool _frequencyInitialized;
        private bool _initialized;

        public int Id { get; } = (new System.Random()).Next();

        public MNASolver Solver { get; } = new();

        public IReadOnlyList<ElectricalComponent> Components => _components.AsReadOnly();
        public IReadOnlyList<PowerSink> PowerSinks => _powerSinks.AsReadOnly();
        public IReadOnlyList<PowerSource> PowerSources => _powerSources.AsReadOnly();
        public IReadOnlyList<INonlinearComponent> NonlinearComponents => _nonlinearComponents.AsReadOnly();

        /// <summary>
        /// The frequency of any AC voltages or currents in the circuit.
        /// </summary>
        public double Frequency { get; private set; }

        /// <summary>
        /// The time delta (dt) to use between each tick.
        /// </summary>
        public double TimeDelta { get; private set; } = 0.5;

        public Circuit()
        {
            _allCircuits.Add(new WeakReference<Circuit>(this));
        }

        public void AddComponent(ElectricalComponent component)
        {
            // Hardwired.LogDebug($"Circuit {Id} - Adding component: {component.GetType()}");

            _components.Add(component);

            if (component is PowerSink powerSink)
            {
                _powerSinks.Add(powerSink);
            }

            if (component is INonlinearComponent nonlinearComponent)
            {
                _nonlinearComponents.Add(nonlinearComponent);
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
            // Hardwired.LogDebug($"Circuit {Id} - Removing component: {component.GetType()}");

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

            if (component is INonlinearComponent nonlinearComponent)
            {
                _nonlinearComponents.Remove(nonlinearComponent);
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
            lock (_nodes)
            {
                // pin -1 (or any negative pin) is the common ground
                if (pin < 0) { return null; }

                MNASolver.Unknown? node;

                if (_nodes.TryGetValue((component, pin), out node))
                {
                    return node;
                }

                // Look for any references from the "peers" of this connection (i.e. other components on this object or the connected object that share the connection)
                node = GetPeers(component, pin)
                    .Select(key => _nodes.GetValueOrDefault(key))
                    .FirstOrDefault(n => n != null)
                    // If no reference from a peer is found, add a new node to the solver
                    ?? Solver.AddUnknown();

                _nodes.Add((component, pin), node);

                return node;
            }
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
            lock (_nodes)
            {
                // pin -1 (or any negative pin) is the common ground
                if (pin < 0) { return; }

                // Get the node registered for this connection, if one exists
                if (!_nodes.TryGetValue((component, pin), out MNASolver.Unknown node))
                {
                    // No node registered for this connection, nothing to do...
                    return;
                }

                // Remove the reference for this connection
                _nodes.Remove((component, pin));

                // Hardwired.LogDebug($"Circuit {Id} - Removing node reference for node {node.Index} - remaining references: {_nodes.Count(e => e.Value.Index == node.Index)}");

                // Check if there are any other references left to this node
                bool stillAlive = _nodes.Any(entry => entry.Value.Index == node.Index);
                if (!stillAlive)
                {
                    // Hardwired.LogDebug($"Circuit {Id} - Removing node {node.Index} ({node.GetHashCode()})");
                    // If no more references, remove the node from the MNA solver
                    Solver.RemoveUnknown(node);
                }
            }
        }

        private IEnumerable<(ElectricalComponent component, int pin)> GetPeers(ElectricalComponent component, int pin)
        {
            if (pin < 0) { yield break; }

            // Look for any other components on the same object that share this pin
            foreach (var otherComponent in component.GetComponents<ElectricalComponent>().Where(c => c != component && c.UsesConnection(pin)))
            {
                yield return (otherComponent, pin);
            }

            // Look for components on the "peer" to this connection (i.e. other device we're connected to, if any)
            if (component.TryGetComponent(out SmallGrid smallGrid))
            {
                var connection = smallGrid.OpenEnds[pin];
                var peerIndex = connection.GetPeerIndex();
                var peerComponents = connection.GetOther(false)?.GetComponents<ElectricalComponent>().Where(c => c.UsesConnection(peerIndex));
                foreach (var peer in peerComponents ?? Enumerable.Empty<ElectricalComponent>())
                {
                    yield return (peer, peerIndex);
                }
            }
            // If we're not actually on a device (and so don't have connections), assume the pin is a global index, and look for other node references to the same pin
            // (this is mostly used as a unit test tool, so the unit tests don't have to set up a full game object/cable network, and can instead use global node indecies)
            else
            {
                foreach (var key in _nodes.Keys.Where(k => k.pin == pin))
                {
                    yield return key;
                }
            }
        }

        public void ProcessTick()
        {
            try
            {
                if (SolveInitial())
                {
                    // If initial solve had a solution, iterate non-linear components until convergance
                    SolveIterative();
                }
            }
            catch (Exception e)
            {
                Hardwired.LogDebug($"Circuit {Id} -- Error processing tick! {e}");

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

        private bool SolveInitial()
        {
            // Initialize
            InitializeFrequency();
            Initialize();

            // Clear/reset values
            Solver.Z.Clear();

            // Update A matrix & Z vector
            UpdateState();

            // Solve initial state (x_0)
            return Solver.SolveInitial();
        }

        private void SolveIterative()
        {
            bool hasConverged = false;
            int i = 0;
            for (; i < 50 && !hasConverged; i++)
            {
                // clear/reset values
                Solver.BeginNRIteration();

                // Update J matrix & F vector
                foreach (var nonlinearComponent in NonlinearComponents)
                {
                    nonlinearComponent.UpdateDifferentialState();
                }

                // Solve for x(i + 1)
                hasConverged = Solver.SolveNRIteration(i);
            }

            // Hardwired.LogDebug($"Circuit {Id} -- finished solving after {i} iterations -- converged: {hasConverged}");
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