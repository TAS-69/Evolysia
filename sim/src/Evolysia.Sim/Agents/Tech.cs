using Evolysia.Sim.Core;

namespace Evolysia.Sim.Agents;

public enum TechId : byte
{
    StoneTools, ControlledFire, Cooking, Spear, HideClothing, Shelter, Baskets, Fishing, Herbalism,
    FoodStorage, Bow, Pottery, Weaving, Boats, Domestication, Agriculture, Copper, Bronze,
}

/// <summary>
/// A technique someone can discover. Nothing is unlocked globally: individuals know techniques,
/// teach them, and can lose them if everyone who knows dies or abandons them.
/// </summary>
public sealed record TechDef(TechId Id, Domain Domain, float Difficulty, float Value, TechId[] Requires);

public static class Techs
{
    public static readonly TechDef[] All =
    {
        new(TechId.StoneTools, Domain.Crafting, 1f, 0.5f, Array.Empty<TechId>()),
        new(TechId.ControlledFire, Domain.Crafting, 2f, 0.9f, Array.Empty<TechId>()),
        new(TechId.Cooking, Domain.Foraging, 1.5f, 0.6f, new[] { TechId.ControlledFire }),
        new(TechId.Spear, Domain.Hunting, 1.5f, 0.7f, new[] { TechId.StoneTools }),
        new(TechId.HideClothing, Domain.Crafting, 2f, 0.7f, new[] { TechId.StoneTools }),
        new(TechId.Shelter, Domain.Building, 2f, 0.7f, new[] { TechId.StoneTools }),
        new(TechId.Baskets, Domain.Crafting, 2f, 0.5f, new[] { TechId.StoneTools }),
        new(TechId.Fishing, Domain.Fishing, 2.5f, 0.6f, new[] { TechId.Baskets }),
        new(TechId.Herbalism, Domain.Healing, 3f, 0.6f, new[] { TechId.Cooking }),
        new(TechId.FoodStorage, Domain.Foraging, 3f, 0.8f, new[] { TechId.Baskets, TechId.ControlledFire }),
        new(TechId.Bow, Domain.Hunting, 4f, 0.8f, new[] { TechId.Spear }),
        new(TechId.Pottery, Domain.Crafting, 4f, 0.6f, new[] { TechId.ControlledFire, TechId.Baskets }),
        new(TechId.Weaving, Domain.Crafting, 4f, 0.5f, new[] { TechId.Baskets }),
        new(TechId.Boats, Domain.Building, 5f, 0.6f, new[] { TechId.Fishing }),
        new(TechId.Domestication, Domain.Hunting, 6f, 0.9f, new[] { TechId.Spear, TechId.FoodStorage }),
        new(TechId.Agriculture, Domain.Farming, 7f, 1f, new[] { TechId.FoodStorage, TechId.Baskets }),
        new(TechId.Copper, Domain.Crafting, 8f, 0.8f, new[] { TechId.Pottery }),
        new(TechId.Bronze, Domain.Crafting, 10f, 0.9f, new[] { TechId.Copper }),
    };

    public static TechDef Get(TechId id) => All[(int)id];
    public const int Count = 18;

    public static ulong Bit(TechId id) => 1UL << (int)id;
    public static bool Has(ulong known, TechId id) => (known & Bit(id)) != 0;

    public static bool CanDiscover(ulong known, TechId id)
    {
        if (Has(known, id)) return false;
        foreach (var r in Get(id).Requires) if (!Has(known, r)) return false;
        return true;
    }

    public static int CountKnown(ulong known) => System.Numerics.BitOperations.PopCount(known);

    public static string Name(TechId id) => Ids.Snake(id);
}
