# Evolysia simulation

An engine-independent C# simulation of autonomous people, ecology and emerging societies. `Evolysia.Sim` and `Evolysia.Narration` target .NET 8; the runner and tests target .NET 10.

Run from the repository root:

```powershell
dotnet test sim/Evolysia.Sim.slnx -c Release
dotnet run -c Release --project sim/src/Evolysia.Sim.Runner -- --seed 42 --years 25 --threads 4 --out build/runs/seed42
```

The runner exports generated/end maps, yearly statistics, agents, deaths, a chronicle and narratable moments. These are reports, not resumable saves. Add `--profile` for timing by phase, `--trace N` for early-year traces, `--planet Terra|Ignis|Cryos|Thalassia` or `--tribes N` for world setup. Optional `--narrate N` and `--minds N` connect to a separately running local server; the default endpoint is `http://127.0.0.1:8081`.

## Structure

| Location | Responsibility |
|---|---|
| `World/` | Cylindrical world generation, climate, biomes and ecological stocks |
| `Agents/` | Genomes, agent state and the 106-technique table |
| `Psych/` | Emotion appraisal, fear learning, character change and pivotal responses |
| `Society/` | Bands, cultures, language and names |
| `Economy/` | Quantity inventories, 107 goods, 146 gear definitions, 40 recipes and 90 structures |
| `Simulation.*.cs` | Daily activities, social contact, settlement, economy, sea travel, messages and disasters |
| `Gods.cs` | Seventeen player powers and their targeting data |
| `Decisions.cs` | Pending pivotal choices and accepted decision records |
| `Evolysia.Narration/` | Structured prompts, bounded narration queues and local endpoint communication |
| `Evolysia.Sim.Runner/` | Reports and headless experiments |
| `tests/` | World generation, inheritance, psychology, economy, sea travel, powers, narration and determinism checks |

The default world is 800 × 400 cells, wraps east-west and uses 360-day years. Utility scoring handles routine work. With deferred choices enabled, selected pivotal moments expose a shortlist; only valid, timely answers are accepted. Otherwise instinct resolves the decision.

Band work uses per-band random streams and commits global effects in fixed order. The test suite compares histories across one, four and sixteen threads. Repeatability requires the same seed, player actions and accepted pivotal choices; external narration text is not deterministic.

## Current scope

Read [Worlds and people](../docs/wiki/Worlds-and-people.md), [Economy and construction](../docs/wiki/Economy-and-construction.md) and [Known limitations](../docs/wiki/Known-limitations.md). In particular, inventories are quantities by good type, not persistent stack entities inside every rendered container. The catalogue is broader than the currently completed gameplay and visual integration.

`SimConfig` exposes population, climate, timing and tuning parameters. Large population capacity and late-game balance remain development targets. Historical timing estimates are not release hardware guarantees; use the runner's profile output on the build and hardware being evaluated.
