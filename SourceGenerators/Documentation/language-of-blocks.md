# Language of blocks

[<kbd>◀ Back</kbd>](../README.md)

The language of blocks is the API through which a source generator describes the C# code it writes. It stands between the generator's domain logic and Roslyn: the generator decides what to write, and the language turns that description into Roslyn syntax trees and source text.

## Responsibilities

The language covers the output code and nothing else. Each generator keeps its own domain logic: what it looks for in the user's code, the models of its incremental pipeline and its domain checks. The language takes values from those models and writes them into code.

## Blocks

A block is one step of the description: a namespace, a class, a modifier, a method, a statement, an expression, a comment. The author describes all code with blocks, down to single statements and expressions, and passes no C# text to the language: neither templates nor fragments to parse.

Blocks follow the order in which the same C# is written, so a description reads like the code it produces:

```csharp
file.Namespace("Generic.Samples", ns => ns
    .Internal().Sealed().Partial().Class("Drawer", drawer => drawer
        .Inherits(T("Editor"))));
```

Expressions and types are values built the same way, left to right: `Id("root").Dot("Add").Call([Id("defaultRoot")])` writes `root.Add(defaultRoot)`.

Comments, blank lines and preprocessor directives are blocks too. The author places them where they appear in the output, and the language keeps them through formatting.

## Styles

The language comes in two styles. They offer the same blocks and differ only in how a description nests:

- The scoped style nests each scope in a lambda, as C# nests it in braces.
- The chained style is one flat chain: a block that opens a scope continues the chain inside it, and a closing verb named after the scope returns to the outer one: `EndNamespace`, `EndClass`, `EndMethod`, `EndIf`.

Each style lives in its own namespace, and a generator picks a style by its `using`. Both styles can serve one generator side by side, so a generator may keep its emitting code in both and pick one with a preprocessor symbol in its own source.

Each style builds its Roslyn nodes on its own; the two styles are independent implementations of one language.

## Scopes

Every level of nesting has a scope of its own: the file, a namespace, a type, a method signature, a method body, an `if` branch, a `switch`. A scope offers only the blocks C# allows at that level, so the IDE completes only valid blocks, and a description that places a block in the wrong scope fails to compile.

In the chained style a scope also remembers the scope around it. Each closing verb exists only in the scope it closes, so a closing verb in the wrong place is a compile error.

## Prefixes

Modifiers and attributes are blocks written before the declaration they belong to, in C# order: `.Private().Readonly().Field(...)`. They attach to the nearest declaration that follows them. Consecutive attributes share one bracket list.

## Conditions

Every block takes an optional condition, its last parameter, and writes nothing when the condition is false. The condition is the only parameter every block shares. A modifier that depends on domain data takes the data as its condition: `.Partial(model.RequiresCustomUI)`.

## Repetition

`ForEach` repeats blocks over domain data: for each item of a collection it runs a description with that item. `ForEach` steers generation and knows nothing about the domain types it walks. `Foreach` and `If`, in contrast, are blocks that write C# loops and conditions into the output.

The scoped style also accepts the generator's own `foreach` and `if` inside a lambda, since a lambda body is ordinary C#.

## Extensions

The block chain shows no Roslyn type. When a generator needs a construct the language lacks, it adds a block of its own through the extensions namespace of the style it uses. That namespace is the only place where blocks meet Roslyn: it turns any of Roslyn's four base node kinds — type, expression, statement and member — into a block. An extension block reads in the chain exactly like a built-in one.

The language offers fine-grained blocks only. A generator declares its domain vocabulary as custom blocks, and a custom block may be a meta-block: one call that chains several other blocks.

## Sessions and Submit

A generation session covers one owner: all the files a generator writes for one type of the user's code. The session ends with Submit.

Submit compiles the session's files together with the user's code before they reach Roslyn. A session that passes the check replaces the owner's previous files. A session that fails the check reports the failure and leaves the previous version of its files in place; when no previous version exists, it emits no files.
