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
        public Circuit? Circuit;

        public int PinA = -1;

        public int PinB = -1;

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
                stringBuilder.AppendLine($"Circuit network: {Circuit.Id}");
            }
        }

        /// <summary>
        /// Called by the network manager during a power update to (re)initialize the MNA solver's A matrix with this component's values.
        /// 
        /// This function is only called once after a network change in order to initialize the solver's A matrix.
        /// Since decomposing the A matrix is the most costly step of simulating the circuit, we want to avoid changing the A matrix
        /// when possible. In general we should only need to change the A matrix (and therefore call this function) when the network
        /// topology changes (i.e. a component is added/removed, etc). Otherwise changes to components that have already been initialized
        /// should happen in `UpdateSolverInputs()`.
        /// </summary>
        /// <param name="solver"></param>
        public virtual void Initialize(Circuit circuit)
        {
            // Remove from previous circuit if initializing for a new one
            if (Circuit != null && Circuit != circuit)
            {
                Remove(Circuit);
            }

            Circuit = circuit;

            _vA = Circuit.GetNode(this, PinA);
            _vB = Circuit.GetNode(this, PinB);
        }

        /// <summary>
        /// Called by the network manager to remove this component from the given circuit.
        /// </summary>
        /// <param name="circuit"></param>
        public virtual void Remove(Circuit circuit)
        {
            Circuit?.RemoveNodeReference(this, PinA);
            Circuit?.RemoveNodeReference(this, PinB);
            Circuit = null;
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

        protected virtual bool UsesConnection(int connection)
            => PinA == connection || PinB == connection;
    }
}
