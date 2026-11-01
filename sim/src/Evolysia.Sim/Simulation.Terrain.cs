using Evolysia.Sim.Core;
using Evolysia.Sim.World;

namespace Evolysia.Sim;

public sealed partial class Simulation
{
    void ShapeLand(int centre, int radius, float change)
    {
        var cells = CellsNear(centre, radius).Distinct().ToArray();
        foreach (int cell in cells)
        {
            World.Elevation[cell] = Math.Clamp(World.Elevation[cell] + change, -8000f, 8000f);
            World.TempMean[cell] = Math.Clamp(World.TempMean[cell] - change * 0.0065f, -65f, 55f);
            Roads.Remove(cell); Traffic.Remove(cell);
        }
        WorldGen.RefreshEditedTerrain(World, cells, Config.CapacityScale);
        foreach (int cell in Roads.Keys.Where(c => !World.IsDryLand(c)).ToArray()) { Roads.Remove(cell); Traffic.Remove(cell); }
        foreach (var building in Structures.Values.Where(s => s.Standing && s.Kind.Role != Economy.StructureRole.Vessel).ToArray())
            if (Enumerable.Range(0, building.Kind.W * building.Kind.H).Any(i => !World.IsDryLand(World.Index(World.X(building.Cell) + i % building.Kind.W, Math.Min(World.H - 1, World.Y(building.Cell) + i / building.Kind.W))))) Ruin(building);
        foreach (var band in Bands.Values.ToArray())
        {
            if (World.IsDryLand(band.Cell)) continue;
            int target = CellsNear(band.Cell, World.Cells(4)).Where(Walkable).OrderBy(c => World.Distance(c, band.Cell)).FirstOrDefault(-1);
            if (target >= 0) { LeaveCamp(band, target); band.Cell = target; band.LastMoveDay = Day; ArrivedAt(band); Reconsider(band.Id); }
            else foreach (int id in band.Members.ToArray()) if (Living.TryGetValue(id, out var person)) Kill(person, DeathCause.Disaster, Stimulus.Flood, ", lost beneath the changed waters");
        }
        if (Living.TryGetValue(ControlledAgentId, out var controlled) && controlled.Alive && !World.IsDryLand(controlled.Site))
        {
            int escape = CellsNear(controlled.Site, World.Cells(1)).Where(Walkable).OrderBy(c => World.Distance(c, controlled.Site)).FirstOrDefault(-1);
            if (escape >= 0) { controlled.Site = escape; ControlTargetCell = escape; }
            else Kill(controlled, DeathCause.Disaster, Stimulus.Flood, ", lost beneath the changed waters");
        }
        BandsChanged();
        Chronicle.Add(new WorldEvent(Day, EventCategory.Danger, "terrain_changed", change > 0 ? "You raise the land; new heights emerge." : "You lower the land; water claims the hollows.", 0.8f));
    }
}
