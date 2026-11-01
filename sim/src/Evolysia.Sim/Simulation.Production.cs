using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Economy;
using Evolysia.Sim.Persistence;
using Evolysia.Sim.Psych;
using Evolysia.Sim.Society;

namespace Evolysia.Sim;

public enum ProductionState : byte { WaitingInputs, Working, WaitingPower, WaitingOutput, Complete, Cancelled }
public sealed record ProductionSpec(string Id, TechId[] Techs, string Station, (ushort good, float qty)[] Inputs,
    (ushort good, float qty)[] Outputs, float Labour, float Power, int ItemIndex = -1);
public sealed class ProductionJob
{
    public int Id, BandId, WorkerId, StationId = -1, Cell, RecipientId = -1, OutputId, Remaining, Completed, CreatedDay, SpoilDay = -1;
    public string Product = "", Outcome = "";
    public float Progress;
    public ProductionState State;
    public Inventory Inputs = new();
    public bool Active => State is not (ProductionState.Complete or ProductionState.Cancelled);
}

public sealed partial class Simulation
{
    [SaveOptional] public readonly Dictionary<int, ProductionJob> Production = new();
    [SaveOptional] int _nextProduction;
    [SaveOptional] readonly Dictionary<int, int> _productionWorkers = new();
    public static readonly ProductionSpec[] Products = Crafts.Recipes.Select(r => new ProductionSpec("recipe/" + r.Id, new[] { r.Tech }, r.Station,
        r.Inputs, r.Outputs, r.Labour, r.Tech is TechId.PowerLooms or TechId.Electronics or TechId.Fabrication or TechId.AdvancedMaterials ? 1f : 0f))
        .Concat(Crafts.Items.Select(i => new ProductionSpec("gear/" + Ids.Snake(i.Slot) + "/" + i.Id, i.Techs, i.Station, i.Inputs,
            Array.Empty<(ushort, float)>(), i.Labour, i.Techs.Any(t => t is TechId.Fabrication or TechId.EnergyTools or TechId.Robotics) ? 2f : 0f, i.Index))).ToArray();
    static readonly IReadOnlyDictionary<string, ProductionSpec> ProductsById = Products.ToDictionary(p => p.Id, StringComparer.Ordinal);

    public static ProductionSpec? Product(string id) => ProductsById.GetValueOrDefault(id);
    public ProductionJob? ProductionFor(int worker) => _productionWorkers.TryGetValue(worker, out int id) && Production.TryGetValue(id, out var job) && job.Active ? job : null;
    public IEnumerable<ProductionSpec> Manufacturable(Band band)
    {
        var known = BandKnowledge(band);
        var stations = band.Structures.Where(id => Structures.TryGetValue(id, out var s) && s.Standing).Select(id => Structures[id].Kind.Id).ToHashSet(StringComparer.Ordinal);
        return Products.Where(p => p.Techs.All(known.Has) && (p.Station.Length == 0 || stations.Contains(p.Station)));
    }

    int ProductionStation(Band band, ProductionSpec spec) => spec.Station.Length == 0 ? -1 : band.Structures.FirstOrDefault(id =>
        Structures.TryGetValue(id, out var s) && s.Standing && s.Kind.Id == spec.Station && !Production.Values.Any(j => j.Active && j.StationId == id), -1);

    bool ValidateProduction(Band band, ProductionOrder[] orders, out string error)
    {
        error = "Production needs distinct adult workers who know the product, an available station, an owned output container and a count of 1–30. Gear recipients must be living members.";
        if (orders.Length > 4 || orders.Any(o => o == null) || orders.Select(o => o.WorkerId).Distinct().Count() != orders.Length) return false;
        var stations = new HashSet<int>();
        foreach (var o in orders)
        {
            if (o.WorkerId == ControlledAgentId || !Living.TryGetValue(o.WorkerId, out var worker) || worker.BandId != band.Id || !worker.IsAdult(Day) || HaulFor(worker.Id) != null || ProductionFor(worker.Id) != null ||
                RoadFor(worker.Id) != null || o.Count is < 1 or > 30 || Product(o.Product) is not { } spec || spec.Techs.Any(t => !worker.Knows.Has(t)) ||
                o.RecipientId != -1 && (!Living.TryGetValue(o.RecipientId, out var recipient) || recipient.BandId != band.Id) ||
                CargoLocation(band.Id, o.OutputId == 0 ? -band.Id : o.OutputId) is not { } output) return false;
            if (spec.Outputs.Any(g => !output.inventory.Accepts(g.good))) return false;
            int station = ProductionStation(band, spec);
            if (spec.Station.Length > 0 && (station < 0 || !stations.Add(station))) return false;
        }
        error = ""; return true;
    }

    public bool QueueProduction(int bandId, ProductionOrder order, out string error)
    {
        error = "Unknown community.";
        if (!Bands.TryGetValue(bandId, out var band) || !ValidateProduction(band, new[] { order }, out error)) return false;
        Services.Remove(order.WorkerId); AddProduction(band, order); return true;
    }

    void AddProduction(Band band, ProductionOrder order)
    {
        var spec = Product(order.Product)!; int id = ++_nextProduction, station = ProductionStation(band, spec);
        Production[id] = new ProductionJob { Id = id, BandId = band.Id, WorkerId = order.WorkerId, Product = spec.Id, Remaining = order.Count,
            RecipientId = order.RecipientId, StationId = station, Cell = station < 0 ? band.Cell : Structures[station].Cell,
            OutputId = order.OutputId == 0 ? -band.Id : order.OutputId, CreatedDay = Day, Inputs = new Inventory($"production/{id}") };
        _productionWorkers[order.WorkerId] = id;
    }

    bool WorkProduction(Agent worker, Band band)
    {
        if (ProductionFor(worker.Id) is not { } job) return false;
        var spec = Product(job.Product)!; worker.Site = job.Cell;
        worker.Needs[(int)Need.Energy] += 0.1f;
        if (CargoLocation(band.Id, job.OutputId) is not { } output || job.StationId >= 0 && (!Structures.TryGetValue(job.StationId, out var station) || !station.Standing)) return true;
        if (spec.Inputs.Any(m => !band.Stores.Has(m.good, MathF.Max(0f, m.qty - job.Inputs[m.good])))) { job.State = ProductionState.WaitingInputs; return true; }
        foreach (var (g, q) in spec.Inputs)
        {
            float missing = MathF.Max(0f, q - job.Inputs[g]);
            if (missing > 0f) band.Stores.TransferTo(job.Inputs, g, missing);
        }
        if (spec.Inputs.Any(m => !job.Inputs.Has(m.good, m.qty))) { job.State = ProductionState.WaitingInputs; return true; }
        float effort = MathF.Min(MathF.Max(0f, spec.Labour - job.Progress), 0.3f * (0.6f + worker.Efficacy[(int)Domain.Crafting]) * ToolBonus(worker, Activity.Craft));
        if (!ConsumePower(band.Id, spec.Power * effort / MathF.Max(0.01f, spec.Labour))) { job.State = ProductionState.WaitingPower; return true; }
        job.Progress += effort; job.State = ProductionState.Working;
        if (job.Progress + 1e-5f < spec.Labour) return true;
        float bulk = spec.Outputs.Sum(o => o.qty * Goods.All[o.good].Weight);
        for (var inventory = output.inventory; inventory != null; inventory = inventory.Parent)
            if (inventory.Bulk + bulk > inventory.Capacity + 1e-4f) { job.State = ProductionState.WaitingOutput; return true; }
        foreach (var (g, q) in spec.Inputs) job.Inputs.Take(g, q);
        foreach (var (g, q) in spec.Outputs) output.inventory.Add(g, q);
        if (spec.ItemIndex >= 0)
        {
            var gear = Crafts.Items[spec.ItemIndex];
            if (job.RecipientId >= 0 && Living.TryGetValue(job.RecipientId, out var recipient) && recipient.BandId == band.Id) Equip(recipient, band, gear);
            else band.Spares.Add(new Item(gear.Index));
        }
        job.Progress = 0f; job.Completed++; job.Remaining--;
        SawMade(band, spec);
        Psychology.Practice(worker, Domain.Crafting, true, 0.6f);
        if (job.Remaining == 0) { job.State = ProductionState.Complete; job.Outcome = "Manufacturing order completed."; }
        return true;
    }

    public bool CancelProduction(int id)
    {
        if (!Production.TryGetValue(id, out var job) || !job.Active) return false;
        job.Inputs.MoveAllTo(GroundAt(job.Cell)); job.State = ProductionState.Cancelled; job.Outcome = "Surviving inputs left at the work site."; return true;
    }

    void MaintainProduction()
    {
        foreach (var job in Production.Values.Where(j => j.Active))
        {
            if (!Living.TryGetValue(job.WorkerId, out var worker) || worker.BandId != job.BandId || CargoLocation(job.BandId, job.OutputId) == null ||
                job.StationId >= 0 && (!Structures.TryGetValue(job.StationId, out var station) || !station.Standing || station.BandId != job.BandId)) CancelProduction(job.Id);
            else if (job.SpoilDay != Day) { job.Inputs.Spoil(1f); job.SpoilDay = Day; }
        }
        foreach (int id in Production.Values.Where(j => !j.Active).OrderByDescending(j => j.Id).Skip(256).Select(j => j.Id).ToArray()) Production.Remove(id);
    }

    void WeatherLooseStock()
    {
        var empty = new List<int>();
        foreach (var (cell, inventory) in GroundStock)
        {
            inventory.Spoil(World.Temperature(cell, Day) > 20f ? 1.5f : 1f);
            if (inventory.Stacks.Count == 0 && !Hauls.Values.Any(h => h.Active && (h.SourceId == GroundContainerId(cell) || h.TargetId == GroundContainerId(cell)))) empty.Add(cell);
        }
        foreach (int cell in empty) GroundStock.Remove(cell);
    }

    public void RebuildWorkIndexes()
    {
        _productionWorkers.Clear(); _haulWorkers.Clear(); _roadWorkers.Clear();
        foreach (var job in Production.Values) if (job.Active) _productionWorkers[job.WorkerId] = job.Id;
        foreach (var job in Hauls.Values) if (job.Active) _haulWorkers[job.WorkerId] = job.Id;
        foreach (var job in RoadProjects.Values) if (job.Active) _roadWorkers[job.WorkerId] = job.Id;
    }
}
