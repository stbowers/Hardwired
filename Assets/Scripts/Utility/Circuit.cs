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

namespace Hardwired.Utility
{
    public class Circuit : CableNetwork
    {
        private List<Node> _nodes = new();
        private List<ElectricalComponent> _components = new();
        private List<VoltageSource> _voltageSources = new();
        private MNASolver _solver = new();
        private bool _solverInitialized;

        public IReadOnlyList<ElectricalComponent> Components => _components.AsReadOnly();

        private void EnsureConnectionHasNode(Connection? connection)
        {
            if (connection == null) { return; }

            // Try to get an existing node attached to this connection
            Node? node = GetNode(connection);
            if (node != null) { return; }

            // Next, try to get an existing node attached to this connection's peer.
            // If one isn't found, create a new node
            node = GetNode(connection.GetPeer()); // ?? new();

            if (node == null)
            {
                node = new();
                _nodes.Add(node);
            }

            // Attach this connection to the node
            node.Connections.Add(connection);
        }

        public void AddComponent(ElectricalComponent component)
        {
            lock (_solver)
            {
                // Create a node (or add the connection to an existing node) for each connection on this component
                EnsureConnectionHasNode(component.ConnectionA);
                EnsureConnectionHasNode(component.ConnectionB);

                if (component is VoltageSource voltageSource)
                {
                    _voltageSources.Add(voltageSource);
                }

                _components.Add(component);

                // Circuit topology has changed, so we need to reset solver
                InvalidateSolver();
            }
        }

        public void RemoveComponent(ElectricalComponent component)
        {
            lock (_solver)
            {
                _components.Remove(component);

                if (component is VoltageSource voltageSource)
                {
                    _voltageSources.Remove(voltageSource);
                }

                // Circuit topology has changed, so we need to reset solver
                InvalidateSolver();

                // Refresh network will clean up and destroy the network if all components are removed...
                RefreshNetwork();
            }
        }

        int _powerTicks = 0;
        TimeSpan _timeProcessing = TimeSpan.Zero;
        public override void OnPowerTick()
        {
            var tick = DateTime.Now;

            _powerTicks += 1;

            Solve();

            if ((_powerTicks % 30) == 0)
            {
                double averageTickTime = _timeProcessing.Milliseconds / 30.0;
                _timeProcessing = TimeSpan.Zero;
                Hardwired.LogDebug($"Circuit.OnPowerTick() -- Network ID: {ReferenceId} -- Average tick time: {averageTickTime} ms -- components: {_components.Count}, nodes: {_nodes.Count}, vsources: {_voltageSources.Count}");
            }

            var tock = DateTime.Now;
            _timeProcessing += tock - tick;
        }

        public override bool IsNetworkValid()
        {
            return Components.Count > 0;
        }

        /// <summary>
        /// Marks the solver as uninitialized so it will be re-initialized on the next power tick
        /// </summary>
        private void InvalidateSolver()
        {
            _solverInitialized = false;
        }

        private void Solve()
        {
            lock (_solver)
            {
                // (re)initialize solver if needed
                if (!_solverInitialized)
                {
                    int nNodes = Math.Max(_nodes.Count, 1);
                    int nVSources = _voltageSources.Count;

                    // Determine the AC frequency to use
                    double frequency = 0f;
                    foreach (var component in Components)
                    {
                        double componentFrequency = 0f;

                        if (component is VoltageSource voltageSource)
                        {
                            componentFrequency = voltageSource.Frequency;
                        }
                        else if (component is CurrentSource currentSource)
                        {
                            componentFrequency = currentSource.Frequency;
                        }
                        else
                        {
                            continue;
                        }

                        if (frequency != 0f && componentFrequency != 0f && componentFrequency != frequency)
                        {
                            Hardwired.LogDebug($"Circuit network {ReferenceId} -- Invalid network, cannot have multiple AC sources at different frequencies ({frequency}, {componentFrequency})");
                            return;
                        }
                        else if (componentFrequency != 0f)
                        {
                            frequency = componentFrequency;
                        }
                    }

                    // Assign indices to each node
                    for (int i = 0; i < _nodes.Count; i++)
                    {
                        _nodes[i].Index = i;
                    }

                    Hardwired.LogDebug($"Circuit network {ReferenceId} - Initializing solver with {nNodes} nodes and {nVSources} voltage sources, at {frequency} Hz");

                    _solver.Initialize(nNodes, frequency);

                    foreach (var component in Components)
                    {
                        component.InitializeSolver(_solver);
                    }

                    _solverInitialized = true;
                }

                // Update solver inputs (voltage source voltages, current source currents, etc) from each component
                foreach (var component in Components)
                {
                    component.UpdateSolverInputs(_solver);
                }

                // Solve the circuit
                _solver.Solve();

                // Update components with solver output
                foreach (var component in Components)
                {
                    component.GetSolverOutputs(_solver);
                }
            }
        }

        public Node? GetNode(Connection? connection)
        {
            if (connection == null) { return null; }

            return _nodes.FirstOrDefault(n => n.Connections.Contains(connection));
        }

        public int? GetVoltageSourceIndex(VoltageSource? voltageSource)
        {
            if (voltageSource == null)
            {
                return null;
            }

            var result = _voltageSources?.IndexOf(voltageSource);

            if (result < 0)
            {
                return null;
            }

            return result;
        }

        public static Circuit? Merge(Circuit? a, Circuit? b)
        {
            if (a is null) { return b; }
            if (b is null) { return a; }

            foreach (var component in b.Components.ToList())
            {
                b.RemoveComponent(component);

                component.Circuit = a;
                a.AddComponent(component);
            }

            // Clear circuit B
            b._components.Clear();
            b._nodes.Clear();
            b._voltageSources.Clear();

            return a;
        }
    }
}