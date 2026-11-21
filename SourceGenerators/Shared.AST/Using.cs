using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Shared.AST;

[SuppressMessage("ReSharper", "MemberHidesStaticFromOuterClass")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
internal static class Using
{
    public static class System
    {
        private const string Value = $"{nameof(System)}";
        public static UsingDirectiveSyntax Name { get; } = UsingDirective(ParseName(text: Value));

        public static class CodeDom
        {
            private const string Value = $"{System.Value}.{nameof(CodeDom)}";
            public static UsingDirectiveSyntax Name { get; } = UsingDirective(ParseName(text: Value));

            public static class Compiler
            {
                private const string Value = $"{CodeDom.Value}.{nameof(Compiler)}";
                public static UsingDirectiveSyntax Name { get; } = UsingDirective(ParseName(text: Value));
            }
        }

        public static class Diagnostics
        {
            private const string Value = $"{System.Value}.{nameof(Diagnostics)}";
            public static UsingDirectiveSyntax Name { get; } = UsingDirective(ParseName(text: Value));

            public static class CodeAnalysis
            {
                private const string Value = $"{Diagnostics.Value}.{nameof(CodeAnalysis)}";
                public static UsingDirectiveSyntax Name { get; } = UsingDirective(ParseName(text: Value));
            }
        }
    }

    public static class Unity
    {
        public static class Engine
        {
            private const string Value = $"{nameof(Unity)}{nameof(Engine)}";
            public static UsingDirectiveSyntax Name { get; } = UsingDirective(ParseName(text: Value));

            public static class UIElements
            {
                private const string Value = $"{Engine.Value}.{nameof(UIElements)}";
                public static UsingDirectiveSyntax Name { get; } = UsingDirective(ParseName(text: Value));
            }
        }

        public static class Editor
        {
            private const string Value = $"{nameof(Unity)}{nameof(Editor)}";
            public static UsingDirectiveSyntax Name { get; } = UsingDirective(ParseName(text: Value));
        }
    }
}
