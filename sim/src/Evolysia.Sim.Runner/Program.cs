using System.Globalization;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.Text;
using System.Text.Json;
using Evolysia.Sim;
using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Psych;
using Evolysia.Sim.Runner;
using Evolysia.Sim.World;

// Headless runner: dotnet run -c Release --project src/Evolysia.Sim.Runner -- --seed 42 --years 500 --out runs/seed42
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var cfg = new SimConfig();
int years = 500;
string outDir = "runs/latest";
bool quiet = false;
int traceYears = 0, narrateCount = 0, mindsBudget = 0;
string llmUrl = "http://127.0.0.1:8081";
for (int i = 0; i < args.Length; i++)
{
    string Next() => args[++i];
    switch (args[i])
    {
        case "--seed": cfg.Seed = ulong.Parse(Next()); break;
        case "--years": years = int.Parse(Next()); break;
        case "--out": outDir = Next(); break;
        case "--width": cfg.Width = int.Parse(Next()); break;
        case "--height": cfg.Height = int.Parse(Next()); break;
        case "--bands": cfg.StartBands = int.Parse(Next()); break;
        case "--band-size": cfg.StartBandSize = int.Parse(Next()); break;
        case "--invention-rate": cfg.InventionRate = float.Parse(Next()); break;
        case "--disaster-rate": cfg.DisasterRate = float.Parse(Next()); break;
        case "--quiet": quiet = true; break;
        case "--trace": traceYears = int.Parse(Next()); break;
        case "--narrate": narrateCount = int.Parse(Next()); break;
        case "--minds": mindsBudget = int.Parse(Next()); break;
        case "--threads": cfg.Threads = int.Parse(Next()); break;
        case "--llm": llmUrl = Next(); break;
        default: Console.Error.WriteLine($"Unknown argument {args[i]}"); return 1;
    }
}
Directory.CreateDirectory(outDir);

var sw = Stopwatch.StartNew();
var sim = new Simulation(cfg);
Console.WriteLine($"World {cfg.Width}x{cfg.Height}, seed {cfg.Seed}: generated in {sw.ElapsedMilliseconds} ms. " +
                  $"{sim.Living.Count} people in {sim.Bands.Count} bands.");
WriteMaps(sim, Path.Combine(outDir, "world"));
var startTraits = TraitSnapshot(sim);
if (traceYears > 0) Trace(sim, traceYears);

void OnYear(Simulation s)
{
    var r = s.Stats.History[^1];
    if (!quiet && (r.Year % 25 == 0 || s.Living.Count == 0))
        Console.WriteLine($"  year {r.Year,4}: pop {r.Population,6}  bands {r.Bands,4}  cultures {r.Cultures,3}  techs {r.TechsKnownWorld,2}  " +
                          $"memes {r.Memes,4}  births {r.Births,4}  deaths {r.Deaths.Sum(),4}  ({sw.Elapsed.TotalSeconds:0.0}s)");
}
if (mindsBudget > 0) await RunWithMinds(sim, years, mindsBudget, llmUrl, Path.Combine(outDir, "minds.md"), OnYear);
else sim.RunYears(years, OnYear);
if (sim.Stats.Year.Births + sim.Stats.Year.Deaths.Sum() > 0) { /* partial final year is left out of the CSV */ }
Console.WriteLine("Time by phase: " + string.Join(", ", sim.Profile.OrderByDescending(p => p.Value).Select(p => $"{p.Key} {p.Value:0.0}s")));
var settled = sim.Bands.Values.Select(b => sim.World.Landmass[b.Cell]).Distinct().Count();
Console.WriteLine($"Settled landmasses: {settled}; voyages: {sim.Stats.History.Sum(r => r.Voyages)} " +
                  $"({sim.Stats.History.Sum(r => r.LostAtSea)} lost at sea); shores sighted: {sim.Stats.History.Sum(r => r.ShoresSighted)}.");
Console.WriteLine($"Done: {sim.Year} years in {sw.Elapsed.TotalSeconds:0.0}s. Population {sim.Living.Count}, ever lived {sim.EverLived}. " +
                  $"Peak memory {System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64 / 1_000_000} MB.");
// The built world and the economy at the end.
Console.WriteLine("Structures: " + string.Join(", ", sim.Structures.Values.GroupBy(s => $"{s.Kind.Id}/{s.State}").OrderByDescending(g => g.Count()).Take(30).Select(g => $"{g.Key} {g.Count()}")));
var stores = new Evolysia.Sim.Economy.Inventory();
foreach (var b in sim.Bands.Values) foreach (var (g, q) in b.Stores.Held()) stores.Add(g.Index, q);
Console.WriteLine("Stores: " + string.Join(", ", stores.Held().OrderByDescending(h => h.qty).Take(25).Select(h => $"{h.good.Id} {h.qty:0}")));
Console.WriteLine("Gear: " + string.Join(", ", sim.Living.Values.SelectMany(a => a.Gear).GroupBy(i => i.Kind.Id).OrderByDescending(g => g.Count()).Take(25).Select(g => $"{g.Key} {g.Count()}")));
foreach (var b in sim.Bands.Values.OrderByDescending(b => b.Members.Count).Take(3))
{
    var built = b.Structures.Select(id => sim.Structures[id]).GroupBy(s => $"{s.Kind.Id}/{s.State}").Select(g => $"{g.Key} {g.Count()}");
    var known = default(TechSet); foreach (int id in b.Members) if (sim.Living.TryGetValue(id, out var m)) known |= m.Knows;
    Console.WriteLine($"Band {b.Name}: {b.Members.Count} people, {(b.Sedentary ? "village" : "wandering")}, food {b.FoodPool:0}, shelter {b.Shelter:0.00}, " +
                      $"{known.Count} techniques; built: {string.Join(", ", built)}");
}
Console.WriteLine("Herds: " + string.Join(", ", sim.Bands.Values.SelectMany(b => b.Herds).GroupBy(h => h.Key).Select(g => $"{g.Key} {g.Sum(h => h.Value):0}")));

WriteStats(sim, Path.Combine(outDir, "stats_yearly.csv"));
WriteAgents(sim, Path.Combine(outDir, "agents_end.csv"));
File.WriteAllLines(Path.Combine(outDir, "deaths.csv"), new[] { "id,year,age,cause,sex,generation" }.Concat(
    sim.DeathLog.Select(d => $"{d.Id},{d.Day / SimConfig.DaysPerYear},{d.Age:0.0},{Ids.Snake(d.Cause)},{d.Sex},{d.Generation}")));
WriteChronicle(sim, Path.Combine(outDir, "chronicle.md"), startTraits);
WriteMoments(sim, Path.Combine(outDir, "llm_moments.jsonl"));
WriteMaps(sim, Path.Combine(outDir, "end"));
if (narrateCount > 0) await Narrate(sim, narrateCount, llmUrl, Path.Combine(outDir, "narrated.md"));
Console.WriteLine($"Wrote {Path.GetFullPath(outDir)}");
return 0;

// ---------------------------------------------------------------------------------------------

// Pivotal choices made by the local model (up to a budget), instinct for the rest; report how they differ.
static async Task RunWithMinds(Simulation sim, int years, int budget, string url, string path, Action<Simulation> onYear)
{
    using var backend = new Evolysia.Narration.LlamaServerBackend(url, url.Replace(":8081", ":8082"));
    if (!await backend.HealthyAsync(CancellationToken.None))
    {
        Console.WriteLine($"Minds unavailable: no model at {url}. Running on instinct.");
        sim.RunYears(years, onYear);
        return;
    }
    sim.DeferDecisions = true;
    var narrator = new Evolysia.Narration.Narrator(backend);
    var made = new List<(Decision d, Evolysia.Narration.Narration n)>();
    int seen = 0, end = sim.Day + years * SimConfig.DaysPerYear;
    while (sim.Day < end && sim.Living.Count > 0)
    {
        sim.Step();
        if (WorldMap.DayOfYear(sim.Day) == 0) onYear(sim);
        var batch = new List<Task<Evolysia.Narration.Narration?>>();
        for (; seen < sim.Chronicle.Moments.Count; seen++)
        {
            var m = sim.Chronicle.Moments[seen];
            if (m.Kind != "decision" || made.Count + batch.Count >= budget) continue;
            if (Evolysia.Narration.Prompts.Build(sim, seen, m) is { } req) batch.Add(narrator.NarrateNowAsync(req));
        }
        foreach (var t in batch)
            if (await t is { } n && Evolysia.Narration.Prompts.DecisionChoice(n) is { } c && sim.Decide(c.DecisionId, c.Choice))
                made.Add((sim.Decisions.First(d => d.Id == c.DecisionId), n));
    }

    var sb = new StringBuilder($"# Choices made by minds (seed {sim.Config.Seed})\n\nModel: {backend.Describe()}\n\n");
    var instinct = sim.Decisions.Where(d => d.Resolved && d.Source == "instinct").ToList();
    int agree = made.Count(x => x.d.Chosen == x.d.InstinctFavourite);
    sb.Append($"- Decisions by mind: {made.Count}, by instinct: {instinct.Count}\n");
    sb.Append($"- Mind picked what temperament alone most favoured: {agree}/{made.Count} ({(made.Count == 0 ? 0 : 100.0 * agree / made.Count):0}%)\n");
    string Dist(IEnumerable<Decision> ds) => string.Join(", ", ds.GroupBy(d => d.Chosen).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}"));
    sb.Append($"- Mind choices: {Dist(made.Select(x => x.d))}\n- Instinct choices: {Dist(instinct)}\n\n");
    foreach (var (d, n) in made)
    {
        var a = sim.Find(d.AgentId);
        sb.Append($"## Year {d.Day / SimConfig.DaysPerYear}: {a?.Name} ({n.Moment.Facts.GetValueOrDefault("trigger")}: {n.Moment.Facts.GetValueOrDefault("subject") ?? n.Moment.Facts.GetValueOrDefault("other_band")})\n\n");
        if (a != null) sb.Append($"*{Evolysia.Narration.CharacterSheet.Personality(a)}*\n\n");
        for (int i = 0; i < d.Options.Length; i++)
            sb.Append($"- {(d.Options[i] == d.Chosen ? "**→**" : "  ")} `{d.Options[i]}` {d.Descriptions[i]}{(d.Options[i] == d.InstinctFavourite ? " *(instinct's favourite)*" : "")}\n");
        sb.Append($"\n> {n.Get("thought")}\n>\n> “{n.Get("says")}”\n\n");
    }
    File.WriteAllText(path, sb.ToString());
    Console.WriteLine($"Minds made {made.Count} choices ({agree} matched instinct's favourite) -> {path}");
}

// Give the most important moments a voice through the local model (tools/llm_lab `lab.py serve`).
static async Task Narrate(Simulation sim, int count, string url, string path)
{
    using var backend = new Evolysia.Narration.LlamaServerBackend(url, url.Replace(":8081", ":8082"));
    if (!await backend.HealthyAsync(CancellationToken.None))
    {
        Console.WriteLine($"Narration skipped: no model at {url} (start it with `python tools/llm_lab/lab.py serve`).");
        return;
    }
    var narrator = new Evolysia.Narration.Narrator(backend);
    // A mix: round-robin over kinds, most important kind first, newest moments first within a kind.
    var byKind = sim.Chronicle.Moments.Select((m, i) => (m, i)).GroupBy(x => x.m.Kind)
        .OrderByDescending(g => Evolysia.Narration.Prompts.Priority(g.Key))
        .Select(g => new Queue<(LlmMoment m, int i)>(g.Reverse())).ToList();
    var picks = new List<(LlmMoment m, int i)>();
    while (picks.Count < count && byKind.Any(q => q.Count > 0))
        foreach (var q in byKind.Where(q => q.Count > 0)) if (picks.Count < count) picks.Add(q.Dequeue());

    var sb = new StringBuilder($"# Narrated moments (seed {sim.Config.Seed})\n\nModel: {backend.Describe()}\n\n");
    var sw = Stopwatch.StartNew();
    async Task<(Evolysia.Narration.NarrationRequest req, Evolysia.Narration.Narration? n)?> One((LlmMoment m, int i) p)
    {
        var req = Evolysia.Narration.Prompts.Build(sim, p.i, p.m);
        return req == null ? null : (req, await narrator.NarrateNowAsync(req));
    }
    var tasks = picks.Select(One).ToList();   // the server's parallel slots handle them concurrently
    foreach (var t in tasks)
    {
        var result = await t;
        if (result is not { } r) continue;
        var m = r.req.Moment;
        sb.Append($"## Year {m.Day / SimConfig.DaysPerYear} - {m.Kind}: {r.req.AgentName}\n\n");
        sb.Append($"*Facts:* {string.Join(", ", m.Facts.Select(f => $"{f.Key}={f.Value}"))}\n\n");
        if (r.n == null) { sb.Append("(model output rejected)\n\n"); continue; }
        foreach (var f in r.n.Fields) sb.Append($"- **{f.Key}**: {f.Value}\n");
        sb.Append($"\n<sub>{r.n.LatencyMs:0} ms</sub>\n\n");
    }
    File.WriteAllText(path, sb.ToString());
    var s = narrator.Status;
    Console.WriteLine($"Narrated {s.Done} moments ({s.Failed} rejected) in {sw.Elapsed.TotalSeconds:0.0}s, avg {s.AvgLatencyMs:0} ms each -> {path}");
}

// ---------------------------------------------------------------------------------------------

// Monthly view of the oldest surviving band's economy: what people do, eat and suffer.
static void Trace(Simulation sim, int years)
{
    Console.WriteLine("year.mo  band  n  kids  pool  food/p  hunger  plant%  prey%  grow  temp  shelter  activities");
    var counts = new int[Enum.GetValues<Activity>().Length];
    for (int d = 0; d < years * SimConfig.DaysPerYear && sim.Living.Count > 0; d++)
    {
        sim.Step();
        var band = sim.Bands.Values.FirstOrDefault();
        if (band == null) break;
        foreach (int id in band.Members) if (sim.Living.TryGetValue(id, out var a)) counts[(int)a.Today]++;
        if (sim.Day % 30 != 0) continue;
        var m = band.Members.Select(id => sim.Living.GetValueOrDefault(id)).Where(a => a != null).ToList();
        int c = band.Cell;
        var w = sim.World;
        string acts = string.Join(" ", counts.Select((n, i) => (n, i)).Where(x => x.n > 0).OrderByDescending(x => x.n).Take(6)
            .Select(x => $"{Ids.Snake((Activity)x.i)}:{x.n}"));
        Console.WriteLine($"{sim.Day / 360,4}.{sim.Day % 360 / 30,-2} {band.Id,5} {m.Count,2} {m.Count(a => a!.IsChild(sim.Day)),4} {band.FoodPool,6:0} " +
                          $"{(m.Count > 0 ? m.Average(a => a!.Food) : 0),6:0.0} {(m.Count > 0 ? m.Average(a => a!.NeedOf(Need.Hunger)) : 0),7:0.00} " +
                          $"{w.PlantStock[c] / Math.Max(1, w.PlantK[c]) * 100,6:0} {w.PreyStock[c] / Math.Max(1, w.PreyK[c]) * 100,6:0} " +
                          $"{w.Growth(c, sim.Day),5:0.00} {w.Temperature(c, sim.Day),5:0} {band.Shelter,7:0.00}  {acts}");
        Array.Clear(counts);
    }
}

static float[][] TraitSnapshot(Simulation sim)
{
    var adults = sim.Living.Values.Where(a => a.IsAdult(sim.Day)).ToList();
    return Enum.GetValues<Trait>().Select(t => adults.Select(a => a.T(t)).ToArray()).ToArray();
}

static void WriteStats(Simulation sim, string path)
{
    var sb = new StringBuilder();
    var traits = Enum.GetValues<Trait>().Select(t => Ids.Snake(t)).ToArray();
    var causes = Enum.GetValues<DeathCause>().Skip(1).ToArray();
    var pivots = Enum.GetValues<PivotChoice>();
    var cats = Enum.GetValues<StimulusCategory>();
    sb.Append("year,population,bands,cultures,sedentary_bands,memes,children,elders,fertile_women,fertile_paired,pregnant,single_adults,mean_age,mean_band_size,mean_hunger,mean_generation,");
    sb.Append("techs_world,techs_per_band,temp_anomaly,births,pairings,inventions,myths,taboos,norms,schisms,splits,migrations,new_cultures,");
    sb.Append("raids,trades,first_contacts,quarrels,assaults,disasters,plagues,drownings,confrontations,fears_overcome,praises,creations,shores_sighted,voyages,lost_at_sea,new_landmasses,");
    sb.Append(string.Join(",", causes.Select(c => "deaths_" + Ids.Snake(c)))).Append(',');
    sb.Append(string.Join(",", pivots.Select(p => "pivot_" + Ids.Snake(p)))).Append(',');
    sb.Append(string.Join(",", traits.Select(t => "mean_" + t))).Append(',');
    sb.Append(string.Join(",", traits.Select(t => "sd_" + t))).Append(',');
    sb.AppendLine(string.Join(",", cats.Select(c => "fear_" + Ids.Snake(c))));
    foreach (var r in sim.Stats.History)
    {
        sb.Append($"{r.Year},{r.Population},{r.Bands},{r.Cultures},{r.Sedentary},{r.Memes},{r.Children},{r.Elders},{r.FertileWomen},{r.FertilePaired},{r.Pregnant},{r.SingleAdults},{r.MeanAge:0.0},{r.MeanBandSize:0.0},{r.MeanHunger:0.000},{r.MeanGeneration:0.0},");
        sb.Append($"{r.TechsKnownWorld},{r.TechsMeanPerBand:0.0},{r.TempAnomaly:0.00},{r.Births},{r.Pairings},{r.Inventions},{r.Myths},{r.Taboos},{r.Norms},{r.Schisms},{r.Splits},{r.Migrations},{r.NewCultures},");
        sb.Append($"{r.Raids},{r.Trades},{r.FirstContacts},{r.Quarrels},{r.Assaults},{r.Disasters},{r.Plagues},{r.Drownings},{r.Confrontations},{r.FearsOvercome},{r.Praises},{r.Creations},{r.ShoresSighted},{r.Voyages},{r.LostAtSea},{r.Landmasses},");
        sb.Append(string.Join(",", causes.Select(c => r.Deaths[(int)c]))).Append(',');
        sb.Append(string.Join(",", pivots.Select(p => r.Pivots[(int)p]))).Append(',');
        sb.Append(string.Join(",", r.TraitMean.Select(v => v.ToString("0.000")))).Append(',');
        sb.Append(string.Join(",", r.TraitSd.Select(v => v.ToString("0.000")))).Append(',');
        sb.AppendLine(string.Join(",", r.CategoryFearMean.Select(v => v.ToString("0.000"))));
    }
    File.WriteAllText(path, sb.ToString());
}

static void WriteAgents(Simulation sim, string path)
{
    var sb = new StringBuilder();
    var traits = Enum.GetValues<Trait>();
    sb.Append("id,name,sex,age,generation,band,culture,role,prestige,health,scars,children,techs,memes,");
    sb.Append(string.Join(",", traits.Select(t => Ids.Snake(t)))).Append(',');
    sb.Append(string.Join(",", traits.Select(t => "gene_" + Ids.Snake(t)))).Append(',');
    sb.AppendLine("top_fear,top_fear_value,quirks");
    foreach (var a in sim.Living.Values.OrderBy(a => a.Id))
    {
        int top = 0;
        for (int s = 1; s < Ids.StimulusCount; s++) if (a.Fear[s] > a.Fear[top]) top = s;
        sb.Append($"{a.Id},{a.Name},{a.Sex},{a.Age(sim.Day):0.0},{a.Generation},{a.BandId},{a.CultureId},{Ids.Snake(a.MainActivity())},{a.Prestige:0.00},{a.Health:0.00},{a.Scars},{a.Children.Count},{Techs.CountKnown(a.Knows)},{a.Memes.Count},");
        sb.Append(string.Join(",", traits.Select(t => a.T(t).ToString("0.000")))).Append(',');
        sb.Append(string.Join(",", traits.Select(t => a.Genome.Genes[(int)t].ToString("0.000")))).Append(',');
        sb.AppendLine($"{Ids.Snake((Stimulus)top)},{a.Fear[top]:0.00},{a.Genome.Quirks.ToString().Replace(", ", "|")}");
    }
    File.WriteAllText(path, sb.ToString());
}

static void WriteMoments(Simulation sim, string path)
{
    using var f = new StreamWriter(path);
    foreach (var m in sim.Chronicle.Moments)
    {
        var agent = sim.Find(m.AgentId);
        var payload = new Dictionary<string, object?>
        {
            ["day"] = m.Day, ["year"] = m.Day / SimConfig.DaysPerYear, ["kind"] = m.Kind,
            ["agent"] = agent?.Name, ["agent_id"] = m.AgentId, ["band_id"] = m.BandId, ["facts"] = m.Facts,
        };
        f.WriteLine(JsonSerializer.Serialize(payload));
    }
}

static void WriteChronicle(Simulation sim, string path, float[][] startTraits)
{
    var sb = new StringBuilder();
    var h = sim.Stats.History;
    sb.AppendLine($"# Chronicle of seed {sim.Config.Seed}").AppendLine();
    if (h.Count > 0)
    {
        var last = h[^1];
        int peak = h.Max(r => r.Population);
        sb.AppendLine($"{h.Count} years. Population {h[0].Population} -> {last.Population} (peak {peak}). " +
                      $"{sim.EverLived} people lived. Bands: {last.Bands}, cultures alive: {last.Cultures}, " +
                      $"cultures ever: {sim.Cultures.Count}, memes: {sim.Memes.Count}, techniques known: {last.TechsKnownWorld}/{Techs.Count}.").AppendLine();
        sb.AppendLine("## Population").AppendLine().AppendLine("```").AppendLine(Spark(h.Select(r => (float)r.Population).ToArray())).AppendLine("```").AppendLine();

        var deaths = Enum.GetValues<DeathCause>().Skip(1).Select(c => (c, n: h.Sum(r => r.Deaths[(int)c]))).Where(x => x.n > 0).OrderByDescending(x => x.n);
        sb.AppendLine("## Deaths by cause").AppendLine();
        foreach (var (c, n) in deaths) sb.AppendLine($"- {Ids.Snake(c)}: {n}");
        sb.AppendLine().AppendLine("## Pivot choices (responses to social rejection)").AppendLine();
        foreach (var p in Enum.GetValues<PivotChoice>()) sb.AppendLine($"- {Ids.Snake(p)}: {h.Sum(r => r.Pivots[(int)p])}");
        sb.AppendLine().AppendLine($"Fears overcome: {h.Sum(r => r.FearsOvercome)} of {h.Sum(r => r.Confrontations)} confrontations. " +
                                   $"Inventions: {h.Sum(r => r.Inventions)}. Myths: {h.Sum(r => r.Myths)}. Taboos: {h.Sum(r => r.Taboos)}. " +
                                   $"Schisms: {h.Sum(r => r.Schisms)}. Raids: {h.Sum(r => r.Raids)}. Feasts/trade: {h.Sum(r => r.Trades)}.").AppendLine();

        sb.AppendLine("## Personality: first adults vs. living adults at the end").AppendLine();
        sb.AppendLine("| trait | start mean | end mean | start sd | end sd |").AppendLine("|---|---|---|---|---|");
        var endTraits = TraitSnapshot(sim);
        foreach (var t in Enum.GetValues<Trait>())
        {
            var s0 = startTraits[(int)t]; var s1 = endTraits[(int)t];
            sb.AppendLine($"| {Ids.Snake(t)} | {Mean(s0):0.00} | {Mean(s1):0.00} | {Sd(s0):0.00} | {Sd(s1):0.00} |");
        }
        sb.AppendLine();
    }

    sb.AppendLine("## Firsts").AppendLine();
    foreach (var e in sim.Chronicle.Events.Where(e => e.Salience >= 0.75f)) sb.AppendLine($"- **Year {e.Day / SimConfig.DaysPerYear}**: {e.Text}");
    sb.AppendLine().AppendLine("## Notable events by century").AppendLine();
    foreach (var century in sim.Chronicle.Events.GroupBy(e => e.Day / SimConfig.DaysPerYear / 100))
    {
        sb.AppendLine($"### Years {century.Key * 100}-{century.Key * 100 + 99}").AppendLine();
        foreach (var e in century.OrderByDescending(e => e.Salience).Take(25).OrderBy(e => e.Day))
            sb.AppendLine($"- Year {e.Day / SimConfig.DaysPerYear}: {e.Text}");
        sb.AppendLine();
    }
    sb.AppendLine("## Event counts").AppendLine();
    foreach (var (k, n) in sim.Chronicle.Counts.OrderByDescending(x => x.Value)) sb.AppendLine($"- {k}: {n}");
    File.WriteAllText(path, sb.ToString());

    static float Mean(float[] v) => v.Length == 0 ? 0f : v.Average();
    static float Sd(float[] v) { if (v.Length == 0) return 0f; float m = v.Average(); return MathF.Sqrt(v.Average(x => (x - m) * (x - m))); }
}

static string Spark(float[] values, int width = 100, int height = 10)
{
    if (values.Length == 0) return "";
    float max = Math.Max(1f, values.Max());
    var cols = Enumerable.Range(0, Math.Min(width, values.Length))
        .Select(c => values[(int)((long)c * values.Length / Math.Min(width, values.Length))]).ToArray();
    var sb = new StringBuilder();
    for (int row = height; row >= 1; row--)
    {
        sb.Append(row == height ? $"{max,7:0} |" : "        |");
        foreach (var v in cols) sb.Append(v / max * height >= row - 0.5f ? '#' : ' ');
        sb.AppendLine();
    }
    sb.Append("      0 +").Append(new string('-', cols.Length)).AppendLine();
    sb.Append($"         year 0{new string(' ', Math.Max(0, cols.Length - 12))}year {values.Length - 1}");
    return sb.ToString();
}

static void WriteMaps(Simulation sim, string prefix)
{
    var w = sim.World;
    var biome = new byte[w.Count * 3];
    var elev = new byte[w.Count * 3];
    for (int i = 0; i < w.Count; i++)
    {
        var (r, g, b) = Color(w.Biome[i]);
        if (w.IsLand(i) && w.Flow[i] > WorldGen.RiverFlow * 3f && w.Biome[i] != Biome.Lake) (r, g, b) = (70, 120, 200);
        if (w.Ash[i] > 0.2f) (r, g, b) = ((byte)(r * 0.6f), (byte)(g * 0.6f), (byte)(b * 0.6f));
        biome[i * 3] = r; biome[i * 3 + 1] = g; biome[i * 3 + 2] = b;
        float e = w.Elevation[i];
        byte v = (byte)Math.Clamp(e >= 0 ? 80 + e / 4500f * 175f : 40 + (e + 5000f) / 5000f * 40f, 0, 255);
        elev[i * 3] = v; elev[i * 3 + 1] = e >= 0 ? v : (byte)(v / 2); elev[i * 3 + 2] = e >= 0 ? (byte)(v * 0.8f) : (byte)Math.Min(255, v * 2);
    }
    // Bands as dots coloured by culture hue.
    var people = (byte[])biome.Clone();
    foreach (var band in sim.Bands.Values)
    {
        var c = sim.Cultures[band.CultureId];
        var (r, g, b) = Hue(c.Hue);
        int i = band.Cell;
        people[i * 3] = r; people[i * 3 + 1] = g; people[i * 3 + 2] = b;
    }
    Png.Write(prefix + "_biomes.png", w.W, w.H, biome, 4);
    Png.Write(prefix + "_elevation.png", w.W, w.H, elev, 4);
    Png.Write(prefix + "_people.png", w.W, w.H, people, 4);
}

static (byte, byte, byte) Hue(int h)
{
    float a = h / 12f * 6f;
    int k = (int)a; float f = a - k;
    byte up = (byte)(255 * f), down = (byte)(255 * (1 - f));
    return k switch { 0 => (255, up, 0), 1 => (down, 255, 0), 2 => (0, 255, up), 3 => (0, down, 255), 4 => (up, 0, 255), _ => (255, 0, down) };
}

static (byte, byte, byte) Color(Biome b) => b switch
{
    Biome.DeepOcean => (20, 40, 90),
    Biome.Ocean => (30, 60, 120),
    Biome.Coast => (50, 95, 150),
    Biome.Lake => (60, 110, 170),
    Biome.IceSheet => (235, 240, 245),
    Biome.Tundra => (170, 175, 150),
    Biome.BorealForest => (60, 100, 75),
    Biome.ColdDesert => (180, 170, 140),
    Biome.TemperateGrassland => (160, 180, 90),
    Biome.TemperateForest => (70, 130, 60),
    Biome.TemperateRainforest => (40, 110, 70),
    Biome.Shrubland => (165, 150, 90),
    Biome.HotDesert => (225, 200, 130),
    Biome.Savanna => (190, 175, 80),
    Biome.TropicalSeasonalForest => (100, 150, 50),
    Biome.TropicalRainforest => (30, 120, 40),
    Biome.Wetland => (80, 120, 100),
    Biome.Alpine => (140, 130, 125),
    Biome.Volcanic => (90, 50, 45),
    _ => (255, 0, 255),
};
