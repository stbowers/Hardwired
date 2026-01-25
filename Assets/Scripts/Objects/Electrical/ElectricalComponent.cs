#nullable enable

using System.Text;
using Assets.Scripts.UI.HelperHints.Extensions;
using Hardwired.Simulation.Electrical;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    /// <summary>
    /// Base class for electrical components.
    /// 
    /// Each pin (PinA and PinB) should be assigned to a Connection in the editor.
    /// If either pin is assigned `-1`, it will be assumed to be connected to the common ground pin for the network.
    /// 
    /// Note that in order to make simulation easier, every component in the network shares the same ground, i.e. in particular
    /// internal resistance in the ground wire is not explicitly simulated. Instead internal resistance on the "live" wire is
    /// doubled, which should have the same effect in practice since the live and ground wires are always paired in a single
    /// cable (so i.e. it's impossible to have one without the other, and they will always end up in series, meaning their
    /// resistances can be added together).
    /// </summary>
    public abstract class ElectricalComponent : MonoBehaviour
    {
        /// <summary>
        /// Resistance value to use to tie nodes to ground, to prevent floating nodes.
        /// Should be relatively large, to avoid leaking any significant current, but not too large to avoid causing an ill-conditioned (singular or near-singular) matrix, which can cause problems for the solver.
        /// </summary>
        public const double R_GND = 1e6;

        /// <summary>
        /// Resistance value to use between the two nodes of the breaker when closed.
        /// Should be relatively small, to avoid voltage drop across the breaker, but not too small as to introduce numerical errors into the solver.
        /// </summary>
        public const double R_CLOSED = 1e-4;

        public Circuit? Circuit;

        public int PinA = -1;

        public int PinB = -1;

        protected bool _initialized;

        protected MNASolver.Unknown? _vA;

        protected MNASolver.Unknown? _vB;

        /// <summary>
        /// For debugging - add info to the passive tooltip
        /// </summary>
        /// <param name="stringBuilder"></param>
        public virtual void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            if (Circuit == null)
            {
                stringBuilder.AppendColorText("red", "Network Not Found");
            }
            else
            {
                stringBuilder.AppendLine($"-- Circuit network: {Circuit.Id} --");
                stringBuilder.AppendLine($"Node A: {_vA?.Index ?? -1} (connection {PinA}) | Node B: {_vB?.Index ?? -1} (connection {PinB})");
            }
        }

        public virtual string DebugInfo()
        {
            return $"pinA: {_vA?.Index ?? -1} ({PinA}) | pinB: {_vB?.Index ?? -1} ({PinB})";
        }

        /// <summary>
        /// Called by the network manager to add this component to a circuit
        /// </summary>
        /// <param name="solver"></param>
        public virtual void AddTo(Circuit circuit)
        {
            // Remove from previous circuit if initializing for a new one
            if (Circuit != null && Circuit != circuit)
            {
                RemoveFrom(Circuit);
            }

            // Add to new circuit
            Circuit = circuit;

            // Initialize pins
            _vA = Circuit.GetNode(this, PinA);
            _vB = Circuit.GetNode(this, PinB);
        }

        /// <summary>
        /// Called by the network manager to remove this component from the given circuit.
        /// </summary>
        /// <param name="circuit"></param>
        public virtual void RemoveFrom(Circuit circuit)
        {
            if (circuit != Circuit) { return; }

            // Clean up
            Deinitialize();

            // Remove from circuit
            Circuit = null;
            circuit.RemoveComponent(this);
            circuit.RemoveNodeReference(this, PinA);
            circuit.RemoveNodeReference(this, PinB);
        }

        /// <summary>
        /// Called by the network manager during a power update to (re)initialize the MNA solver's A matrix with this component's values.
        /// 
        /// This method is only called once whenever the circuit topology changes, but otherwise each tick tries to re-use the existing matrix for efficiency.
        /// If a re-initialization is required (such as when changing resistance values or other values in the A matrix), call Circuit.Reinitialize().
        /// </summary>
        public void Initialize()
        {
            // Clean up any existing state
            if (_initialized) { Deinitialize(); }

            InitializeInternal();

            _initialized = true;
        }

        protected virtual void InitializeInternal()
        {
        }

        /// <summary>
        /// Removes this component from the solver (i.e. remove admittance from the A matrix, clean up systems, etc).
        /// </summary>
        public void Deinitialize()
        {
            if (!_initialized) { return; }

            DeinitializeInternal();

            _initialized = false;
        }

        protected virtual void DeinitializeInternal()
        {
        }

        /// <summary>
        /// Called by the network manager during a power update to update the MNA solver's z vector with this component's values.
        /// 
        /// Compared to `InitializeSolver()`, this function is called for every power update tick. In order to avoid slowing down the simulation,
        /// this function should only make changes to the solver's z vector (set of inputs). The solver can then use the existing A matrix
        /// decomposition to quickly solve for the x vector (outputs).
        /// </summary>
        /// <param name="solver"></param>
        public virtual void UpdateState()
        {
        }

        /// <summary>
        /// Called by the network manager during a power update after the MNA solver has been updated with a new solution.
        /// 
        /// This function should retrieve any required solved values, such as voltage at a given node, in order to update this component.
        /// </summary>
        /// <param name="solver"></param>
        public virtual void ApplyState()
        {
        }

        public virtual bool UsesConnection(int connection)
            => PinA == connection || PinB == connection;
    }
}
