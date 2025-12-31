#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using MathNet.Numerics.LinearAlgebra;

using Complex = System.Numerics.Complex;

namespace Hardwired.Utility
{
    // At the moment this is really more of an NA solver, rather than an MNA solver... That may change in the future, or otherwise this class may be renamed.
    //
    // Note that there are also other approaches to solving circuits - in particular the direct alternative to "node analysis" is "mesh analysis", which uses
    // Kirchoff's voltage law (KVL) rather than Kirchoff's current law (KCL) for the system of equations. According to Wikipedia, mesh analysis is particularly
    // suitable for "planar circuits" (i.e. circuits where no wires cross each other). In our case, all our circuits should be planar (in fact, they follow a
    // fairly regular structure, since all devices must be wired in parallel), so it may be possible to create a more optimal solution that doesn't have to do
    // so many calculations.
    //
    // References:
    // - https://en.wikipedia.org/wiki/Nodal_analysis
    // - https://en.wikipedia.org/wiki/Mesh_analysis
    // - https://en.wikipedia.org/wiki/System_of_linear_equations
    // - https://github.com/age-series/ElectricalAge/blob/main/src/main/java/mods/eln/sim/mna/SubSystem.java
    public class MNASolver
    {
        /// <summary>
        /// Matrix of admittance values between each node.
        /// 
        /// Admittance represents the ability for electricity to flow (i.e. reciprocol of resistance for DC circuits).
        /// 
        /// The matrix equation
        /// 
        /// A * v = i
        /// 
        /// Represents a system of equations following Kirchoff's Current Law (KCL), which we end up solving for `v` (the voltage at each node).
        /// </summary>
        private Matrix<Complex>? _A;

        // A * v = I
        private Vector<Complex>? _i;
        private Vector<Complex>? _v;

        public void Initialize(int nodes)
        {
            _A = Matrix<Complex>.Build.Dense(nodes, nodes);
            _i = Vector<Complex>.Build.Dense(nodes);
        }

        /// <summary>
        /// Adds the given admittance value between the given nodes.
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
        public void AddAdmittance(int n, int m, Complex a)
        {
            if (_A is null || _i is null) { ThrowNotInitializedException(); }

            _A[n, n] += a;
            _A[m, m] += a;

            _A[n, m] -= a;
            _A[m, n] -= a;
        }

        /// <summary>
        /// Resets the current flowing through each node to 0.
        /// This should be called prior to adding any current sources.
        /// </summary>
        public void ClearCurrent()
        {
            if (_A is null || _i is null) { ThrowNotInitializedException(); }

            _i.Clear();
        }

        /// <summary>
        /// Adds a current source between the given nodes with the given complex current value.
        /// </summary>
        /// <param name="n"></param>
        /// <param name="m"></param>
        /// <param name="i"></param>
        public void AddCurrent(int n, int m, Complex i)
        {
            if (_A is null || _i is null) { ThrowNotInitializedException(); }

            _i[n] += i;
            _i[m] -= i;
        }

        /// <summary>
        /// Solves the system for the voltage at each node.
        /// </summary>
        public void Solve()
        {
            if (_A is null || _i is null) { ThrowNotInitializedException(); }

            _v = _A.Solve(_i);
        }

        /// <summary>
        /// Gets the voltage (relative to common ground) at the given node.
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        public Complex GetVoltage(int n)
        {
            return _v?.At(n) ?? Complex.Zero;
        }


        [DoesNotReturn]
        private void ThrowNotInitializedException()
        {
            throw new InvalidOperationException("MNASolver is not initialized");
        }
    }
}