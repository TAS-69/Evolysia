using System.Text.Json;

namespace Evolysia.Sim.Persistence;

public sealed record SaveSlot(string Id, string Title, ulong Seed, int Day, int Population, int Bands, DateTime SavedUtc, bool Automatic);

public sealed class SaveRepository
{
    readonly string _root;
    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, WriteIndented = true };
    public SaveRepository(string root) { _root = Path.GetFullPath(root); Directory.CreateDirectory(_root); }

    string FileOf(string id, string suffix)
    {
        if (id.Length is < 1 or > 48 || id.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_'))) throw new ArgumentException("Invalid save slot.", nameof(id));
        return Path.Combine(_root, id + suffix);
    }

    public IReadOnlyList<SaveSlot> List()
    {
        var slots = new List<SaveSlot>();
        foreach (string path in Directory.EnumerateFiles(_root, "*.json"))
        {
            try
            {
                if (new FileInfo(path).Length > 16384) continue;
                var slot = JsonSerializer.Deserialize<SaveSlot>(File.ReadAllText(path), Json);
                if (slot != null && Path.GetFileNameWithoutExtension(path) == slot.Id && File.Exists(FileOf(slot.Id, ".evo"))) slots.Add(slot);
            }
            catch (Exception e) when (e is IOException or JsonException or ArgumentException) { }
        }
        return slots.OrderByDescending(s => s.SavedUtc).ThenBy(s => s.Id, StringComparer.Ordinal).ToArray();
    }

    public SaveSlot Save(Simulation sim, string id, string title, bool automatic = false)
    {
        if (!automatic && id.StartsWith("auto-", StringComparison.Ordinal)) throw new ArgumentException("Autosave slot IDs are reserved.", nameof(id));
        string world = FileOf(id, ".evo"), meta = FileOf(id, ".json");
        if (string.IsNullOrWhiteSpace(title) || title.Length > 80 || title.Any(char.IsControl)) throw new ArgumentException("Use a save title of 1–80 printable characters.", nameof(title));
        WorldSave.Save(sim, world);
        var slot = new SaveSlot(id, title.Trim(), sim.Config.Seed, sim.Day, sim.Living.Count, sim.Bands.Count, DateTime.UtcNow, automatic);
        string temp = meta + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(slot, Json)); File.Move(temp, meta, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        return slot;
    }

    public SaveSlot SaveNew(Simulation sim, string title) => Save(sim, "world-" + Guid.NewGuid().ToString("N"), title);
    public Simulation Load(string id) => WorldSave.Load(FileOf(id, ".evo"));

    public SaveSlot Autosave(Simulation sim, int keep = 3)
    {
        keep = Math.Clamp(keep, 1, 10);
        var slots = List().Where(s => s.Automatic).OrderBy(s => s.SavedUtc).ToArray();
        string id = slots.Length < keep ? Enumerable.Range(0, keep).Select(i => "auto-" + i).First(candidate => slots.All(s => s.Id != candidate)) : slots[0].Id;
        var saved = Save(sim, id, $"Autosave · year {sim.Year}, day {sim.Day % SimConfig.DaysPerYear + 1}", true);
        foreach (var extra in List().Where(s => s.Automatic).Skip(keep)) Delete(extra.Id);
        return saved;
    }

    public bool Delete(string id)
    {
        string world = FileOf(id, ".evo"), meta = FileOf(id, ".json"); bool exists = File.Exists(world);
        File.Delete(world); File.Delete(meta); return exists;
    }
}
