using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Economy;
using Evolysia.Sim.Society;

namespace Evolysia.Sim;

public sealed partial class Simulation
{
    public (ushort good, float qty)[] ConstructionMaterials(Band band, StructureDef kind)
    {
        bool bamboo = World.Biome[band.Cell] is Biome.TropicalRainforest or Biome.TropicalSeasonalForest or Biome.Wetland;
        bool reeds = World.Biome[band.Cell] == Biome.Wetland;
        return kind.Materials.Select(m => (good: bamboo && Goods.All[m.good].Id is "wood" or "log" ? Goods.Id("bamboo") : reeds && Goods.All[m.good].Id == "fiber" ? Goods.Id("reed") : m.good, m.qty))
            .GroupBy(m => m.good).Select(g => (g.Key, g.Sum(m => m.qty))).ToArray();
    }

    void BuildInterior(Structure parent)
    {
        if (parent.InteriorBuilt || parent.ParentBuildingId >= 0 || parent.Layout is not { } room || !Bands.TryGetValue(parent.BandId, out var band)) return;
        var known = BandKnowledge(band);
        if (parent.Contents.Parent == null) band.Stores.Attach(parent.Contents);
        foreach (var obj in room.Objects)
        {
            int cell = World.Index(World.X(parent.Cell) + obj.X, World.Y(parent.Cell) + obj.Y);
            if (Structures.Values.Any(s => s.ParentBuildingId == parent.Id && s.Cell == cell && s.Kind.Id == obj.Kind && s.State is not (StructureState.Ruined or StructureState.Burned))) continue;
            var kind = Economy.Structures.Get(obj.Kind);
            if (kind == null || !Economy.Structures.CanBuild(known, kind)) continue;
            var materials = ConstructionMaterials(band, kind);
            if (materials.Any(m => !band.Stores.Has(m.good, m.qty))) continue;
            foreach (var (g, q) in materials) band.Stores.Take(g, q);
            var fixture = new Structure { Id = _nextStructure++, Def = kind.Index, Cell = cell, BandId = band.Id, ParentBuildingId = parent.Id,
                State = StructureState.Complete, Condition = 1f, BuiltDay = Day, PlannedDay = Day, InteriorBuilt = true, RegionalMaterials = materials };
            Structures[fixture.Id] = fixture; band.Structures.Add(fixture.Id);
            if (kind.Capacity > 0f && fixture.Contents.Bulk <= parent.Contents.Capacity - parent.Contents.Bulk) parent.Contents.Attach(fixture.Contents);
        }
        parent.InteriorBuilt = room.Objects.All(o => Structures.Values.Any(s => s.ParentBuildingId == parent.Id && s.Kind.Id == o.Kind && s.Standing && s.Cell == World.Index(World.X(parent.Cell) + o.X, World.Y(parent.Cell) + o.Y)));
    }

    public bool Walkable(int cell)
    {
        if (!World.IsDryLand(cell)) return false;
        int id = World.StructureAt[cell];
        if (id == 0 || !Structures.TryGetValue(id, out var structure) || !structure.Standing) return true;
        if (structure.Kind.Role == StructureRole.Defence) return false;
        if (structure.Layout is not { } room) return true;
        int x = (World.X(cell) - World.X(structure.Cell) + World.W) % World.W, y = World.Y(cell) - World.Y(structure.Cell);
        return room.Walkable.Contains((x, y));
    }
}
