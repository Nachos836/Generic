# Glossary

- Read a term defined below with that meaning wherever a rule uses it.
  - Package — an `Assets` folder holding a `package.json`; users install it by its git URL.
  - Generator — the Roslyn source generator that writes inspector drawers for `[Button]` methods.
  - Generator DLL — the generator's build, committed inside the InspectorAttributes package.
  - Samples — `Assets/Samples`: scenes and scripts that exercise the packages, in no package.
  - Compound rule — a rule whose nested bullets list its items: definitions, theses or cases.

---

# Conventions

## Packages

> **Purpose:** to keep every package installable and versioned on its own.

- Bump the `version` in `package.json` of each package a change touches, in the same commit.
- Add each new package's git URL `?path=Assets/<Folder>` to the root README install list.
- Reference another package of this repository by its git URL, never by a relative path.
- Treat UPM's `Skipping invalid dependency` on `com.nachos836.dot-net-compat` as intended.
- Keep usage examples and test scenes in Samples, outside every package.

## Unity code

> **Purpose:** to keep code working under the project's editor settings.

- Give each static field a reset in a method marked `[ClearCacheBetweenCodeLoads]`.
- Set `name` of an asmdef using Editor internals to `Unity.InternalAPIEngineBridge.<NNN>`.
- Reach Unity.Collections internals through an asmref into the Unity.Collections assembly.

## Generator

> **Purpose:** to keep the generator DLL in step with its attributes and the editor's compiler.

- Change the generator in `SourceGenerators/`, then publish the generator DLL; never edit it.
- Build the generator only with this Rider run configuration of its solution:

  ```
  Publish Generator.InspectorAttributesEditor to UnityPackage
  ```

- Bump the InspectorAttributes package version before you publish the generator DLL.
- Change an attribute in the generator's `InspectorAttributes` folder with its runtime twin.
- Add each Unity type the generator names to `Shared.UnityBridge` as an empty stub.
- Pin `Microsoft.CodeAnalysis.*` to the Roslyn version of the project's editor: 4.10.0 now.

## Checks

> **Purpose:** to check a change with the tools the project builds with.

- Compile Unity code with `unity recompile .` while an editor holds the project.
- Run the Unity tests with `unity test . --mode EditMode`.
- Run one test class with `unity test . --mode EditMode --filter <Namespace>.<Class>`.
- Check each generator change with `dotnet test` on the generator's `.Tests.csproj`.

## Commits

> **Purpose:** to keep the history a release log of the packages.

- Write a package commit's subject as `[UPDATE] [<Package>] <old> -> <new>` of its version.
- Write an editor upgrade commit's subject as `[UPDATE] Editor: <old> -> <new>`.
- Write the subject of a project change outside packages as `[UPDATE] [UnityProject] <What>`.
- Write the body as one line per change: `[<TAG>] <Description>`.
- Pick each body line's tag by its change:
  - `[ADD]` — a new feature, API, file or dependency;
  - `[UPDATE]` — an improved or extended feature, or a bumped version;
  - `[CHANGE]` — one approach replaced by another, written `Old -> New`;
  - `[REMOVE]` — a deleted feature, file or dependency;
  - `[HOTFIX]` — a fixed defect;
  - `[CLEANUP]` — tidied code with its behaviour kept;
  - `[WIP]` — unfinished work.
- Start each description with a capital letter and end it without a period.

## Scopes

> **Purpose:** to apply each scope's own rules before touching that scope.

- Read a child scope's agents file before any task that reads, reviews or changes its files:
  - [Source generators agent rules](SourceGenerators/AGENTS.md) — the `SourceGenerators` scope.
