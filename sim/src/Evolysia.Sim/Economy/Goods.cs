namespace Evolysia.Sim.Economy;

/// <summary>What a good is for. Foods feed people; the rest are made into things or burned.</summary>
public enum GoodKind : byte { Food, Material, Fuel, Metal, Manufactured, Medicine }

/// <summary>
/// A kind of stored good. Ids match the civilisation art (assets/civilisation/stored_&lt;id&gt;), so
/// what a basket or granary holds is what is drawn in it.
/// </summary>
/// <param name="Food">Person-days of food per unit (0 for non-food).</param>
/// <param name="Spoil">Fraction lost per day at a mild temperature, unstored.</param>
/// <param name="Weight">Relative bulk: how much of a carrier's or store's room a unit takes.</param>
public sealed record GoodDef(ushort Index, string Id, GoodKind Kind, float Food, float Spoil, float Weight);

/// <summary>Every good, by index. Indices are stable within a build (they follow this table's order).</summary>
public static class Goods
{
    public static readonly GoodDef[] All;
    static readonly Dictionary<string, ushort> ByName = new();

    // id, kind, food value, spoil per day, weight
    static readonly (string id, GoodKind kind, float food, float spoil, float weight)[] Table =
    {
        // Gathered plant foods
        ("berries", GoodKind.Food, 0.5f, 0.06f, 0.5f), ("nuts", GoodKind.Food, 1.2f, 0.004f, 0.6f), ("roots", GoodKind.Food, 0.8f, 0.015f, 0.8f),
        ("mushrooms", GoodKind.Food, 0.4f, 0.08f, 0.4f), ("fruit", GoodKind.Food, 0.6f, 0.04f, 0.6f), ("herbs", GoodKind.Medicine, 0.1f, 0.05f, 0.2f),
        ("honey", GoodKind.Food, 1.5f, 0.0005f, 0.5f), ("dates", GoodKind.Food, 1.0f, 0.006f, 0.5f), ("coconut", GoodKind.Food, 0.9f, 0.01f, 0.9f),
        ("banana", GoodKind.Food, 0.7f, 0.05f, 0.6f), ("fig", GoodKind.Food, 0.7f, 0.04f, 0.5f),
        // Orchard and garden produce
        ("apple", GoodKind.Food, 0.6f, 0.012f, 0.6f), ("pear", GoodKind.Food, 0.6f, 0.02f, 0.6f), ("peach", GoodKind.Food, 0.6f, 0.04f, 0.6f),
        ("plum", GoodKind.Food, 0.5f, 0.04f, 0.5f), ("orange", GoodKind.Food, 0.6f, 0.02f, 0.6f), ("lemon", GoodKind.Food, 0.3f, 0.015f, 0.5f),
        ("grapes", GoodKind.Food, 0.6f, 0.05f, 0.5f), ("melon", GoodKind.Food, 0.6f, 0.03f, 1.0f),
        ("carrot", GoodKind.Food, 0.5f, 0.012f, 0.6f), ("potato", GoodKind.Food, 1.0f, 0.006f, 0.9f), ("beet", GoodKind.Food, 0.6f, 0.01f, 0.8f),
        ("onion", GoodKind.Food, 0.4f, 0.006f, 0.5f), ("cabbage", GoodKind.Food, 0.4f, 0.02f, 0.8f), ("lettuce", GoodKind.Food, 0.2f, 0.08f, 0.5f),
        ("tomato", GoodKind.Food, 0.3f, 0.05f, 0.5f), ("pepper", GoodKind.Food, 0.3f, 0.03f, 0.4f), ("pumpkin", GoodKind.Food, 0.9f, 0.006f, 1.2f),
        ("squash", GoodKind.Food, 0.8f, 0.008f, 1.0f), ("peas", GoodKind.Food, 1.1f, 0.003f, 0.5f), ("beans", GoodKind.Food, 1.2f, 0.002f, 0.5f),
        ("lentils", GoodKind.Food, 1.2f, 0.002f, 0.5f),
        // Grains (raw grain must be ground or cooked to feed well)
        ("grain", GoodKind.Food, 0.8f, 0.002f, 0.5f), ("wheat", GoodKind.Food, 0.8f, 0.002f, 0.5f), ("barley", GoodKind.Food, 0.8f, 0.002f, 0.5f),
        ("oats", GoodKind.Food, 0.8f, 0.002f, 0.5f), ("rye", GoodKind.Food, 0.8f, 0.002f, 0.5f), ("rice", GoodKind.Food, 0.9f, 0.0015f, 0.5f),
        ("maize", GoodKind.Food, 0.9f, 0.002f, 0.6f), ("millet", GoodKind.Food, 0.8f, 0.002f, 0.5f),
        ("flour", GoodKind.Food, 1.0f, 0.004f, 0.5f), ("bread", GoodKind.Food, 1.4f, 0.03f, 0.5f),
        // Animal foods
        ("meat_raw", GoodKind.Food, 1.0f, 0.10f, 0.8f), ("meat_cooked", GoodKind.Food, 1.25f, 0.06f, 0.7f), ("dried_meat", GoodKind.Food, 1.1f, 0.003f, 0.4f),
        ("poultry", GoodKind.Food, 0.8f, 0.1f, 0.6f), ("eggs", GoodKind.Food, 0.4f, 0.02f, 0.3f), ("milk", GoodKind.Food, 0.5f, 0.15f, 0.8f),
        ("cheese", GoodKind.Food, 1.5f, 0.004f, 0.5f), ("butter", GoodKind.Food, 1.6f, 0.01f, 0.4f),
        ("fish", GoodKind.Food, 0.9f, 0.12f, 0.7f), ("freshwater_fish", GoodKind.Food, 0.9f, 0.12f, 0.7f), ("saltwater_fish", GoodKind.Food, 0.9f, 0.12f, 0.7f),
        ("dried_fish", GoodKind.Food, 1.0f, 0.003f, 0.4f), ("shellfish", GoodKind.Food, 0.5f, 0.15f, 0.5f), ("crab", GoodKind.Food, 0.6f, 0.15f, 0.6f),
        ("shrimp", GoodKind.Food, 0.4f, 0.15f, 0.4f), ("squid", GoodKind.Food, 0.6f, 0.15f, 0.6f),
        // Prepared and traded
        ("food", GoodKind.Food, 1.0f, 0.02f, 0.6f), ("oil", GoodKind.Food, 2.0f, 0.002f, 0.6f), ("salt", GoodKind.Material, 0f, 0f, 0.5f),
        ("spices", GoodKind.Food, 0.1f, 0.001f, 0.1f), ("tea", GoodKind.Food, 0.05f, 0.001f, 0.1f), ("coffee", GoodKind.Food, 0.05f, 0.001f, 0.1f),
        ("water", GoodKind.Material, 0f, 0f, 1f), ("medicine", GoodKind.Medicine, 0f, 0.001f, 0.1f),
        // Raw materials
        ("wood", GoodKind.Fuel, 0f, 0f, 1f), ("log", GoodKind.Material, 0f, 0f, 3f), ("bamboo", GoodKind.Material, 0f, 0f, 1.5f),
        ("reed", GoodKind.Material, 0f, 0.002f, 0.6f), ("fiber", GoodKind.Material, 0f, 0.001f, 0.4f), ("stone", GoodKind.Material, 0f, 0f, 3f),
        ("flint", GoodKind.Material, 0f, 0f, 0.8f), ("clay", GoodKind.Material, 0f, 0f, 2f), ("sand", GoodKind.Material, 0f, 0f, 2f),
        ("hide", GoodKind.Material, 0f, 0.01f, 1f), ("fur", GoodKind.Material, 0f, 0.002f, 0.8f), ("bone", GoodKind.Material, 0f, 0f, 0.5f),
        ("antler", GoodKind.Material, 0f, 0f, 0.6f), ("ochre", GoodKind.Material, 0f, 0f, 0.4f), ("snow", GoodKind.Material, 0f, 0.05f, 1f),
        ("ice", GoodKind.Material, 0f, 0.03f, 1.5f), ("coal", GoodKind.Fuel, 0f, 0f, 1.5f), ("charcoal", GoodKind.Fuel, 0f, 0f, 0.8f),
        ("sulfur", GoodKind.Material, 0f, 0f, 0.8f), ("quartz", GoodKind.Material, 0f, 0f, 0.8f),
        ("copper_ore", GoodKind.Material, 0f, 0f, 2f), ("tin_ore", GoodKind.Material, 0f, 0f, 2f), ("iron_ore", GoodKind.Material, 0f, 0f, 2.5f),
        ("gold_ore", GoodKind.Material, 0f, 0f, 2f), ("silver_ore", GoodKind.Material, 0f, 0f, 2f),
        // Worked materials
        ("rope", GoodKind.Manufactured, 0f, 0f, 0.5f), ("leather", GoodKind.Manufactured, 0f, 0f, 0.7f), ("cloth", GoodKind.Manufactured, 0f, 0f, 0.4f),
        ("ceramic", GoodKind.Manufactured, 0f, 0f, 1f), ("brick", GoodKind.Manufactured, 0f, 0f, 2f), ("stone_blocks", GoodKind.Manufactured, 0f, 0f, 3f),
        ("glass", GoodKind.Manufactured, 0f, 0f, 1f), ("concrete", GoodKind.Manufactured, 0f, 0f, 3f),
        ("copper_ingot", GoodKind.Metal, 0f, 0f, 1.5f), ("bronze_ingot", GoodKind.Metal, 0f, 0f, 1.5f), ("iron_ingot", GoodKind.Metal, 0f, 0f, 1.5f),
        ("steel", GoodKind.Metal, 0f, 0f, 1.5f), ("advanced_alloy", GoodKind.Metal, 0f, 0f, 1f),
        ("components", GoodKind.Manufactured, 0f, 0f, 0.8f), ("electronics", GoodKind.Manufactured, 0f, 0f, 0.5f), ("battery", GoodKind.Manufactured, 0f, 0f, 1f),
    };

    static Goods()
    {
        All = new GoodDef[Table.Length];
        for (int i = 0; i < Table.Length; i++)
        {
            var (id, kind, food, spoil, weight) = Table[i];
            All[i] = new GoodDef((ushort)i, id, kind, food, spoil, weight);
            ByName[id] = (ushort)i;
        }
    }

    public static int Count => All.Length;

    /// <summary>A good's index by id; throws for an unknown id (a typo in a recipe should fail loudly).</summary>
    public static ushort Id(string id) => ByName.TryGetValue(id, out var i) ? i : throw new KeyNotFoundException($"unknown good '{id}'");
    public static bool TryId(string id, out ushort index) => ByName.TryGetValue(id, out index);
    public static GoodDef Get(ushort index) => All[index];

    /// <summary>Goods that are not food (materials, fuel, metal, made things, medicine), in index order.</summary>
    public static readonly ushort[] NonFood = Table.Select((t, i) => (t, i)).Where(x => x.t.kind != GoodKind.Food).Select(x => (ushort)x.i).ToArray();
    /// <summary>Goods that spoil, in index order.</summary>
    public static readonly ushort[] Perishable = Table.Select((t, i) => (t, i)).Where(x => x.t.spoil > 0f).Select(x => (ushort)x.i).ToArray();

    /// <summary>Food goods, most perishable first: what people eat before it goes off.</summary>
    public static readonly ushort[] FoodsByPerishability = Table.Select((t, i) => (t, i)).Where(x => x.t.food > 0.15f)
        .OrderByDescending(x => x.t.spoil).Select(x => (ushort)x.i).ToArray();
}
