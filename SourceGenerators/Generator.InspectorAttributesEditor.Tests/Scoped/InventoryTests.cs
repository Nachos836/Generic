using NUnit.Framework;
using Shared.CodeGeneration.Scoped;
using static Shared.CodeGeneration.Scoped.BinaryOperator;
using static Shared.CodeGeneration.Scoped.Blocks;

namespace Generator.InspectorAttributesEditor.Tests.Scoped;

public sealed class InventoryTests
{
    private const string Namespace = "Generic.Samples.GameplayInventory";
    private const string NamespaceWarning = "ReSharper disable once CheckNamespace";
    private const string UnsupportedIncome = "Income is not supported!";
    private const string UnknownItem = "Unknown item type";

    private static readonly IReadOnlyList<(string Type, string Field, int Id)> Items =
    [
        ("Prize", "prize", 1),
        ("Money", "money", 2),
        ("Gem", "gem", 3)
    ];

    private static readonly Expr CollectionsChecks = Id("ENABLE_UNITY_COLLECTIONS_CHECKS");

    private static IReadOnlyList<Constraint> ItemConstraints => [Constraint.Unmanaged, T("IItem")];

    [Test]
    public void PickableItemIsExpressed()
    {
        var generated = Code.File(static file => file
            .Nullable()
            .Gap().Using("System").Using("System.Runtime.CompilerServices")
            .Gap().Comment(NamespaceWarning)
            .Namespace(Namespace, static scope => scope
                .Attribute("CompilerGenerated").Internal().Interface("IPickableItem", static type => type
                    .Method(T("void"), "ProvideTo", static method => method.Param(T("Inventory"), "_")))
                .Attribute("CompilerGenerated").Internal().Interface("IPickableItem", static type => type
                    .TypeParam("TItem", Variance.Out).Implements(T("IPickableItem")).Where("TItem", ItemConstraints)
                    .Method(T("void"), "Accept", static method => method
                        .Param(T("Inventory"), "inventory")
                        .Param(T("Action", [T("Inventory"), T("TItem")]), "callback"))
                    .Gap().ExplicitFor(T("IPickableItem")).Method(T("void"), "ProvideTo", static method => method
                        .Param(T("Inventory"), "inventory")
                        .Body(static body => body.Do(Id("Accept").Call([
                            Id("inventory"),
                            StaticLambda(["inventory", "income"]).Body(static lambda => lambda
                                .Do(Id("inventory").Dot("AddOrSet").Call([InArg(Id("income"))])))
                        ])))))));

        InventorySample.AssertExpresses(InventorySample.Read("PickableItem.cs"), generated);
    }

    [Test]
    public void InventoryIsExpressed()
    {
        var generated = Code.File(static file => file
            .Nullable()
            .Gap().Using("System").Using("System.Collections.Generic").Using("System.Diagnostics.Contracts")
            .Using("System.Runtime.CompilerServices").Using("NativeRefCollections.Runtime").Using("Unity.Burst")
            .Using("Unity.Collections")
            .Gap().Comment(NamespaceWarning)
            .Namespace(Namespace, static scope => scope
                .Attribute("BurstCompile").SplitAttributes().Attribute("CompilerGenerated")
                .Public().Sealed().Partial().Class("Inventory", WriteInventoryMembers)));

        InventorySample.AssertExpresses(InventorySample.Read("Inventory.cs"), generated);
    }

    [Test]
    public void ItemIsExpressed()
    {
        var generated = Code.File(static file => file
            .Nullable()
            .Gap().Using("System").Using("System.Runtime.CompilerServices").Using("System.Runtime.InteropServices")
            .Using("Generic.Samples.GameplayInventory.Concrete").Using("Unity.Burst")
            .Using("Unity.Collections.LowLevel.Unsafe").UsingStatic("System.Runtime.CompilerServices.MethodImplOptions")
            .Gap().Comment(NamespaceWarning)
            .Namespace(Namespace, static scope => scope
                .DocComment(Lines("<summary>",
                    "Visitor for exhaustive matching over the <see cref=\"Item\"/> tagged union.",
                    "Adding a new <see cref=\"IItem\"/> type forces every implementation to handle it.",
                    "</summary>"))
                .Attribute("CompilerGenerated").Public().Interface("IItemVisitor", static type => type
                    .TypeParam("TResult", Variance.Out)
                    .ForEach(Items, static (members, item) => members
                        .Method(T("TResult"), "Visit", method => method.In().Param(T(item.Type), item.Field))))
                .Gap().DocComment(Lines("<summary>", "Void-returning visitor (side effects only).", "</summary>"))
                .Attribute("CompilerGenerated").Public().Interface("IItemVisitor", static type => type
                    .ForEach(Items, static (members, item) => members
                        .Method(T("void"), "Visit", method => method.In().Param(T(item.Type), item.Field))))
                .Gap().Attribute("BurstCompile").SplitAttributes()
                .Attribute("StructLayout", [Id("LayoutKind").Dot("Explicit")]).SplitAttributes()
                .Attribute("CompilerGenerated")
                .Public().Readonly().Partial().Struct("Item", WriteItemMembers)
                .Gap().Attribute("CompilerGenerated").Internal().Static().Class("ItemExtensions", static type => type
                    .Attribute("MethodImpl", [Id("AggressiveInlining")])
                    .Public().Static().Method(T("Item"), "ToUnion", static method => method
                        .TypeParam("TItem").This().Param(T("TItem"), "income").Where("TItem", ItemConstraints)
                        .Body(static body => WriteItemBranches(body, static (branch, item) => branch
                            .Return(New(T("Item"), [
                                Id("UnsafeUtility").Dot("As").TypeArgs([T("TItem"), T(item.Type)])
                                    .Call([RefArg(Id("income"))])
                            ]))))))));

        InventorySample.AssertExpresses(InventorySample.Read("Item.cs"), generated);
    }

    private static void WriteInventoryMembers(TypeScope type)
    {
        var map = T("NativeRefHashMap", [T("Item").Nested("TypeId"), T("Item")]);

        type.Implements(T("IDisposable"))
            .Private().Field(map, "_map")
            .Gap().DocComment(Lines("<summary>",
                "Event that indicates that some Item is changed, or Items amount changed", "</summary>"))
            .Public().Event(T("Action").Nullable(), "ItemsChanged")
            .Gap().Public().Constructor(static constructor => constructor.Body(static body => body
                .Do(Id("_map").Assign(New([
                    Named("initialCapacity", Id("Item").Dot("TypeIdCount")),
                    Id("Allocator").Dot("Domain")
                ])))))
            .Gap().Public().Constructor(static constructor => constructor
                .Param(T("IEnumerable", [T("Item")]), "items").CallsThis()
                .Body(static body => body.Foreach("item", Id("items"), static loop => loop
                    .Var("type", Id("Item").Dot("GetType").Call([InArg(Id("item"))]))
                    .Do(Id("_map").Dot("Add").Call([Id("type"), Id("item")])))))
            .Gap().Attribute("MethodImpl", [Id("MethodImplOptions").Dot("AggressiveInlining")])
            .Public().Method(map.Nested("ReadOnly"), "ProvideItems", static method => method
                .Body(static body => body.Return(Id("_map").Dot("AsReadOnly").Call())))
            .Gap().Public().Method(T("void"), "Add", static method => DeclareItemMethod(method, withItem: true)
                .Body(static body => body
                    .Var("id", CallGetType()).Var("union", CallToUnion())
                    .Do(Id("_map").Dot("Add").Call([Id("id"), Id("union")]))
                    .Gap().Do(InvokeItemsChanged())))
            .Gap().Public().Method(T("void"), "Remove", static method => DeclareItemMethod(method, withItem: false)
                .Body(static body => body
                    .Var("type", CallGetType())
                    .If(CallRemoveIsFalse(), static then => then.Throw(New(T("KeyNotFoundException"),
                        [Id("type").Dot("ToString").Call()])), braced: false)
                    .Gap().Do(InvokeItemsChanged())))
            .Gap().Attribute("Pure").Public().Ref().Readonly().Method(T("TItem"), "Read", static method =>
                DeclareItemMethod(method, withItem: false).Body(static body => body
                    .Var("type", CallGetType())
                    .Ref().Readonly().Var("candidate", Ref(Id("_map").Index([Id("type")])))
                    .Gap().Return(Ref(Id("candidate").Dot("Cast").TypeArgs([T("TItem")]).Call()))))
            .Gap().Public().Method(T("void"), "Write", static method => DeclareItemMethod(method, withItem: true)
                .Body(static body => body
                    .Var("type", CallGetType()).Var("union", CallToUnion())
                    .Do(Id("_map").Index([Id("type")]).Assign(Id("union")))
                    .Gap().Do(InvokeItemsChanged())))
            .Gap().Public().Method(T("bool"), "TryAdd", static method => DeclareItemMethod(method, withItem: true)
                .Body(static body => body
                    .Var("type", CallGetType()).Var("union", CallToUnion())
                    .If(Id("_map").Dot("TryAdd").Call([Id("type"), Id("union")]).Is(Lit(false)),
                        static then => then.Return(Lit(false)), braced: false)
                    .Gap().Do(InvokeItemsChanged()).Return(Lit(true))))
            .Gap().Public().Method(T("bool"), "TryRemove", static method => DeclareItemMethod(method, withItem: false)
                .Body(static body => body
                    .Var("type", CallGetType())
                    .If(CallRemoveIsFalse(), static then => then.Return(Lit(false)), braced: false)
                    .Gap().Do(InvokeItemsChanged()).Return(Lit(true))))
            .Gap().Attribute("Pure").Public().Ref().Readonly().Method(T("TItem"), "TryRead", static method =>
                DeclareItemMethod(method, withItem: false).Body(static body => body
                    .Var("type", CallGetType())
                    .Ref().Var("item", Ref(Id("_map").Index([Id("type")])))
                    .If(Id("Unsafe").Dot("IsNullRef").Call([RefArg(Id("item"))]),
                        static then => then.Return(Ref(Id("Unsafe").Dot("NullRef").TypeArgs([T("TItem")]).Call())),
                        braced: false)
                    .Gap().Return(Ref(Id("item").Dot("Cast").TypeArgs([T("TItem")]).Call()))))
            .Gap().Public().Method(T("bool"), "TryWrite", static method => DeclareItemMethod(method, withItem: true)
                .Body(static body => body
                    .Var("type", CallGetType())
                    .If(Id("_map").Dot("ContainsKey").Call([Id("type")]).Is(Lit(false)),
                        static then => then.Return(Lit(false)), braced: false)
                    .Gap().Var("union", CallToUnion())
                    .Do(Id("_map").Index([Id("type")]).Assign(Id("union")))
                    .Gap().Do(InvokeItemsChanged()).Return(Lit(true))))
            .Gap().Public().Method(T("void"), "AddOrSet", static method => DeclareItemMethod(method, withItem: true)
                .Body(static body => body
                    .Var("type", CallGetType()).Var("union", CallToUnion())
                    .Gap().Do(Id("_map").Dot("SetValue").Call([InArg(Id("type")), InArg(Id("union"))]))))
            .Gap().Public().Method(T("void"), "Dispose", static method => method.Body(static body => body
                .Do(Id("_map").Dot("Dispose").Call())
                .Do(Id("_map").Assign(Default().NullForgiving()))));
    }

    private static MethodScope DeclareItemMethod(MethodScope method, bool withItem)
    {
        return method.TypeParam("TItem").In(withItem).Param(T("TItem"), "item", when: withItem)
            .Where("TItem", ItemConstraints);
    }

    private static Expr CallGetType() => Id("Item").Dot("GetType").TypeArgs([T("TItem")]).Call();
    private static Expr CallToUnion() => Id("item").Dot("ToUnion").Call();
    private static Expr InvokeItemsChanged() => Id("ItemsChanged").NullDot("Invoke").Call();
    private static Expr CallRemoveIsFalse() => Id("_map").Dot("Remove").Call([Id("type")]).Is(Lit(false));

    private static void WriteItemMembers(TypeScope type)
    {
        var privateTypeId = T("PrivateTypeId");

        type.Implements(T("IItem"))
            .Internal().Static().Readonly().Field(T("int"), "TypeIdCount", Id("Enum").Dot("GetValues")
                .Call([TypeOf(privateTypeId)]).Dot("Length"))
            .Gap().Attribute("FieldOffset", [Lit(0)]).Private().Readonly().Field(privateTypeId, "CurrentPrivateTypeId")
            .Gap().ForEach(Items, static (members, item) => members
                .Attribute("FieldOffset", [SizeOf(T("PrivateTypeId"))])
                .Private().Readonly().Field(T(item.Type), "_" + item.Field))
            .ForEach(Items, static (members, item) => members
                .Gap().Internal().Constructor(constructor => constructor
                    .Param(T(item.Type), item.Field)
                    .Body(body => body
                        .Do(Id("CurrentPrivateTypeId").Assign(Id("PrivateTypeId").Dot(item.Type)))
                        .ForEach(Items, (statements, other) => statements
                            .Do(Id("_" + other.Field).Assign(Default()), other.Field != item.Field))
                        .Do(Id("_" + item.Field).Assign(Id(item.Field))))))
            .Gap().Attribute("MethodImpl", [Id("AggressiveInlining")])
            .Internal().Static().Method(T("TypeId"), "GetType", static method => method
                .TypeParam("TItem").Where("TItem", ItemConstraints)
                .Body(static body => WriteItemBranches(body, static (branch, item) => branch
                    .Return(New(T("TypeId"), [Cast(T("ulong"), Id("PrivateTypeId").Dot(item.Type))])))))
            .Gap().Attribute("MethodImpl", [Id("AggressiveInlining")])
            .Internal().Static().Method(T("TypeId"), "GetType", static method => method
                .TypeParam("TItem").In().Param(T("TItem"), "income").Where("TItem", ItemConstraints)
                .Body(static body => body.Return(New(T("TypeId"), [
                    Cast(T("ulong"),
                        Id("income").Dot("ToUnion").Call().Dot("CurrentPrivateTypeId"))
                ]))))
            .Gap().Attribute("MethodImpl", [Id("AggressiveInlining")])
            .Internal().Unsafe().Ref().Readonly().Method(T("TItem"), "Cast", static method => method
                .TypeParam("TItem").Where("TItem", ItemConstraints)
                .Body(static body => WriteItemBranches(body, static (branch, item) => branch
                    .Fixed(T("void").Pointer(), "raw", AddressOf(Id("_" + item.Field)), static block => block
                        .Return(Ref(Id("UnsafeUtility").Dot("ArrayElementAsRef").TypeArgs([T("TItem")])
                            .Call([Id("raw"), Named("index", Lit(0))])))))))
            .Gap().Attribute("MethodImpl", [Id("AggressiveInlining")])
            .Public().Method(T("TResult"), "Accept", static method => method
                .TypeParam("TResult").Param(T("IItemVisitor", [T("TResult")]), "visitor")
                .Body(static body => body.Return(Id("CurrentPrivateTypeId").Switch(static arms => arms
                    .ForEach(Items,
                        static (scope, item) => scope.Arm(Id("PrivateTypeId").Dot(item.Type), CallVisit(item)))
                    .Gap().IfDirective(CollectionsChecks)
                    .Arm(Discard(), ThrowExpression(CreateOutOfRange()))
                    .ElseDirective()
                    .Arm(Discard(), Default().NullForgiving())
                    .EndIfDirective()))))
            .Gap().Attribute("MethodImpl", [Id("AggressiveInlining")])
            .Public().Method(T("void"), "Accept", static method => method
                .Param(T("IItemVisitor"), "visitor")
                .Body(static body => body.Switch(Id("CurrentPrivateTypeId"), static sections => sections
                    .ForEach(Items, static (scope, item) => scope.Case(Id("PrivateTypeId").Dot(item.Type),
                        section => section.Do(CallVisit(item)).Break()))
                    .Gap().IfDirective(CollectionsChecks)
                    .Default(static section => section.Throw(CreateOutOfRange()))
                    .EndIfDirective())))
            .Gap().Private().Enum("PrivateTypeId", static members => members
                .Inherits(T("ulong"))
                .ForEach(Items, static (scope, item) => scope.EnumMember(item.Type, Lit(item.Id))))
            .Gap().Attribute("BurstCompile").Public().Readonly().Struct("TypeId", WriteTypeIdMembers);
    }

    private static Expr CallVisit((string Type, string Field, int Id) item)
    {
        return Id("visitor").Dot("Visit").Call([InArg(Id("_" + item.Field))]);
    }

    private static Expr CreateOutOfRange()
    {
        return New(T("ArgumentOutOfRangeException"),
            [NameOf(Id("CurrentPrivateTypeId")), Id("CurrentPrivateTypeId"), Lit(UnknownItem)]);
    }

    private static void WriteItemBranches(BodyScope body,
        Action<BodyScope, (string Type, string Field, int Id)> branch)
    {
        IfScope chain = default!;
        foreach (var item in Items)
        {
            var condition = TypeOf(T("TItem")).Binary(Equal, TypeOf(T(item.Type)));

            chain = chain == default
                ? body.If(condition, then => branch(then, item))
                : chain.ElseIf(condition, then => branch(then, item));
        }

        chain.Else(static other => other
            .IfDirective(CollectionsChecks.Binary(And, Id("DEBUG")))
            .Throw(New(T("ArgumentOutOfRangeException"), [TypeOf(T("TItem")).Dot("Name"), Lit(UnsupportedIncome)]))
            .ElseDirective()
            .Return(Default())
            .EndIfDirective());
    }

    private static void WriteTypeIdMembers(TypeScope type)
    {
        var privateTypeId = Id("_privateTypeId");

        type.Implements(T("IEquatable", [T("TypeId")]))
            .Private().Readonly().Field(T("ulong"), "_privateTypeId")
            .Gap().Internal().Constructor(constructor => constructor
                .Param(T("ulong"), "privateTypeId").Arrow(privateTypeId.Assign(Id("privateTypeId"))))
            .Gap().Public().Method(T("bool"), "Equals", method => method
                .Param(T("TypeId"), "other").Arrow(privateTypeId.Binary(Equal, Id("other").Dot("_privateTypeId"))))
            .Public().Override().Method(T("bool"), "Equals", static method => method
                .Param(T("object").Nullable(), "obj")
                .Arrow(ThrowExpression(New(T("Exception"), [Lit("Should not be used")]))))
            .Public().Override().Method(T("int"), "GetHashCode", method => method
                .Arrow(Cast(T("int"), privateTypeId)))
            .Public().Override().Method(T("string"), "ToString", method => method
                .Arrow(privateTypeId.Dot("ToString").Call()));
    }
}