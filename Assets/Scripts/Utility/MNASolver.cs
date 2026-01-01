#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Factorization;
using Complex = System.Numerics.Complex;

namespace Hardwired.Utility
{
    /// <summary>
    /// A Modified Node Analysis solver which can solve most linear circuits, both AC and DC, by using a system of equations built up using Kirchoff's laws.
    /// 
    /// Non-linear components can be approximated using various strategies, though I don't think we'll actually end up needing any non-linear components...
    /// 
    /// References:
    /// - https://en.wikipedia.org/wiki/Nodal_analysis
    /// - https://en.wikipedia.org/wiki/Mesh_analysis
    /// - https://en.wikipedia.org/wiki/System_of_linear_equations
    /// - https://github.com/age-series/ElectricalAge/blob/main/src/main/java/mods/eln/sim/mna/SubSystem.java
    /// - https://ecircuitcenter.com/SpiceTopics/Nodal%20Analysis/Nodal%20Analysis.htm#top
    /// - https://cheever.domains.swarthmore.edu/Ref/mna/MNA2.html
    /// </summary>
    public class MNASolver
    {
        /// <summary>
        /// The number of nodes in the circuit
        /// </summary>
        private int _nodes;

        /// <summary>
        /// The number of voltage sources in the circuit
        /// </summary>
        private int _voltageSources;

        /// <summary>
        /// Matrix of coefficients for the system of equations.
        /// 
        /// A * x = z
        /// 
        /// Where "x" is a vector of unknown values (to be solved for), and "z" is a vector of known values.
        /// 
        /// A[n, m] for values of n and m between 0 and `_nodes` represents the admittance between the nodes
        /// n and m, which is a measure of the ability for electricity to flow between two points (for simple
        /// circuits this is essentially the reciprocol of resistance, though other properties can contribute
        /// to this too, especially for AC circuits).
        /// 
        /// Row A[(_nodes + v), _] for values of v between 0 and `_voltageSources` represents the linear equation
        /// for the voltage of voltage source `v`.
        /// 
        /// Column A[n, (_nodes + v)] for values of v between 0 and `_voltageSources` represents the contribution
        /// of voltage source `v`'s current to node n.
        /// </summary>
        private Matrix<Complex>? _A;

        private LU<Complex>? _A_LU;

        /// <summary>
        /// Vector of unknown values to be solved for.
        /// The first `_nodes` values will be the voltages at each node.
        /// The next `_voltageSources` values will be the currents across each voltage source.
        /// </summary>
        private Vector<Complex>? _x;

        /// <summary>
        /// Vector of known values to be used as inputs.
        /// The first `_nodes` values will be the current flowing through each node from current sources (positive values indicate current flowing out of the node).
        /// The next `_voltageSources` values will be the voltage of each voltage source.
        /// </summary>
        private Vector<Complex>? _z;

        /// <summary>
        /// (re)initializes the solver for a circuit with the given number of nodes and voltage sources.
        /// </summary>
        /// <param name="nodes"></param>
        /// <param name="voltageSources"></param>
        public void Initialize(int nodes, int voltageSources)
        {
            _nodes = nodes;
            _voltageSources = voltageSources;

            int totalSize = _nodes + _voltageSources;

            // (re)create A matrix, or zero if existing matrix is correct size
            if (_A is null || _A.RowCount != totalSize)
            {
                _A = Matrix<Complex>.Build.Dense(totalSize, totalSize);
            }
            else
            {
                _A.Clear();
            }

            // (re)create z vector, or zero if existing vector is correct size
            if (_z is null || _z.Count != totalSize)
            {
                _z = Vector<Complex>.Build.Dense(totalSize);
            }
            else
            {
                _z.Clear();
            }

            // Clear cached factorization, regardless of if we made a new A matrix or not
            _A_LU = null;
        }

        /// <summary>
        /// Adds the given admittance value between the given nodes.
        /// 
        /// If n is null, it is assumed to be the common ground node.
        /// 
        /// Admittance is a complex value that describes the ability of electricity to flow between two points.
        /// The real part represents conductance (i.e. reciprocal of resistance).
        /// The imaginary part represents susceptance (i.e. reciprocal of reactance).
        /// 
        /// The reciprocal of admittance is also known as impedance.
        /// </summary>
        /// <param name="n"></param>
        /// <param name="m"></param>
        /// <param name="a"></param>
        public void AddAdmittance(int? n, int? m, Complex value)
        {
            if (_A is null || _z is null) { ThrowNotInitializedException(); }

            if (n != null)
            {
                _A[n.Value, n.Value] += value;
            }

            if (m != null)
            {
                _A[m.Value, m.Value] += value;
            }

            if (n != null && m != null)
            {
                _A[n.Value, m.Value] -= value;
                _A[m.Value, n.Value] -= value;
            }

            // Since A was modified, invalidate factorization so it will be re-factored on the next solve
            _A_LU = null;
        }

        /// <summary>
        /// Adds the given resistance value between the given nodes.
        /// 
        /// If n is null, it is assumed to be the common ground node.
        /// </summary>
        /// <param name="n"></param>
        /// <param name="m"></param>
        /// <param name="value"></param>
        public void AddResistance(int? n, int? m, double value)
            => AddAdmittance(n, m, 1.0 / value);

        /// <summary>
        /// Adds the equation `V(m) - V(n) = z[v]` to the system of equations.
        /// 
        /// If either `n` or `m` is null, it is assumed to be the common ground node.
        /// 
        /// Must be called when initializing the solver, as this method modifies the A matrix.
        /// 
        /// After initializing, the voltage of this voltage source can be updated by calling `SetVoltage()`
        /// </summary>
        /// <param name="n"></param>
        /// <param name="m"></param>
        /// <param name="v"></param>
        /// <param name="value"></param>
        public void InitializeVoltageSource(int? n, int? m, int v)
        {
            if (_A is null || _z is null) { ThrowNotInitializedException(); }

            // Calculate index for this voltage source - by convention, the equations for each voltage source are put at the end of the matrix,
            // after node voltage equations
            int j = _nodes + v;

            if (n != null)
            {
                // V(n) ... = V
                _A[j, n.Value] = -1;

                // Add the voltage source's current to the destination node
                _A[n.Value, j] = -1;
            }

            if (m != null)
            {
                // ... -V(m) = V
                _A[j, m.Value] = 1;

                // Subtract the voltage source's current from the source node
                _A[m.Value, j] = 1;
            }

            // Since A was modified, invalidate factorization so it will be re-factored on the next solve
            _A_LU = null;
        }

        /// <summary>
        /// Sets the voltage for voltage source `v` to the given value.
        /// 
        /// `InitializeVoltageSource()` must be called before this method to ensure the system of equations was correctly set up.
        /// </summary>
        /// <param name="v"></param>
        /// <param name="value"></param>
        public void SetVoltage(int v, Complex value)
        {
            if (_A is null || _z is null) { ThrowNotInitializedException(); }

            // Calculate index for this voltage source - by convention, the equations for each voltage source are put at the end of the matrix,
            // after node voltage equations
            int j = _nodes + v;

            // Set input voltage to the given value
            _z[j] = value;
        }

        /// <summary>
        /// Sets the current between two nodes to a specific value, repersenting a current source in the circuit.
        /// 
        /// If either n or m is null, it is assumed to be the common ground node.
        /// </summary>
        /// <param name="n"></param>
        /// <param name="m"></param>
        /// <param name="i"></param>
        public void SetCurrent(int? n, int? m, Complex value)
        {
            if (_A is null || _z is null) { ThrowNotInitializedException(); }

            if (n != null)
            {
                _z[n.Value] = -value;
            }

            if (m != null)
            {
                _z[m.Value] = value;
            }
        }

        /// <summary>
        /// Solves the system for the voltage at each node and current through each voltage source.
        /// </summary>
        public void Solve()
        {
            if (_A is null || _z is null) { ThrowNotInitializedException(); }

            // Factorize A into L & U matricies, if not already set up
            _A_LU ??= _A.LU();

            // Solve for x
            _x = _A_LU.Solve(_z);
        }

        /// <summary>
        /// Gets the voltage (relative to common ground) at the given node.
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        public Complex GetVoltage(int? n)
        {
            if (n != null && _x is not null && _x.Count > n.Value)
            {
                return _x[n.Value];
            }
            else
            {
                return Complex.Zero;
            }
        }

        /// <summary>
        /// Gets the current for the given voltage source from the result vector
        /// </summary>
        /// <param name="vIndex"></param>
        /// <returns></returns>
        public Complex GetCurrent(int v)
        {
            int m = _nodes + v;

            if (_x is not null && _x.Count > m)
            {
                return _x[m];
            }
            else
            {
                return Complex.Zero;
            }
        }

        [DoesNotReturn]
        private void ThrowNotInitializedException()
        {
            throw new InvalidOperationException("MNASolver is not initialized");
        }
    }
}