# Generic

**Sub-Volumes**
- [Source generators](SourceGenerators/README.md) — the Roslyn generators that ship inside the packages.

Collection of Unity code snippets in the form of a package

### Installation
1. Open Package Manager from Window > Package Manager.
2. Click on the "+" button > Add package from git URL.
3. Enter the following URL:
```
https://github.com/Nachos836/Generic.git?path=Assets/Generic
```

(OPTIONAL) If you want to install editor extensions add the following URL:
```
https://github.com/Nachos836/Generic.git?path=Assets/EditorUtils
```

(OPTIONAL) If you want to install debug flow snippets add the following URL:
```
https://github.com/Nachos836/Generic.git?path=Assets/Debug
```

(OPTIONAL) If you want to install Serializable Value Objects add the following URL:
```
https://github.com/Nachos836/Generic.git?path=Assets/SerializableValueObjects
```

(OPTIONAL) If you want to install Inspector Attributes add the following URL:
```
https://github.com/Nachos836/Generic.git?path=Assets/InspectorAttributes
```

(OPTIONAL) If you want to install Async Pool add the following URL:
```
https://github.com/Nachos836/Generic.git?path=Assets/Pooling
```

(OPTIONAL) If you want to install .NetCompatibility add the following URL:
```
https://github.com/Nachos836/Generic.git?path=Assets/dotNetCompat
```

Serializable Value Objects, Inspector Attributes, Async Pool and Root/Initializer depend on .NetCompatibility: add its URL together with theirs.

(OPTIONAL) If you want to install Root/Initializer add the following URL:
```
https://github.com/Nachos836/Generic.git?path=Assets/Initializer
```

(OPTIONAL) If you want to install NativeRefCollections add the following URL:
```
https://github.com/Nachos836/Generic.git?path=Assets/NativeRefCollections
```

### Development
- Unity 6000.6.10f1 with an active license; no extra modules
- Unity CLI (`unity`) to compile and run the Unity tests from a terminal
- .NET SDK 10 to build and run the generator tests
- JetBrains Rider to run the generator's `Publish Generator.InspectorAttributesEditor to UnityPackage` configuration
- Git
- Network access to the package sources: `packages.unity.com`, `package.openupm.com`, `unitynuget-registry.openupm.com`, `github.com`, `gitlab.com`, `api.nuget.org`
