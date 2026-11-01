using Evolysia.Sim.Agents;

namespace Evolysia.Sim.Economy;

public enum StructureRole : byte { Dwelling, Storage, Station, Hearth, Field, Pen, Shrine, Defence, Water, Civic, Vessel, Power, Decoration }

public sealed record StructureDef(ushort Index, string Id, StructureRole Role, TechId[] Techs, int W, int H, (ushort good, float qty)[] Materials,
                                  float Labour, int Housing, float Warmth, float Capacity, float Effect, bool Portable, int Days);

public enum StructureState : byte { Planned, Building, Complete, Damaged, Ruined, Burned }

public sealed class Structure
{
    Inventory? _contents;
    public int TransitCell = -1;
    public int ParentBuildingId = -1;
    public bool InteriorBuilt;
    public (ushort good, float qty)[]? RegionalMaterials;
    public StructureDef? CustomKind;
    public RoomLayout? CustomLayout;
    public string CustomName = "";
    public RoomLayout? Layout => CustomLayout ?? RoomLayouts.All.GetValueOrDefault(Kind.Id);
    public (ushort good, float qty)[] Materials => RegionalMaterials ?? Kind.Materials;
    public Inventory Contents => _contents ??= new Inventory($"structure/{Id}", MathF.Max(Kind.Capacity, Layout != null ? 100f : Kind.Role == StructureRole.Vessel ? Kind.Effect * 50f : 0f));
    public int Id;
    public ushort Def;
    public int Cell;
    public int BandId = -1;
    public StructureState State;
    public float Progress;
    public float Condition = 1f;
    public int BuiltDay = -1;
    public int PlannedDay;
    public string Crop = "";
    public float Growth;
    public float Yield;
    public float Clearing;
    public string Style = "";
    public StructureDef Kind => CustomKind ?? Structures.All[Def];
    public bool Standing => State is StructureState.Complete or StructureState.Damaged;
}

public static class Structures
{
    public static readonly StructureDef[] All;
    static readonly Dictionary<string, ushort> ByName = new();
    static readonly List<StructureDef> _list = new();

    static void S(string id, StructureRole role, TechId[] techs, string materials, float labour, int w = 1, int h = 1, int housing = 0,
                  float warmth = 0f, float capacity = 0f, float effect = 0f, bool portable = false, int days = 3600)
    {
        var mats = materials.Length == 0 ? Array.Empty<(ushort, float)>()
            : materials.Split(',').Select(p => p.Split(':')).Select(p => (Goods.Id(p[0]), float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture))).ToArray();
        var def = new StructureDef((ushort)_list.Count, id, role, techs, w, h, mats, labour, housing, warmth, capacity, effect, portable, days);
        ByName[id] = def.Index;
        _list.Add(def);
    }

    static TechId[] T(params TechId[] t) => t;

    static Structures()
    {
        var D = StructureRole.Dwelling; var St = StructureRole.Storage; var X = StructureRole.Station;

        S("windbreak", D, T(TechId.StoneTools), "fiber:3,wood:1", 1f, housing: 3, warmth: 2f, days: 400);
        S("lean_to", D, T(TechId.Woodcutting), "wood:4,hide:1", 2f, w: 2, housing: 4, warmth: 4f, days: 700);
        S("hide_tent", D, T(TechId.Shelter), "hide:4,wood:3,rope:0.5", 3f, w: 2, h: 2, housing: 6, warmth: 8f, portable: true, days: 1500);
        S("pit_house", D, T(TechId.Shelter, TechId.Thatching), "wood:6,fiber:6", 8f, w: 2, h: 2, housing: 7, warmth: 10f, days: 2500);
        S("hut", D, T(TechId.Thatching), "wood:6,fiber:8", 6f, w: 2, h: 2, housing: 6, warmth: 9f, days: 2500);
        S("house", D, T(TechId.Carpentry), "log:6,wood:4,fiber:6", 14f, w: 3, h: 2, housing: 8, warmth: 13f, days: 6000);
        S("mudbrick_house", D, T(TechId.Mudbrick), "brick:12,wood:3,fiber:4", 14f, w: 3, h: 2, housing: 8, warmth: 13f, days: 7000);
        S("stone_house", D, T(TechId.Masonry), "stone_blocks:10,wood:4", 20f, w: 3, h: 2, housing: 9, warmth: 15f, days: 15000);
        S("brick_house", D, T(TechId.FiredBrick), "brick:16,wood:4", 18f, w: 3, h: 2, housing: 9, warmth: 15f, days: 12000);
        S("housing_block", D, T(TechId.Concrete), "concrete:20,steel:4,glass:4", 40f, w: 4, h: 3, housing: 30, warmth: 18f, days: 20000);
        S("pressure_habitat", D, T(TechId.SpaceHabitats), "advanced_alloy:8,glass:6,electronics:4", 60f, w: 3, h: 3, housing: 12, warmth: 25f, days: 30000);

        S("hearth", StructureRole.Hearth, T(TechId.ControlledFire), "", 0.6f, warmth: 5f, days: 3000);
        S("basket", St, T(TechId.Baskets), "fiber:2", 0.4f, capacity: 12f, days: 500);
        S("storage_pit", St, T(TechId.FoodStorage), "stone:2", 3f, capacity: 40f, days: 4000);
        S("crate", St, T(TechId.Carpentry), "wood:3", 1f, capacity: 25f, days: 2000);
        S("chest", St, T(TechId.Carpentry), "wood:3,copper_ingot:0.2", 1.5f, capacity: 30f, days: 4000);
        S("grain_bin", St, T(TechId.Agriculture, TechId.FoodStorage), "wood:5", 2f, capacity: 80f, days: 3000);
        S("granary", St, T(TechId.Agriculture, TechId.Thatching), "wood:8,fiber:6,stone:4", 10f, w: 2, h: 2, capacity: 300f, days: 6000);
        S("warehouse", St, T(TechId.Counting, TechId.Masonry), "stone_blocks:12,log:6", 25f, w: 3, h: 2, capacity: 1200f, days: 15000);
        S("refrigerated_store", St, T(TechId.Refrigeration), "steel:4,components:3,concrete:4", 20f, w: 2, h: 2, capacity: 600f, effect: 1f, days: 15000);
        S("knapping_block", X, T(TechId.StoneTools), "stone:1", 0.2f, days: 3000);
        S("chopping_block", X, T(TechId.Woodcutting), "log:1", 0.3f, days: 1500);
        S("fish_drying_rack", X, T(TechId.Drying), "wood:3,rope:0.5", 1f, days: 900);
        S("stone_quern", X, T(TechId.Grinding), "stone:2", 1f, days: 6000);
        S("millstone", X, T(TechId.Milling), "stone_blocks:3,wood:3", 6f, days: 10000);
        S("oven", X, T(TechId.Baking), "brick:4,clay:2", 3f, days: 6000);
        S("brew_vat", X, T(TechId.Brewing), "wood:3,ceramic:1", 2f, days: 3000);
        S("press", X, T(TechId.Grinding, TechId.Carpentry), "wood:4", 2f, days: 4000);
        S("loom", X, T(TechId.Weaving), "wood:3,rope:1", 2f, days: 3000);
        S("spinning_wheel", X, T(TechId.Weaving, TechId.Wheel), "wood:3", 1.5f, effect: 1.3f, days: 4000);
        S("potter_wheel", X, T(TechId.Pottery, TechId.Wheel), "wood:2,stone:1", 1.5f, effect: 1.3f, days: 5000);
        S("dye_vat", X, T(TechId.Weaving, TechId.Pigments), "wood:2,ceramic:1", 1.5f, days: 3000);
        S("kiln", X, T(TechId.Pottery), "clay:6,stone:3", 4f, days: 5000);
        S("forge", X, T(TechId.Bronze), "stone:4,brick:3", 5f, days: 6000);
        S("anvil", X, T(TechId.Copper), "copper_ingot:2,stone:2", 2f, days: 10000);
        S("workbench", X, T(TechId.Carpentry), "wood:4", 1.5f, days: 3000);
        S("sawbench", X, T(TechId.Carpentry), "wood:3", 1f, effect: 1.2f, days: 3000);
        S("tool_rack", X, T(TechId.Carpentry), "wood:2", 0.5f, days: 3000);
        S("lathe", X, T(TechId.Machinery), "steel:3,components:1", 6f, days: 8000);
        S("fabricator", X, T(TechId.Fabrication), "advanced_alloy:3,electronics:2", 12f, effect: 2f, days: 10000);
        S("future_fabricator", X, T(TechId.Fabrication, TechId.FusionPower), "advanced_alloy:5,electronics:4", 20f, effect: 3f, days: 15000);
        S("robot_arm", X, T(TechId.Robotics), "steel:2,electronics:2,components:2", 8f, effect: 1.8f, days: 8000);
        S("cooking_pot", X, T(TechId.Cooking, TechId.Pottery), "ceramic:1", 0.3f, effect: 1.1f, days: 1500);

        S("field", StructureRole.Field, T(TechId.Agriculture), "", 1.5f, days: 100000);
        S("orchard", StructureRole.Field, T(TechId.Orchards), "", 3f, days: 100000);
        S("feed_trough", StructureRole.Pen, T(TechId.Domestication), "wood:3", 1f, effect: 6f, days: 2500);
        S("animal_pen", StructureRole.Pen, T(TechId.Domestication, TechId.Carpentry), "wood:8,rope:1", 4f, w: 2, h: 2, effect: 16f, days: 4000);
        S("beehive", StructureRole.Pen, T(TechId.Beekeeping), "reed:2,clay:1", 1f, effect: 1f, days: 1500);
        S("compost_bin", X, T(TechId.Agriculture, TechId.Carpentry), "wood:2", 0.5f, effect: 1.1f, days: 2000);
        S("hydroponic_rack", StructureRole.Field, T(TechId.Hydroponics), "steel:2,glass:2,electronics:1", 6f, effect: 4f, days: 8000);
        S("biotech_vat", X, T(TechId.Biotech), "glass:3,steel:2,electronics:2", 8f, effect: 2f, days: 8000);

        S("well", StructureRole.Water, T(TechId.Wells), "stone:8,wood:2,rope:1", 10f, effect: 0.6f, days: 20000);
        S("water_barrel", StructureRole.Water, T(TechId.Carpentry), "wood:3", 1f, effect: 0.15f, days: 2000);
        S("water_pump", StructureRole.Water, T(TechId.Machinery), "steel:3,components:2", 6f, effect: 0.9f, days: 10000);
        S("wash_basin", StructureRole.Civic, T(TechId.Sanitation), "ceramic:2", 1f, effect: 0.1f, days: 5000);
        S("medical_table", StructureRole.Civic, T(TechId.Medicine), "wood:3,cloth:1", 2f, effect: 0.3f, days: 5000);

        S("totem", StructureRole.Shrine, T(TechId.Carving), "log:1", 2f, effect: 0.3f, days: 5000);
        S("burial_mound", StructureRole.Shrine, T(TechId.Burial), "", 3f, effect: 0.2f, days: 100000);
        S("cairn", StructureRole.Shrine, T(TechId.Burial), "stone:6", 2f, effect: 0.2f, days: 50000);
        S("standing_stone", StructureRole.Shrine, T(TechId.Burial, TechId.Quarrying), "stone:10", 10f, effect: 0.4f, days: 100000);
        S("altar", StructureRole.Shrine, T(TechId.Priesthood), "stone_blocks:3", 4f, effect: 0.5f, days: 30000);
        S("brazier", StructureRole.Shrine, T(TechId.Priesthood, TechId.Copper), "copper_ingot:2", 2f, warmth: 2f, effect: 0.3f, days: 8000);
        S("ceremonial_screen", StructureRole.Shrine, T(TechId.Priesthood, TechId.Carpentry), "wood:3,cloth:1", 2f, effect: 0.3f, days: 4000);
        S("alchemy_table", StructureRole.Civic, T(TechId.Alchemy), "wood:3,glass:2", 3f, effect: 0.3f, days: 5000);
        S("lectern", StructureRole.Civic, T(TechId.Writing), "wood:2", 1f, effect: 0.2f, days: 5000);
        S("bookcase", StructureRole.Civic, T(TechId.Writing, TechId.Carpentry), "wood:4", 2f, effect: 0.3f, days: 6000);
        S("telescope", StructureRole.Civic, T(TechId.Glass, TechId.Counting), "glass:2,copper_ingot:2", 4f, effect: 0.3f, days: 8000);
        S("throne", StructureRole.Civic, T(TechId.Monarchy), "wood:4,gold_ore:0.5", 4f, effect: 0.5f, days: 20000);
        S("market_stall", StructureRole.Civic, T(TechId.Trade), "wood:3,cloth:1", 1.5f, effect: 0.3f, days: 2500);
        S("console", StructureRole.Civic, T(TechId.Computing), "electronics:2,steel:1", 4f, effect: 0.6f, days: 8000);
        S("communication_terminal", StructureRole.Civic, T(TechId.Telecommunication), "electronics:1,components:2", 4f, effect: 0.5f, days: 8000);
        S("generator", StructureRole.Power, T(TechId.Electricity), "steel:4,copper_ingot:4,components:2", 10f, effect: 1f, days: 10000);
        S("solar_panel", StructureRole.Power, T(TechId.SolarPower), "glass:2,electronics:1,steel:1", 5f, effect: 0.6f, days: 10000);
        S("battery_bank", StructureRole.Power, T(TechId.Batteries), "battery:4,steel:1", 4f, effect: 0.5f, days: 8000);

        S("weapons_rack", StructureRole.Defence, T(TechId.Fortification), "wood:2", 1f, effect: 0.1f, days: 4000);
        S("armour_stand", StructureRole.Defence, T(TechId.Armour), "wood:2", 1f, effect: 0.05f, days: 4000);
        S("palisade", StructureRole.Defence, T(TechId.Fortification), "log:2", 1.5f, effect: 0.04f, days: 4000);

        S("raft", StructureRole.Vessel, T(TechId.Rafts), "log:4,rope:1", 3f, effect: 0.5f, days: 1500);
        S("canoe", StructureRole.Vessel, T(TechId.Boats), "log:2", 4f, effect: 1f, days: 2500);
        S("rowing_boat", StructureRole.Vessel, T(TechId.Boats, TechId.Carpentry), "wood:6", 5f, effect: 1.2f, days: 4000);
        S("sailboat", StructureRole.Vessel, T(TechId.Sailing), "wood:8,cloth:3,rope:2", 10f, effect: 2f, days: 5000);
        S("cargo_ship", StructureRole.Vessel, T(TechId.Shipbuilding), "log:20,cloth:6,rope:4", 40f, w: 3, h: 2, effect: 4f, days: 8000);
        S("sled", StructureRole.Vessel, T(TechId.Sledges), "wood:3,rope:1", 1.5f, effect: 0.3f, days: 1500);
        S("handcart", StructureRole.Vessel, T(TechId.Wheel), "wood:4", 2f, effect: 0.4f, days: 2500);
        S("wagon", StructureRole.Vessel, T(TechId.Wheel, TechId.Domestication), "wood:8,copper_ingot:0.5", 5f, w: 2, effect: 0.8f, days: 4000);
        S("bicycle", StructureRole.Vessel, T(TechId.Machinery), "steel:1,components:1", 2f, effect: 0.3f, days: 4000);
        S("truck", StructureRole.Vessel, T(TechId.Motors), "steel:6,components:4", 15f, w: 2, effect: 2f, days: 8000);
        S("rover", StructureRole.Vessel, T(TechId.Rocketry), "advanced_alloy:4,electronics:3,battery:2", 20f, w: 2, effect: 2f, days: 10000);
        S("hover_carrier", StructureRole.Vessel, T(TechId.Hover), "advanced_alloy:5,electronics:4,battery:3", 25f, w: 2, effect: 3f, days: 10000);
        S("construction_crane", StructureRole.Civic, T(TechId.Machinery), "steel:6,rope:2", 10f, effect: 1.5f, days: 8000);

        AddRooms();
        All = _list.ToArray();
    }

    static void AddRooms()
    {
        foreach (var room in RoomLayouts.All.Values)
        {
            TechId tech = room.Id switch
            {
                "camp" => TechId.StoneTools, "home" or "rural_home" => TechId.Carpentry, "granary" => TechId.Agriculture,
                "pottery_workshop" => TechId.Pottery, "weaver" => TechId.Weaving, "smithy" => TechId.Iron, "bakery" => TechId.Baking,
                "infirmary" => TechId.Herbalism, "machine_shop" or "construction_yard" => TechId.Machinery,
                "research_lab" => TechId.Chemistry, "future_fabricator" => TechId.Fabrication, "royal_hall" => TechId.Monarchy,
                "council_hall" => TechId.Governance, "ritual_hall" or "ceremonial_hall" => TechId.Priesthood,
                "magic_workshop" => TechId.Alchemy, "fort_gatehouse" or "barracks" or "watch_post" => TechId.Fortification,
                "carpentry_workshop" => TechId.Carpentry, "fishing_hut" => TechId.Fishing, "ranch" => TechId.Domestication,
                "market" => TechId.Trade, "industrial_hall" => TechId.Machinery, "shipyard" => TechId.Shipbuilding,
                "transport_depot" => TechId.Motors, "brew_house" => TechId.Brewing, "medical_clinic" => TechId.Medicine, "library" => TechId.Writing,
                _ => TechId.Shelter,
            };
            var role = room.Id is "home" or "rural_home" or "camp" ? StructureRole.Dwelling : room.Id == "granary" ? StructureRole.Storage : StructureRole.Civic;
            string materials = room.Wall is "stone" or "granite" or "limestone" or "sandstone" or "basalt" ? "stone:30,log:10,fiber:8" :
                room.Floor is "concrete" or "metal_deck" ? "concrete:24,steel:8,glass:4" : "wood:30,fiber:12,clay:8";
            if (ByName.TryGetValue(room.Id, out ushort existing))
                _list[existing] = _list[existing] with { W = room.Width, H = room.Height, Labour = MathF.Max(20f, _list[existing].Labour), Materials = InMaterials(materials) };
            else S(room.Id, role, T(tech), materials, 30f, w: room.Width, h: room.Height, housing: role == StructureRole.Dwelling ? 8 : 0,
                warmth: role == StructureRole.Dwelling ? room.Id == "camp" ? 2f : 8f : 0f, capacity: role == StructureRole.Storage ? 300f : 0f, effect: 1f, days: 8000);
            foreach (var obj in room.Objects)
                if (!ByName.ContainsKey(obj.Kind))
                    S(obj.Kind, obj.Kind == "bed" ? StructureRole.Dwelling : StructureRole.Decoration, T(tech), "wood:2,fiber:1", 1f,
                        housing: obj.Kind == "bed" ? 1 : 0, warmth: obj.Kind == "bed" ? 2f : 0f, days: 4000);
        }
    }

    static (ushort good, float qty)[] InMaterials(string text) => text.Split(',').Select(p => p.Split(':')).Select(p => (Goods.Id(p[0]), float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture))).ToArray();

    public static StructureDef? Get(string id) => ByName.TryGetValue(id, out var i) ? All[i] : null;
    public static StructureDef Need(string id) => All[ByName[id]];

    public static bool CanBuild(TechSet known, StructureDef def)
    {
        foreach (var t in def.Techs) if (!known.Has(t)) return false;
        return true;
    }
}
