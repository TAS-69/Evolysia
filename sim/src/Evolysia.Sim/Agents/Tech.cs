using Evolysia.Sim.Core;

namespace Evolysia.Sim.Agents;

public enum TechId : byte
{

    StoneTools, ControlledFire, Cooking, Spear, HideClothing, Shelter, Baskets, Fishing, Herbalism,
    FoodStorage, Bow, Pottery, Weaving, Boats, Domestication, Agriculture, Copper, Bronze,

    Woodcutting, Butchery, BoneTools, Cordage, Quarrying, Pigments, Music, Burial, Carving, Drying,
    Tanning, Netting, Rafts, Sledges, Grinding,

    Dairy, Beekeeping, Milling, Baking, Brewing, Irrigation, Wells, Orchards, Plough, Thatching,
    Mudbrick, Masonry, Carpentry, Kilns, FiredBrick, Wheel, Sailing, Trade, Writing, Counting,
    Governance, Monarchy, Priesthood, Medicine, Fortification, Armour, Alchemy, Mysticism,

    Charcoal, Iron, Steel, Glass, Mining, Swords, Crossbow, Roads, Riding, Shipbuilding, Schools, Sanitation,

    Machinery, Printing, SteamPower, Railways, PowerLooms, Chemistry, Firearms, Concrete, Hospitals,

    Electricity, Refrigeration, Telecommunication, Motors, Aviation, SolarPower, Batteries,

    Electronics, Computing, Robotics, Recycling, Networks,

    Biotech, Hydroponics, Fabrication, AdvancedMaterials, FusionPower, Hover, EnergyTools,

    Rocketry, LifeSupport, SpaceHabitats, OrbitalIndustry, Interplanetary,
}

public enum Affordance : byte { None, Water, Coast, Forest, Rock, Ore, Clay, Sand, Cold, Hot, Dry, Grass, Settled, Many, Herds, Spirit }

public sealed record TechDef(TechId Id, Domain Domain, float Difficulty, float Value, TechId[] Requires,
                             Affordance Needs = Affordance.None, string Stage = "foraging");

public struct TechSet : IEquatable<TechSet>
{
    ulong _a, _b, _c, _d;

    public static TechSet Of(TechId t) { var s = new TechSet(); s.Add(t); return s; }

    public readonly bool Has(TechId t)
    {
        int i = (int)t;
        ulong bit = 1UL << (i & 63);
        return (i >> 6) switch { 0 => (_a & bit) != 0, 1 => (_b & bit) != 0, 2 => (_c & bit) != 0, _ => (_d & bit) != 0 };
    }

    public void Add(TechId t)
    {
        int i = (int)t;
        ulong bit = 1UL << (i & 63);
        switch (i >> 6) { case 0: _a |= bit; break; case 1: _b |= bit; break; case 2: _c |= bit; break; default: _d |= bit; break; }
    }

    public readonly bool IsEmpty => (_a | _b | _c | _d) == 0;
    public readonly int Count => System.Numerics.BitOperations.PopCount(_a) + System.Numerics.BitOperations.PopCount(_b)
                                 + System.Numerics.BitOperations.PopCount(_c) + System.Numerics.BitOperations.PopCount(_d);

    public readonly IEnumerable<TechId> Ids()
    {
        for (int i = 0; i < Techs.Count; i++) if (Has((TechId)i)) yield return (TechId)i;
    }

    public static TechSet operator |(TechSet x, TechSet y) => new() { _a = x._a | y._a, _b = x._b | y._b, _c = x._c | y._c, _d = x._d | y._d };
    public static TechSet operator &(TechSet x, TechSet y) => new() { _a = x._a & y._a, _b = x._b & y._b, _c = x._c & y._c, _d = x._d & y._d };
    public static TechSet operator ~(TechSet x) => new() { _a = ~x._a, _b = ~x._b, _c = ~x._c, _d = ~x._d };
    public static bool operator ==(TechSet x, TechSet y) => x.Equals(y);
    public static bool operator !=(TechSet x, TechSet y) => !x.Equals(y);
    public readonly bool Equals(TechSet o) => _a == o._a && _b == o._b && _c == o._c && _d == o._d;
    public override readonly bool Equals(object? obj) => obj is TechSet o && Equals(o);
    public override readonly int GetHashCode() => HashCode.Combine(_a, _b, _c, _d);
}

public static class Techs
{
    static TechDef T(TechId id, Domain d, float difficulty, float value, Affordance needs, string stage, params TechId[] requires) =>
        new(id, d, difficulty, value, requires, needs, stage);

    const Affordance N = Affordance.None;

    public static readonly int Count = Enum.GetValues<TechId>().Length;
    public static readonly TechDef[] All = Build();

    static TechDef[] Build()
    {
        var list = new List<TechDef>
        {

            T(TechId.StoneTools, Domain.Crafting, 1f, 0.5f, N, "foraging"),
            T(TechId.ControlledFire, Domain.Crafting, 2f, 0.9f, N, "foraging"),
            T(TechId.Cooking, Domain.Foraging, 1.5f, 0.6f, N, "foraging", TechId.ControlledFire),
            T(TechId.Spear, Domain.Hunting, 1.5f, 0.7f, N, "foraging", TechId.StoneTools),
            T(TechId.HideClothing, Domain.Crafting, 2f, 0.7f, N, "foraging", TechId.StoneTools),
            T(TechId.Shelter, Domain.Building, 2f, 0.7f, N, "foraging", TechId.StoneTools),
            T(TechId.Baskets, Domain.Crafting, 2f, 0.5f, N, "foraging", TechId.StoneTools),
            T(TechId.Fishing, Domain.Fishing, 2.5f, 0.6f, Affordance.Water, "foraging", TechId.Baskets),
            T(TechId.Herbalism, Domain.Healing, 3f, 0.6f, N, "foraging", TechId.Cooking),
            T(TechId.FoodStorage, Domain.Foraging, 3f, 0.8f, N, "foraging", TechId.Baskets, TechId.ControlledFire),
            T(TechId.Bow, Domain.Hunting, 4f, 0.8f, N, "foraging", TechId.Spear),
            T(TechId.Pottery, Domain.Crafting, 4f, 0.6f, Affordance.Clay, "agriculture", TechId.ControlledFire, TechId.Baskets),
            T(TechId.Weaving, Domain.Crafting, 4f, 0.5f, N, "agriculture", TechId.Baskets),
            T(TechId.Boats, Domain.Building, 5f, 0.6f, Affordance.Coast, "stonecraft", TechId.Fishing),
            T(TechId.Domestication, Domain.Hunting, 6f, 0.9f, Affordance.Grass, "agriculture", TechId.Spear, TechId.FoodStorage),
            T(TechId.Agriculture, Domain.Farming, 7f, 1f, N, "agriculture", TechId.FoodStorage, TechId.Baskets),
            T(TechId.Copper, Domain.Crafting, 8f, 0.8f, Affordance.Ore, "metallurgy", TechId.Pottery),
            T(TechId.Bronze, Domain.Crafting, 10f, 0.9f, Affordance.Ore, "metallurgy", TechId.Copper),

            T(TechId.Woodcutting, Domain.Building, 1.5f, 0.6f, Affordance.Forest, "foraging", TechId.StoneTools),
            T(TechId.Butchery, Domain.Hunting, 1.5f, 0.5f, N, "foraging", TechId.StoneTools),
            T(TechId.BoneTools, Domain.Crafting, 2.5f, 0.5f, N, "foraging", TechId.Butchery),
            T(TechId.Cordage, Domain.Crafting, 2f, 0.5f, N, "foraging", TechId.StoneTools),
            T(TechId.Quarrying, Domain.Building, 2.5f, 0.5f, Affordance.Rock, "stonecraft", TechId.StoneTools),
            T(TechId.Pigments, Domain.Spiritual, 2f, 0.4f, Affordance.Clay, "foraging", TechId.StoneTools),
            T(TechId.Music, Domain.Social, 2.5f, 0.4f, N, "foraging", TechId.Carving),
            T(TechId.Burial, Domain.Spiritual, 2f, 0.4f, Affordance.Spirit, "stonecraft", TechId.StoneTools),
            T(TechId.Carving, Domain.Crafting, 2.5f, 0.4f, N, "stonecraft", TechId.Woodcutting),
            T(TechId.Drying, Domain.Foraging, 2.5f, 0.7f, N, "foraging", TechId.ControlledFire, TechId.Cordage),
            T(TechId.Tanning, Domain.Crafting, 3f, 0.5f, N, "foraging", TechId.HideClothing, TechId.Butchery),
            T(TechId.Netting, Domain.Fishing, 3f, 0.6f, Affordance.Water, "stonecraft", TechId.Fishing, TechId.Cordage),
            T(TechId.Rafts, Domain.Building, 3f, 0.5f, Affordance.Water, "agriculture", TechId.Woodcutting, TechId.Cordage),
            T(TechId.Sledges, Domain.Building, 3f, 0.5f, Affordance.Cold, "agriculture", TechId.Woodcutting, TechId.Cordage),
            T(TechId.Grinding, Domain.Foraging, 3f, 0.6f, N, "stonecraft", TechId.StoneTools, TechId.Cooking),

            T(TechId.Dairy, Domain.Farming, 7f, 0.7f, Affordance.Herds, "agriculture", TechId.Domestication, TechId.Pottery),
            T(TechId.Beekeeping, Domain.Farming, 6f, 0.5f, Affordance.Forest, "agriculture", TechId.Baskets, TechId.ControlledFire),
            T(TechId.Milling, Domain.Farming, 7f, 0.7f, Affordance.Settled, "agriculture", TechId.Agriculture, TechId.Grinding),
            T(TechId.Baking, Domain.Foraging, 7f, 0.6f, Affordance.Settled, "agriculture", TechId.Milling, TechId.Kilns),
            T(TechId.Brewing, Domain.Foraging, 7f, 0.5f, Affordance.Settled, "agriculture", TechId.Agriculture, TechId.Pottery),
            T(TechId.Irrigation, Domain.Farming, 9f, 0.8f, Affordance.Dry, "agriculture", TechId.Agriculture, TechId.Woodcutting),
            T(TechId.Wells, Domain.Building, 7f, 0.7f, Affordance.Dry, "agriculture", TechId.Agriculture, TechId.Quarrying),
            T(TechId.Orchards, Domain.Farming, 8f, 0.6f, Affordance.Settled, "agriculture", TechId.Agriculture),
            T(TechId.Plough, Domain.Farming, 10f, 0.9f, Affordance.Herds, "agriculture", TechId.Agriculture, TechId.Domestication, TechId.Carpentry),
            T(TechId.Thatching, Domain.Building, 4f, 0.6f, N, "agriculture", TechId.Shelter, TechId.Cordage),
            T(TechId.Mudbrick, Domain.Building, 6f, 0.6f, Affordance.Clay, "agriculture", TechId.Shelter, TechId.Pottery),
            T(TechId.Masonry, Domain.Building, 8f, 0.7f, Affordance.Rock, "stonecraft", TechId.Quarrying, TechId.Shelter),
            T(TechId.Carpentry, Domain.Building, 6f, 0.7f, Affordance.Forest, "agriculture", TechId.Woodcutting, TechId.Carving),
            T(TechId.Kilns, Domain.Crafting, 6f, 0.6f, N, "agriculture", TechId.Pottery),
            T(TechId.FiredBrick, Domain.Building, 9f, 0.6f, Affordance.Clay, "agriculture", TechId.Kilns, TechId.Mudbrick),
            T(TechId.Wheel, Domain.Crafting, 10f, 0.8f, N, "agriculture", TechId.Carpentry),
            T(TechId.Sailing, Domain.Building, 9f, 0.7f, Affordance.Coast, "metallurgy", TechId.Boats, TechId.Weaving),
            T(TechId.Trade, Domain.Social, 7f, 0.7f, Affordance.Many, "agriculture", TechId.Baskets, TechId.FoodStorage),
            T(TechId.Writing, Domain.Social, 14f, 0.8f, Affordance.Settled, "metallurgy", TechId.Trade, TechId.Pottery),
            T(TechId.Counting, Domain.Social, 9f, 0.6f, Affordance.Many, "agriculture", TechId.Trade),
            T(TechId.Governance, Domain.Social, 12f, 0.6f, Affordance.Many, "metallurgy", TechId.Counting),
            T(TechId.Monarchy, Domain.Social, 14f, 0.5f, Affordance.Many, "metallurgy", TechId.Governance),
            T(TechId.Priesthood, Domain.Spiritual, 9f, 0.6f, Affordance.Spirit, "stonecraft", TechId.Burial, TechId.Music),
            T(TechId.Medicine, Domain.Healing, 12f, 0.9f, N, "metallurgy", TechId.Herbalism, TechId.Writing),
            T(TechId.Fortification, Domain.Building, 9f, 0.6f, Affordance.Settled, "metallurgy", TechId.Carpentry),
            T(TechId.Armour, Domain.Crafting, 9f, 0.6f, N, "metallurgy", TechId.Tanning, TechId.Weaving),
            T(TechId.Alchemy, Domain.Healing, 16f, 0.5f, N, "metallurgy", TechId.Herbalism, TechId.Kilns, TechId.Writing),
            T(TechId.Mysticism, Domain.Spiritual, 10f, 0.5f, Affordance.Spirit, "metallurgy", TechId.Priesthood, TechId.Pigments),

            T(TechId.Charcoal, Domain.Crafting, 6f, 0.6f, Affordance.Forest, "metallurgy", TechId.ControlledFire, TechId.Woodcutting),
            T(TechId.Iron, Domain.Crafting, 16f, 1f, Affordance.Ore, "metallurgy", TechId.Bronze, TechId.Charcoal),
            T(TechId.Steel, Domain.Crafting, 20f, 0.9f, Affordance.Ore, "metallurgy", TechId.Iron),
            T(TechId.Glass, Domain.Crafting, 14f, 0.6f, Affordance.Sand, "metallurgy", TechId.Kilns, TechId.Charcoal),
            T(TechId.Mining, Domain.Building, 12f, 0.7f, Affordance.Ore, "metallurgy", TechId.Copper, TechId.Carpentry),
            T(TechId.Swords, Domain.Crafting, 14f, 0.6f, N, "metallurgy", TechId.Bronze),
            T(TechId.Crossbow, Domain.Hunting, 18f, 0.6f, N, "metallurgy", TechId.Bow, TechId.Iron, TechId.Carpentry),
            T(TechId.Roads, Domain.Building, 14f, 0.7f, Affordance.Settled, "metallurgy", TechId.Masonry, TechId.Wheel),
            T(TechId.Riding, Domain.Hunting, 12f, 0.8f, Affordance.Grass, "metallurgy", TechId.Domestication, TechId.Tanning),
            T(TechId.Shipbuilding, Domain.Building, 16f, 0.7f, Affordance.Coast, "metallurgy", TechId.Sailing, TechId.Carpentry),
            T(TechId.Schools, Domain.Social, 18f, 0.7f, Affordance.Settled, "metallurgy", TechId.Writing),
            T(TechId.Sanitation, Domain.Healing, 18f, 0.8f, Affordance.Settled, "metallurgy", TechId.Masonry, TechId.Medicine),

            T(TechId.Machinery, Domain.Crafting, 24f, 0.9f, N, "mechanisation", TechId.Steel, TechId.Wheel, TechId.Counting),
            T(TechId.Printing, Domain.Social, 24f, 0.8f, N, "mechanisation", TechId.Writing, TechId.Machinery),
            T(TechId.SteamPower, Domain.Crafting, 28f, 1f, N, "mechanisation", TechId.Machinery, TechId.Mining),
            T(TechId.Railways, Domain.Building, 30f, 0.8f, N, "mechanisation", TechId.SteamPower, TechId.Roads),
            T(TechId.PowerLooms, Domain.Crafting, 28f, 0.7f, N, "mechanisation", TechId.SteamPower, TechId.Weaving),
            T(TechId.Chemistry, Domain.Healing, 28f, 0.8f, N, "mechanisation", TechId.Alchemy, TechId.Glass, TechId.Printing),
            T(TechId.Firearms, Domain.Hunting, 30f, 0.6f, N, "mechanisation", TechId.Chemistry, TechId.Steel),
            T(TechId.Concrete, Domain.Building, 26f, 0.7f, N, "mechanisation", TechId.Masonry, TechId.Kilns, TechId.Machinery),
            T(TechId.Hospitals, Domain.Healing, 30f, 0.9f, N, "mechanisation", TechId.Medicine, TechId.Sanitation, TechId.Chemistry),

            T(TechId.Electricity, Domain.Crafting, 36f, 1f, N, "electrification", TechId.SteamPower, TechId.Chemistry),
            T(TechId.Refrigeration, Domain.Foraging, 38f, 0.8f, N, "electrification", TechId.Electricity),
            T(TechId.Telecommunication, Domain.Social, 40f, 0.8f, N, "electrification", TechId.Electricity, TechId.Printing),
            T(TechId.Motors, Domain.Crafting, 40f, 0.9f, N, "electrification", TechId.Electricity, TechId.Machinery),
            T(TechId.Aviation, Domain.Building, 46f, 0.7f, N, "electrification", TechId.Motors),
            T(TechId.SolarPower, Domain.Crafting, 44f, 0.8f, Affordance.Hot, "electrification", TechId.Electricity, TechId.Glass),
            T(TechId.Batteries, Domain.Crafting, 42f, 0.7f, N, "electrification", TechId.Electricity),

            T(TechId.Electronics, Domain.Crafting, 50f, 0.9f, N, "information", TechId.Electricity, TechId.Chemistry),
            T(TechId.Computing, Domain.Social, 56f, 0.9f, N, "information", TechId.Electronics, TechId.Counting),
            T(TechId.Robotics, Domain.Crafting, 62f, 0.8f, N, "information", TechId.Computing, TechId.Motors),
            T(TechId.Recycling, Domain.Building, 54f, 0.6f, N, "information", TechId.Chemistry, TechId.Machinery),
            T(TechId.Networks, Domain.Social, 60f, 0.8f, N, "information", TechId.Computing, TechId.Telecommunication),

            T(TechId.Biotech, Domain.Healing, 70f, 0.9f, N, "advanced", TechId.Computing, TechId.Hospitals),
            T(TechId.Hydroponics, Domain.Farming, 64f, 0.8f, N, "advanced", TechId.Electricity, TechId.Biotech),
            T(TechId.Fabrication, Domain.Crafting, 72f, 0.9f, N, "advanced", TechId.Robotics, TechId.AdvancedMaterials),
            T(TechId.AdvancedMaterials, Domain.Crafting, 68f, 0.8f, N, "advanced", TechId.Chemistry, TechId.Computing),
            T(TechId.FusionPower, Domain.Crafting, 90f, 1f, N, "advanced", TechId.AdvancedMaterials, TechId.Computing),
            T(TechId.Hover, Domain.Building, 85f, 0.7f, N, "advanced", TechId.FusionPower, TechId.Motors),
            T(TechId.EnergyTools, Domain.Crafting, 88f, 0.6f, N, "advanced", TechId.FusionPower, TechId.Fabrication),

            T(TechId.Rocketry, Domain.Building, 80f, 0.8f, N, "space", TechId.Aviation, TechId.Chemistry, TechId.Computing),
            T(TechId.LifeSupport, Domain.Healing, 90f, 0.8f, N, "space", TechId.Rocketry, TechId.Hydroponics),
            T(TechId.SpaceHabitats, Domain.Building, 100f, 0.8f, N, "space", TechId.LifeSupport, TechId.AdvancedMaterials),
            T(TechId.OrbitalIndustry, Domain.Crafting, 110f, 0.8f, N, "space", TechId.SpaceHabitats, TechId.Fabrication),
            T(TechId.Interplanetary, Domain.Building, 130f, 1f, N, "space", TechId.OrbitalIndustry, TechId.FusionPower),
        };
        var all = new TechDef[Count];
        foreach (var d in list) all[(int)d.Id] = d;
        for (int i = 0; i < Count; i++)
            if (all[i] == null) throw new InvalidOperationException($"technique {(TechId)i} has no definition");
        return all;
    }

    public static TechDef Get(TechId id) => All[(int)id];

    public static TechSet Bit(TechId id) => TechSet.Of(id);
    public static bool Has(TechSet known, TechId id) => known.Has(id);

    public static bool CanDiscover(TechSet known, TechId id)
    {
        if (known.Has(id)) return false;
        return CanLearn(known, id);
    }

    public static bool CanLearn(TechSet known, TechId id)
    {
        foreach (var r in Get(id).Requires) if (!known.Has(r)) return false;
        return true;
    }

    public static int CountKnown(TechSet known) => known.Count;

    public static string Name(TechId id) => Ids.Snake(id);
}
