#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Text;
using Assets.Scripts.Networks;
using Assets.Scripts.Objects;
using Hardwired.Objects.Electrical;

namespace Hardwired.Utility
{
    public class Circuit : CableNetwork
    {
        private List<Node> _nodes = new();
        private List<Component> _components = new();
        private List<CableSegment> _cableSegments = new();
        private List<VoltageSource> _voltageSources = new();
        private MNASolver _solver = new();
        private bool _solverAdmittanceInitialized;

        public IReadOnlyList<Component> Components => _components.AsReadOnly();

        public void AddComponent(Component component)
        {
            if (component is Cable cable)
            {
                // If cable is a junction (more than 2 connections), create a cable segment for each connection
                if (cable.OpenEnds.Count > 2)
                {
                    var commonNode = new Node();
                    _nodes.Add(commonNode);

                    var connections = cable.OpenEnds;

                    // TODO
                }
                // Otherwise, if cable only has 2 connections, create a single cable segment (merging existing connections if needed)
                else
                {
                    CableSegment? cableSegment = null;
                    
                    var connectedSegments = cable.Connected()
                        .OfType<Cable>()
                        .Select(c => GetCableSegment(c)!)
                        .Where(s => s != null)
                        .ToList();

                    if (connectedSegments.Count == 1)
                    {
                        cableSegment = connectedSegments[0];
                        cableSegment.Cables.Add(cable);
                    }
                    else
                    {
                        Node nodeA = connectedSegments.ElementAtOrDefault(0)?.NodeA ?? new();
                        Node nodeB = connectedSegments.ElementAtOrDefault(1)?.NodeB ?? new();

                        cableSegment = new(nodeA, nodeB);

                        if (connectedSegments.Count > 0)
                        {
                            cableSegment.Cables.AddRange(connectedSegments[0].Cables);

                            _nodes.Remove(connectedSegments[0].NodeB);
                        }
                        else
                        {
                            _nodes.Add(nodeA);
                        }

                        cableSegment.Cables.Add(cable);

                        if (connectedSegments.Count > 1)
                        {
                            cableSegment.Cables.AddRange(connectedSegments[1].Cables);

                            _nodes.Remove(connectedSegments[1].NodeA);
                        }
                        else
                        {
                            _nodes.Add(nodeB);
                        }

                        foreach (var connectedSegment in connectedSegments)
                        {
                            _cableSegments.Remove(connectedSegment);
                        }

                        _cableSegments.Add(cableSegment);
                    }
                }
            }
            else if (component is VoltageSource voltageSource)
            {
                _voltageSources.Add(voltageSource);
            }

            _components.Add(component);

            // Circuit topology has changed, so we need to reset solver
            ReinitializeSolver();
        }

        public void RemoveComponent(Component component)
        {
            _components.Remove(component);

            if (component is Cable cable && GetCableSegment(cable) is CableSegment cableSegment)
            {
                // TODO: If the removed cable was in the "middle" of a segment, we need to split the
                // segment in to two segments, and even potentially split the network into two networks
                cableSegment.Cables.Remove(cable);

                if (cableSegment.Cables.Count == 0)
                {
                    _cableSegments.Remove(cableSegment);
                }
            }
            else if (component is VoltageSource voltageSource)
            {
                _voltageSources.Remove(voltageSource);
            }

            // Circuit topology has changed, so we need to reset solver
            ReinitializeSolver();

            // Refresh network will clean up and destroy the network if all components are removed...
            RefreshNetwork();
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
                Hardwired.LogDebug($"Circuit.OnPowerTick() -- Network ID: {ReferenceId} -- Average tick time: {averageTickTime} ms -- nodes: {_nodes.Count}, vsources: {_voltageSources.Count}");
            }

            var tock = DateTime.Now;
            _timeProcessing += tock - tick;
        }

        public override bool IsNetworkValid()
        {
            return Components.Count > 0;
        }

        private CableSegment? GetCableSegment(Cable cable)
        {
            return _cableSegments.FirstOrDefault(s => s.Cables.Contains(cable));
        }

        private void ReinitializeSolver()
        {
            int nNodes = _nodes.Count;
            int nVSources = _voltageSources.Count;

            _solver.Initialize(nNodes, nVSources);
            _solverAdmittanceInitialized = false;
        }

        private void Solve()
        {
            // Add adittance from cables if needed (only has to be done once after (re)initializing the solver)
            if (!_solverAdmittanceInitialized)
            {
                foreach (var cableSegment in _cableSegments)
                {
                    int n = _nodes.IndexOf(cableSegment.NodeA);
                    int m = _nodes.IndexOf(cableSegment.NodeB);

                    // A cable segment represents one or more cables in series - add each cable's resistance to get the total resistance of the segment
                    // TODO: need to add reactive components for AC
                    double r = cableSegment.Cables.Select(c => c.Resistance).Sum();
                    _solver.AddResistance(n, m, r);
                }
                _solverAdmittanceInitialized = true;

                // TEMP - add resistor at end of circuit so there's always current flow even when we don't have a load
                _solver.AddAdmittance(null, _nodes.Count - 1, 0.001);
            }

            // Add voltage sources
            for (int v = 0; v < _voltageSources.Count; v++)
            {
                var vsource = _voltageSources[v];

                var node = GetNode(vsource.OpenEnds[0]) ?? 0;

                _solver.SetVoltage(node, v, vsource.Voltage);
            }

            // Solve the circuit
            _solver.Solve();

            // Update components with solved values as applicable
            for (int v = 0; v < _voltageSources.Count; v++)
            {
                var vsource = _voltageSources[v];

                vsource.Current = _solver.GetCurrent(v).Magnitude;
            }
        }

        private int? GetNode(Connection connection)
        {
            var cable = connection.GetOther() as Cable;
            if (cable == null) { return null; }

            var cableSegment = GetCableSegment(cable);
            if (cableSegment == null) { return null; }

            int cableIndex = cableSegment.Cables.IndexOf(cable);

            if (cableIndex == 0)
            {
                return _nodes.IndexOf(cableSegment.NodeA);
            }
            else
            {
                return _nodes.IndexOf(cableSegment.NodeB);
            }
        }

        public static Circuit? Merge(Circuit? a, Circuit? b)
        {
            if (a is null) { return b; }
            if (b is null) { return a; }

            foreach (var component in b._components.ToList())
            {
                component.Circuit = a;
                a.AddComponent(component);
                b.RemoveComponent(component);
            }

            return a;
        }
    }
}