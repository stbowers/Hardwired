#nullable enable

namespace Hardwired.Objects.Electrical
{
    public enum JunctionType
    {
        /// <summary>
        /// Represents a cable that is _not_ a junction.
        /// </summary>
        None,

        /// <summary>
        /// Default junction type - all "conductors" from each endpoint will be "tied together" (i.e. positive to positive, negative to negative).
        /// </summary>
        Parallel,

        /// <summary>
        /// Represents a series junction, where each "input" connection's positive conductor will be tied to the negative conductor of the next
        /// input connection.
        /// The "output" connection will connect to the positive conductor of the last "input" connection, and the negative conductor of the first "input" connection.
        /// The OpenEnds must be specified in a specific order, where for N connections the first N-1 connections are "inputs" (in order), and the last connection is the output.
        /// </summary>
        Series,
    }
}
