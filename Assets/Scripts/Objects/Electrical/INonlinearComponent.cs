#nullable enable

namespace Hardwired.Objects.Electrical
{
    /// <summary>
    /// Interface for components which have a non-linear voltage-current relationship.
    /// These components are solved using the Newton-Raphson method, which iteratively updates the solution based on the derivative until the solution converges (i.e. stops changing significantly).
    /// </summary>
    public interface INonlinearComponent
    {
        /// <summary>
        /// Called several times per tick to evalutate the partial derivative given the current solution.
        /// Components should calculate the derivative and "stamp" it into the Y matrix (jacobian) and F vector of the solver.
        /// </summary>
        /// <returns></returns>
        public void UpdateDifferentialState();
    }
}