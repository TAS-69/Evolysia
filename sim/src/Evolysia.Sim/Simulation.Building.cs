using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Economy;
using Evolysia.Sim.Psych;
using Evolysia.Sim.Society;
using Evolysia.Sim.World;

namespace Evolysia.Sim;

public sealed partial class Simulation
{

    public readonly Dictionary<int, Structure> Structures = new();
    int _nextStructure = 1;

    void EconomyUpkeep(Band band)
    {
        var known = default(TechSet);
        foreach (int id in band.Members) if (Living.TryGetValue(id, out var m)) known |= m.Knows;
        int housing = 0;
        float warmth = 0f, storage = 0f;
        bool fire = false, cold = false;
        int unfinished = 0;
        foreach (int sid in band.Structures.ToList())
        {
            if (!Structures.TryGetValue(sid, out var s)) { band.Structures.Remove(sid); continue; }
            var k = s.Kind;

            if (s.Standing)
            {
                s.Condition -= 15f / k.Days;
                if (s.Condition < 0.5f) s.State = StructureState.Damaged;
                if (s.Condition <= 0.05f) { Ruin(s); continue; }
            }
            if (s.State is StructureState.Planned or StructureState.Building)
            {

                if (Day - s.PlannedDay > 120 && s.Progress < (s.Kind.Labour + s.Clearing) * 0.5f) { Remove(s); continue; }
                unfinished++;
                continue;
            }
            if (!s.Standing) continue;
            float c = s.State == StructureState.Damaged ? 0.6f : 1f;
            housing += (int)(k.Housing * c);
            if (k.Housing > 0) warmth = MathF.Max(warmth, k.Warmth * c);
            storage += k.Capacity * c;
            fire |= k.Role == StructureRole.Hearth;
            cold |= k.Id == "refrigerated_store";
        }
        int n = Math.Max(1, band.Members.Count);
        band.Shelter = Math.Clamp(housing / (float)n, 0f, 1f);
        band.ShelterWarmth = warmth;
        band.StorageRoom = (int)storage;
        band.HasFire = fire || (known.Has(TechId.ControlledFire) && band.Structures.Count == 0);
        band.HasColdStore = cold;
        float water = 0f;
        foreach (int sid in band.Structures)
            if (Structures.TryGetValue(sid, out var s) && s.Standing && s.Kind.Role == StructureRole.Water) water = MathF.Max(water, s.Kind.Effect);
        band.WaterBonus = water;
        AdoptAbandoned(band);

        if (band.Sedentary && !known.Has(TechId.Agriculture)) band.Sedentary = false;
        UpdateTargets(band, known);
        DiscardClutter(band);
        HerdUpkeep(band);
        if (unfinished < (band.Sedentary ? 3 : 1)) PlanNext(band, known, housing, storage);
    }

    internal bool OpenLand(int cell, int bandId = -1)
    {
        var w = World;
        int used = 0, r = 6;
        for (int dy = -r; dy <= r; dy += 2)
            for (int dx = -r; dx <= r; dx += 2)
            {
                int y = w.Y(cell) + dy;
                if (y < 0 || y >= w.H) continue;
                int id = w.StructureAt[w.Index(w.X(cell) + dx, y)];
                if (id != 0 && Structures.TryGetValue(id, out var s) && s.BandId >= 0 && s.BandId != bandId) used++;
            }
        return used <= 14;
    }

    void AdoptAbandoned(Band band)
    {
        var w = World;
        int r = band.Sedentary ? 6 : 3;
        for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int y = w.Y(band.Cell) + dy;
                if (y < 0 || y >= w.H) continue;
                int id = w.StructureAt[w.Index(w.X(band.Cell) + dx, y)];
                if (id == 0 || !Structures.TryGetValue(id, out var s) || s.BandId >= 0 || !s.Standing) continue;
                s.BandId = band.Id;
                band.Structures.Add(id);
            }
    }

    void PlanNext(Band band, TechSet known, int housing, float storage)
    {
        var w = World;
        int n = band.Members.Count;
        int Count(Func<StructureDef, bool> pick)
        {
            int c = 0;
            foreach (int sid in band.Structures) if (Structures.TryGetValue(sid, out var s) && pick(s.Kind)) c++;
            return c;
        }
        bool Has(string id) => Count(k => k.Id == id) > 0;
        StructureDef? Best(StructureRole role, Func<StructureDef, float> score)
        {
            StructureDef? best = null;
            float bestScore = 0f;
            foreach (var d in Economy.Structures.All)
            {
                if (d.Role != role || !Buildable(d)) continue;
                float s = score(d) * Availability(d);
                if (s > bestScore) { bestScore = s; best = d; }
            }
            return best;
        }

        float Availability(StructureDef d)
        {
            float f = 1f;
            foreach (var (g, q) in d.Materials)
            {
                float have = band.Stores[g] / q;
                bool raw = Goods.All[g].Kind is GoodKind.Material or GoodKind.Fuel && !Goods.All[g].Id.EndsWith("_ore");
                f *= have >= 1f ? 1f : raw ? 0.6f + 0.4f * have : 0.15f + 0.85f * have;
            }
            return f;
        }

        bool Buildable(StructureDef d)
        {
            if (!Economy.Structures.CanBuild(known, d)) return false;
            foreach (var (g, _) in d.Materials) if (!CanObtain(known, g)) return false;
            return true;
        }

        bool Pending(StructureRole role)
        {
            foreach (int sid in band.Structures)
                if (Structures.TryGetValue(sid, out var s) && s.Kind.Role == role && s.State is StructureState.Planned or StructureState.Building) return true;
            return false;
        }
        void Plan(StructureDef? def, int count = 1)
        {
            if (def != null && !Buildable(def)) return;
            for (int k = 0; k < count && def != null; k++)
                if (Site(band, def) is { } cell) Place(band, def, cell);
        }

        if (known.Has(TechId.ControlledFire) && !Has("hearth")) { Plan(Economy.Structures.Get("hearth")); return; }

        int sown = Count(k => k.Role == StructureRole.Field);
        if (band.Sedentary && known.Has(TechId.Agriculture) && (housing >= n / 2 || sown < Math.Max(4, n / 6)))
        {
            int fields = sown;
            int wanted = Math.Clamp(n / 2, 4, 60);
            if (fields < Math.Min(wanted, 6 + n / 4) && !Pending(StructureRole.Field))
            {
                var fieldDef = Economy.Structures.Get(known.Has(TechId.Orchards) && fields % 6 == 5 ? "orchard" : "field")!;

                if (fields < 2 && Site(band, fieldDef) == null) { band.Sedentary = false; return; }
                Plan(fieldDef, Math.Min(4, wanted - fields));
                return;
            }
        }
        int going = 0;
        foreach (int sid in band.Structures)
            if (Structures.TryGetValue(sid, out var st) && st.Kind.Role == StructureRole.Dwelling && st.State is StructureState.Planned or StructureState.Building) going++;
        if (housing < n && going < (band.Sedentary ? 3 : 1))
        {

            var home = Best(StructureRole.Dwelling, d => band.Sedentary ? d.Housing * (1f + d.Warmth / 10f) * MathF.Sqrt(d.Days / 1000f) / MathF.Sqrt(d.Labour)
                                                                        : d.Housing * (d.Portable ? 2f : 1f) / (1f + d.Labour * 0.3f));
            if (home != null)
            {
                int lacking = n - housing - going * home.Housing;
                Plan(home, Math.Clamp((lacking + home.Housing - 1) / home.Housing, 1, (band.Sedentary ? 3 : 1) - going));
                return;
            }
        }
        if (band.Stores.FoodValue > (20f + n * 6f + storage) * 0.6f && !Pending(StructureRole.Storage) && Best(StructureRole.Storage, d => band.Sedentary ? d.Capacity / d.Labour : d.Capacity / (1f + d.Labour * 2f)) is { } store)
        { Plan(store); return; }
        if (band.Sedentary && known.Has(TechId.Agriculture))
        {
            int fields = Count(k => k.Role == StructureRole.Field);
            int wanted = Math.Clamp(n / 2, 4, 60);
            if (fields < wanted && !Pending(StructureRole.Field)) { Plan(Economy.Structures.Get(known.Has(TechId.Orchards) && fields % 6 == 5 ? "orchard" : "field"), Math.Min(4, wanted - fields)); return; }
        }

        if (!Pending(StructureRole.Station))
            foreach (var id in WantedStations(band, known))
                if (!Has(id) && Economy.Structures.Get(id) is { } station && Economy.Structures.CanBuild(known, station)) { Plan(station); return; }
        if (HoldsSpirit(band) && Best(StructureRole.Shrine, d => d.Effect / MathF.Sqrt(d.Labour)) is { } shrine && Count(k => k.Role == StructureRole.Shrine) < (band.Sedentary ? 2 : 1))
        { Plan(shrine); return; }
        if (band.Sedentary && w.WaterAccess[band.Cell] < 0.9f && Best(StructureRole.Water, d => d.Effect) is { } well && !Has(well.Id)) { Plan(well); return; }
        float head = band.Herds.Where(h => h.Key != "dog").Sum(h => h.Value);
        if (head > 4f && Best(StructureRole.Pen, d => d.Effect) is { } pen && Count(k => k.Role == StructureRole.Pen) < 1 + head / 12f) { Plan(pen); return; }
        if (IsCoastal(band.Cell) && Best(StructureRole.Vessel, d => w.Biome[band.Cell] is Biome.Tundra or Biome.IceSheet ? 0f : d.Id is "raft" or "canoe" or "rowing_boat" or "sailboat" or "cargo_ship" ? d.Effect : 0f) is { } boat
            && Count(k => k.Role == StructureRole.Vessel) < 1 + n / 40) { Plan(boat); return; }
        if (w.TempMean[band.Cell] < 0f && Economy.Structures.Get("sled") is { } sled && Economy.Structures.CanBuild(known, sled) && !Has("sled")) { Plan(sled); return; }
        if (band.Sedentary && Day - band.LastRaidedDay < 3 * SimConfig.DaysPerYear && Economy.Structures.Get("palisade") is { } wall
            && Economy.Structures.CanBuild(known, wall) && Count(k => k.Id == "palisade") < 24)
        { PlanPalisade(band, wall); return; }
        if (band.Sedentary)
            foreach (var d in Economy.Structures.All)
                if (d.Role is StructureRole.Civic or StructureRole.Power && Economy.Structures.CanBuild(known, d) && !Has(d.Id) && Rng.Chance(0.3f)) { Plan(d); return; }

        if (band.Sedentary && Best(StructureRole.Dwelling, d => d.Warmth * MathF.Sqrt(d.Days)) is { } better && Count(k => k == better) < n / Math.Max(1, better.Housing) + 1 && Rng.Chance(0.25f))
            Plan(better);
    }

    IEnumerable<string> WantedStations(Band band, TechSet known)
    {
        var wanted = new List<string>();
        foreach (var r in Crafts.Recipes)
            if (r.Station.Length > 0 && known.Has(r.Tech) && !wanted.Contains(r.Station)) wanted.Add(r.Station);
        foreach (var d in Crafts.Items)
            if (d.Station.Length > 0 && Crafts.CanMake(known, d) && !wanted.Contains(d.Station)) wanted.Add(d.Station);
        if (known.Has(TechId.StoneTools)) wanted.Insert(0, "knapping_block");
        bool light(string id) => id is "knapping_block" or "chopping_block" or "fish_drying_rack" or "loom" or "stone_quern" or "hearth" or "cooking_pot";
        return wanted.Where(id => band.Sedentary || light(id));
    }

    bool HoldsSpirit(Band band)
    {
        foreach (var (id, share) in band.MemePrevalence)
            if (share >= 0.3f && Memes.TryGetValue(id, out var m) && (m.Kind is MemeKind.Ritual or MemeKind.SpiritBelief || m.IsPlayerGod)) return true;
        return false;
    }

    void PlanPalisade(Band band, StructureDef wall)
    {
        var w = World;
        int r = 7, placed = 0;
        int x0 = w.X(band.Cell), y0 = w.Y(band.Cell);
        for (int k = 0; k < 8 * r && placed < 6; k++)
        {
            float a = k / (8f * r) * MathF.Tau;
            int y = y0 + (int)MathF.Round(MathF.Sin(a) * r);
            if (y < 0 || y >= w.H) continue;
            int c = w.Index(x0 + (int)MathF.Round(MathF.Cos(a) * r), y);
            if (w.IsDryLand(c) && w.StructureAt[c] == 0) { Place(band, wall, c); placed++; }
        }
    }

    int? Site(Band band, StructureDef def)
    {
        var w = World;
        bool field = def.Role == StructureRole.Field, vessel = def.Role == StructureRole.Vessel;

        int core = band.Sedentary ? Math.Min(14, 4 + band.Members.Count / 12) : 3;
        int inner = field ? core : 1, outer = field ? core + 8 : core;
        if (vessel) outer = World.Cells(0.8f);
        int best = -1;
        float bestScore = float.MinValue;
        for (int k = 0; k < 40; k++)
        {
            int dx = Rng.Range(-outer, outer + 1), dy = Rng.Range(-outer, outer + 1);
            int d = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (d < inner && !(def.Role == StructureRole.Hearth && d == 0)) continue;
            int y = w.Y(band.Cell) + dy;
            if (y < 0 || y + def.H > w.H) continue;
            int c = w.Index(w.X(band.Cell) + dx, y);
            if (!Fits(c, def)) continue;
            if (vessel && !NextToWater(c)) continue;
            float score = -d * (field ? 0.3f : 1f) - w.Trees[c] / 6f - w.Rock[c] / 20f;
            if (field) score += w.PlantK[c] / MathF.Max(1f, w.PlantK[band.Cell]) - (TileBesideBuilding(c) ? 2f : 0f);
            else if (TileBesideBuilding(c)) score += 1.5f;
            if (score > bestScore) { bestScore = score; best = c; }
        }
        return best >= 0 ? best : null;
    }

    bool Fits(int cell, StructureDef def)
    {
        var w = World;
        for (int y = 0; y < def.H; y++)
            for (int x = 0; x < def.W; x++)
            {
                int c = w.Index(w.X(cell) + x, w.Y(cell) + y);
                if (!w.IsDryLand(c) || w.StructureAt[c] != 0 || w.Volcano[c]) return false;
                if (def.Role != StructureRole.Field && TileBake_IsRiver(c)) return false;
            }
        return true;
    }

    bool TileBake_IsRiver(int c) => World.Flow[c] >= WorldGen.RiverFlow * 2f;

    bool NextToWater(int cell)
    {
        Span<int> nb = stackalloc int[8];
        int k = World.Neighbors(cell, nb);
        for (int j = 0; j < k; j++) if (World.IsWater(nb[j])) return true;
        return false;
    }

    bool TileBesideBuilding(int cell)
    {
        Span<int> nb = stackalloc int[8];
        int k = World.Neighbors(cell, nb);
        for (int j = 0; j < k; j++)
            if (World.StructureAt[nb[j]] is var s && s != 0 && Structures.TryGetValue(s, out var st) && st.Kind.Role != StructureRole.Field) return true;
        return false;
    }

    internal Structure Place(Band band, StructureDef def, int cell, float progress = 0f)
    {
        var w = World;
        var s = new Structure { Id = _nextStructure++, Def = def.Index, Cell = cell, BandId = band.Id, State = StructureState.Planned, Progress = progress, PlannedDay = Day };
        float clearing = 0f;
        for (int y = 0; y < def.H; y++)
            for (int x = 0; x < def.W; x++)
            {
                int c = w.Index(w.X(cell) + x, w.Y(cell) + y);
                w.StructureAt[c] = s.Id;
                if (w.Trees[c] > 0.2f) { band.Stores.Add(GLog, w.Trees[c] * 0.8f); clearing += w.Trees[c] * 0.15f; w.Trees[c] = 0f; }
                if (w.Rock[c] > 0.5f && def.Role != StructureRole.Field) { float stone = MathF.Min(w.Rock[c], 6f); band.Stores.Add(GStone, stone); w.Rock[c] -= stone; clearing += stone * 0.1f; }
                if (def.Role == StructureRole.Field) w.Trees[c] = 0f;
            }
        s.Clearing = clearing;
        if (def.Materials.Length == 0 && def.Labour + clearing <= progress) Complete(s);
        Structures[s.Id] = s;
        band.Structures.Add(s.Id);
        return s;
    }

    void Complete(Structure s)
    {
        s.State = StructureState.Complete;
        s.Condition = 1f;
        s.BuiltDay = Day;
    }

    void Ruin(Structure s)
    {
        s.State = StructureState.Ruined;
        s.Condition = 0f;
        if (Bands.TryGetValue(s.BandId, out var band)) band.Structures.Remove(s.Id);
        s.BandId = -1;
        _ruins.Add(s.Id);
    }

    readonly List<int> _ruins = new();

    void ClearRuins()
    {
        foreach (var s in Structures.Values.ToList())
        {
            if (s.BandId >= 0) continue;

            s.Condition -= s.State == StructureState.Ruined ? 0.3f : s.Kind.Role == StructureRole.Field ? 0.35f : 360f / MathF.Max(200, s.Kind.Days) * 3f;
            if (s.Condition <= 0f && s.State != StructureState.Ruined) { s.State = StructureState.Ruined; s.Condition = 0.6f; continue; }
            if (s.Condition > 0f) continue;
            Remove(s);
        }
    }

    void Remove(Structure s)
    {
        var w = World;
        var def = s.Kind;
        for (int y = 0; y < def.H; y++)
            for (int x = 0; x < def.W; x++)
            {
                int c = w.Index(w.X(s.Cell) + x, w.Y(s.Cell) + y);
                if (w.StructureAt[c] == s.Id) w.StructureAt[c] = 0;
            }
        Structures.Remove(s.Id);
        if (Bands.TryGetValue(s.BandId, out var band)) band.Structures.Remove(s.Id);
    }

    [ThreadStatic] static List<Structure>? t_jobs;
    [ThreadStatic] static Band? t_jobsBand;
    [ThreadStatic] static int t_jobsDay;

    List<Structure> JobsOf(Band band)
    {
        var jobs = t_jobs ??= new List<Structure>();
        if (t_jobsBand == band && t_jobsDay == Day) return jobs;
        jobs.Clear();
        foreach (int sid in band.Structures)
            if (Structures.TryGetValue(sid, out var s) && s.State is StructureState.Planned or StructureState.Building or StructureState.Damaged) jobs.Add(s);
        t_jobsBand = band; t_jobsDay = Day;
        return jobs;
    }

    bool BuildWork(Agent a, Band band, in DayContext c)
    {

        Structure? job = null;
        float best = float.MinValue;
        int fields = 0;
        foreach (var f in FieldsOf(band)) if (f.Standing) fields++;
        bool hungry = fields < band.Members.Count / 6 || band.FoodPool < band.Members.Count * 10f;
        foreach (var s in JobsOf(band))
        {
            if (s.State is not (StructureState.Planned or StructureState.Building or StructureState.Damaged)) continue;
            var role = s.Kind.Role;
            float score = role switch
            {
                StructureRole.Dwelling => band.Shelter < 1f ? 4f : 1.5f,
                StructureRole.Hearth => 3f,
                StructureRole.Storage or StructureRole.Station => 2f,
                StructureRole.Field => hungry ? 4.5f : 1f,
                _ => 1.2f,
            };
            if (s.State == StructureState.Damaged) score -= 1f;
            score += s.Progress / MathF.Max(0.1f, s.Kind.Labour + s.Clearing);
            if (score > best) { best = score; job = s; }
        }
        if (job == null) return false;
        a.Site = job.Cell;
        float rate = (0.5f + 0.7f * a.Efficacy[(int)Domain.Building]) * ToolBonus(a, Activity.Build) * (a.IsChild(Day) ? 0.3f : 1f);
        a.Needs[(int)Need.Energy] += 0.18f;
        Psychology.Practice(a, Domain.Building, true, 0.5f);
        if (job.State == StructureState.Damaged)
        {
            job.Condition = MathF.Min(1f, job.Condition + 0.05f * rate);
            if (job.Condition >= 0.5f) job.State = StructureState.Complete;
            band.Stores.Take(GWood, 0.2f);
            return true;
        }
        var k = job.Kind;
        float total = k.Labour + job.Clearing;
        float step = MathF.Min(rate, total - job.Progress);

        float fraction = step / MathF.Max(0.01f, total);
        foreach (var (g, q) in k.Materials)
        {
            float need = q * fraction;
            if (!band.Stores.Has(g, need))
            {
                float can = band.Stores[g] / need;
                if (can < fraction * 0.2f)
                {
                    FetchFor(a, band, g, c);
                    return true;
                }
                step *= MathF.Min(1f, can);
                fraction = step / MathF.Max(0.01f, total);
            }
        }
        foreach (var (g, q) in k.Materials) band.Stores.Take(g, q * fraction);
        job.Progress += step;
        job.State = StructureState.Building;
        if (job.Progress >= total - 1e-3f) Later(() => FinishBuilding(job, a, band));
        return true;
    }

    void FinishBuilding(Structure s, Agent builder, Band band)
    {
        if (!Structures.ContainsKey(s.Id) || s.Standing) return;
        Complete(s);
        var k = s.Kind;
        if (Chronicle.First("built:" + k.Id))
            Chronicle.Add(new WorldEvent(Day, EventCategory.Discovery, "first_building",
                $"The {band.Name} raise the world's first {k.Id.Replace('_', ' ')}.", k.Role is StructureRole.Field ? 0.3f : 0.55f, builder.Id, band.Id));
    }

    void FetchFor(Agent a, Band band, ushort good, in DayContext c)
    {
        if (good == GLog || good == GWood) Chop(a, band, c);
        else if (good == GStone || good == GFlint) Quarry(a, band, c);
        else if (Goods.All[good].Id.EndsWith("_ore") || Goods.All[good].Id is "coal" or "sulfur") Mine(a, band, c);
        else if (good == GClay || good == GReed || good == GSand || good == GFiber || good == GBamboo || good == GSalt) GatherMaterials(a, band, c);
        else CraftWork(a, band, c);
    }

    internal void LeaveCamp(Band band, int newCell)
    {
        var portable = new List<StructureDef>();
        foreach (int sid in band.Structures.ToList())
        {
            if (!Structures.TryGetValue(sid, out var s)) continue;
            if (s.Kind.Portable && s.Standing) { portable.Add(s.Kind); Remove(s); continue; }
            s.BandId = -1;
        }
        band.Structures.Clear();
        float carry = band.Members.Count * 6f;
        foreach (var (species, count) in band.Herds) if (species is "horse" or "camel") carry += count * 10f;

        float foodBulk = 0f;
        foreach (var (good, qty) in band.Stores.Held()) if (good.Kind == GoodKind.Food) foodBulk += qty * good.Weight;
        float room = MathF.Max(0f, carry - foodBulk);
        if (foodBulk > carry)
            foreach (var (good, qty) in band.Stores.Held().ToList()) band.Stores.Take(good.Index, good.Kind == GoodKind.Food ? qty * (1f - carry / foodBulk) : qty);
        else
        {
            float materials = band.Stores.Bulk - foodBulk;
            if (materials > room)
                foreach (var (good, qty) in band.Stores.Held().ToList())
                    if (good.Kind != GoodKind.Food) band.Stores.Take(good.Index, qty * (1f - room / materials));
        }
        int was = band.Cell;
        band.Cell = newCell;
        foreach (var def in portable)
            if (Site(band, def) is { } cell) Place(band, def, cell, progress: def.Labour * 0.7f);
        band.Cell = was;
    }

    internal void DamageStructures(int cell, int radius, float severity, bool fire)
    {
        var w = World;
        int x0 = w.X(cell), y0 = w.Y(cell);
        var hit = new HashSet<int>();
        for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int y = y0 + dy;
                if (y < 0 || y >= w.H) continue;
                int c = w.Index(x0 + dx, y);
                if (fire) w.Trees[c] *= 1f - severity;
                if (w.StructureAt[c] != 0) hit.Add(w.StructureAt[c]);
            }
        foreach (int id in hit.OrderBy(i => i))
        {
            if (!Structures.TryGetValue(id, out var s) || !s.Standing) continue;
            if (s.Kind.Role == StructureRole.Field) { s.Growth *= 1f - severity; s.Yield *= 1f - severity; continue; }
            s.Condition -= severity * Rng.Range(0.3f, 1f);
            if (fire && Rng.Chance(severity * 0.6f)) { s.State = StructureState.Burned; s.Condition = 0f; Ruin(s); s.State = StructureState.Burned; continue; }
            if (s.Condition < 0.5f) s.State = StructureState.Damaged;
            if (s.Condition <= 0.05f) Ruin(s);
        }
    }
}
