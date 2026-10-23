#nullable enable

namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// backport of .NET 5 `MemberNotNullAttribute` (does not need an actual implementation, it's just a marker for code analysis)
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
    [ExcludeFromCodeCoverage, DebuggerNonUserCode]
    public class MemberNotNullAttribute : Attribute
    {
        public string[] Members { get; }

        public MemberNotNullAttribute(string member)
        {
            Members = new[] { member };
        }

        public MemberNotNullAttribute(params string[] members)
        {
            Members = members;
        }
    }
}