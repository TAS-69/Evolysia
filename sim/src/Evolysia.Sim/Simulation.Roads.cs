using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Economy;
using Evolysia.Sim.Persistence;
using Evolysia.Sim.Society;

namespace Evolysia.Sim;

public sealed record RoadOrder(int WorkerId, int FromId, int ToId, string Surface);
public sealed class RoadProject
{
    public int Id, BandId, WorkerId, Index;
    public int[] Cells = Array.Empty<int>();
    public byte Surface;
    public float Progress;
    public bool Cancelled;
    public bool Active => !Cancelled && Index < Cells.Length;
}

public sealed partial class Simulation
{
    [SaveOptional] public readonly Dictionary<int, byte> Roads = new();
    [SaveOptional] public readonly Dictionary<int, float> Traffic = new();
    [SaveOptional] public readonly Dictionary<int, RoadProject> RoadProjects = new();
    [SaveOptional] readonly Dictionary<int, int> _roadWorkers = new();
    [SaveOptional] int _nextRoad;
    public RoadProject? RoadFor(int worker) => _roadWorkers.TryGetValue(worker, out int id) && RoadProjects.TryGetValue(id, out var road) && road.Active ? road : null;

    int RoadEndpoint(Band band, int id)
    {
        if (id == -band.Id) return band.Cell;
        if (!Structures.TryGetValue(id, out var site) || site.BandId != band.Id || !site.Standing || site.ParentBuildingId >= 0 || site.Kind.Role is StructureRole.Vessel or StructureRole.Defence) return -1;
        return site.Layout == null ? site.Cell : World.Index(World.X(site.Cell) + site.Kind.W / 2, World.Y(site.Cell) + site.Kind.H - 1);
    }

    bool ValidateRoads(Band band, RoadOrder[] orders, out string error)
    {
        error = "Roads need distinct adult builders, two different reachable owned endpoints, and path/dirt/stone material known by the builder. Routes are limited to 512 cells.";
        if (orders.Length > 2 || orders.Any(o => o == null) || orders.Select(o => o.WorkerId).Distinct().Count() != orders.Length) return false;
        foreach (var order in orders)
        {
            if (order.WorkerId == ControlledAgentId || !Living.TryGetValue(order.WorkerId, out var worker) || worker.BandId != band.Id || !worker.IsAdult(Day) || RoadFor(worker.Id) != null ||
                ProductionFor(worker.Id) != null || HaulFor(worker.Id) != null || ServiceFor(worker.Id) != null || order.FromId == order.ToId ||
                order.Surface is not ("path" or "dirt" or "stone") || order.Surface == "dirt" && !worker.Knows.Has(TechId.Wheel) || order.Surface == "stone" && !worker.Knows.Has(TechId.Roads)) return false;
            int from = RoadEndpoint(band, order.FromId), to = RoadEndpoint(band, order.ToId);
            if (from < 0 || to < 0 || World.Distance(from, to) > World.Cells(12) || Route(from, to, cardinal: true) is not { Length: > 1 and <= 512 }) return false;
        }
        error = ""; return true;
    }

    public bool QueueRoad(int bandId, RoadOrder order, out string error)
    {
        error = "Unknown community.";
        if (!Bands.TryGetValue(bandId, out var band) || !ValidateRoads(band, new[] { order }, out error)) return false;
        AddRoad(band, order); return true;
    }

    void AddRoad(Band band, RoadOrder order)
    {
        int id = ++_nextRoad;
        RoadProjects[id] = new RoadProject { Id = id, BandId = band.Id, WorkerId = order.WorkerId, Surface = order.Surface == "stone" ? (byte)3 : order.Surface == "dirt" ? (byte)2 : (byte)1,
            Cells = Route(RoadEndpoint(band, order.FromId), RoadEndpoint(band, order.ToId), cardinal: true)! };
        _roadWorkers[order.WorkerId] = id;
    }

    bool WorkRoad(Agent worker, Band band)
    {
        if (RoadFor(worker.Id) is not { } project) return false;
        int cell = project.Cells[project.Index]; worker.Site = cell; worker.Needs[(int)Need.Energy] += 0.12f;
        if (!Walkable(cell)) { project.Cancelled = true; return true; }
        if (Roads.GetValueOrDefault(cell) >= project.Surface) { project.Index++; project.Progress = 0f; return true; }
        if (project.Surface >= 2 && World.Trees[cell] > 0.01f)
        { float cut = MathF.Min(2f, World.Trees[cell]); World.Trees[cell] -= cut; band.Stores.Add("wood", cut); return true; }
        if (project.Surface >= 2 && World.Rock[cell] > 0.01f)
        { float cut = MathF.Min(2f, World.Rock[cell]); World.Rock[cell] -= cut; band.Stores.Add("stone", cut); return true; }
        project.Progress += 0.5f + worker.Efficacy[(int)Domain.Building] * 0.5f;
        if (project.Progress < 1f) return true;
        string material = project.Surface == 3 ? "stone_blocks" : "stone"; float cost = project.Surface == 3 ? 1f : project.Surface == 2 ? 0.3f : 0f;
        if (cost > 0f && !band.Stores.Has(Goods.Id(material), cost)) return true;
        if (cost > 0f) band.Stores.Take(material, cost);
        Roads[cell] = project.Surface; project.Index++; project.Progress = 0f; return true;
    }

    void MaintainRoads()
    {
        foreach (var project in RoadProjects.Values)
            if (project.Active && (!Living.TryGetValue(project.WorkerId, out var worker) || worker.BandId != project.BandId)) project.Cancelled = true;
        foreach (int id in RoadProjects.Values.Where(r => !r.Active).OrderByDescending(r => r.Id).Skip(128).Select(r => r.Id).ToArray()) RoadProjects.Remove(id);
    }

    void Tread(int cell)
    {
        if (!World.IsDryLand(cell)) return;
        Traffic[cell] = MathF.Min(20f, Traffic.GetValueOrDefault(cell) + 1f);
        if (Traffic[cell] >= 20f && !Roads.ContainsKey(cell)) Roads[cell] = 1;
    }
}
