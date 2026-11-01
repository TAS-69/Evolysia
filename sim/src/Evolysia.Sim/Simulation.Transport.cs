using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Economy;
using Evolysia.Sim.Persistence;

namespace Evolysia.Sim;

public enum HaulState : byte { GoingToSource, Carrying, Delivered, Cancelled, GoingToVehicle }
public sealed class HaulJob
{
    public int Id, BandId, WorkerId, SourceId, TargetId, VehicleId, Cell, RouteIndex;
    public ushort Good;
    public float Quantity, Loaded, Delivered;
    public int[] Route = Array.Empty<int>();
    public HaulState State;
    public string Outcome = "";
    [SaveOptional] public float MovementCredit;
    public bool Active => State is not (HaulState.Delivered or HaulState.Cancelled);
}

public sealed partial class Simulation
{
    public static int GroundContainerId(int cell) => -1_000_000_000 - cell;
    public readonly Dictionary<int, HaulJob> Hauls = new();
    public readonly Dictionary<int, Inventory> GroundStock = new();
    public Inventory GroundAt(int cell)
    {
        if (cell < 0 || cell >= World.Count) throw new ArgumentOutOfRangeException(nameof(cell));
        if (!GroundStock.TryGetValue(cell, out var stock)) GroundStock[cell] = stock = new Inventory($"ground/{cell}");
        return stock;
    }
    int _nextHaul = 1;
    [SaveOptional] readonly Dictionary<int, int> _haulWorkers = new();
    public HaulJob? HaulFor(int agentId) => _haulWorkers.TryGetValue(agentId, out int id) && Hauls.TryGetValue(id, out var job) && job.Active ? job : null;
    static bool SeaVehicle(Structure vehicle) => vehicle.Kind.Id is "raft" or "canoe" or "rowing_boat" or "sailboat" or "cargo_ship";
    public (Inventory inventory, int cell)? CargoLocation(int bandId, int id)
    {
        if (!Bands.TryGetValue(bandId, out var band) || band.Disbanded) return null;
        if (id <= -1_000_000_000)
        {
            int cell = -(id + 1_000_000_000);
            return cell >= 0 && cell < World.Count && World.Distance(cell, band.Cell) <= World.Cells(12) && GroundStock.TryGetValue(cell, out var ground) ? (ground, cell) : null;
        }
        if (id == -bandId) return (band.Stores, band.Cell);
        return Structures.TryGetValue(id, out var s) && s.BandId == bandId && s.Standing ? (s.Contents, s.TransitCell >= 0 ? s.TransitCell : s.Cell) : null;
    }

    public int[]? Route(int start, int target, bool water = false, bool cardinal = false)
    {
        if (start < 0 || target < 0 || start >= World.Count || target >= World.Count) return null;
        var previous = new Dictionary<int, int> { [start] = -1 }; var queue = new Queue<int>(); queue.Enqueue(start);
        Span<int> neighbours = stackalloc int[8];
        while (queue.Count > 0 && previous.Count < 32768)
        {
            int cell = queue.Dequeue();
            if (cell == target)
            {
                var path = new List<int>(); for (int c = target; c >= 0; c = previous[c]) path.Add(c);
                path.Reverse(); return path.ToArray();
            }
            int count = World.Neighbors(cell, neighbours);
            for (int i = 0; i < count; i++)
            {
                int next = neighbours[i];
                if (cardinal && World.X(next) != World.X(cell) && World.Y(next) != World.Y(cell)) continue;
                if (previous.ContainsKey(next) || next != target && (water ? !World.IsWater(next) : !Walkable(next))) continue;
                if (!water && World.StructureAt[next] is var sid && sid != 0 && Structures.TryGetValue(sid, out var wall) && wall.Kind.Role == StructureRole.Defence && next != target) continue;
                previous[next] = cell; queue.Enqueue(next);
            }
        }
        return null;
    }

    public bool QueueShipment(int bandId, ShipmentOrder order, out string error)
    {
        if (!ValidateShipments(bandId, new[] { order }, out error)) return false;
        AddShipment(bandId, order); return true;
    }

    bool ValidateShipments(int bandId, ShipmentOrder[] orders, out string error)
    {
        error = "Cargo requires reachable owned containers, available stock, capacity and an adult carrier.";
        if (orders.Length > 4 || orders.Any(o => o == null) || orders.Select(o => o.WorkerId).Distinct().Count() != orders.Length) return false;
        var held = new Dictionary<(int source, ushort good), float>(); var incoming = new Dictionary<int, float>(); var vehicles = new HashSet<int>();
        foreach (var order in orders)
        {
            if (order.WorkerId == ControlledAgentId || !Living.TryGetValue(order.WorkerId, out var a) || a.BandId != bandId || !a.IsAdult(Day) || HaulFor(a.Id) != null || ProductionFor(a.Id) != null || ServiceFor(a.Id) != null || RoadFor(a.Id) != null ||
                CargoLocation(bandId, order.SourceId) is not { } from || CargoLocation(bandId, order.TargetId) is not { } to || order.SourceId == order.TargetId ||
                !float.IsFinite(order.Quantity) || order.Quantity <= 0f) return false;
            var good = Goods.All.FirstOrDefault(g => g.Id == order.Good); if (good == null) return false;
            if (!to.inventory.Accepts(good.Index)) return false;
            float reserved = held.GetValueOrDefault((order.SourceId, good.Index)) + order.Quantity; held[(order.SourceId, good.Index)] = reserved;
            if (from.inventory.AvailableLocal(good.Index) < reserved - 1e-4f) return false;
            float onWay = Hauls.Values.Where(h => h.TargetId == order.TargetId && h.Active).Sum(h => h.Quantity * Goods.All[h.Good].Weight);
            float arriving = incoming.GetValueOrDefault(order.TargetId) + order.Quantity * good.Weight; incoming[order.TargetId] = arriving;
            if (to.inventory.Bulk + onWay + arriving > to.inventory.Capacity + 1e-4f) return false;
            bool sea = false; int pickup = a.Site >= 0 ? a.Site : Bands[bandId].Cell;
            if (order.VehicleId >= 0)
            {
                if (!vehicles.Add(order.VehicleId) || !Structures.TryGetValue(order.VehicleId, out var vehicle) || vehicle.BandId != bandId || !vehicle.Standing ||
                    vehicle.Id == order.SourceId || vehicle.Id == order.TargetId || vehicle.Kind.Role != StructureRole.Vessel || Hauls.Values.Any(h => h.VehicleId == vehicle.Id && h.Active) ||
                    vehicle.Contents.Bulk + order.Quantity * good.Weight > vehicle.Contents.Capacity) return false;
                sea = SeaVehicle(vehicle);
                int vehicleCell = vehicle.TransitCell >= 0 ? vehicle.TransitCell : vehicle.Cell;
                if (Route(pickup, vehicleCell) == null || Route(vehicleCell, from.cell, sea) == null) return false;
                pickup = vehicleCell;
            }
            else if (order.Quantity * good.Weight > 20f) return false;
            if (Route(pickup, from.cell, sea) == null || Route(from.cell, to.cell, sea) == null) return false;
        }
        error = ""; return true;
    }

    void AddShipment(int bandId, ShipmentOrder order)
    {
        var a = Living[order.WorkerId]; var source = CargoLocation(bandId, order.SourceId)!.Value;
        var job = new HaulJob { Id = _nextHaul++, BandId = bandId, WorkerId = a.Id, SourceId = order.SourceId, TargetId = order.TargetId,
            VehicleId = order.VehicleId, Good = Goods.Id(order.Good), Quantity = order.Quantity, Cell = a.Site >= 0 ? a.Site : Bands[bandId].Cell };
        if (job.VehicleId >= 0)
        {
            var vehicle = Structures[job.VehicleId]; job.State = HaulState.GoingToVehicle;
            job.Route = Route(job.Cell, vehicle.TransitCell >= 0 ? vehicle.TransitCell : vehicle.Cell)!;
        }
        else job.Route = Route(job.Cell, source.cell)!;
        source.inventory.Reserve(job.Id, job.Good, job.Quantity); Hauls[job.Id] = job;
        _haulWorkers[job.WorkerId] = job.Id;
    }

    Inventory CargoOf(HaulJob job) => job.VehicleId >= 0 ? Structures[job.VehicleId].Contents : Living[job.WorkerId].Pack;
    void CancelHaul(HaulJob job, string reason)
    {
        if (job.SourceId <= -1_000_000_000 && GroundStock.TryGetValue(-(job.SourceId + 1_000_000_000), out var loose)) loose.Release(job.Id);
        if (Structures.TryGetValue(job.SourceId, out var original)) original.Contents.Release(job.Id);
        else if (job.SourceId == -job.BandId && Bands.TryGetValue(job.BandId, out var band)) band.Stores.Release(job.Id);
        if (job.VehicleId >= 0 && Structures.TryGetValue(job.VehicleId, out var vehicle)) vehicle.Contents.Release(job.Id);
        else if (Living.TryGetValue(job.WorkerId, out var carrier)) carrier.Pack.Release(job.Id);
        job.State = HaulState.Cancelled; job.Outcome = reason;
    }

    void AdvanceHauls()
    {
        foreach (var job in Hauls.Values.OrderBy(h => h.Id))
        {
            if (job.State is HaulState.Delivered or HaulState.Cancelled) continue;
            if (!Living.TryGetValue(job.WorkerId, out var a) || a.BandId != job.BandId ||
                CargoLocation(job.BandId, job.SourceId) is not { } source || CargoLocation(job.BandId, job.TargetId) is not { } target ||
                job.VehicleId >= 0 && (!Structures.TryGetValue(job.VehicleId, out var v) || !v.Standing || v.BandId != job.BandId))
            { CancelHaul(job, "Carrier or container became unavailable."); continue; }
            if (a.Today != Activity.Haul) continue;
            if (job.RouteIndex + 1 < job.Route.Length)
            {
                bool sea = job.State != HaulState.GoingToVehicle && job.VehicleId >= 0 && SeaVehicle(Structures[job.VehicleId]);
                job.MovementCredit = MathF.Min(3f, job.MovementCredit + 1f);
                while (job.RouteIndex + 1 < job.Route.Length)
                {
                    int next = job.Route[job.RouteIndex + 1];
                    bool dock = sea && job.RouteIndex + 2 == job.Route.Length && World.IsDryLand(next) && World.Distance(next, job.Cell) == 1;
                    bool boarding = job.State == HaulState.GoingToVehicle && job.RouteIndex + 2 == job.Route.Length && World.IsWater(next);
                    if (!(sea ? World.IsWater(next) || dock : Walkable(next) || boarding))
                    {
                        int destination = job.State == HaulState.GoingToVehicle ? Structures[job.VehicleId].Cell : job.State == HaulState.GoingToSource ? source.cell : target.cell;
                        var alternative = Route(job.Cell, destination, sea);
                        if (alternative is not { Length: > 1 } || alternative[1] == next) { CancelHaul(job, "The route became impassable."); break; }
                        job.Route = alternative; job.RouteIndex = 0; job.MovementCredit = 0f; break;
                    }
                    byte road = Math.Min(Roads.GetValueOrDefault(job.Cell), Roads.GetValueOrDefault(next));
                    float cost = sea ? 1f : road >= 3 ? 0.5f : road == 2 ? 2f / 3f : road == 1 ? 0.8f : 1f;
                    if (job.MovementCredit + 1e-5f < cost) break;
                    job.MovementCredit -= cost; job.Cell = next; job.RouteIndex++; a.Site = job.Cell; Tread(job.Cell);
                    if (job.State != HaulState.GoingToVehicle && job.VehicleId >= 0)
                    {
                        var vehicle = Structures[job.VehicleId]; vehicle.TransitCell = vehicle.Cell = job.Cell;
                    }
                }
                continue;
            }
            if (job.State == HaulState.GoingToVehicle)
            {
                var vehicle = Structures[job.VehicleId];
                for (int y = 0; y < vehicle.Kind.H; y++) for (int x = 0; x < vehicle.Kind.W; x++)
                {
                    int cell = World.Index(World.X(vehicle.Cell) + x, World.Y(vehicle.Cell) + y);
                    if (World.StructureAt[cell] == vehicle.Id) World.StructureAt[cell] = 0;
                }
                vehicle.ParentBuildingId = -1; vehicle.TransitCell = job.Cell;
                vehicle.Contents.Detach(); Bands[job.BandId].Stores.Attach(vehicle.Contents);
                job.State = HaulState.GoingToSource; job.RouteIndex = 0;
                job.Route = Route(job.Cell, source.cell, SeaVehicle(vehicle)) ?? Array.Empty<int>();
                if (job.Route.Length == 0) CancelHaul(job, "The vehicle route was blocked.");
            }
            else if (job.State == HaulState.GoingToSource)
            {
                source.inventory.Release(job.Id);
                job.Loaded = source.inventory.TransferLocalTo(CargoOf(job), job.Good, job.Quantity);
                if (job.Loaded <= 0f) { CancelHaul(job, "The stock was no longer available."); continue; }
                CargoOf(job).Reserve(job.Id, job.Good, job.Loaded);
                job.State = HaulState.Carrying; job.RouteIndex = 0;
                job.Route = Route(job.Cell, target.cell, job.VehicleId >= 0 && SeaVehicle(Structures[job.VehicleId])) ?? Array.Empty<int>();
                if (job.Route.Length == 0) CancelHaul(job, "The route was blocked.");
            }
            else
            {
                CargoOf(job).Release(job.Id);
                job.Delivered = CargoOf(job).TransferLocalTo(target.inventory, job.Good, job.Loaded);
                job.State = HaulState.Delivered; job.Outcome = job.Delivered + 1e-4f >= job.Loaded ? "Cargo delivered." : "Part delivered; remaining cargo stays with its carrier.";
            }
        }
        foreach (int id in Hauls.Values.Where(h => h.State is HaulState.Delivered or HaulState.Cancelled).OrderByDescending(h => h.Id).Skip(128).Select(h => h.Id).ToArray()) Hauls.Remove(id);
    }
}
