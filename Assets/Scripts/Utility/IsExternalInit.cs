namespace System.Runtime.CompilerServices
{
    /// <remarks>
    /// Workaround for init setters and records since Unity doesn't support .NET 5 yet
    /// 
    /// https://docs.unity3d.com/2023.1/Documentation/Manual/CSharpCompiler.html
    /// </remarks>
    internal static class IsExternalInit { }
}