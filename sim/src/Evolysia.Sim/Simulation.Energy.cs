using Evolysia.Sim.Economy;
using Evolysia.Sim.Persistence;

namespace Evolysia.Sim;

public sealed class EnergyGrid
{
    public int Day;
    public float Produced, Remaining, Consumed, Stored, Capacity, Reserve, Spilled;
    public float Available => Remaining + MathF.Max(0f, Stored - Capacity * Reserve);
}

public sealed partial class Simulation
{
    [SaveOptional] public readonly Dictionary<int, EnergyGrid> Energy = new();

    void PrepareEnergy()
    {
        foreach (var band in Bands.Values)
        {
            if (!Energy.TryGetValue(band.Id, out var grid)) Energy[band.Id] = grid = new EnergyGrid();
            grid.Day = Day; grid.Produced = grid.Remaining = grid.Consumed = grid.Spilled = 0f; grid.Capacity = 0f;
            var policy = BandPlans.GetValueOrDefault(band.Id)?.Plan.Utilities;
            grid.Reserve = policy?.BatteryReserve ?? 0.2f;
            foreach (int id in band.Structures)
            {
                if (!Structures.TryGetValue(id, out var s) || !s.Standing) continue;
                if (s.Kind.Id == "battery_bank") grid.Capacity += s.Kind.Effect * 10f * s.Condition;
                if (s.Kind.Id == "solar_panel") grid.Produced += s.Kind.Effect * 5f * Math.Clamp(World.Temperature(s.Cell, Day) / 40f + 0.5f, 0.15f, 1f) * s.Condition;
                if (s.Kind.Id == "generator" && (policy?.FuelPower ?? !Config.RequireModel))
                {
                    string? fuel = new[] { "coal", "charcoal", "wood" }.FirstOrDefault(g => band.Stores.Has(Goods.Id(g), g == "wood" ? 1f : 0.5f));
                    if (fuel != null) { band.Stores.Take(fuel, fuel == "wood" ? 1f : 0.5f); grid.Produced += 5f * s.Condition; }
                }
            }
            grid.Spilled = MathF.Max(0f, grid.Stored - grid.Capacity); grid.Stored = MathF.Min(grid.Stored, grid.Capacity);
            grid.Remaining = grid.Produced;
            band.HasColdStore = band.Structures.Any(id => Structures.TryGetValue(id, out var s) && s.Standing && s.Kind.Id == "refrigerated_store") && ConsumePower(band.Id, 0.2f);
        }
    }

    public bool ConsumePower(int bandId, float amount)
    {
        if (!float.IsFinite(amount) || amount < 0f) return false;
        if (amount == 0f) return true;
        if (!Energy.TryGetValue(bandId, out var grid) || grid.Available + 1e-5f < amount) return false;
        float generated = MathF.Min(grid.Remaining, amount); grid.Remaining -= generated;
        grid.Stored = MathF.Max(0f, grid.Stored - (amount - generated)); grid.Consumed += amount; return true;
    }

    void StoreEnergy()
    {
        foreach (var grid in Energy.Values)
        {
            float charged = MathF.Min(grid.Remaining, MathF.Max(0f, grid.Capacity - grid.Stored));
            grid.Stored += charged; grid.Spilled += grid.Remaining - charged; grid.Remaining = 0f;
        }
    }
}
