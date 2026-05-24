using System;
using UnityEngine;

namespace KSPSerializationFix.Test;

// A [Serializable] struct defined in this DLL.
//
// Without SerializationFix, Unity has no idea this assembly exists, so when
// Object.Instantiate deep-clones a host that holds a TestPayload the cloned
// payload comes back zero-initialized -- Unity's serializer skipped the
// fields entirely.
//
// With SerializationFix the assembly is registered, Unity walks the struct's
// fields normally during the instantiate-driven serialize/deserialize, and
// every field survives the round-trip.
[Serializable]
public struct TestPayload
{
    public int answer;
    public string text;
    public float ratio;
    public bool flag;
}

// Object.Instantiate needs a UnityEngine.Object root, so the struct is held
// on a ScriptableObject also defined in this DLL.
public class TestPayloadHost : ScriptableObject
{
    public TestPayload payload;
    public int sentinel;
}
