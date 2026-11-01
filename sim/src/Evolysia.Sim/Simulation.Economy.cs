using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Economy;
using Evolysia.Sim.Psych;
using Evolysia.Sim.Society;
using Evolysia.Sim.World;

namespace Evolysia.Sim;

public sealed partial class Simulation
{
    static readonly ushort GWood = Goods.Id("wood"), GLog = Goods.Id("log"), GStone = Goods.Id("stone"), GFlint = Goods.Id("flint"),
        GClay = Goods.Id("clay"), GSand = Goods.Id("sand"), GReed = Goods.Id("reed"), GFiber = Goods.Id("fiber"), GSalt = Goods.Id("salt"),
        GHide = Goods.Id("hide"), GFur = Goods.Id("fur"), GBone = Goods.Id("bone"), GAntler = Goods.Id("antler"), GMeat = Goods.Id("meat_raw"),
        GHerbs = Goods.Id("herbs"), GBamboo = Goods.Id("bamboo"), GHoney = Goods.Id("honey"), GMilk = Goods.Id("milk"), GEggs = Goods.Id("eggs");

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

    static bool Serves(ItemDef d, Activity act)
    {
        foreach (var (a, _) in d.Uses) if (a == act) return true;
        return false;
    }

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

    ushort PickGood((ushort good, float weight)[] options)
    {
        float total = 0f;
        foreach (var o in options) total += o.weight;
        float r = Rng.NextFloat() * total;
        foreach (var o in options) { if (r < o.weight) return o.good; r -= o.weight; }
        return options[^1].good;
    }

    void ForageYield(Agent a, int site, float value, in DayContext c)
    {
        var w = World;
        var foods = PlantFoods(w.Biome[site], c.Season);

        for (int k = 0; k < 3 && value > 0.01f; k++)
        {
            ushort g = PickGood(foods);
            float part = k == 2 ? value : value * Rng.Range(0.3f, 0.7f);
            a.Pack.Add(g, part / MathF.Max(0.1f, Goods.All[g].Food));
            value -= part;
        }
        if (w.TreesK[site] > 0f && Rng.Chance(0.6f)) a.Pack.Add(GWood, 0.4f * w.Trees[site] / MathF.Max(1f, w.TreesK[site]) + 0.1f);
        if (Rng.Chance(0.25f)) a.Pack.Add(w.Biome[site] is Biome.Wetland ? GReed : GFiber, 0.3f);
        if (w.Biome[site] is Biome.TropicalRainforest or Biome.TropicalSeasonalForest && Rng.Chance(0.1f)) a.Pack.Add(GBamboo, 0.3f);
    }

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

    int FindTile(Band band, in DayContext c, int kind, float minimum)
    {
        int best = -1;
        float bestScore = float.MinValue;
        int r = Math.Max(1, c.Range) * (kind == StoneStock || kind == OreStock ? 2 : 1);
        for (int k = 0; k < 8; k++)
        {
            int t = Around(band.Cell, r);
            float left = Left(kind, t);
            if (left <= minimum) continue;

            float score = left - World.Distance(t, band.Cell) * (kind == Timber ? 1.5f : 0.5f);
            if (score > bestScore) { bestScore = score; best = t; }
        }
        return best;
    }

    void Chop(Agent a, Band band, in DayContext c)
    {
        int site = FindTile(band, c, Timber, 0.5f);
        if (site < 0) { a.Site = band.Cell; return; }
        a.Site = site;
        bool axe = HeldTool(a, Activity.Chop) != null;
        float logs = Take(Timber, site, (axe ? 1.2f : 0.35f) * ToolBonus(a, Activity.Chop) * (0.6f + 0.4f * a.Gene(Gene.Strength)));
        a.Pack.Add(GLog, logs);
        a.Pack.Add(GWood, logs * 0.5f);
        a.Needs[(int)Need.Energy] += 0.2f;
        Psychology.Practice(a, Domain.Building, logs > 0.4f, 0.4f);
    }

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
        options.Add((GHerbs, 0.1f + (BandKnowsCached(c, TechId.Herbalism) ? 0.3f : 0f)));
        options.Add((GFiber, 0.15f));
        ushort g = PickGood(options.ToArray());
        a.Pack.Add(g, 1.6f * ToolBonus(a, Activity.Gather));
        a.Needs[(int)Need.Energy] += 0.15f;
    }

    internal static float Shortage(Band band, ushort good)
    {
        if (band.Targets.Length <= good) return 0f;
        float want = band.Targets[good];
        if (want <= 0f) return 0f;
        return Math.Clamp((want - band.Stores[good]) / want, 0f, 1f);
    }

    static readonly ushort[] Ores = { Goods.Id("copper_ore"), Goods.Id("tin_ore"), Goods.Id("iron_ore"), Goods.Id("coal"), Goods.Id("gold_ore") };

    static float ShortageOf(Band band, params ushort[] goods)
    {
        float s = 0f;
        foreach (var g in goods) s = MathF.Max(s, Shortage(band, g));
        return s;
    }

    void DiscardClutter(Band band)
    {
        foreach (var (good, qty) in band.Stores.Held().ToList())
        {
            if (good.Kind == GoodKind.Food) continue;
            float keep = MathF.Max(10f, (band.Targets.Length > good.Index ? band.Targets[good.Index] : 0f) * 2.5f) * (band.Sedentary ? 3f : 1f);
            if (qty > keep) band.Stores.TransferTo(GroundAt(band.Cell), good.Index, qty - keep);
        }
    }

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

    void UpdateTargets(Band band, TechSet known)
    {
        if (band.Targets.Length != Goods.Count) band.Targets = new float[Goods.Count];
        var t = band.Targets;
        Array.Clear(t);
        int n = band.Members.Count;
        bool cold = World.TempMean[band.Cell] - World.TempAmp[band.Cell] < 8f;
        if (known.Has(TechId.ControlledFire)) t[GWood] = n * (cold ? 0.12f : 0.04f) * 20f;
        if (known.Has(TechId.StoneTools)) t[GFlint] = 4f;
        if (known.Has(TechId.Baskets)) t[GReed] = 4f;
        if (known.Has(TechId.Cordage)) t[GFiber] = 4f;
        if (known.Has(TechId.Herbalism)) t[GHerbs] = 3f;
        foreach (var job in Production.Values.Where(j => j.BandId == band.Id && j.Active))
            foreach (var (g, q) in Product(job.Product)!.Inputs) t[g] += MathF.Max(0f, q * job.Remaining - job.Inputs[g]);
        foreach (var road in RoadProjects.Values.Where(r => r.BandId == band.Id && r.Active && r.Surface >= 2))
            t[Goods.Id(road.Surface == 3 ? "stone_blocks" : "stone")] += (road.Cells.Length - road.Index) * (road.Surface == 3 ? 1f : 0.3f);
        if (BandPlans.GetValueOrDefault(band.Id)?.Plan.Utilities?.FuelPower == true)
            t[Goods.Id("coal")] += band.Structures.Count(id => Structures.TryGetValue(id, out var generator) && generator.Standing && generator.Kind.Id == "generator") * Config.PlanDays * 0.5f;
        foreach (int sid in band.Structures)
            if (Structures.TryGetValue(sid, out var s) && s.State is StructureState.Planned or StructureState.Building)
            {
                float left = 1f - s.Progress / MathF.Max(0.1f, s.Kind.Labour);
                foreach (var (g, q) in s.Materials) t[g] += q * left;
            }

        foreach (int id in band.Members)
        {
            if (!Living.TryGetValue(id, out var a) || a.IsChild(Day)) continue;
            if (WantedItem(a, band, known) is { } want)
                foreach (var (g, q) in want.Inputs) t[g] = MathF.Min(t[g] + q * 0.5f, 40f);
        }

        foreach (var r in Crafts.Recipes)
        {
            if (!known.Has(r.Tech)) continue;
            foreach (var (g, q) in r.Outputs)
                if (t[g] > band.Stores[g])
                    foreach (var (ig, iq) in r.Inputs) t[ig] = MathF.Max(t[ig], iq * MathF.Ceiling((t[g] - band.Stores[g]) / q));
        }
        if (band.Sedentary) { t[GWood] *= 1.5f; t[GStone] = MathF.Max(t[GStone], 6f); t[GLog] = MathF.Max(t[GLog], 6f); }
    }

    ItemDef? WantedItem(Agent a, Band band, TechSet known, Band? from = null)
    {
        foreach (var (layer, preference) in a.Appearance.Garments.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (preference.Part == null || !Enum.TryParse<ItemSlot>(layer, true, out var slot) || Worn(a, slot)?.Id == preference.Part) continue;
            var wanted = Crafts.Item(slot, preference.Part);
            if (wanted != null && Crafts.CanMake(known, wanted) && (from == null || HasInputs(from, wanted.Inputs))) return wanted;
        }

        ItemDef? BestItem(TechSet k, ItemSlot slot, ItemMeasure measure, Activity act = default)
        {
            foreach (var d in Ranked(k, slot, measure, act))
                if (from == null || HasInputs(from, d.Inputs)) return d;
            return null;
        }
        float cold = MathF.Max(0f, 12f - (World.TempMean[band.Cell] - World.TempAmp[band.Cell] * 0.7f));
        if (Warmth(a) < cold)
            for (var slot = ItemSlot.Top; slot >= ItemSlot.Legwear; slot--)
                if (Worn(a, slot) is null && BestItem(known, slot, ItemMeasure.Warmth) is { } warm) return warm;
        if (Worn(a, ItemSlot.Top) is null && BestItem(known, ItemSlot.Top, ItemMeasure.Clothing) is { } top) return top;
        if (Worn(a, ItemSlot.Legwear) is null && BestItem(known, ItemSlot.Legwear, ItemMeasure.Clothing) is { } legs) return legs;
        var main = a.MainActivity();
        int tools = 0;
        foreach (var item in a.Gear) if (item.Kind.Slot == ItemSlot.Tool) tools++;
        if (tools < 3)
        {
            if (HeldTool(a, main) is null && BestItem(known, ItemSlot.Tool, ItemMeasure.Use, main) is { } tool) return tool;
            if (main == Activity.Hunt && WeaponPower(a) <= 0f && BestItem(known, ItemSlot.Tool, ItemMeasure.Power) is { } weapon) return weapon;
        }
        if (Worn(a, ItemSlot.Footwear) is null && BestItem(known, ItemSlot.Footwear, ItemMeasure.Shoes) is { } shoes) return shoes;
        if (band.LeaderId == a.Id && GearPrestige(a) < 0.3f && BestItem(known, ItemSlot.Headwear, ItemMeasure.Prestige) is { } crown) return crown;
        if (Day - band.LastRaidedDay < 2 * SimConfig.DaysPerYear && a.Id % 2 == 0 && Protection(a) < 0.1f
            && BestItem(known, ItemSlot.Armour, ItemMeasure.Protection) is { } armour) return armour;
        return null;
    }

    enum ItemMeasure : byte { Warmth, Clothing, Shoes, Use, Power, Prestige, Protection }

    static float Measure(ItemDef d, ItemMeasure m, Activity act) => m switch
    {
        ItemMeasure.Warmth => d.Warmth,
        ItemMeasure.Clothing => d.Warmth + 1f,
        ItemMeasure.Shoes => d.Warmth + 0.5f,
        ItemMeasure.Use => Serves(d, act) ? Crafts.Use(d, act) : 0f,
        ItemMeasure.Power => d.Power,
        ItemMeasure.Prestige => d.Prestige,
        _ => d.Protection,
    };

    static readonly System.Collections.Concurrent.ConcurrentDictionary<(TechSet, ItemSlot, ItemMeasure, Activity), ItemDef[]> RankedCache = new();

    static ItemDef[] Ranked(TechSet known, ItemSlot slot, ItemMeasure measure, Activity act)
    {
        if (measure != ItemMeasure.Use) act = default;
        if (RankedCache.TryGetValue((known, slot, measure, act), out var hit)) return hit;
        if (RankedCache.Count > 100_000) RankedCache.Clear();
        var ranked = new List<(ItemDef d, float s, int i)>();
        var items = Crafts.BySlot[(int)slot];
        for (int i = 0; i < items.Length; i++)
        {
            var d = items[i];
            if (!Crafts.CanMake(known, d)) continue;
            float s = Measure(d, measure, act);
            if (s <= 0f) continue;
            ranked.Add((d, s + d.Days / 20000f, i));
        }
        var result = ranked.OrderByDescending(r => r.s).ThenBy(r => r.i).Select(r => r.d).ToArray();
        RankedCache[(known, slot, measure, act)] = result;
        return result;
    }

    void CraftWork(Agent a, Band band, in DayContext c)
    {
        if (WorkProduction(a, band)) return;
        if (Config.RequireModel)
        {
            // Without an order a crafter only tinkers: what to make is the council's choice.
            Psychology.Practice(a, Domain.Crafting, Rng.Chance(0.5f), 0.3f);
            a.Needs[(int)Need.Energy] += 0.06f;
            return;
        }
        float skill = (0.6f + 0.6f * a.Efficacy[(int)Domain.Crafting]) * ToolBonus(a, Activity.Craft);

        for (int k = 0; k < Math.Min(8, band.Members.Count); k++)
        {
            int id = band.Members[Rng.Next(band.Members.Count)];
            if (!Living.TryGetValue(id, out var m) || m.IsChild(Day)) continue;
            if (WantedItem(m, band, a.Knows, band) is not { } want || !HasStation(band, want.Station, out int at)) continue;
            if (!ConsumePower(band.Id, Product("gear/" + Ids.Snake(want.Slot) + "/" + want.Id)!.Power)) continue;
            foreach (var (g, q) in want.Inputs) band.Stores.Take(g, q);
            Equip(m, band, want);
            a.Site = at >= 0 ? at : band.Cell;
            Psychology.Practice(a, Domain.Crafting, true, 0.6f);
            a.Needs[(int)Need.Energy] += 0.1f;
            return;
        }

        Recipe? best = null;
        float bestNeed = 0.05f;
        int site = band.Cell;
        foreach (var r in Crafts.Recipes)
        {
            if (!a.Knows.Has(r.Tech) || !HasInputs(band, r.Inputs) || !HasStation(band, r.Station, out int st)) continue;
            float need = 0f;
            foreach (var (g, _) in r.Outputs) need = MathF.Max(need, Shortage(band, g));

            foreach (var (g, q) in r.Inputs)
                if (Goods.All[g].Spoil > 0.05f && Goods.All[g].Food > 0f && band.Stores[g] > q * 2f) need = MathF.Max(need, 0.7f);
            if (need > bestNeed) { bestNeed = need; best = r; site = st >= 0 ? st : band.Cell; }
        }
        a.Site = site;
        a.Needs[(int)Need.Energy] += 0.08f;
        if (best == null) { Psychology.Practice(a, Domain.Crafting, Rng.Chance(0.5f), 0.3f); return; }
        float batches = MathF.Max(0.5f, skill / MathF.Max(0.1f, best.Labour) * 0.5f);
        foreach (var (g, q) in best.Inputs) batches = MathF.Min(batches, band.Stores[g] / q);
        if (!ConsumePower(band.Id, Product("recipe/" + best.Id)!.Power * batches)) return;
        foreach (var (g, q) in best.Inputs) band.Stores.Take(g, q * batches);
        foreach (var (g, q) in best.Outputs) band.Stores.Add(g, q * batches);
        Psychology.Practice(a, best.Domain, true, 0.6f);
    }

    static bool HasInputs(Band band, (ushort good, float qty)[] inputs)
    {
        foreach (var (g, q) in inputs) if (!band.Stores.Has(g, q)) return false;
        return true;
    }

    [ThreadStatic] static Dictionary<string, int>? t_stations;
    [ThreadStatic] static Band? t_stationsBand;
    [ThreadStatic] static int t_stationsDay;

    bool HasStation(Band band, string station, out int cell)
    {
        cell = -1;
        if (station.Length == 0) return true;
        if (t_buffer != null)
        {
            var map = t_stations ??= new Dictionary<string, int>();
            if (t_stationsBand != band || t_stationsDay != Day)
            {
                map.Clear();
                foreach (int sid in band.Structures)
                    if (Structures.TryGetValue(sid, out var st) && st.Standing) map.TryAdd(st.Kind.Id, st.Cell);
                t_stationsBand = band; t_stationsDay = Day;
            }
            if (map.TryGetValue(station, out int at)) { cell = at; return true; }
            return false;
        }
        foreach (int sid in band.Structures)
            if (Structures.TryGetValue(sid, out var s) && s.Standing && s.Kind.Id == station) { cell = s.Cell; return true; }
        return false;
    }

    [ThreadStatic] static List<Structure>? t_fields;
    [ThreadStatic] static Band? t_fieldsBand;
    [ThreadStatic] static int t_fieldsDay;

    List<Structure> FieldsOf(Band band)
    {
        var fields = t_fields ??= new List<Structure>();
        if (t_fieldsBand == band && t_fieldsDay == Day) return fields;
        fields.Clear();
        foreach (int sid in band.Structures)
            if (Structures.TryGetValue(sid, out var s) && s.Standing && s.Kind.Role == StructureRole.Field) fields.Add(s);
        t_fieldsBand = band; t_fieldsDay = Day;
        return fields;
    }

    void FarmWork(Agent a, Band band, in DayContext c)
    {
        Structure? field = null;
        foreach (var s in FieldsOf(band))
            if (s.Standing && (field == null || s.Growth < field.Growth)) field = s;
        if (field == null) { a.Site = band.Cell; return; }
        a.Site = field.Cell;
        float growth = World.Growth(field.Cell, Day);
        float work = (0.6f + 0.4f * a.Efficacy[(int)Domain.Farming]) * ToolBonus(a, Activity.Farm) * (BandKnowsCached(c, TechId.Plough) ? 1.25f : 1f)
                     * (World.Precip[field.Cell] < 600f && BandKnowsCached(c, TechId.Irrigation) ? 1.3f : 1f);
        if (growth > 0.15f)
        {
            if (field.Growth <= 0f) { field.Crop = CropFor(field); field.Growth = 0.02f; }
            field.Growth = MathF.Min(1.05f, field.Growth + 0.04f * MathF.Min(1.2f, growth + 0.2f) * work);
            field.Yield += 6.5f * MathF.Min(1.2f, growth + 0.2f) * MathF.Min(World.YearRain[field.Cell], 1.1f) * work;
        }
        if (field.Growth >= 1f || (growth < 0.1f && field.Growth > 0.3f))
        {
            ushort g = Goods.Id(field.Crop.Length > 0 ? field.Crop : "grain");
            band.Stores.Add(g, field.Yield / MathF.Max(0.2f, Goods.All[g].Food));
            if (Goods.All[g].Food > 0.5f) band.Stores.Add(GFiber, field.Yield * 0.05f);
            field.Growth = 0f; field.Yield = 0f;
        }
        Psychology.Practice(a, Domain.Farming, true, 0.5f);
        a.Needs[(int)Need.Energy] += 0.16f;
    }

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

    internal string HerdSpecies(int cell) => World.Biome[cell] switch
    {
        Biome.Alpine or Biome.Shrubland => "goat",
        Biome.TemperateGrassland or Biome.Savanna => "cattle",
        Biome.HotDesert or Biome.ColdDesert => "camel",
        Biome.Tundra or Biome.BorealForest => "horse",
        Biome.TropicalRainforest or Biome.TropicalSeasonalForest => "pig",
        _ => "sheep",
    };

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
        if (BandKnowsCached(c, TechId.Beekeeping) && HasStation(band, "beehive", out _)) band.Stores.Add(GHoney, 0.1f);
        a.Needs[(int)Need.Energy] += 0.1f;
    }

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

    void MaybeCapture(Band band, int site)
    {
        if (!BandKnows(band, TechId.Domestication) || !Rng.Chance(0.12f)) return;
        string species = Rng.Chance(0.3f) ? (Rng.Chance(0.5f) ? "dog" : "fowl") : HerdSpecies(site);
        float have = band.Herds.GetValueOrDefault(species);
        band.Herds[species] = have + Rng.Range(1, 3);
    }

    void Deposit(Agent a, Band band, float keep)
    {
        if (!AtCamp(a, band)) return;
        if (HaulFor(a.Id) != null) return;
        if (a.Pack is not { } pack) return;
        pack.MoveNonFood(band.Stores);
        float food = pack.FoodValue;
        if (food > keep) pack.GiveFood(band.Stores, (food - keep));
    }

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

        float food = band.Stores.FoodValue, room = 20f + n * (band.Shelter > 0f ? 12f : 6f) + band.StorageRoom;
        float overflow = food > room ? MathF.Min(2.5f, 1f + 0.5f * (food - room) / room) : 1f;
        band.Stores.Spoil(0.3f * heat * kept * overflow);
    }
}
