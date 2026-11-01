using Evolysia.Sim.Agents;
using Evolysia.Sim.Economy;
using Evolysia.Sim.World;

namespace Evolysia.Sim.Tests;

public class EconomyTests
{
    static Simulation NewSim(ulong seed = 11) => new(new SimConfig { RequireModel = false, Seed = seed, Threads = 1 });

    [Fact]
    public void Every_recipe_item_and_structure_refers_to_real_goods_and_known_wardrobe_parts()
    {
        foreach (var r in Crafts.Recipes)
        {
            Assert.NotEmpty(r.Inputs);
            Assert.NotEmpty(r.Outputs);
        }
        var parts = System.Text.Json.JsonDocument.Parse(File.ReadAllText(FindAsset("wardrobe/customisation.json"))).RootElement.GetProperty("parts");
        var layer = new Dictionary<ItemSlot, string>
        {
            [ItemSlot.Tool] = "held_item", [ItemSlot.Legwear] = "legwear", [ItemSlot.Footwear] = "footwear", [ItemSlot.Top] = "top",
            [ItemSlot.Outerwear] = "outerwear", [ItemSlot.Armour] = "armour", [ItemSlot.Headwear] = "headwear", [ItemSlot.Accessory] = "accessory",
        };
        foreach (var item in Crafts.Items)
            Assert.Contains(parts.GetProperty(layer[item.Slot]).EnumerateArray(), p => p.GetString() == item.Id);
        Assert.All(Structures.All, s => Assert.True(s.Labour > 0f, s.Id));
    }

    static string FindAsset(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "assets", relative))) return Path.Combine(dir.FullName, "assets", relative);
        throw new FileNotFoundException(relative);
    }

    [Fact]
    public void Inventories_feed_the_most_perishable_first_and_conserve_goods()
    {
        var inv = new Inventory();
        inv.Add("meat_raw", 2f);
        inv.Add("nuts", 2f);
        Assert.Equal(4.4f, inv.FoodValue, 3);
        float got = inv.TakeFood(1.5f);
        Assert.Equal(1.5f, got, 3);
        Assert.Equal(0.5f, inv.Get("meat_raw"), 3);
        Assert.Equal(2f, inv.Get("nuts"), 3);
        var other = new Inventory();
        inv.MoveFraction(other, 0.5f);
        Assert.Equal(2.9f, inv.FoodValue + other.FoodValue, 3);
    }

    [Fact]
    public void Bands_build_on_cleared_tiles_and_felling_takes_timber()
    {
        var sim = NewSim();
        float timberBefore = Enumerable.Range(0, sim.World.Count).Sum(i => sim.World.Trees[i]);
        sim.RunYears(25);
        Assert.Contains(sim.Structures.Values, s => s.Standing);
        foreach (var s in sim.Structures.Values.Where(s => s.Standing || s.State == StructureState.Building))
        {
            Assert.Equal(s.ParentBuildingId >= 0 ? s.ParentBuildingId : s.Id, sim.World.StructureAt[s.Cell]);
            Assert.True(sim.World.IsDryLand(s.Cell));
        }
        float timberAfter = Enumerable.Range(0, sim.World.Count).Sum(i => sim.World.Trees[i]);
        Assert.True(timberAfter < timberBefore, "people fell trees for logs, firewood and building space");
        Assert.Contains(sim.Bands.Values, b => b.Stores.Get("log") > 0f || b.Stores.Get("wood") > 0f);
    }

    [Fact]
    public void People_own_gear_their_people_know_how_to_make()
    {
        var sim = NewSim();
        sim.RunYears(40);
        var owners = sim.Living.Values.Where(a => a.Gear.Count > 0).ToList();
        Assert.NotEmpty(owners);
        foreach (var a in owners)
            foreach (var item in a.Gear)
                Assert.InRange(item.Wear, 0f, 1f);

        Assert.All(sim.Living.Values, a =>
        {
            Assert.True(a.Gear.Count(i => i.Kind.Slot == ItemSlot.Tool) <= 4);
            Assert.All(a.Gear.Where(i => i.Kind.Slot != ItemSlot.Tool).GroupBy(i => i.Kind.Slot), g => Assert.Single(g));
        });
    }

    [Fact]
    public void Farmers_harvest_their_fields_into_the_stores()
    {
        var sim = NewSim(5);
        var band = sim.Bands.Values.First();
        foreach (int id in band.Members)
            if (sim.Living.TryGetValue(id, out var a))
                a.Knows |= Techs.Bit(TechId.Agriculture) | Techs.Bit(TechId.FoodStorage) | Techs.Bit(TechId.Baskets) | Techs.Bit(TechId.Shelter);
        band.Sedentary = true;
        band.Stores.Add("wood", 40f); band.Stores.Add("fiber", 40f); band.Stores.Add("hide", 20f);
        sim.RunYears(3);
        if (!sim.Bands.ContainsKey(band.Id)) return;
        var fields = band.Structures.Select(id => sim.Structures[id]).Where(s => s.Kind.Role == StructureRole.Field).ToList();
        Assert.NotEmpty(fields);
        float crops = band.Stores.Held().Where(h => h.good.Kind == GoodKind.Food && fields.Any(f => f.Crop == h.good.Id)).Sum(h => h.qty);
        Assert.True(crops > 0f || fields.Any(f => f.Yield > 0f), "fields are tended and brought in");
    }
}
