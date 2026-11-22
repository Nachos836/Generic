using NUnit.Framework;
using Shared.CodeGeneration.Chained;
using static Shared.CodeGeneration.Chained.BinaryOperator;
using static Shared.CodeGeneration.Chained.Blocks;
using ItemSpec = (string Type, string Field, int Id);

namespace Generator.InspectorAttributesEditor.Tests.Chained;

public sealed class InventoryTests
{
    private const string Namespace = "Generic.Samples.GameplayInventory";
    private const string NamespaceWarning = "ReSharper disable once CheckNamespace";
    private const string UnsupportedIncome = "Income is not supported!";
    private const string UnknownItem = "Unknown item type";

    private static readonly IReadOnlyList<ItemSpec> Items =
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
        var generated = Code.File()
            .Nullable()
            .Gap().Using("System").Using("System.Runtime.CompilerServices")
            .Gap().Comment(NamespaceWarning)
            .Namespace(Namespace)
            .Attribute("CompilerGenerated").Internal().Interface("IPickableItem")
            .Method(T("void"), "ProvideTo").Param(T("Inventory"), "_").EndMethod()
            .EndInterface()
            .Attribute("CompilerGenerated").Internal().Interface("IPickableItem")
            .TypeParam("TItem", Variance.Out).Implements(T("IPickableItem")).Where("TItem", ItemConstraints)
            .Method(T("void"), "Accept")
            .Param(T("Inventory"), "inventory").Param(T("Action", [T("Inventory"), T("TItem")]), "callback")
            .EndMethod()
            .Gap().ExplicitFor(T("IPickableItem")).Method(T("void"), "ProvideTo").Param(T("Inventory"), "inventory")
            .Body().Do(Id("Accept").Call([
                Id("inventory"),
                StaticLambda(["inventory", "income"]).Body()
                    .Do(Id("inventory").Dot("AddOrSet").Call([InArg(Id("income"))]))
                    .EndLambda()
            ]))
            .EndMethod()
            .EndInterface()
            .EndNamespace()
            .EndFile();

        InventorySample.AssertExpresses(InventorySample.Read("PickableItem.cs"), generated);
    }

    [Test]
    public void InventoryIsExpressed()
    {
        var inventory = Code.File()
            .Nullable()
            .Gap().Using("System").Using("System.Collections.Generic").Using("System.Diagnostics.Contracts")
            .Using("System.Runtime.CompilerServices").Using("NativeRefCollections.Runtime").Using("Unity.Burst")
            .Using("Unity.Collections")
            .Gap().Comment(NamespaceWarning)
            .Namespace(Namespace)
            .Attribute("BurstCompile").SplitAttributes().Attribute("CompilerGenerated")
            .Public().Sealed().Partial().Class("Inventory");

        var generated = WriteInventoryMembers(inventory).EndClass().EndNamespace().EndFile();

        InventorySample.AssertExpresses(InventorySample.Read("Inventory.cs"), generated);
    }

    [Test]
    public void ItemIsExpressed()
    {
        var item = Code.File()
            .Nullable()
            .Gap().Using("System").Using("System.Runtime.CompilerServices").Using("System.Runtime.InteropServices")
            .Using("Generic.Samples.GameplayInventory.Concrete").Using("Unity.Burst")
            .Using("Unity.Collections.LowLevel.Unsafe").UsingStatic("System.Runtime.CompilerServices.MethodImplOptions")
            .Gap().Comment(NamespaceWarning)
            .Namespace(Namespace)
            .DocComment(Lines("<summary>",
                "Visitor for exhaustive matching over the <see cref=\"Item\"/> tagged union.",
                "Adding a new <see cref=\"IItem\"/> type forces every implementation to handle it.",
                "</summary>"))
            .Attribute("CompilerGenerated").Public().Interface("IItemVisitor")
            .TypeParam("TResult", Variance.Out)
            .ForEach(Items, static (members, item) => members
                .Method(T("TResult"), "Visit").In().Param(T(item.Type), item.Field).EndMethod())
            .EndInterface()
            .Gap().DocComment(Lines("<summary>", "Void-returning visitor (side effects only).", "</summary>"))
            .Attribute("CompilerGenerated").Public().Interface("IItemVisitor")
            .ForEach(Items, static (members, item) => members
                .Method(T("void"), "Visit").In().Param(T(item.Type), item.Field).EndMethod())
            .EndInterface()
            .Gap().Attribute("BurstCompile").SplitAttributes()
            .Attribute("StructLayout", [Id("LayoutKind").Dot("Explicit")]).SplitAttributes()
            .Attribute("CompilerGenerated")
            .Public().Readonly().Partial().Struct("Item");

        var toUnion = WriteItemMembers(item).EndStruct()
            .Gap().Attribute("CompilerGenerated").Internal().Static().Class("ItemExtensions")
            .Attribute("MethodImpl", [Id("AggressiveInlining")])
            .Public().Static().Method(T("Item"), "ToUnion")
            .TypeParam("TItem").This().Param(T("TItem"), "income").Where("TItem", ItemConstraints)
            .Body();

        var generated = WriteItemBranches(toUnion, static (branch, item) => branch
                .Return(New(T("Item"), [
                    Id("UnsafeUtility").Dot("As").TypeArgs([T("TItem"), T(item.Type)]).Call([RefArg(Id("income"))])
                ])))
            .EndMethod()
            .EndClass()
            .EndNamespace()
            .EndFile();

        InventorySample.AssertExpresses(InventorySample.Read("Item.cs"), generated);
    }

    private static ClassChain<TOuter> WriteInventoryMembers<TOuter>(ClassChain<TOuter> type)
        where TOuter : class, IMemberHost
    {
        var map = T("NativeRefHashMap", [T("Item").Nested("TypeId"), T("Item")]);
        return type.Implements(T("IDisposable"))
            .Private().Field(map, "_map")
            .Gap().DocComment(Lines("<summary>",
                "Event that indicates that some Item is changed, or Items amount changed", "</summary>"))
            .Public().Event(T("Action").Nullable(), "ItemsChanged")
            .Gap().Public().Constructor().Body()
            .Do(Id("_map").Assign(New([
                Named("initialCapacity", Id("Item").Dot("TypeIdCount")),
                Id("Allocator").Dot("Domain")
            ])))
            .EndConstructor()
            .Gap().Public().Constructor().Param(T("IEnumerable", [T("Item")]), "items").CallsThis().Body()
            .Foreach("item", Id("items"))
            .Var("type", Id("Item").Dot("GetType").Call([InArg(Id("item"))]))
            .Do(Id("_map").Dot("Add").Call([Id("type"), Id("item")]))
            .EndForeach()
            .EndConstructor()
            .Gap().Attribute("MethodImpl", [Id("MethodImplOptions").Dot("AggressiveInlining")])
            .Public().Method(map.Nested("ReadOnly"), "ProvideItems")
            .Body().Return(Id("_map").Dot("AsReadOnly").Call())
            .EndMethod()
            .Gap().Public().Method(T("void"), "Add")
            .TypeParam("TItem").In().Param(T("TItem"), "item").Where("TItem", ItemConstraints)
            .Body().Var("id", CallGetType()).Var("union", CallToUnion())
            .Do(Id("_map").Dot("Add").Call([Id("id"), Id("union")]))
            .Gap().Do(InvokeItemsChanged())
            .EndMethod()
            .Gap().Public().Method(T("void"), "Remove")
            .TypeParam("TItem").Where("TItem", ItemConstraints)
            .Body().Var("type", CallGetType())
            .If(CallRemoveIsFalse(), braced: false)
            .Throw(New(T("KeyNotFoundException"), [Id("type").Dot("ToString").Call()]))
            .EndIf()
            .Gap().Do(InvokeItemsChanged())
            .EndMethod()
            .Gap().Attribute("Pure").Public().Ref().Readonly().Method(T("TItem"), "Read")
            .TypeParam("TItem").Where("TItem", ItemConstraints)
            .Body().Var("type", CallGetType())
            .Ref().Readonly().Var("candidate", Ref(Id("_map").Index([Id("type")])))
            .Gap().Return(Ref(Id("candidate").Dot("Cast").TypeArgs([T("TItem")]).Call()))
            .EndMethod()
            .Gap().Public().Method(T("void"), "Write")
            .TypeParam("TItem").In().Param(T("TItem"), "item").Where("TItem", ItemConstraints)
            .Body().Var("type", CallGetType()).Var("union", CallToUnion())
            .Do(Id("_map").Index([Id("type")]).Assign(Id("union")))
            .Gap().Do(InvokeItemsChanged())
            .EndMethod()
            .Gap().Public().Method(T("bool"), "TryAdd")
            .TypeParam("TItem").In().Param(T("TItem"), "item").Where("TItem", ItemConstraints)
            .Body().Var("type", CallGetType()).Var("union", CallToUnion())
            .If(Id("_map").Dot("TryAdd").Call([Id("type"), Id("union")]).Is(Lit(false)), braced: false)
            .Return(Lit(false))
            .EndIf()
            .Gap().Do(InvokeItemsChanged()).Return(Lit(true))
            .EndMethod()
            .Gap().Public().Method(T("bool"), "TryRemove")
            .TypeParam("TItem").Where("TItem", ItemConstraints)
            .Body().Var("type", CallGetType())
            .If(CallRemoveIsFalse(), braced: false).Return(Lit(false)).EndIf()
            .Gap().Do(InvokeItemsChanged()).Return(Lit(true))
            .EndMethod()
            .Gap().Attribute("Pure").Public().Ref().Readonly().Method(T("TItem"), "TryRead")
            .TypeParam("TItem").Where("TItem", ItemConstraints)
            .Body().Var("type", CallGetType())
            .Ref().Var("item", Ref(Id("_map").Index([Id("type")])))
            .If(Id("Unsafe").Dot("IsNullRef").Call([RefArg(Id("item"))]), braced: false)
            .Return(Ref(Id("Unsafe").Dot("NullRef").TypeArgs([T("TItem")]).Call()))
            .EndIf()
            .Gap().Return(Ref(Id("item").Dot("Cast").TypeArgs([T("TItem")]).Call()))
            .EndMethod()
            .Gap().Public().Method(T("bool"), "TryWrite")
            .TypeParam("TItem").In().Param(T("TItem"), "item").Where("TItem", ItemConstraints)
            .Body().Var("type", CallGetType())
            .If(Id("_map").Dot("ContainsKey").Call([Id("type")]).Is(Lit(false)), braced: false)
            .Return(Lit(false))
            .EndIf()
            .Gap().Var("union", CallToUnion())
            .Do(Id("_map").Index([Id("type")]).Assign(Id("union")))
            .Gap().Do(InvokeItemsChanged()).Return(Lit(true))
            .EndMethod()
            .Gap().Public().Method(T("void"), "AddOrSet")
            .TypeParam("TItem").In().Param(T("TItem"), "item").Where("TItem", ItemConstraints)
            .Body().Var("type", CallGetType()).Var("union", CallToUnion())
            .Gap().Do(Id("_map").Dot("SetValue").Call([InArg(Id("type")), InArg(Id("union"))]))
            .EndMethod()
            .Gap().Public().Method(T("void"), "Dispose")
            .Body().Do(Id("_map").Dot("Dispose").Call()).Do(Id("_map").Assign(Default().NullForgiving()))
            .EndMethod();
    }

    private static Expr CallGetType() => Id("Item").Dot("GetType").TypeArgs([T("TItem")]).Call();
    private static Expr CallToUnion() => Id("item").Dot("ToUnion").Call();
    private static Expr InvokeItemsChanged() => Id("ItemsChanged").NullDot("Invoke").Call();
    private static Expr CallRemoveIsFalse() => Id("_map").Dot("Remove").Call([Id("type")]).Is(Lit(false));

    private static StructChain<TOuter> WriteItemMembers<TOuter>(StructChain<TOuter> type)
        where TOuter : class, IMemberHost
    {
        var privateTypeId = T("PrivateTypeId");

        var getType = type.Implements(T("IItem"))
            .Internal().Static().Readonly().Field(T("int"), "TypeIdCount", Id("Enum").Dot("GetValues")
                .Call([TypeOf(privateTypeId)]).Dot("Length"))
            .Gap().Attribute("FieldOffset", [Lit(0)]).Private().Readonly().Field(privateTypeId, "CurrentPrivateTypeId")
            .Gap().ForEach(Items, static (members, item) => members
                .Attribute("FieldOffset", [SizeOf(T("PrivateTypeId"))])
                .Private().Readonly().Field(T(item.Type), "_" + item.Field))
            .ForEach(Items, static (members, item) => members
                .Gap().Internal().Constructor().Param(T(item.Type), item.Field)
                .Body().Do(Id("CurrentPrivateTypeId").Assign(Id("PrivateTypeId").Dot(item.Type)))
                .ForEach(Items, (statements, other) => statements
                    .Do(Id("_" + other.Field).Assign(Default()), other.Field != item.Field))
                .Do(Id("_" + item.Field).Assign(Id(item.Field)))
                .EndConstructor())
            .Gap().Attribute("MethodImpl", [Id("AggressiveInlining")])
            .Internal().Static().Method(T("TypeId"), "GetType")
            .TypeParam("TItem").Where("TItem", ItemConstraints)
            .Body();

        var cast = WriteItemBranches(getType, static (branch, item) => branch
                .Return(New(T("TypeId"), [Cast(T("ulong"), Id("PrivateTypeId").Dot(item.Type))])))
            .EndMethod()
            .Gap().Attribute("MethodImpl", [Id("AggressiveInlining")])
            .Internal().Static().Method(T("TypeId"), "GetType")
            .TypeParam("TItem").In().Param(T("TItem"), "income").Where("TItem", ItemConstraints)
            .Body().Return(New(T("TypeId"), [
                Cast(T("ulong"), Id("income").Dot("ToUnion").Call().Dot("CurrentPrivateTypeId"))
            ]))
            .EndMethod()
            .Gap().Attribute("MethodImpl", [Id("AggressiveInlining")])
            .Internal().Unsafe().Ref().Readonly().Method(T("TItem"), "Cast")
            .TypeParam("TItem").Where("TItem", ItemConstraints)
            .Body();

        var typeId = WriteItemBranches(cast, static (branch, item) => branch
                .Fixed(T("void").Pointer(), "raw", AddressOf(Id("_" + item.Field)))
                .Return(Ref(Id("UnsafeUtility").Dot("ArrayElementAsRef").TypeArgs([T("TItem")])
                    .Call([Id("raw"), Named("index", Lit(0))])))
                .EndFixed())
            .EndMethod()
            .Gap().Attribute("MethodImpl", [Id("AggressiveInlining")])
            .Public().Method(T("TResult"), "Accept")
            .TypeParam("TResult").Param(T("IItemVisitor", [T("TResult")]), "visitor")
            .Body().Return(Id("CurrentPrivateTypeId").Switch()
                .ForEach(Items, static (arms, item) => arms.Arm(Id("PrivateTypeId").Dot(item.Type), CallVisit(item)))
                .Gap().IfDirective(CollectionsChecks)
                .Arm(Discard(), ThrowExpression(CreateOutOfRange()))
                .ElseDirective()
                .Arm(Discard(), Default().NullForgiving())
                .EndIfDirective()
                .EndSwitch())
            .EndMethod()
            .Gap().Attribute("MethodImpl", [Id("AggressiveInlining")])
            .Public().Method(T("void"), "Accept").Param(T("IItemVisitor"), "visitor")
            .Body().Switch(Id("CurrentPrivateTypeId"))
            .ForEach(Items, static (sections, item) => sections
                .Case(Id("PrivateTypeId").Dot(item.Type)).Do(CallVisit(item)).Break().EndCase())
            .Gap().IfDirective(CollectionsChecks)
            .Default().Throw(CreateOutOfRange()).EndCase()
            .EndIfDirective()
            .EndSwitch()
            .EndMethod()
            .Gap().Private().Enum("PrivateTypeId").Inherits(T("ulong"))
            .ForEach(Items, static (members, item) => members.EnumMember(item.Type, Lit(item.Id)))
            .EndEnum()
            .Gap().Attribute("BurstCompile").Public().Readonly().Struct("TypeId");
        return WriteTypeIdMembers(typeId).EndStruct();
    }

    private static Expr CallVisit(ItemSpec item) => Id("visitor").Dot("Visit").Call([InArg(Id("_" + item.Field))]);

    private static Expr CreateOutOfRange()
    {
        return New(T("ArgumentOutOfRangeException"),
            [NameOf(Id("CurrentPrivateTypeId")), Id("CurrentPrivateTypeId"), Lit(UnknownItem)]);
    }

    private static TBody WriteItemBranches<TBody>(TBody body, Func<IfChain<TBody>, ItemSpec, IfChain<TBody>> branch)
        where TBody : StatementChain<TBody>
    {
        IfChain<TBody> chain = default!;
        foreach (var item in Items)
        {
            var condition = TypeOf(T("TItem")).Binary(Equal, TypeOf(T(item.Type)));
            chain = branch(chain == default ? body.If(condition) : chain.ElseIf(condition), item);
        }

        return chain.Else()
            .IfDirective(CollectionsChecks.Binary(And, Id("DEBUG")))
            .Throw(New(T("ArgumentOutOfRangeException"), [TypeOf(T("TItem")).Dot("Name"), Lit(UnsupportedIncome)]))
            .ElseDirective()
            .Return(Default())
            .EndIfDirective()
            .EndIf();
    }

    private static StructChain<TOuter> WriteTypeIdMembers<TOuter>(StructChain<TOuter> type)
        where TOuter : class, IMemberHost
    {
        var privateTypeId = Id("_privateTypeId");
        return type.Implements(T("IEquatable", [T("TypeId")]))
            .Private().Readonly().Field(T("ulong"), "_privateTypeId")
            .Gap().Internal().Constructor().Param(T("ulong"), "privateTypeId")
            .Arrow(privateTypeId.Assign(Id("privateTypeId")))
            .EndConstructor()
            .Gap().Public().Method(T("bool"), "Equals").Param(T("TypeId"), "other")
            .Arrow(privateTypeId.Binary(Equal, Id("other").Dot("_privateTypeId")))
            .EndMethod()
            .Public().Override().Method(T("bool"), "Equals").Param(T("object").Nullable(), "obj")
            .Arrow(ThrowExpression(New(T("Exception"), [Lit("Should not be used")])))
            .EndMethod()
            .Public().Override().Method(T("int"), "GetHashCode").Arrow(Cast(T("int"), privateTypeId)).EndMethod()
            .Public().Override().Method(T("string"), "ToString").Arrow(privateTypeId.Dot("ToString").Call())
            .EndMethod();
    }
}