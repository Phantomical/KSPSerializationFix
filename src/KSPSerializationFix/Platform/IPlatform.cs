using System;
using System.Diagnostics.CodeAnalysis;

namespace KSPSerializationFix.Platform;

/// <summary>
/// Variant-agnostic operations on a Unity <c>dynamic_array&lt;T&gt;</c>.
/// Implemented by the per-flavor (Release/Debug) DynamicArray struct so
/// generic patcher code can read size/pointer and append without knowing
/// the underlying layout.
/// </summary>
internal interface IDynamicArrayOps
{
    ulong Size { get; }
    IntPtr Ptr { get; }

    void Append<T>(T[] values)
        where T : unmanaged;
}

/// <summary>
/// Field accessors for Unity's MonoManager. Implemented by each variant's
/// MonoManager struct, exposing ref-returning properties so callers can
/// mutate the underlying dynamic_array fields in place without taking
/// pointers or memorizing field offsets.
/// </summary>
internal interface IMonoManager<TArray>
    where TArray : unmanaged, IDynamicArrayOps
{
    bool AreAssembliesLoaded { get; }

    ref TArray AssemblyNames { [UnscopedRef] get; }
    ref TArray AssemblyTypes { [UnscopedRef] get; }
    ref TArray ScriptImages { [UnscopedRef] get; }
    ref TArray AssemblyMonoPathsIndex { [UnscopedRef] get; }
}

/// <summary>
/// Per-variant strategy: factory for the variant's string struct, and
/// resolution of the MonoManager singleton via the variant's RVA of
/// GetMonoManager().
/// </summary>
internal interface IPlatform<TString>
    where TString : unmanaged
{
    TString CreateString(IntPtr data, int size);
    IntPtr GetMonoManagerPointer();
}
