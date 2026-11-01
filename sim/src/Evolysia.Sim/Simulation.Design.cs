using Evolysia.Sim.Core;
using Evolysia.Sim.Economy;
using Evolysia.Sim.Society;

namespace Evolysia.Sim;

public sealed partial class Simulation
{
    public readonly Dictionary<string, StructureDesign> Designs = new();
    int _nextDesign = 1;
    public StructureDef? BuildingDefinition(string id) => Designs.TryGetValue(id, out var design) ? design.Definition : Economy.Structures.Get(id);
    public static bool BuildingTemplate(StructureDef def) => RoomLayouts.All.ContainsKey(def.Id) || def.Id is "windbreak" or "lean_to" or "hut" or "house" or "stone_house" or "brick_house" or "mudbrick_house";

    public bool ValidateDesign(Band band, BuildingIdea idea, out string error)
    {
        error = "A design needs a known building template, a 3–12 cell footprint, affordable known fixtures, and access to every fixture from the south-centre doorway.";
        if (Designs.Values.Count(d => d.BandId == band.Id) >= 64 || Designs.Count >= 4096 || idea == null || !ValidText(idea.Name, 80) || string.IsNullOrWhiteSpace(idea.Name) || idea.Width is < 3 or > 12 || idea.Height is < 3 or > 12 ||
            idea.Objects == null || idea.Objects.Length > 16) return false;
        var known = BandKnowledge(band); var template = Economy.Structures.Get(idea.Template);
        if (template == null || !BuildingTemplate(template) || !Economy.Structures.CanBuild(known, template) || template.Materials.Any(m => !CanObtain(known, m.good))) return false;
        var occupied = new HashSet<(int x, int y)>();
        foreach (var obj in idea.Objects)
        {
            if (obj == null || obj.X < 1 || obj.Y < 1 || obj.X >= idea.Width - 1 || obj.Y >= idea.Height - 1 || !occupied.Add((obj.X, obj.Y)))
            { error = $"Design {idea.Name} ({idea.Width}x{idea.Height}): {obj?.Kind} at ({obj?.X},{obj?.Y}) is outside the interior or overlaps another object. X must be 1..{idea.Width - 2}, Y 1..{idea.Height - 2}. Use designs: [] if a new layout is unnecessary."; return false; }
            var kind = Economy.Structures.Get(obj.Kind);
            if (kind == null || kind.W != 1 || kind.H != 1 || kind.Role is StructureRole.Vessel or StructureRole.Field or StructureRole.Defence || !Economy.Structures.CanBuild(known, kind) || kind.Materials.Any(m => !CanObtain(known, m.good)))
            { error = $"Design fixture {obj.Kind} is unavailable or cannot go indoors. Use a known one-cell indoor fixture, or designs: []."; return false; }
        }
        var entrance = (x: idea.Width / 2, y: idea.Height - 1);
        var reached = new HashSet<(int x, int y)> { entrance }; var queue = new Queue<(int x, int y)>(); queue.Enqueue(entrance);
        var directions = new[] { (x: 0, y: -1), (x: 1, y: 0), (x: 0, y: 1), (x: -1, y: 0) };
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var d in directions)
            {
                var next = (x: cell.x + d.x, y: cell.y + d.y);
                if (next.x < 1 || next.y < 1 || next.x >= idea.Width - 1 || next.y >= idea.Height - 1 || occupied.Contains(next) || !reached.Add(next)) continue;
                queue.Enqueue(next);
            }
        }
        foreach (var obj in idea.Objects)
            if (!directions.Any(d => reached.Contains((obj.X + d.x, obj.Y + d.y))))
            { error = $"No clear route from door ({entrance.x},{entrance.y}) to {obj.Kind} at ({obj.X},{obj.Y}). Leave an empty corridor, or use designs: []."; return false; }
        error = ""; return true;
    }

    string RegisterDesign(Band band, BuildingIdea idea)
    {
        var template = Economy.Structures.Need(idea.Template); string id = "designed_" + _nextDesign++;
        float scale = idea.Width * idea.Height / (float)(template.W * template.H);
        var definition = template with { Index = ushort.MaxValue, Id = id, W = idea.Width, H = idea.Height,
            Materials = template.Materials.Select(m => (m.good, MathF.Max(1f, m.qty * scale))).ToArray(), Labour = MathF.Max(5f, template.Labour * scale),
            Housing = template.Housing > 0 ? Math.Clamp((int)(template.Housing * scale), 1, 40) : 0, Capacity = template.Capacity * scale };
        var original = RoomLayouts.All.GetValueOrDefault(template.Id);
        var occupied = idea.Objects.Select(o => (o.X, o.Y)).ToHashSet(); var cells = new List<(int, int)> { (idea.Width / 2, idea.Height - 1) };
        for (int y = 1; y < idea.Height - 1; y++) for (int x = 1; x < idea.Width - 1; x++) if (!occupied.Contains((x, y))) cells.Add((x, y));
        var layout = new RoomLayout(id, idea.Width, idea.Height, original?.Floor ?? "earth", original == null ? template.Id is "stone_house" or "brick_house" ? "stone" : "wattle" : original.Wall,
            original == null ? template.Id is "windbreak" or "lean_to" ? null : "thatch" : original.Roof,
            idea.Objects.ToArray(), cells.ToArray());
        Designs[id] = new StructureDesign(idea.Name, band.Id, definition, layout);
        return id;
    }
}
