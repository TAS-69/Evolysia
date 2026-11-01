using System.Text.Json;

namespace Evolysia.Sim.Economy;

public sealed record RoomObject(string Kind, int X, int Y);
public sealed record RoomLayout(string Id, int Width, int Height, string Floor, string? Wall, string? Roof, RoomObject[] Objects, (int x, int y)[] Walkable);
public static class RoomLayouts
{
    public static readonly IReadOnlyDictionary<string, RoomLayout> All = Load();
    static Dictionary<string, RoomLayout> Load()
    {
        using var stream = typeof(RoomLayouts).Assembly.GetManifestResourceStream("Evolysia.Rooms.json")!;
        using var json = JsonDocument.Parse(stream);
        var result = new Dictionary<string, RoomLayout>();
        foreach (var room in json.RootElement.GetProperty("blueprints").EnumerateArray())
        {
            string? Optional(string name) => room.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            string id = room.GetProperty("id").GetString()!;
            var objects = room.GetProperty("objects").EnumerateArray().Select(o => new RoomObject(o.GetProperty("asset_id").GetString()!, o.GetProperty("cell")[0].GetInt32(), o.GetProperty("cell")[1].GetInt32())).ToArray();
            var walkable = room.GetProperty("walkable_cells").EnumerateArray().Select(c => (c[0].GetInt32(), c[1].GetInt32())).ToArray();
            result[id] = new RoomLayout(id, room.GetProperty("size_cells")[0].GetInt32(), room.GetProperty("size_cells")[1].GetInt32(), Optional("floor") ?? "earth", Optional("wall"), Optional("roof"), objects, walkable);
        }
        return result;
    }
}
