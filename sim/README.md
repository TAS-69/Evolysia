# Evolysia simulation

An engine-independent C# simulation of autonomous people, ecology and emerging societies. `Evolysia.Sim` and `Evolysia.Narration` target .NET 8; the runner and tests target .NET 10. Current source requires a working generative model for ordinary progression.

From the repository root:

```powershell
dotnet test sim/Evolysia.Sim.slnx -c Release
python tools/llm_lab/lab.py bootstrap --helper off --embed off
dotnet run -c Release --project sim/src/Evolysia.Sim.Runner -- --seed 42 --width 160 --height 80 --tribes 1 --bands 1 --days 2 --save build/world.evo --out build/runs/seed42
```

The runner exports maps, statistics, people, deaths, chronicle, accepted plans and rejected-reply diagnostics. `--save PATH` writes a resumable world; `--load PATH` restores it. `--llm URL` selects the server root. A required-model run returns an error if no progress can be made for 150 seconds.

For engine-rule and performance diagnostics only, `--rules-only` explicitly selects the existing utility simulation. This is not a gameplay fallback. `--profile`, `--trace N`, `--planet`, `--tribes`, `--width`, `--height`, `--bands`, `--band-size`, `--threads` and tuning options are available in the argument parser. The older `--narrate` and `--minds` modes remain evaluation tools for diagnostic runs.

## Structure

| Location | Responsibility |
|---|---|
| `World/` | Cylindrical generation, climate, biomes and ecological stock |
| `Agents/` | Genomes, appearance, needs and the 106-technique table |
| `Psych/` | Appraisal, fear learning, personality changes and pivotal responses |
| `Society/` | Bands, cultures, names and language |
| `Economy/` | Physical inventories, goods, gear, recipes, structures and room layouts |
| `Persistence/` | Versioned, checksummed world snapshots |
| `Simulation.*.cs` | Daily work, councils, societies, design, construction, transport and interventions |
| `Evolysia.Narration/` | Structured model contracts, validation feedback and request scheduling |
| `Evolysia.Sim.Runner/` | Headless progression, reports and diagnostics |
| `tests/` | World rules, model gating, invalid plans, ownership, logistics and replay checks |

The default world is 800 × 400 cells with 360-day years. Councils last fifteen days by default. Six rotating people receive direct intentions; the rest follow generated labour shares. Invalid or unavailable model replies hold time. Read [Required model connection](../docs/wiki/Local-models.md) for the contract and operational limits.

Band work uses per-band random streams and commits shared effects in fixed order. Repeatability needs the same seed, actions and accepted plans. Saves preserve random state and collection order; generated replies themselves are not deterministic.

Physical stock and explicit cargo jobs now persist, but some everyday economic transfers remain aggregate. Specialist layouts and advanced definitions do not imply complete institutional, modern, future or magical gameplay. [Development status](../docs/DEVELOPMENT.md) tracks remaining integration.
