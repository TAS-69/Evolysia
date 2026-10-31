# Evolysia simulation core (step 2)

A headless, deterministic, engine-agnostic simulation of a generated planet and the people who live
on it. Godot (step 3) will render it; the LLM (step 4) will give it a voice. This layer decides what
actually happens, all of it numerically.

```bash
cd sim
dotnet test tests/Evolysia.Sim.Tests
dotnet run -c Release --project src/Evolysia.Sim.Runner -- --seed 42 --years 500 --out runs/seed42
```

Runner flags: `--seed --years --out --width --height --bands --band-size --invention-rate --disaster-rate --quiet`
and `--trace N`, which prints one band's monthly food economy for N years (useful for tuning).
`--minds N` has the local model make up to N pivotal choices during the run (writes `minds.md`).
`--narrate N` voices a mix of the N most important moments after the run, through the local model
(`python tools/llm_lab/lab.py serve`), and writes `narrated.md`. `--llm URL` points it at a different server.

| project | target | role |
|---|---|---|
| `src/Evolysia.Sim` | net8.0, no dependencies | the simulation library (Godot-compatible) |
| `src/Evolysia.Sim.Runner` | net10.0 console | headless runs, CSV/Markdown/PNG/JSONL output |
| `src/Evolysia.Narration` | net8.0 | LLM voices for `Chronicle.Moments`: character sheets, per-moment prompts with JSON schemas, llama-server client, prioritised narrator queue |
| `tests/Evolysia.Sim.Tests` | xUnit | psychology, genetics, world generation, determinism, narration (fake backend) |

## Outputs (`runs/<name>/`)
| file | contents |
|---|---|
| `chronicle.md` | Population curve, deaths by cause, pivot choices, personality at start vs end, world firsts, notable events by century |
| `stats_yearly.csv` | One row per year: population, bands, cultures, fertility, techniques, all event counters, deaths by cause, pivots, trait means and spreads, fear by category |
| `agents_end.csv` | Every living person: traits (effective and genetic), role, techniques, beliefs, top fear, quirks |
| `deaths.csv` | Every death: year, age, cause |
| `llm_moments.jsonl` | The moments to hand to the LLM for narration (pivots, myths, inventions, schisms, overcome fears, notable deaths), each with its decided facts |
| `world_*.png`, `end_*.png` | Biome, elevation and people/culture maps at the start and the end |

## What's modelled

**World** (`World/`). Continents and ridged mountains from cylindrical noise (wraps east-west); temperature by
latitude, altitude and continentality; seasons from axial tilt; rain from prevailing-wind bands with rain shadows;
rivers by priority-flood plus flow accumulation; lakes; Whittaker biomes (IDs shared with `docs/art`); volcanoes on
faults. Ecology runs as logistic stocks (plants, game, fish) per cell with seasonal growth and yearly regional rain
anomalies (droughts). The planet is 800 x 400 cells; each cell is one art tile (a person is about a tile tall).
Rules written for the original 160 x 80 grid scale through `WorldMap.Scale` (5) and `CellArea`. Every land cell also
holds standing timber, loose stone and boulders, and sometimes an ore deposit (copper, tin, iron, gold, silver, coal,
sulfur) that people fell, quarry and dig out; trees grow back over decades, stone and ore don't.

**People** (`Agents/`, `Psych/`).
- **Genome:** 4 temperament genes (novelty seeking, harm avoidance, reward dependence, persistence), starting tilts for 3 character traits, 8 aptitudes, pigment and hair genes, and quirks. A child blends both parents (Beta(2,2) per gene), plus polygenic segregation noise (keeps population variety), plus **1-3 divergent genes or a new quirk**.
- **Personality** = genes + **experience offsets**. Offsets move with lived emotion (chronic fear raises harm avoidance, pride and self-esteem raise self-directedness, love and joy against anger and envy shift cooperativeness), scaled by age-dependent plasticity. They fade back towards the genetic baseline (half-life about 10 years) unless experience keeps renewing them. Children start tilted towards their parents' lived character, beliefs and fears.
- **Emotions:** 11 of them, from OCC-style appraisal, decaying daily.
- **Fear learning:** scares raise fear of the specific thing and spread to its category in proportion to harm avoidance. **Avoidance** preserves the fear. **Facing it and winning** cuts it sharply, leaves lasting inoculation, builds confidence and adds a little thrill-seeking. **Losing** sensitises it further. Watching someone else's terror, or hearing stories of it, teaches a weaker version.
- **Pivots:** an invention, a myth, a creation (song, carving, trick) or a courtship gift that the circle scorns. The person conforms (abandons it and suppresses that whole domain; a technique can be lost this way), doubles down, withdraws, resents, or leaves and founds a new band, sometimes a new people. The choice is a softmax over trait loadings that match the LLM lab's scenarios, adjusted by allies, belonging, shame and anger.

**Society** (`Society/`, `Simulation.*.cs`).
- **Daily life:** utility-AI activities (forage, hunt as a party, fish, farm, herd, build, craft, fell trees, quarry, mine, gather materials, experiment, ritual, explore, teach, care for children, court, mourn, play). Each person works a real tile in the band's home range (`Agent.Site`). Food is shared or hoarded according to cooperativeness and norms; parents feed children first; infants nurse.
- **Bodies:** hunger, thirst, cold, disease and plague, forager-like mortality with crowding effects, pregnancy and childbirth.
- **Bands:** they migrate, bud off pioneers to new land when hungry, split along grudges, merge when tiny, marry across bands, meet neighbours for feasts (trading techniques and memes) or raids, and settle into villages with agriculture.
- **Techniques:** 101 of them, from stone tools to interplanetary travel (`Agents/Tech.cs`), known by individuals. They're invented (driven by need and by surroundings: forest, rock, ore, clay, sand, water, climate, a settled or large community, herds, faith), judged by the band, taught (only to those who know what they build on), spread on contact, and can be lost.

**Economy** (`Economy/`, `Simulation.Economy.cs`, `Simulation.Building.cs`). See `docs/OVERHAUL.md`.
- **Goods:** 107 kinds (foods, materials, fuels, metals, manufactured goods), each with its food value, spoil rate and bulk. Bands keep stores; people carry packs. Food is eaten most-perishable first; heat speeds spoiling, storage and pottery slow it.
- **Gear:** 140 tools and clothing pieces (the wardrobe art's parts), owned by people and worn out by use. Clothes keep out the cold, armour turns blows, weapons win fights and hunts, tools speed their job, finery lends standing. Crafters make what members lack from the stores, at the right workstation.
- **Recipes:** 41 (cooking, drying, grinding, baking, dairy, brewing, rope, cloth, leather, charcoal, pottery, bricks, dressed stone, smelting copper, bronze, iron and steel, glass, concrete, machine parts, electronics, batteries, alloys).
- **Structures:** 92 kinds (dwellings from windbreaks to housing blocks, stores, workstations, fields and orchards, pens, wells, shrines, defences, boats and vehicles). Bands plan them in priority order, site them on open land near camp, clear the site (felling and quarrying for logs and stone), and build them with materials and labour. They age and are repaired; tents move with the band; the rest is abandoned and later adopted or reclaimed by the wild.
- **Farming and herding:** fields are sown, tended and harvested into the stores by crop and climate; herds breed up to their pens, give milk, eggs and wool, and their surplus is slaughtered. Villages grow, lay out more fields beyond their core, and bud off new villages on open land.
- **Memes:** myths (an unexplained disaster plus a spiritual person produces a god and a ritual), taboos (born from shared fear), and norms (from leaders' temperaments). They spread with conformity and prestige bias. Bands whose beliefs drift become new cultures with related-sounding languages.

**The sea** (`Simulation.Sea.cs`). World generation labels landmasses. Rafts hop narrow straits through ordinary migration.
- **Sighting land:** with *boats*, which any coastal band can invent, islanders most readily, scouts sight land across open water. Each band remembers up to 12 shores.
- **Range:** 6 sea cells with boats, 10 with boats plus weaving (sails).
- **Who sails:** hungry pioneers bud off across the sea when a remembered shore beats the nearby valleys; a desperate band may sail as a whole.
- **Risk:** losses grow with distance and winter, and sometimes a voyage is never seen again. First landing on a landmass is a world event, and survivors' fear of water shifts.
- **Results:** across seeds 7, 42, 3 and 11, people reached 5-12 landmasses in 500 years, with 14-164 voyages.

**The player** (`Gods.cs`, `Simulation.Gods.cs`). `Simulation.Act(GodAction)` applies one of 16 powers between days and logs it for replay.
- **Recognition:** witnessed acts build awe until a seer names the player-god for that culture (Watcher Above or Hand in the Sky, depending on how kind you have been). Dreams can make a prophet directly.
- **Prayers:** believers in crisis open prayers (rain, food, healing, children, protection). Answering one deepens faith; letting it expire after 120 days breeds doubt.
- **Dreams:** worship, leave, peace, seek, courage and love each have a concrete effect. Your words reach the narrator, sanitised, as the dream's content.

**Minds** (`Decisions.cs`, `Simulation.Decisions.cs`). With `DeferDecisions` on, pivots and first contacts between peoples become `Decision`s.
- **The shortlist:** instinct's top three options, with plain-language descriptions. `Simulation.Decide(id, option)` accepts a choice only from that shortlist while the decision is pending.
- **Fallback:** at the deadline, instinct rolls as before.
- **Replay:** everything goes into `DecisionLog`.
- **Runner mode:** `--minds N` lets the local model make up to N choices and writes `minds.md`. It records, for each choice, the options, instinct's favourite, the pick, the thought and the words.

## Tuning knobs
- `SimConfig`: world size, land fraction, axial tilt, starting bands, invention/disaster/predator multipliers, soft agent cap.
- Main rates are commented in place: foraging yield (`Simulation.Day.cs`), fertility and mortality (`Body`/`Reproduce`), drift (`Psychology.DailyUpdate`), contact (`Contacts`).

Typical outcome on the 800 x 400 world: about 200 founders reach 4-5 thousand in 250 years, with villages from
roughly year 75; growth then levels off (see `docs/OVERHAUL.md`, phase 7).

**Performance.** Band days run in parallel: bands sharing a cell run together, different cells run concurrently, and the heaviest groups go first.
- **Determinism:** each band uses its own random stream (seed, band, day). Global effects (births, death bookkeeping, chronicle entries, courtship across bands, inventions and schisms) are deferred and committed in a fixed order, so **any thread count gives the identical history**. `ParallelTests` checks 1, 4 and 16 threads.
- **Also parallel:** ecology runs in parallel by row.
- **Memory:** server GC is on, memes nobody remembers are pruned monthly, and the ordinary dead are forgotten 25 years on (`DeathLog` keeps every death compactly). Seed 42 peaks at about 0.6 GB.
- **Timing:** seed 42 on the 800 x 400 world: 300 years in about 100 s on 16 threads.
- **Settings:** `SimConfig.Threads` (default: all cores) and runner `--threads N`.

## Next steps
- **Godot 4 (.NET build):** the Godot in the repo root is the standard build, which can't run C#. Use the .NET build ("mono"), reference `Evolysia.Sim`, step the simulation on a worker thread, and render from `WorldMap` and `Simulation`.
- **More speed:** the remaining sequential phases (band decisions and the commit, about 20% of runtime) are next.
