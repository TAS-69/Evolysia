using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;

namespace Evolysia.Sim.Economy;

/// <summary>Where an item is worn or carried. Clothing slots match the wardrobe layers.</summary>
public enum ItemSlot : byte { Tool, Legwear, Footwear, Top, Outerwear, Armour, Headwear, Accessory }

/// <summary>
/// A kind of tool or clothing piece. Ids are wardrobe part ids (assets/wardrobe/customisation.json),
/// so what someone owns is exactly what is drawn on them.
/// </summary>
/// <param name="Warmth">Degrees of cold it keeps off when worn.</param>
/// <param name="Protection">0..1 of a blow it turns aside (armour, helmets, shields).</param>
/// <param name="Prestige">Standing it lends (ceremonial and royal dress).</param>
/// <param name="Power">Fighting strength added when held (weapons).</param>
/// <param name="Days">Days of use before it wears out.</param>
public sealed record ItemDef(ushort Index, string Id, ItemSlot Slot, TechId[] Techs, (ushort good, float qty)[] Inputs, float Labour,
                             float Warmth, float Protection, float Prestige, float Power, int Days, (Activity act, float mult)[] Uses, string Station);

/// <summary>An owned tool or clothing piece and how worn it is (0 new .. 1 worn out).</summary>
public struct Item
{
    public ushort Def;
    public float Wear;
    public Item(ushort def) { Def = def; Wear = 0f; }
    public readonly ItemDef Kind => Crafts.Items[Def];
}

/// <summary>Turning goods into goods: inputs, outputs, the technique, the workstation and the labour.</summary>
public sealed record Recipe(string Id, TechId Tech, string Station, (ushort good, float qty)[] Inputs, (ushort good, float qty)[] Outputs,
                            float Labour, Domain Domain);

public static class Crafts
{
    public static readonly ItemDef[] Items;
    public static readonly Recipe[] Recipes;
    static readonly Dictionary<string, ushort> ItemByName = new();

    static (ushort, float)[] In(string spec) =>
        spec.Length == 0 ? Array.Empty<(ushort, float)>()
        : spec.Split(',').Select(p => p.Split(':')).Select(p => (Goods.Id(p[0].Trim()), float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture))).ToArray();

    static readonly List<ItemDef> _items = new();

    static void I(string id, ItemSlot slot, string inputs, TechId[] techs, float warmth = 0f, float protection = 0f, float prestige = 0f,
                  float power = 0f, int days = 600, float labour = 0.5f, string station = "", params (Activity, float)[] uses)
    {
        var def = new ItemDef((ushort)_items.Count, id, slot, techs, In(inputs), labour, warmth, protection, prestige, power, days, uses, station);
        ItemByName[$"{slot}:{id}"] = def.Index;
        _items.Add(def);
    }

    static TechId[] T(params TechId[] t) => t;

    static Crafts()
    {
        var A = Activity.Hunt;
        // ---- tools (held_item) ----------------------------------------------------------------
        I("club", ItemSlot.Tool, "wood:1", T(TechId.StoneTools), power: 0.06f, days: 400, labour: 0.2f, uses: (A, 1.0f));
        I("knife", ItemSlot.Tool, "flint:1,wood:0.2", T(TechId.StoneTools), power: 0.03f, days: 300, labour: 0.3f, uses: new[] { (Activity.Craft, 1.15f), (Activity.Hunt, 1.05f) });
        I("spear", ItemSlot.Tool, "wood:1,flint:0.5", T(TechId.Spear), power: 0.12f, days: 400, labour: 0.4f, uses: new[] { (A, 1.25f), (Activity.Fish, 1.1f) });
        I("stone_axe", ItemSlot.Tool, "wood:0.5,flint:1,fiber:0.2", T(TechId.Woodcutting), power: 0.06f, days: 350, labour: 0.5f, uses: new[] { (Activity.Chop, 1.0f), (Activity.Build, 1.1f) });
        I("basket", ItemSlot.Tool, "fiber:1.5", T(TechId.Baskets), days: 250, labour: 0.4f, uses: new[] { (Activity.Forage, 1.2f), (Activity.Gather, 1.2f) });
        I("torch", ItemSlot.Tool, "wood:0.5,fiber:0.2", T(TechId.ControlledFire), days: 40, labour: 0.1f, uses: (Activity.Explore, 1.1f));
        I("bow", ItemSlot.Tool, "wood:1,fiber:0.5,flint:0.3", T(TechId.Bow), power: 0.08f, days: 500, labour: 0.8f, uses: (A, 1.45f));
        I("fishing_rod", ItemSlot.Tool, "wood:0.5,fiber:0.3,bone:0.1", T(TechId.Fishing), days: 300, labour: 0.3f, uses: (Activity.Fish, 1.3f));
        I("net", ItemSlot.Tool, "rope:2", T(TechId.Netting), days: 400, labour: 0.8f, uses: (Activity.Fish, 1.7f));
        I("staff", ItemSlot.Tool, "wood:1", T(TechId.Carving), power: 0.04f, days: 900, labour: 0.3f, uses: new[] { (Activity.Herd, 1.15f), (Activity.Ritual, 1.1f), (Activity.Explore, 1.1f) });
        I("medicine_bag", ItemSlot.Tool, "leather:0.5,herbs:1", T(TechId.Herbalism), days: 300, labour: 0.3f, uses: (Activity.CareChild, 1.1f));
        I("hoe", ItemSlot.Tool, "wood:1,flint:0.5", T(TechId.Agriculture), days: 400, labour: 0.4f, uses: (Activity.Farm, 1.3f));
        I("pick", ItemSlot.Tool, "wood:1,antler:0.5", T(TechId.Quarrying), days: 400, labour: 0.5f, uses: new[] { (Activity.Quarry, 1.5f), (Activity.Mine, 1.3f) });
        I("hammer", ItemSlot.Tool, "wood:0.5,stone:0.5", T(TechId.Carpentry), power: 0.05f, days: 800, labour: 0.4f, uses: new[] { (Activity.Build, 1.3f), (Activity.Craft, 1.05f) });
        I("shovel", ItemSlot.Tool, "wood:1.5", T(TechId.Agriculture, TechId.Carpentry), days: 600, labour: 0.4f, uses: new[] { (Activity.Farm, 1.2f), (Activity.Gather, 1.15f), (Activity.Mine, 1.1f) });
        I("axe", ItemSlot.Tool, "wood:0.5,copper_ingot:1", T(TechId.Copper), power: 0.1f, days: 1200, labour: 0.6f, station: "anvil", uses: new[] { (Activity.Chop, 1.7f), (Activity.Build, 1.2f) });
        I("chisel", ItemSlot.Tool, "copper_ingot:0.5,wood:0.2", T(TechId.Copper), days: 1200, labour: 0.4f, station: "anvil", uses: new[] { (Activity.Craft, 1.25f), (Activity.Quarry, 1.2f) });
        I("tongs", ItemSlot.Tool, "copper_ingot:0.5", T(TechId.Copper), days: 1500, labour: 0.4f, station: "anvil", uses: (Activity.Craft, 1.2f));
        I("mace", ItemSlot.Tool, "bronze_ingot:1,wood:0.5", T(TechId.Bronze), power: 0.22f, days: 1500, labour: 0.6f, station: "anvil", uses: (A, 1.1f));
        I("sword", ItemSlot.Tool, "bronze_ingot:1.5,leather:0.2", T(TechId.Swords), power: 0.3f, prestige: 0.1f, days: 1500, labour: 1f, station: "anvil", uses: (A, 1.15f));
        I("shield", ItemSlot.Tool, "wood:1.5,leather:0.5", T(TechId.Fortification), protection: 0.15f, days: 700, labour: 0.6f);
        I("book", ItemSlot.Tool, "leather:0.3,cloth:0.5", T(TechId.Writing), prestige: 0.1f, days: 2000, labour: 1f, uses: (Activity.Teach, 1.4f));
        I("measuring_rod", ItemSlot.Tool, "wood:0.5", T(TechId.Counting), days: 2000, labour: 0.2f, uses: new[] { (Activity.Build, 1.15f), (Activity.Farm, 1.05f) });
        I("construction_plan", ItemSlot.Tool, "cloth:0.3", T(TechId.Writing, TechId.Masonry), days: 2000, labour: 0.8f, uses: (Activity.Build, 1.4f));
        I("crossbow", ItemSlot.Tool, "wood:1,iron_ingot:0.5,rope:0.3", T(TechId.Crossbow), power: 0.25f, days: 1500, labour: 1.2f, station: "workbench", uses: (A, 1.6f));
        I("wand", ItemSlot.Tool, "wood:0.3,ochre:0.2", T(TechId.Mysticism), prestige: 0.15f, days: 3000, labour: 0.5f, uses: (Activity.Ritual, 1.2f));
        I("orb_focus", ItemSlot.Tool, "glass:1,copper_ingot:0.3", T(TechId.Mysticism, TechId.Glass), prestige: 0.25f, days: 4000, labour: 1f, uses: (Activity.Ritual, 1.3f));
        I("wrench", ItemSlot.Tool, "steel:0.5", T(TechId.Machinery), days: 3000, labour: 0.5f, station: "workbench", uses: new[] { (Activity.Build, 1.25f), (Activity.Craft, 1.2f) });
        I("rifle", ItemSlot.Tool, "steel:1,wood:0.5,components:0.5", T(TechId.Firearms), power: 0.5f, days: 3000, labour: 1.5f, station: "lathe", uses: (A, 1.9f));
        I("pistol", ItemSlot.Tool, "steel:0.5,components:0.5", T(TechId.Firearms), power: 0.4f, days: 3000, labour: 1.2f, station: "lathe", uses: (A, 1.4f));
        I("energy_tool", ItemSlot.Tool, "advanced_alloy:0.5,battery:1,electronics:0.5", T(TechId.EnergyTools), days: 5000, labour: 1.5f, station: "fabricator",
          uses: new[] { (Activity.Chop, 2.6f), (Activity.Quarry, 2.6f), (Activity.Mine, 2.6f), (Activity.Build, 2f), (Activity.Craft, 1.8f) });
        I("energy_blade", ItemSlot.Tool, "advanced_alloy:1,battery:1", T(TechId.EnergyTools), power: 0.7f, days: 5000, labour: 1.5f, station: "fabricator", uses: (A, 1.5f));

        // ---- clothing --------------------------------------------------------------------------
        var L = ItemSlot.Legwear; var F = ItemSlot.Footwear; var Tp = ItemSlot.Top; var O = ItemSlot.Outerwear; var Ar = ItemSlot.Armour; var H = ItemSlot.Headwear; var Ac = ItemSlot.Accessory;
        I("wrap_skirt", L, "hide:1", T(TechId.HideClothing), warmth: 1f, days: 500, labour: 0.3f);
        I("fur_leggings", L, "fur:1.5", T(TechId.HideClothing), warmth: 5f, days: 600, labour: 0.5f);
        I("shorts", L, "cloth:0.5", T(TechId.Weaving), warmth: 0.5f, days: 500);
        I("skirt", L, "cloth:1", T(TechId.Weaving), warmth: 1.5f, days: 600);
        I("long_skirt", L, "cloth:1.5", T(TechId.Weaving), warmth: 2.5f, days: 700);
        I("trousers", L, "cloth:1.5", T(TechId.Weaving), warmth: 3f, days: 700);
        I("loose_trousers", L, "cloth:1.5", T(TechId.Weaving), warmth: 2f, days: 700);
        I("leggings", L, "cloth:1", T(TechId.Weaving), warmth: 2f, days: 600);
        I("padded_trousers", L, "cloth:2,fiber:1", T(TechId.Armour), warmth: 4f, protection: 0.05f, days: 800);
        I("work_trousers", L, "cloth:1.5", T(TechId.PowerLooms), warmth: 3f, days: 1200, labour: 0.2f);
        I("formal_trousers", L, "cloth:1.5", T(TechId.PowerLooms), warmth: 3f, prestige: 0.05f, days: 1200, labour: 0.2f);
        I("environment_trousers", L, "advanced_alloy:0.5,cloth:1", T(TechId.SpaceHabitats), warmth: 12f, protection: 0.05f, days: 3000, station: "fabricator");

        I("cloth_wraps", F, "fiber:0.5", T(TechId.Cordage), warmth: 1f, days: 200, labour: 0.2f);
        I("fur_boots", F, "fur:1", T(TechId.HideClothing), warmth: 4f, days: 500, labour: 0.4f);
        I("sandals", F, "leather:0.5", T(TechId.Tanning), warmth: 0.5f, days: 500, labour: 0.3f);
        I("soft_shoes", F, "leather:0.7", T(TechId.Tanning), warmth: 1.5f, days: 500, labour: 0.3f);
        I("leather_boots", F, "leather:1", T(TechId.Tanning), warmth: 3f, days: 800, labour: 0.5f);
        I("ceremonial_shoes", F, "leather:0.7,ochre:0.2", T(TechId.Priesthood), warmth: 1.5f, prestige: 0.05f, days: 1000);
        I("work_boots", F, "leather:1,steel:0.1", T(TechId.Machinery), warmth: 3f, protection: 0.02f, days: 1500);
        I("metal_boots", F, "steel:0.8,leather:0.3", T(TechId.Steel, TechId.Armour), warmth: 2f, protection: 0.05f, days: 2000, station: "anvil");
        I("water_shoes", F, "advanced_alloy:0.2", T(TechId.AdvancedMaterials), warmth: 1f, days: 2000);
        I("environment_boots", F, "advanced_alloy:0.5", T(TechId.SpaceHabitats), warmth: 6f, days: 3000, station: "fabricator");

        I("hide_wrap", Tp, "hide:1.5", T(TechId.HideClothing), warmth: 6f, days: 500, labour: 0.4f);
        I("fur_coat", Tp, "fur:3,hide:1", T(TechId.HideClothing), warmth: 14f, days: 700, labour: 0.8f);
        I("hunter_vest", Tp, "leather:1.5", T(TechId.Tanning), warmth: 4f, protection: 0.03f, days: 800, labour: 0.5f);
        I("woven_tunic", Tp, "cloth:1.5", T(TechId.Weaving), warmth: 4f, days: 600);
        I("linen_shirt", Tp, "cloth:1", T(TechId.Weaving), warmth: 2f, days: 600);
        I("wrap_shirt", Tp, "cloth:1", T(TechId.Weaving), warmth: 2.5f, days: 600);
        I("long_tunic", Tp, "cloth:2", T(TechId.Weaving), warmth: 5f, days: 700);
        I("desert_robe", Tp, "cloth:2", T(TechId.Weaving), warmth: 3f, days: 800);
        I("farmer_smock", Tp, "cloth:1.5", T(TechId.Weaving, TechId.Agriculture), warmth: 4f, days: 700);
        I("fisher_coat", Tp, "cloth:1.5,leather:0.5", T(TechId.Weaving, TechId.Netting), warmth: 6f, days: 800);
        I("winter_parka", Tp, "fur:2,cloth:1.5", T(TechId.Weaving, TechId.HideClothing), warmth: 18f, days: 900, labour: 0.8f);
        I("artisan_apron", Tp, "leather:1,cloth:0.5", T(TechId.Carpentry), warmth: 3f, days: 900, uses: (Activity.Craft, 1.05f));
        I("smith_apron", Tp, "leather:1.5", T(TechId.Copper, TechId.Tanning), warmth: 3f, protection: 0.03f, days: 1000, uses: (Activity.Craft, 1.05f));
        I("cook_apron", Tp, "cloth:1", T(TechId.Baking), warmth: 2f, days: 800);
        I("healer_coat", Tp, "cloth:1.5", T(TechId.Medicine), warmth: 4f, prestige: 0.05f, days: 900);
        I("scholar_robe", Tp, "cloth:2", T(TechId.Writing), warmth: 4f, prestige: 0.08f, days: 1000);
        I("traveller_coat", Tp, "cloth:2,leather:0.5", T(TechId.Trade), warmth: 7f, days: 900);
        I("merchant_coat", Tp, "cloth:2", T(TechId.Trade, TechId.Weaving), warmth: 5f, prestige: 0.08f, days: 1000);
        I("guard_tabard", Tp, "cloth:1.5", T(TechId.Fortification), warmth: 4f, protection: 0.02f, days: 900);
        I("ceremonial_robe", Tp, "cloth:2.5,ochre:0.3", T(TechId.Priesthood), warmth: 4f, prestige: 0.2f, days: 1500);
        I("royal_robe", Tp, "cloth:3,gold_ore:0.2", T(TechId.Monarchy), warmth: 5f, prestige: 0.4f, days: 2000);
        I("court_coat", Tp, "cloth:2.5", T(TechId.Governance), warmth: 5f, prestige: 0.15f, days: 1500);
        I("sailor_shirt", Tp, "cloth:1", T(TechId.Sailing), warmth: 3f, days: 800);
        I("mage_robe", Tp, "cloth:2.5,ochre:0.3", T(TechId.Mysticism), warmth: 4f, prestige: 0.15f, days: 1500);
        I("raincoat", Tp, "cloth:1.5,oil:0.3", T(TechId.Chemistry), warmth: 6f, days: 1500);
        I("work_shirt", Tp, "cloth:1", T(TechId.PowerLooms), warmth: 3f, days: 1200, labour: 0.2f);
        I("formal_jacket", Tp, "cloth:2", T(TechId.PowerLooms), warmth: 5f, prestige: 0.1f, days: 1500, labour: 0.3f);
        I("mechanic_coverall", Tp, "cloth:2", T(TechId.Machinery), warmth: 5f, days: 1500, labour: 0.3f, uses: (Activity.Build, 1.05f));
        I("engineer_jacket", Tp, "cloth:2,leather:0.5", T(TechId.SteamPower), warmth: 7f, days: 1500, labour: 0.3f);
        I("lab_coat", Tp, "cloth:1.5", T(TechId.Chemistry), warmth: 3f, prestige: 0.05f, days: 1500, labour: 0.3f, uses: (Activity.Experiment, 1.1f));
        I("flight_suit", Tp, "cloth:2,advanced_alloy:0.2", T(TechId.Aviation), warmth: 10f, days: 2000);
        I("environment_suit", Tp, "advanced_alloy:1,cloth:1,electronics:0.3", T(TechId.SpaceHabitats), warmth: 25f, protection: 0.1f, days: 3000, station: "fabricator");

        I("fur_mantle", O, "fur:2", T(TechId.HideClothing), warmth: 8f, days: 700, labour: 0.5f);
        I("short_cape", O, "cloth:1", T(TechId.Weaving), warmth: 2f, days: 700);
        I("long_cape", O, "cloth:2", T(TechId.Weaving), warmth: 4f, days: 800);
        I("hooded_cloak", O, "cloth:2", T(TechId.Weaving), warmth: 5f, days: 800);
        I("rain_cape", O, "cloth:1.5,reed:0.5", T(TechId.Weaving, TechId.Thatching), warmth: 3f, days: 600);
        I("desert_shawl", O, "cloth:1", T(TechId.Weaving), warmth: 1f, days: 800);
        I("traveller_mantle", O, "cloth:2,leather:0.5", T(TechId.Trade), warmth: 5f, days: 900);
        I("ceremonial_mantle", O, "cloth:2,ochre:0.3", T(TechId.Priesthood), warmth: 3f, prestige: 0.15f, days: 1500);
        I("royal_mantle", O, "cloth:2.5,fur:1,gold_ore:0.2", T(TechId.Monarchy), warmth: 6f, prestige: 0.35f, days: 2000);
        I("artisan_overcoat", O, "cloth:2,leather:0.5", T(TechId.Carpentry, TechId.Weaving), warmth: 5f, days: 1000);
        I("officer_coat", O, "cloth:2.5", T(TechId.Firearms), warmth: 6f, prestige: 0.15f, days: 1500);
        I("insulated_overcoat", O, "cloth:2,advanced_alloy:0.2", T(TechId.AdvancedMaterials), warmth: 16f, days: 2500);

        I("padded", Ar, "cloth:2,fiber:1.5", T(TechId.Armour), warmth: 3f, protection: 0.15f, days: 800);
        I("hide", Ar, "hide:2,leather:0.5", T(TechId.Armour), warmth: 3f, protection: 0.15f, days: 800);
        I("bone", Ar, "bone:3,rope:0.5", T(TechId.Armour, TechId.BoneTools), warmth: 1f, protection: 0.18f, days: 700);
        I("woven_guard", Ar, "reed:2,rope:0.5", T(TechId.Armour), warmth: 1f, protection: 0.12f, days: 500);
        I("wood_lamellar", Ar, "wood:2,rope:0.5", T(TechId.Armour, TechId.Carpentry), warmth: 1f, protection: 0.2f, days: 900);
        I("leather_scale", Ar, "leather:3", T(TechId.Armour, TechId.Tanning), warmth: 2f, protection: 0.22f, days: 1200);
        I("copper_scale", Ar, "copper_ingot:2,leather:1", T(TechId.Armour, TechId.Copper), warmth: 1f, protection: 0.28f, days: 1800, station: "anvil");
        I("iron_chain", Ar, "iron_ingot:2.5", T(TechId.Armour, TechId.Iron), warmth: 1f, protection: 0.35f, days: 2500, labour: 1.5f, station: "anvil");
        I("iron_plate", Ar, "steel:3,leather:0.5", T(TechId.Armour, TechId.Steel), protection: 0.45f, prestige: 0.05f, days: 3000, labour: 2f, station: "anvil");
        I("ceramic_plate", Ar, "ceramic:2,advanced_alloy:0.5", T(TechId.AdvancedMaterials), protection: 0.5f, days: 3000, station: "fabricator");
        I("composite", Ar, "advanced_alloy:2", T(TechId.AdvancedMaterials), protection: 0.55f, days: 4000, station: "fabricator");
        I("powered", Ar, "advanced_alloy:3,battery:2,electronics:1", T(TechId.Robotics), protection: 0.65f, power: 0.3f, days: 4000, labour: 2f, station: "fabricator");

        I("woven_hat", H, "reed:0.5", T(TechId.Baskets), warmth: 0.5f, days: 300, labour: 0.2f);
        I("fur_hat", H, "fur:0.5", T(TechId.HideClothing), warmth: 3f, days: 500, labour: 0.2f);
        I("feather_band", H, "fiber:0.2,ochre:0.1", T(TechId.Pigments), prestige: 0.05f, days: 400, labour: 0.2f);
        I("beaded_band", H, "bone:0.3,fiber:0.2", T(TechId.Carving), prestige: 0.05f, days: 800, labour: 0.3f);
        I("cloth_cap", H, "cloth:0.3", T(TechId.Weaving), warmth: 1f, days: 500, labour: 0.2f);
        I("wide_brim_hat", H, "cloth:0.5,reed:0.3", T(TechId.Weaving), warmth: 0.5f, days: 600, labour: 0.2f);
        I("hood", H, "cloth:0.5", T(TechId.Weaving), warmth: 2f, days: 600, labour: 0.2f);
        I("headwrap", H, "cloth:0.5", T(TechId.Weaving), warmth: 1f, days: 600, labour: 0.2f);
        I("veil", H, "cloth:0.3", T(TechId.Weaving), prestige: 0.03f, days: 600, labour: 0.2f);
        I("ceremonial_headdress", H, "fiber:0.5,ochre:0.3,bone:0.2", T(TechId.Priesthood), prestige: 0.2f, days: 1500);
        I("royal_circlet", H, "copper_ingot:0.5,gold_ore:0.1", T(TechId.Monarchy), prestige: 0.3f, days: 5000, station: "anvil");
        I("royal_crown", H, "gold_ore:0.5,bronze_ingot:0.3", T(TechId.Monarchy, TechId.Bronze), prestige: 0.45f, days: 8000, station: "anvil");
        I("leather_helmet", H, "leather:0.7", T(TechId.Armour, TechId.Tanning), warmth: 1f, protection: 0.06f, days: 900);
        I("bone_helmet", H, "bone:1,rope:0.2", T(TechId.Armour, TechId.BoneTools), protection: 0.06f, days: 700);
        I("copper_helmet", H, "copper_ingot:1", T(TechId.Armour, TechId.Copper), protection: 0.1f, days: 2000, station: "anvil");
        I("iron_helmet", H, "iron_ingot:1", T(TechId.Armour, TechId.Iron), protection: 0.12f, days: 2500, station: "anvil");
        I("guard_helmet", H, "iron_ingot:1,cloth:0.2", T(TechId.Fortification, TechId.Iron), protection: 0.13f, prestige: 0.03f, days: 2500, station: "anvil");
        I("visor_helmet", H, "steel:1.2", T(TechId.Armour, TechId.Steel), protection: 0.16f, days: 3000, station: "anvil");
        I("cook_hat", H, "cloth:0.3", T(TechId.Baking), days: 600, labour: 0.2f);
        I("scholar_cap", H, "cloth:0.3", T(TechId.Schools), prestige: 0.05f, days: 1000, labour: 0.2f);
        I("work_helmet", H, "steel:0.5", T(TechId.Machinery), protection: 0.08f, days: 3000);
        I("respirator_hood", H, "cloth:0.5,glass:0.3", T(TechId.Chemistry), warmth: 1f, days: 1500);
        I("environment_helmet", H, "glass:1,advanced_alloy:0.5,electronics:0.3", T(TechId.SpaceHabitats), warmth: 5f, protection: 0.1f, days: 3000, station: "fabricator");
        I("mage_hat", H, "cloth:0.5,ochre:0.1", T(TechId.Mysticism), prestige: 0.1f, days: 1200);

        I("necklace", Ac, "bone:0.2,fiber:0.1", T(TechId.Carving), prestige: 0.05f, days: 2000, labour: 0.3f);
        I("beads", Ac, "bone:0.2,ochre:0.1", T(TechId.Pigments), prestige: 0.04f, days: 1500, labour: 0.3f);
        I("belt_pouch", Ac, "leather:0.4", T(TechId.Tanning), days: 1000, labour: 0.2f, uses: (Activity.Forage, 1.05f));
        I("scarf", Ac, "cloth:0.3", T(TechId.Weaving), warmth: 2f, days: 600, labour: 0.2f);
        I("face_wrap", Ac, "cloth:0.3", T(TechId.Weaving), warmth: 1f, days: 600, labour: 0.2f);
        I("backpack_straps", Ac, "leather:0.5,reed:1", T(TechId.Tanning, TechId.Baskets), days: 1000, labour: 0.4f, uses: (Activity.Gather, 1.2f));
        I("tool_belt", Ac, "leather:0.6", T(TechId.Tanning, TechId.Carpentry), days: 1200, labour: 0.3f, uses: (Activity.Build, 1.05f));
        I("ceremonial_sash", Ac, "cloth:0.5,ochre:0.2", T(TechId.Priesthood), prestige: 0.1f, days: 1500);
        I("royal_chain", Ac, "gold_ore:0.3,copper_ingot:0.3", T(TechId.Monarchy), prestige: 0.25f, days: 8000, station: "anvil");
        I("spectacles", Ac, "glass:0.2,copper_ingot:0.1", T(TechId.Glass, TechId.Counting), days: 3000, labour: 0.4f, uses: new[] { (Activity.Craft, 1.05f), (Activity.Teach, 1.05f) });
        I("goggles", Ac, "glass:0.3,leather:0.2", T(TechId.Glass, TechId.Machinery), days: 2000, uses: (Activity.Craft, 1.05f));
        I("respirator", Ac, "cloth:0.3,components:0.2", T(TechId.Chemistry), days: 1500);

        Items = _items.ToArray();

        // ---- goods recipes --------------------------------------------------------------------
        Recipe R(string id, TechId tech, string station, string inputs, string outputs, float labour, Domain d = Domain.Crafting) =>
            new(id, tech, station, In(inputs), In(outputs), labour, d);
        Recipes = new[]
        {
            // Food: cooking, preserving, grinding, baking, dairy, brewing, pressing
            R("cook_meat", TechId.Cooking, "hearth", "meat_raw:2,wood:0.3", "meat_cooked:2", 0.2f, Domain.Foraging),
            R("dry_meat", TechId.Drying, "fish_drying_rack", "meat_raw:3", "dried_meat:2", 0.3f, Domain.Foraging),
            R("dry_fish", TechId.Drying, "fish_drying_rack", "fish:3", "dried_fish:2", 0.3f, Domain.Fishing),
            R("dry_fish_fresh", TechId.Drying, "fish_drying_rack", "freshwater_fish:3", "dried_fish:2", 0.3f, Domain.Fishing),
            R("dry_fish_salt", TechId.Drying, "fish_drying_rack", "saltwater_fish:3", "dried_fish:2", 0.3f, Domain.Fishing),
            R("grind_grain", TechId.Grinding, "stone_quern", "grain:3", "flour:3", 0.4f, Domain.Farming),
            R("grind_wheat", TechId.Grinding, "stone_quern", "wheat:3", "flour:3", 0.4f, Domain.Farming),
            R("grind_barley", TechId.Grinding, "stone_quern", "barley:3", "flour:3", 0.4f, Domain.Farming),
            R("grind_maize", TechId.Grinding, "stone_quern", "maize:3", "flour:3", 0.4f, Domain.Farming),
            R("mill_grain", TechId.Milling, "millstone", "grain:8", "flour:8", 0.5f, Domain.Farming),
            R("bake_bread", TechId.Baking, "oven", "flour:3,wood:0.5", "bread:3", 0.4f, Domain.Foraging),
            R("make_butter", TechId.Dairy, "", "milk:4", "butter:1", 0.3f, Domain.Farming),
            R("make_cheese", TechId.Dairy, "", "milk:5", "cheese:1.5", 0.4f, Domain.Farming),
            R("brew", TechId.Brewing, "brew_vat", "barley:3", "food:3", 0.4f, Domain.Foraging),
            R("press_oil", TechId.Grinding, "press", "nuts:4", "oil:1", 0.4f, Domain.Foraging),
            R("salt_meat", TechId.Drying, "", "meat_raw:3,salt:0.5", "dried_meat:2.5", 0.3f, Domain.Foraging),
            R("medicine", TechId.Herbalism, "", "herbs:3", "medicine:1", 0.4f, Domain.Healing),
            // Materials
            R("split_wood", TechId.Woodcutting, "chopping_block", "log:1", "wood:4", 0.25f, Domain.Building),
            R("twist_rope", TechId.Cordage, "", "fiber:2", "rope:1", 0.3f),
            R("weave_cloth", TechId.Weaving, "loom", "fiber:3", "cloth:1.5", 0.5f),
            R("power_loom", TechId.PowerLooms, "loom", "fiber:6", "cloth:5", 0.4f),
            R("tan_leather", TechId.Tanning, "", "hide:2,wood:0.3", "leather:2", 0.5f),
            R("make_charcoal", TechId.Charcoal, "", "wood:5", "charcoal:2", 0.4f),
            R("fire_pottery", TechId.Pottery, "kiln", "clay:2,wood:1", "ceramic:2", 0.5f),
            R("mud_brick", TechId.Mudbrick, "", "clay:3,reed:0.5", "brick:2", 0.5f, Domain.Building),
            R("fire_brick", TechId.FiredBrick, "kiln", "clay:3,wood:1", "brick:3", 0.5f, Domain.Building),
            R("dress_stone", TechId.Masonry, "", "stone:2", "stone_blocks:1", 0.5f, Domain.Building),
            R("grind_ochre", TechId.Pigments, "", "clay:1", "ochre:0.5", 0.2f, Domain.Spiritual),
            R("smelt_copper", TechId.Copper, "kiln", "copper_ore:2,charcoal:1", "copper_ingot:1", 0.6f),
            R("smelt_copper_wood", TechId.Copper, "kiln", "copper_ore:2,wood:3", "copper_ingot:0.8", 0.7f),
            R("cast_bronze", TechId.Bronze, "forge", "copper_ingot:2,tin_ore:1,charcoal:1", "bronze_ingot:2", 0.8f),
            R("smelt_iron", TechId.Iron, "forge", "iron_ore:2,charcoal:2", "iron_ingot:1", 0.8f),
            R("make_steel", TechId.Steel, "forge", "iron_ingot:2,charcoal:1", "steel:1.5", 0.8f),
            R("blow_glass", TechId.Glass, "kiln", "sand:2,charcoal:1", "glass:1", 0.6f),
            R("mix_concrete", TechId.Concrete, "", "stone:1,sand:1,clay:1", "concrete:2", 0.4f, Domain.Building),
            R("machine_parts", TechId.Machinery, "lathe", "steel:1,copper_ingot:0.5", "components:2", 0.8f),
            R("electronics", TechId.Electronics, "workbench", "components:1,glass:0.5,copper_ingot:0.5", "electronics:1", 0.8f),
            R("batteries", TechId.Batteries, "workbench", "copper_ingot:1,components:0.5,sulfur:0.3", "battery:1", 0.6f),
            R("alloy", TechId.AdvancedMaterials, "fabricator", "steel:2,electronics:0.5", "advanced_alloy:1", 1f),
            R("fabricate_parts", TechId.Fabrication, "fabricator", "advanced_alloy:0.5,battery:0.2", "components:4", 0.5f),
        };
    }

    public static ItemDef? Item(ItemSlot slot, string id) => ItemByName.TryGetValue($"{slot}:{id}", out var i) ? Items[i] : null;

    /// <summary>Whether someone knowing <paramref name="known"/> can make the item.</summary>
    public static bool CanMake(TechSet known, ItemDef def)
    {
        foreach (var t in def.Techs) if (!known.Has(t)) return false;
        return true;
    }

    /// <summary>How much an item speeds an activity (1 when it doesn't help).</summary>
    public static float Use(ItemDef def, Activity act)
    {
        foreach (var (a, m) in def.Uses) if (a == act) return m;
        return 1f;
    }
}
