# Source generators

[<kbd>◀ Back</kbd>](../README.md)

**Documentation**
- [Language of blocks](Documentation/language-of-blocks.md) — the API through which a generator describes the C# code it writes.

The Roslyn source generators that ship inside this repository's packages, with the shared projects they build from and their tests. The generators' solution opens all of them together.

- `Generator.InspectorAttributesEditor` — writes inspector drawers for `[Button]` methods of the InspectorAttributes package.
- `Generator.InspectorAttributesEditor.Tests` — runs the generator against source code the way the editor's compiler does.
- Shared projects — code that every generator imports as source: the language of blocks, the Unity type stubs, the compiler polyfills and the common generator parts.
