#nullable enable

namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// backport of .NET 5 `MemberNotNullWhenAttribute` (does not need an actual implementation, it's just a marker for code analysis)
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
    [ExcludeFromCodeCoverage, DebuggerNonUserCode]
    public class MemberNotNullWhenAttribute : Attribute
    {
        public bool ReturnValue { get; }

        public string[] Members { get; }

        public MemberNotNullWhenAttribute(bool returnValue, string member)
        {
            ReturnValue = returnValue;
            Members = new[] { member };
        }

        public MemberNotNullWhenAttribute(bool returnValue, params string[] members)
        {
            ReturnValue = returnValue;
            Members = members;
        }
    }
}