// Polyfills for attributes that ship in newer .NET runtimes but are
// recognised by the C# 11+ compiler regardless of where they are defined,
// as long as the type name and namespace match.

namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(
    AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Parameter,
    AllowMultiple = false,
    Inherited = false
)]
internal sealed class UnscopedRefAttribute : Attribute { }
