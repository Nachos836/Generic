using JetBrains.Annotations;

namespace Generic.Samples.GameplayInventory.Concrete
{
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    internal static class Test
    {
        private static void Try()
        {
            using var inventory = new Inventory();

            var money = inventory.Read<Money>();
            inventory.Write(new Money { Amount = money.Amount + 42 });
        }
    }
}
