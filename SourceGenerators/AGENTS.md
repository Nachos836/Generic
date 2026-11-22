# Glossary

- Read a term defined below with that meaning wherever a rule uses it.
  - Owner — a `MonoBehaviour` class whose `[Button]` methods the generator writes a drawer for.
  - Drawer — the nested `Drawer : UnityEditor.Editor` class the generator writes into an owner.
  - Shared project — a `.shproj` whose `.projitems` a generator's `.csproj` imports as source.
  - Pipeline model — a value a generator's incremental pipeline passes between its stages.

---

# Conventions

## Solution

> **Purpose:** to find each part of a generator by the question it answers.

- Read `ButtonDrawerGenerator` to see the pipeline: analysis, grouping by owner, output.
- Read `ButtonAnalysis.Analyze` to see which `[Button]` methods and owners pass.
- Read `CodeGenerator.GenerateDrawer` to see the two files the generator writes per owner.
- Add a diagnostic only one generator reports to its file `Components/Diagnostics.cs`.
- Add a diagnostic several generators report to `Shared.Generator`.
- Add a compiler polyfill that `netstandard2.0` lacks to `Shared.DotNetCompatibility`.
- Import each shared project into a generator's `.csproj` through its `.projitems`.

## Generated code

> **Purpose:** to keep generated code compiling in the editor and absent from player builds.

- Describe every part of a generator's output with the language of blocks, never as raw text.
- Call `SyntaxFactory` only inside `Shared.CodeGeneration.*`, never in a generator's own code.
- Wrap each generated editor file in `#if UNITY_EDITOR` with `IfDirective` and `EndIfDirective`.
- Give each pipeline model value equality over all of its fields.

## Version

> **Purpose:** to stamp each generator build with the version of the package that ships it.

- Write no version number by hand in a generator's `.csproj`; its build reads `package.json`.

## Tests

> **Purpose:** to run a generator against real source the way the editor's compiler does.

- Run one generator test with `dotnet test --filter "FullyQualifiedName~<TestName>"`.
- Name each test compilation `TestAssembly` to let its source see the internal stubs.
