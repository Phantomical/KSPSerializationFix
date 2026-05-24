using System;
using System.Diagnostics.CodeAnalysis;

namespace KSPSerializationFix.Platform;

/// <summary>
/// Interface for a Unity <c>dynamic_array&lt;T&gt;</c>.
/// </summary>
internal interface IDynamicArray<T>
    where T : unmanaged
{
    ref IntPtr Ptr { [UnscopedRef] get; }
    ref ulong Size { [UnscopedRef] get; }
    ref ulong Capacity { [UnscopedRef] get; }
}

/// <summary>
/// Field accessors for Unity's MonoManager.
/// </summary>
internal interface IMonoManager<TString, TStringArray, TIntArray, TPtrArray>
    where TString : unmanaged
    where TStringArray : unmanaged, IDynamicArray<TString>
    where TIntArray : unmanaged, IDynamicArray<int>
    where TPtrArray : unmanaged, IDynamicArray<IntPtr>
{
    bool AreAssembliesLoaded { get; }

    ref TStringArray AssemblyNames { [UnscopedRef] get; }
    ref TIntArray AssemblyTypes { [UnscopedRef] get; }
    ref TPtrArray ScriptImages { [UnscopedRef] get; }
    ref TIntArray AssemblyMonoPathsIndex { [UnscopedRef] get; }
}

/// <summary>
/// Stuff that needs to be implemented individually for each platform.
/// </summary>
internal interface IPlatform<TString>
    where TString : unmanaged
{
    TString CreateString(IntPtr data, int size);
    IntPtr GetMonoManagerPointer();
}
