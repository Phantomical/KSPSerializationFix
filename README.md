# SerializationFix

This is a mod that makes it so that Unity's serialization system can handle
`[Serializable]` types declared in mod assemblies. It does this by patching
some internal unity data structures to include said assemblies.

## Using it in your mod
In order to preserve backwards compatibility SerializationFix only adds mods
that have a `KSPAssemblyDependency` on it. This also makes sure that those
mods are loaded after it.

In order to declare this, add this attribute to your mod
```cs
[assembly: KSPAssemblyDependency("SerializationFix", 0, 1, 0)]
```

## Install
[GitHub][releases]

[releases]: https://github.com/Phantomical/KSPSerializationFix/releases/latest

Download the mod zip from the link above and drag the `GameData` folder into
your KSP installation directory.

## License
SerializationFix is available under the MIT license.
