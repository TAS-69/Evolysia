using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Economy;
using Evolysia.Sim.Psych;
using Evolysia.Sim.Society;
using Evolysia.Sim.World;

namespace Evolysia.Sim;

/// <summary>
/// Goods, gear and work: what each day's work brings in (by biome, season and the tile worked),
/// tools and clothing (owned, worn out, remade), crafting by recipe at a band's workstations,
/// storage and spoilage, fuel for the hearth, fields and herds. Everything here touches only the
/// band living its day, or shared tiles through the deferred harvest tally.
/// </summary>
public sealed partial class Simulation
{
    static readonly ushort GWood = Goods.Id("wood"), GLog = Goods.Id("log"), GStone = Goods.Id("stone"), GFlint = Goods.Id("flint"),
        GClay = Goods.Id("clay"), GSand = Goods.Id("sand"), GReed = Goods.Id("reed"), GFiber = Goods.Id("fiber"), GSalt = Goods.Id("salt"),
        GHide = Goods.Id("hide"), GFur = Goods.Id("fur"), GBone = Goods.Id("bone"), GAntler = Goods.Id("antler"), GMeat = Goods.Id("meat_raw"),
        GHerbs = Goods.Id("herbs"), GBamboo = Goods.Id("bamboo"), GHoney = Goods.Id("honey"), GMilk = Goods.Id("milk"), GEggs = Goods.Id("eggs");

    // ------------------------------------------------------------------ gear

    /// <summary>How much this person's best tool speeds an activity (1 with nothing that helps).</summary>
    internal static float ToolBonus(Agent a, Activity act)
    {
        float best = 1f;
        foreach (var item in a.Gear)
        {
            var k = item.Kind;
            if (k.Slot != ItemSlot.Tool && k.Uses.Length == 0) continue;
            best = MathF.Max(best, Crafts.Use(k, act));
        }
        return best;
    }

    /// <summary>The tool someone works with today (for drawing them), or null.</summary>
    public static ItemDef? HeldTool(Agent a, Activity act)
    {
        ItemDef? best = null;
        float bestUse = 0f;
        foreach (var item in a.Gear)
        {
            if (item.Kind.Slot != ItemSlot.Tool || !Serves(item.Kind, act)) continue;
            float u = Crafts.Use(item.Kind, act);
            if (u > bestUse) { best = item.Kind; bestUse = u; }
        }
        return best;
    }

    /// <summary>Whether a tool is meant for an activity (a club is a hunting tool even though it adds nothing).</summary>
    static bool Serves(ItemDef d, Activity act)
    {
        foreach (var (a, _) in d.Uses) if (a == act) return true;
        return false;
    }

    /// <summary>The piece worn in a clothing slot (the warmest, if several), or null.</summary>
    public static ItemDef? Worn(Agent a, ItemSlot slot)
    {
        ItemDef? best = null;
        foreach (var item in a.Gear)
            if (item.Kind.Slot == slot && (best == null || item.Kind.Warmth + item.Kind.Protection * 10f + item.Kind.Prestige * 5f > best.Warmth + best.Protection * 10f + best.Prestige * 5f))
                best = item.Kind;
        return best;
    }

    internal static float Warmth(Agent a)
    {
        float sum = 0f;
        for (var s = ItemSlot.Legwear; s <= ItemSlot.Accessory; s++) sum += Worn(a, s)?.Warmth ?? 0f;
        return sum;
    }

    internal static float Protection(Agent a)
    {
        float p = 0f;
        foreach (var item in a.Gear) p += item.Kind.Protection;
        return MathF.Min(0.8f, p);
    }

    internal static float WeaponPower(Agent a)
    {
        float best = 0f;
        foreach (var item in a.Gear) if (item.Kind.Slot == ItemSlot.Tool) best = MathF.Max(best, item.Kind.Power);
        return best;
    }

    internal static float GearPrestige(Agent a)
    {
        float p = 0f;
        foreach (var item in a.Gear) p += item.Kind.Prestige;
        return p;
    }

    /// <summary>A day's wear: the tool used and the clothes worn age; worn-out things are thrown away.</summary>
    void WearGear(Agent a, Activity act)
    {
        var tool = HeldTool(a, act);
        for (int i = a.Gear.Count - 1; i >= 0; i--)
        {
            var item = a.Gear[i];
            var k = item.Kind;
            bool used = k.Slot != ItemSlot.Tool || k == tool;
            if (!used) continue;
            item.Wear += 1f / MathF.Max(30, k.Days);
            if (item.Wear >= 1f) a.Gear.RemoveAt(i);
            else a.Gear[i] = item;
        }
    }

    /// <summary>Gives someone an item, replacing a worse one in the same clothing slot (it goes to the band's spares).</summary>
    void Equip(Agent a, Band band, ItemDef def)
    {
        if (def.Slot != ItemSlot.Tool)
            for (int i = a.Gear.Count - 1; i >= 0; i--)
                if (a.Gear[i].Kind.Slot == def.Slot)
                {
                    if (band.Spares.Count < 40) band.Spares.Add(a.Gear[i]);
                    a.Gear.RemoveAt(i);
                }
        a.Gear.Add(new Item(def.Index));
    }

    // ------------------------------------------------------------------ what work brings in

    /// <summary>The plant foods of a place and season, with relative weights.</summary>
    static (ushort good, float weight)[] PlantFoods(Biome b, Season s)
    {
        static (ushort, float) G(string id, float w) => (Goods.Id(id), w);
        bool autumn = s is Season.Autumn or Season.Dry, spring = s is Season.Spring or Season.Wet;
        return b switch
        {
            Biome.TemperateForest or Biome.TemperateRainforest => new[] { G("berries", spring ? 0.5f : 2f), G("nuts", autumn ? 3f : 0.4f), G("roots", 1.5f), G("mushrooms", autumn ? 1.5f : 0.4f), G("herbs", 0.4f), G("honey", 0.08f), G("fruit", autumn ? 0.8f : 0.1f) },
            Biome.BorealForest => new[] { G("berries", 2f), G("mushrooms", 1.2f), G("roots", 1f), G("nuts", 0.6f), G("herbs", 0.3f) },
            Biome.TemperateGrassland or Biome.Shrubland => new[] { G("roots", 2f), G("berries", 1f), G("grain", autumn ? 1.5f : 0.3f), G("herbs", 0.5f), G("nuts", 0.3f), G("honey", 0.05f) },
            Biome.Savanna => new[] { G("roots", 2f), G("fruit", 1f), G("millet", autumn ? 1f : 0.2f), G("nuts", 0.6f), G("honey", 0.1f) },
            Biome.TropicalSeasonalForest => new[] { G("fruit", 2f), G("roots", 1.5f), G("banana", 0.8f), G("nuts", 0.8f), G("mushrooms", 0.4f), G("herbs", 0.4f) },
            Biome.TropicalRainforest => new[] { G("fruit", 2.5f), G("banana", 1.2f), G("roots", 1f), G("nuts", 0.6f), G("mushrooms", 0.6f), G("herbs", 0.6f), G("honey", 0.1f) },
            Biome.Wetland => new[] { G("roots", 2f), G("berries", 1f), G("rice", autumn ? 0.6f : 0.1f), G("herbs", 0.5f) },
            Biome.HotDesert => new[] { G("dates", 2f), G("roots", 1f), G("fig", 0.5f) },
            Biome.ColdDesert or Biome.Tundra or Biome.Alpine => new[] { G("berries", 1.5f), G("roots", 1.5f), G("mushrooms", 0.5f), G("herbs", 0.3f) },
            _ => new[] { G("roots", 1f), G("berries", 1f) },
        };
    }

    /// <summary>Picks one of weighted options with this band's stream.</summary>
    ushort PickGood((ushort good, float weight)[] options)
    {
        float total = 0f;
        foreach (var o in options) total += o.weight;
        float r = Rng.NextFloat() * total;
        foreach (var o in options) { if (r < o.weight) return o.good; r -= o.weight; }
        return options[^1].good;
    }

    /// <summary>A forager's haul: <paramref name="value"/> person-days of the place's foods, plus firewood, fibre and herbs on the way.</summary>
    void ForageYield(Agent a, int site, float value, in DayContext c)
    {
        var w = World;
        var foods = PlantFoods(w.Biome[site], c.Season);
        // A haul is two or three kinds of food.
        for (int k = 0; k < 3 && value > 0.01f; k++)
        {
            ushort g = PickGood(foods);
            float part = k == 2 ? value : value * Rng.Range(0.3f, 0.7f);
            a.Pack.Add(g, part / MathF.Max(0.1f, Goods.All[g].Food));
            value -= part;
        }
        if (w.TreesK[site] > 0f && Rng.Chance(0.6f)) a.Pack.Add(GWood, 0.4f * w.Trees[site] / MathF.Max(1f, w.TreesK[site]) + 0.1f);   // fallen wood
        if (Rng.Chance(0.25f)) a.Pack.Add(w.Biome[site] is Biome.Wetland ? GReed : GFiber, 0.3f);
        if (w.Biome[site] is Biome.TropicalRainforest or Biome.TropicalSeasonalForest && Rng.Chance(0.1f)) a.Pack.Add(GBamboo, 0.3f);
    }

    /// <summary>The game of a place: (meat per animal, hide, bone, antler, fur) - what a kill gives besides meat.</summary>
    (float hide, float bone, float antler, float fur) Carcass(int site)
    {
        var w = World;
        bool cold = w.TempMean[site] < 4f;
        return w.Biome[site] switch
        {
            Biome.BorealForest or Biome.Tundra => (1f, 1f, 0.8f, cold ? 1f : 0.4f),
            Biome.TemperateForest or Biome.TemperateRainforest => (1f, 1f, 0.5f, 0.3f),
            Biome.TemperateGrassland or Biome.Savanna => (1.5f, 1.5f, 0.2f, 0.1f),
            Biome.Alpine => (0.8f, 0.8f, 0.4f, 0.8f),
            _ => (0.8f, 0.8f, 0.2f, cold ? 0.6f : 0.1f),
        };
    }

    void HuntYield(List<Agent> hunters, int site, float meat)
    {
        var (hide, bone, antler, fur) = Carcass(site);
        int animals = Math.Max(1, (int)(meat / 9f));
        var share = new List<Agent>();
        foreach (var h in hunters) if (h.Alive) share.Add(h);
        if (share.Count == 0) return;
        foreach (var h in share)
        {
            h.Pack.Add(GMeat, meat / share.Count);
            h.Pack.Add(GHide, hide * animals / share.Count);
            h.Pack.Add(GBone, bone * animals / share.Count);
            h.Pack.Add(GAntler, antler * animals / share.Count);
            h.Pack.Add(GFur, fur * animals / share.Count);
        }
    }

    void FishYield(Agent a, int site, float value)
    {
        var w = World;
        bool sea = w.Biome[site] is Biome.Coast or Biome.Ocean;
        ushort fish = Goods.Id(sea ? "saltwater_fish" : "freshwater_fish");
        if (sea && Rng.Chance(0.3f))
        {
            string[] shore = { "shellfish", "crab", "shrimp", "squid" };
            fish = Goods.Id(shore[Rng.Next(shore.Length)]);
        }
        a.Pack.Add(fish, value / Goods.All[fish].Food);
    }

    /// <summary>A tile within the home range rich in a stock (the best of a few looks), or -1.</summary>
    int FindTile(Band band, in DayContext c, int kind, float minimum)
    {
        int best = -1;
        float bestScore = float.MinValue;
        int r = Math.Max(1, c.Range) * (kind == StoneStock || kind == OreStock ? 2 : 1);   // people walk further for stone and ore
        for (int k = 0; k < 8; k++)
        {
            int t = Around(band.Cell, r);
            float left = Left(kind, t);
            if (left <= minimum) continue;
            // The nearest good stand: woods round a camp get felled first, so settlements open out over time.
            float score = left - World.Distance(t, band.Cell) * (kind == Timber ? 1.5f : 0.5f);
            if (score > bestScore) { bestScore = score; best = t; }
        }
        return best;
    }

    /// <summary>Felling: logs from a wooded tile (an axe makes it far quicker); thins the trees there.</summary>
    void Chop(Agent a, Band band, in DayContext c)
    {
        int site = FindTile(band, c, Timber, 0.5f);
        if (site < 0) { a.Site = band.Cell; return; }
        a.Site = site;
        bool axe = HeldTool(a, Activity.Chop) != null;
        float logs = Take(Timber, site, (axe ? 1.2f : 0.35f) * ToolBonus(a, Activity.Chop) * (0.6f + 0.4f * a.Gene(Gene.Strength)));
        a.Pack.Add(GLog, logs);
        a.Pack.Add(GWood, logs * 0.5f);   // branches and offcuts
        a.Needs[(int)Need.Energy] += 0.2f;
        Psychology.Practice(a, Domain.Building, logs > 0.4f, 0.4f);
    }

    /// <summary>Quarrying: stone (and flint) from boulders and outcrops; the boulders are used up.</summary>
    void Quarry(Agent a, Band band, in DayContext c)
    {
        int site = FindTile(band, c, StoneStock, 0.5f);
        if (site < 0) { a.Site = band.Cell; return; }
        a.Site = site;
        float got = Take(StoneStock, site, 1.0f * ToolBonus(a, Activity.Quarry) * (0.5f + 0.5f * a.Gene(Gene.Strength)));
        a.Pack.Add(GStone, got * 0.8f);
        a.Pack.Add(GFlint, got * 0.25f);
        a.Needs[(int)Need.Energy] += 0.22f;
        Psychology.Practice(a, Domain.Building, got > 0.5f, 0.4f);
    }

    /// <summary>Mining: ore from a surface deposit (copper, tin, iron, gold, silver, coal, sulfur).</summary>
    void Mine(Agent a, Band band, in DayContext c)
    {
        int site = FindTile(band, c, OreStock, 0.5f);
        if (site < 0) { a.Site = band.Cell; return; }
        a.Site = site;
        bool skilled = Techs.Has(a.Knows, TechId.Mining);
        float got = Take(OreStock, site, (skilled ? 1.0f : 0.4f) * ToolBonus(a, Activity.Mine));
        a.Pack.Add(World.OreGood[site], got);
        a.Needs[(int)Need.Energy] += 0.25f;
        if (Rng.Chance(0.0003f)) Kill(a, DeathCause.Accident, null, ", in a collapse");
    }

    /// <summary>Gathering materials: clay and reeds by water, sand on beaches and dunes, salt on flats and shores, fibre in grass.</summary>
    void GatherMaterials(Agent a, Band band, in DayContext c)
    {
        var w = World;
        int site = _land[Rng.Next(_land.Count)];
        a.Site = site;
        var b = w.Biome[site];
        bool water = w.Flow[site] > WorldGen.RiverFlow || b == Biome.Wetland;
        var options = new List<(ushort, float)>();
        float want(ushort g) => 0.2f + Shortage(band, g);
        if (water) { options.Add((GClay, want(GClay))); options.Add((GReed, want(GReed))); }
        if (b is Biome.HotDesert or Biome.ColdDesert || IsCoastal(site)) options.Add((GSand, want(GSand)));
        if (IsCoastal(site) || b == Biome.ColdDesert) options.Add((GSalt, want(GSalt) * 0.6f));
        if (b is Biome.TemperateGrassland or Biome.Savanna or Biome.Shrubland or Biome.Wetland) options.Add((GFiber, want(GFiber)));
        if (b is Biome.TropicalRainforest or Biome.TropicalSeasonalForest) options.Add((GBamboo, want(GBamboo)));
        options.Add((GHerbs, 0.1f + (BandKnows(band, TechId.Herbalism) ? 0.3f : 0f)));
        options.Add((GFiber, 0.15f));
        ushort g = PickGood(options.ToArray());
        a.Pack.Add(g, 1.6f * ToolBonus(a, Activity.Gather));
        a.Needs[(int)Need.Energy] += 0.15f;
    }

    // ------------------------------------------------------------------ wants

    /// <summary>0..1: how short the band is of a good against what it wants on hand.</summary>
    internal static float Shortage(Band band, ushort good)
    {
        if (band.Targets.Length <= good) return 0f;
        float want = band.Targets[good];
        if (want <= 0f) return 0f;
        return Math.Clamp((want - band.Stores[good]) / want, 0f, 1f);
    }

    static float ShortageOf(Band band, params ushort[] goods)
    {
        float s = 0f;
        foreach (var g in goods) s = MathF.Max(s, Shortage(band, g));
        return s;
    }

    /// <summary>
    /// Nobody keeps piles of what they have no use for: non-food goods beyond about twice what the
    /// band wants on hand (and a little of everything) are left lying, given away or burned.
    /// </summary>
    void DiscardClutter(Band band)
    {
        foreach (var (good, qty) in band.Stores.Held().ToList())
        {
            if (good.Kind == GoodKind.Food) continue;
            float keep = MathF.Max(10f, (band.Targets.Length > good.Index ? band.Targets[good.Index] : 0f) * 2.5f) * (band.Sedentary ? 3f : 1f);
            if (qty > keep) band.Stores.Take(good.Index, qty - keep);
        }
    }

    /// <summary>Whether a band could come by a good: gathered raw from the land, or made by a recipe it knows.</summary>
    internal static bool CanObtain(TechSet known, ushort good)
    {
        var g = Goods.All[good];
        if (g.Kind is GoodKind.Material or GoodKind.Fuel && !g.Id.EndsWith("_ore") && g.Id is not ("coal" or "sulfur" or "quartz" or "ochre" or "ice" or "snow" or "water"))
            return g.Id != "log" || known.Has(TechId.Woodcutting) || known.Has(TechId.StoneTools);
        if (g.Id.EndsWith("_ore") || g.Id is "coal" or "sulfur" or "quartz") return known.Has(TechId.Copper) || known.Has(TechId.Mining);
        foreach (var r in Crafts.Recipes)
            if (known.Has(r.Tech))
                foreach (var (o, _) in r.Outputs) if (o == good) return true;
        return g.Kind == GoodKind.Food;
    }

    /// <summary>
    /// What a band wants on hand (every 15 days): firewood for the season, materials for what it is
    /// building, the inputs of the tools and clothes its people lack, and a little of everything it
    /// knows how to use.
    /// </summary>
    void UpdateTargets(Band band, TechSet known)
    {
        if (band.Targets.Length != Goods.Count) band.Targets = new float[Goods.Count];
        var t = band.Targets;
        Array.Clear(t);
        int n = band.Members.Count;
        bool cold = World.TempMean[band.Cell] - World.TempAmp[band.Cell] < 8f;
        if (known.Has(TechId.ControlledFire)) t[GWood] = n * (cold ? 0.12f : 0.04f) * 20f;   // about three weeks of firewood
        if (known.Has(TechId.StoneTools)) t[GFlint] = 4f;
        if (known.Has(TechId.Baskets)) t[GReed] = 4f;
        if (known.Has(TechId.Cordage)) t[GFiber] = 4f;
        if (known.Has(TechId.Herbalism)) t[GHerbs] = 3f;
        foreach (int sid in band.Structures)
            if (Structures.TryGetValue(sid, out var s) && s.State is StructureState.Planned or StructureState.Building)
            {
                float left = 1f - s.Progress / MathF.Max(0.1f, s.Kind.Labour);
                foreach (var (g, q) in s.Kind.Materials) t[g] += q * left;
            }
        // The inputs of the gear people lack (one of each wanted item per two people lacking it, at most a few).
        foreach (int id in band.Members)
        {
            if (!Living.TryGetValue(id, out var a) || a.IsChild(Day)) continue;
            if (WantedItem(a, band, known) is { } want)
                foreach (var (g, q) in want.Inputs) t[g] = MathF.Min(t[g] + q * 0.5f, 40f);
        }
        // Raw materials for intermediate goods the band can make.
        foreach (var r in Crafts.Recipes)
        {
            if (!known.Has(r.Tech)) continue;
            foreach (var (g, q) in r.Outputs)
                if (t[g] > band.Stores[g])
                    foreach (var (ig, iq) in r.Inputs) t[ig] = MathF.Max(t[ig], iq * MathF.Ceiling((t[g] - band.Stores[g]) / q));
        }
        if (band.Sedentary) { t[GWood] *= 1.5f; t[GStone] = MathF.Max(t[GStone], 6f); t[GLog] = MathF.Max(t[GLog], 6f); }
    }

    /// <summary>
    /// The most useful item this person lacks that their people know how to make: warm clothes in the
    /// cold, the tool for their main work, a weapon for a hunter, finery for a leader.
    /// </summary>
    ItemDef? WantedItem(Agent a, Band band, TechSet known, Band? from = null)
    {
        // With a store to make it from, only what its materials allow; otherwise the best known (what to gather for).
        ItemDef? BestItem(TechSet k, ItemSlot slot, Func<ItemDef, float> score) =>
            Simulation.BestItem(k, slot, d => from == null || HasInputs(from, d.Inputs) ? score(d) : 0f);
        float cold = MathF.Max(0f, 12f - (World.TempMean[band.Cell] - World.TempAmp[band.Cell] * 0.7f));
        if (Warmth(a) < cold)
            for (var slot = ItemSlot.Top; slot >= ItemSlot.Legwear; slot--)
                if (Worn(a, slot) is null && BestItem(known, slot, d => d.Warmth) is { } warm) return warm;
        if (Worn(a, ItemSlot.Top) is null && BestItem(known, ItemSlot.Top, d => d.Warmth + 1f) is { } top) return top;
        if (Worn(a, ItemSlot.Legwear) is null && BestItem(known, ItemSlot.Legwear, d => d.Warmth + 1f) is { } legs) return legs;
        var main = a.MainActivity();
        int tools = 0;
        foreach (var item in a.Gear) if (item.Kind.Slot == ItemSlot.Tool) tools++;
        if (tools < 3)
        {
            if (HeldTool(a, main) is null && BestItem(known, ItemSlot.Tool, d => Serves(d, main) ? Crafts.Use(d, main) : 0f) is { } tool) return tool;
            if (main == Activity.Hunt && WeaponPower(a) <= 0f && BestItem(known, ItemSlot.Tool, d => d.Power) is { } weapon) return weapon;
        }
        if (Worn(a, ItemSlot.Footwear) is null && BestItem(known, ItemSlot.Footwear, d => d.Warmth + 0.5f) is { } shoes) return shoes;
        if (band.LeaderId == a.Id && GearPrestige(a) < 0.3f && BestItem(known, ItemSlot.Headwear, d => d.Prestige) is { } crown) return crown;
        if (Day - band.LastRaidedDay < 2 * SimConfig.DaysPerYear && a.Id % 2 == 0 && Protection(a) < 0.1f
            && BestItem(known, ItemSlot.Armour, d => d.Protection) is { } armour) return armour;
        return null;
    }

    /// <summary>The best item in a slot by some measure, among those the band can make (score must be positive).</summary>
    static ItemDef? BestItem(TechSet known, ItemSlot slot, Func<ItemDef, float> score)
    {
        ItemDef? best = null;
        float bestScore = 0f;
        foreach (var d in Crafts.Items)
        {
            if (d.Slot != slot || !Crafts.CanMake(known, d)) continue;
            float s = score(d);
            if (s <= 0f) continue;
            s += d.Days / 20000f;   // ties go to the more lasting
            if (s > bestScore) { bestScore = s; best = d; }
        }
        return best;
    }

    // ------------------------------------------------------------------ crafting

    /// <summary>
    /// A day's crafting: first something a member lacks, made from the stores at the right
    /// workstation; otherwise turning raw goods into what the band wants (preserving food, rope,
    /// cloth, leather, bricks, metal). The crafter works at the station.
    /// </summary>
    void CraftWork(Agent a, Band band, in DayContext c)
    {
        float skill = (0.6f + 0.6f * a.Efficacy[(int)Domain.Crafting]) * ToolBonus(a, Activity.Craft);
        // 1. Gear someone lacks.
        foreach (int id in band.Members)
        {
            if (!Living.TryGetValue(id, out var m) || m.IsChild(Day) || Rng.Chance(0.5f)) continue;
            if (WantedItem(m, band, a.Knows, band) is not { } want || !HasStation(band, want.Station, out int at)) continue;
            foreach (var (g, q) in want.Inputs) band.Stores.Take(g, q);
            Equip(m, band, want);
            a.Site = at >= 0 ? at : band.Cell;
            Psychology.Practice(a, Domain.Crafting, true, 0.6f);
            a.Needs[(int)Need.Energy] += 0.1f;
            return;
        }
        // 2. Goods the band wants: the recipe whose output it is shortest of.
        Recipe? best = null;
        float bestNeed = 0.05f;
        int site = band.Cell;
        foreach (var r in Crafts.Recipes)
        {
            if (!a.Knows.Has(r.Tech) || !HasInputs(band, r.Inputs) || !HasStation(band, r.Station, out int st)) continue;
            float need = 0f;
            foreach (var (g, _) in r.Outputs) need = MathF.Max(need, Shortage(band, g));
            // Preserving: raw perishables in store are worth drying or cooking before they rot.
            foreach (var (g, q) in r.Inputs)
                if (Goods.All[g].Spoil > 0.05f && Goods.All[g].Food > 0f && band.Stores[g] > q * 2f) need = MathF.Max(need, 0.7f);
            if (need > bestNeed) { bestNeed = need; best = r; site = st >= 0 ? st : band.Cell; }
        }
        a.Site = site;
        a.Needs[(int)Need.Energy] += 0.08f;
        if (best == null) { Psychology.Practice(a, Domain.Crafting, Rng.Chance(0.5f), 0.3f); return; }
        float batches = MathF.Max(0.5f, skill / MathF.Max(0.1f, best.Labour) * 0.5f);
        foreach (var (g, q) in best.Inputs) batches = MathF.Min(batches, band.Stores[g] / q);
        foreach (var (g, q) in best.Inputs) band.Stores.Take(g, q * batches);
        foreach (var (g, q) in best.Outputs) band.Stores.Add(g, q * batches);
        Psychology.Practice(a, best.Domain, true, 0.6f);
    }

    static bool HasInputs(Band band, (ushort good, float qty)[] inputs)
    {
        foreach (var (g, q) in inputs) if (!band.Stores.Has(g, q)) return false;
        return true;
    }

    /// <summary>Whether the band has a standing workstation of this kind (none needed: true), and where.</summary>
    bool HasStation(Band band, string station, out int cell)
    {
        cell = -1;
        if (station.Length == 0) return true;
        foreach (int sid in band.Structures)
            if (Structures.TryGetValue(sid, out var s) && s.Standing && s.Kind.Id == station) { cell = s.Cell; return true; }
        return false;
    }

    // ------------------------------------------------------------------ fields and herds

    /// <summary>
    /// A day in the fields: sowing at the start of the growing season, tending (each day of work
    /// adds to the harvest), and bringing the crop in when it is ripe or the season turns.
    /// </summary>
    void FarmWork(Agent a, Band band, in DayContext c)
    {
        Structure? field = null;
        foreach (int sid in band.Structures)
            if (Structures.TryGetValue(sid, out var s) && s.Standing && s.Kind.Role == StructureRole.Field && (field == null || s.Growth < field.Growth))
                field = s;
        if (field == null) { a.Site = band.Cell; return; }
        a.Site = field.Cell;
        float growth = World.Growth(field.Cell, Day);
        float work = (0.6f + 0.4f * a.Efficacy[(int)Domain.Farming]) * ToolBonus(a, Activity.Farm) * (BandKnows(band, TechId.Plough) ? 1.25f : 1f)
                     * (World.Precip[field.Cell] < 600f && BandKnows(band, TechId.Irrigation) ? 1.3f : 1f);
        if (growth > 0.15f)
        {
            if (field.Growth <= 0f) { field.Crop = CropFor(field); field.Growth = 0.02f; }
            field.Growth = MathF.Min(1.05f, field.Growth + 0.04f * MathF.Min(1.2f, growth + 0.2f) * work);
            field.Yield += 2.6f * MathF.Min(1.2f, growth + 0.2f) * MathF.Min(World.YearRain[field.Cell], 1.1f) * work;
        }
        if (field.Growth >= 1f || (growth < 0.1f && field.Growth > 0.3f))
        {
            ushort g = Goods.Id(field.Crop.Length > 0 ? field.Crop : "grain");
            band.Stores.Add(g, field.Yield / MathF.Max(0.2f, Goods.All[g].Food));
            if (Goods.All[g].Food > 0.5f) band.Stores.Add(GFiber, field.Yield * 0.05f);   // straw
            field.Growth = 0f; field.Yield = 0f;
        }
        Psychology.Practice(a, Domain.Farming, true, 0.5f);
        a.Needs[(int)Need.Energy] += 0.16f;
    }

    /// <summary>What a field grows: the staple that suits its climate (orchards: fruit).</summary>
    string CropFor(Structure field)
    {
        var w = World;
        int i = field.Cell;
        float t = w.TempMean[i], p = w.Precip[i];
        uint h = (uint)(i * 2654435761u) >> 8;
        if (field.Kind.Id == "orchard")
            return t > 22f ? (p < 400f ? "dates" : new[] { "orange", "fig", "banana", "coconut" }[h % 4])
                 : t > 12f ? new[] { "peach", "grapes", "plum", "lemon" }[h % 4] : new[] { "apple", "pear", "plum" }[h % 3];
        if (t > 22f && p > 1400f) return h % 3 == 0 ? "beans" : "rice";
        if (t > 20f) return new[] { "maize", "millet", "squash", "beans", "pumpkin", "pepper", "tomato" }[h % 7];
        if (p < 450f) return h % 2 == 0 ? "millet" : "lentils";
        if (t < 6f) return new[] { "rye", "oats", "potato", "barley", "cabbage" }[h % 5];
        return new[] { "wheat", "barley", "wheat", "peas", "lentils", "onion", "carrot", "beet", "potato", "oats", "lettuce", "melon" }[h % 12];
    }

    /// <summary>The band's tame animals by the land: dogs everywhere, and the herd that suits it.</summary>
    internal string HerdSpecies(int cell) => World.Biome[cell] switch
    {
        Biome.Alpine or Biome.Shrubland => "goat",
        Biome.TemperateGrassland or Biome.Savanna => "cattle",
        Biome.HotDesert or Biome.ColdDesert => "camel",
        Biome.Tundra or Biome.BorealForest => "horse",
        Biome.TropicalRainforest or Biome.TropicalSeasonalForest => "pig",
        _ => "sheep",
    };

    /// <summary>Herding: milk, eggs and wool from the band's animals, more of them over time, surplus slaughtered.</summary>
    void HerdWork(Agent a, Band band, in DayContext c)
    {
        a.Site = Around(band.Cell, Math.Max(1, c.Range));
        float skill = (0.7f + 0.3f * a.Efficacy[(int)Domain.Hunting]) * ToolBonus(a, Activity.Herd);
        float head = 0f;
        foreach (var (species, count) in band.Herds) head += species == "dog" ? 0f : count;
        if (head <= 0f) { a.Pack.Add(GMeat, 0.3f); return; }
        float per = MathF.Min(1f, head / 8f) * skill;
        foreach (var (species, count) in band.Herds)
        {
            if (count <= 0f) continue;
            float share = count / head * per;
            switch (species)
            {
                case "cattle" or "goat" or "camel": band.Stores.Add(GMilk, 2.2f * share); break;
                case "sheep": band.Stores.Add(GMilk, 1.2f * share); band.Stores.Add(GFiber, 0.6f * share); break;
                case "fowl": band.Stores.Add(GEggs, 2.5f * share); break;
                case "pig" or "horse": band.Stores.Add(GMeat, 0.5f * share); break;
            }
        }
        if (BandKnows(band, TechId.Beekeeping) && HasStation(band, "beehive", out _)) band.Stores.Add(GHoney, 0.1f);
        a.Needs[(int)Need.Energy] += 0.1f;
    }

    /// <summary>Every 15 days: herds grow towards what the pens and land hold; the surplus is slaughtered for meat and hides.</summary>
    void HerdUpkeep(Band band)
    {
        if (band.Herds.Count == 0) return;
        float room = 6f;
        foreach (int sid in band.Structures)
            if (Structures.TryGetValue(sid, out var s) && s.Standing && s.Kind.Role == StructureRole.Pen) room += s.Kind.Effect;
        foreach (var species in band.Herds.Keys.ToList())
        {
            float count = band.Herds[species];
            count *= 1f + (species == "fowl" ? 0.06f : 0.025f);
            float cap = species == "dog" ? 2f + band.Members.Count / 10f : room;
            if (count > cap)
            {
                float slaughtered = count - cap;
                float size = species is "cattle" or "horse" or "camel" ? 10f : species == "fowl" ? 1f : 4f;
                band.Stores.Add(species == "fowl" ? Goods.Id("poultry") : GMeat, slaughtered * size);
                if (species != "fowl") band.Stores.Add(GHide, slaughtered);
                count = cap;
            }
            band.Herds[species] = count;
        }
    }

    /// <summary>A successful hunt by people who know how to tame animals may bring young ones home alive.</summary>
    void MaybeCapture(Band band, int site)
    {
        if (!BandKnows(band, TechId.Domestication) || !Rng.Chance(0.12f)) return;
        string species = Rng.Chance(0.3f) ? (Rng.Chance(0.5f) ? "dog" : "fowl") : HerdSpecies(site);
        float have = band.Herds.GetValueOrDefault(species);
        band.Herds[species] = have + Rng.Range(1, 3);
    }

    // ------------------------------------------------------------------ stores

    /// <summary>
    /// Evening: everyone puts materials into the band's stores and shares food beyond what they
    /// keep for themselves (how much they keep depends on how cooperative they are).
    /// </summary>
    void Deposit(Agent a, Band band, float keep)
    {
        if (a.Pack is not { } pack) return;
        foreach (var (good, qty) in pack.Held().ToList())
            if (good.Kind != GoodKind.Food) band.Stores.Add(good.Index, pack.Take(good.Index, qty));
        float food = pack.FoodValue;
        if (food > keep) pack.GiveFood(band.Stores, (food - keep));
    }

    /// <summary>
    /// The stores' day: firewood burns on the hearth (more in the cold), goods spoil (heat speeds it,
    /// pits, granaries, pottery and cold stores slow it), and what doesn't fit in the stores lies
    /// out and goes off faster.
    /// </summary>
    void StoresUpkeep(Band band, in DayContext c)
    {
        int n = _members.Count;
        if (band.HasFire)
        {
            float burn = n * (c.Temp < 5f ? 0.12f : c.Temp < 15f ? 0.06f : 0.03f);
            float got = band.Stores.Take(GWood, burn);
            if (got < burn * 0.5f) band.HasFireToday = false; else band.HasFireToday = true;
        }
        else band.HasFireToday = false;
        float heat = c.Temp > 25f ? 1.6f : c.Temp > 12f ? 1f : c.Temp > 2f ? 0.5f : 0.15f;
        float kept = 1f;
        if (band.StorageRoom > 0) kept = 0.55f;
        if (Techs.Has(c.Tech, TechId.Pottery)) kept *= 0.8f;
        if (band.HasColdStore) kept *= 0.3f;
        // Food that doesn't fit in the stores (or in people's homes: a few weeks' worth each) lies
        // out and goes off faster.
        float food = band.Stores.FoodValue, room = 20f + n * (band.Shelter > 0f ? 12f : 6f) + band.StorageRoom;
        float overflow = food > room ? MathF.Min(2.5f, 1f + 0.5f * (food - room) / room) : 1f;
        band.Stores.Spoil(0.5f * heat * kept * overflow);
    }
}
