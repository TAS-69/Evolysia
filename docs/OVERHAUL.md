# The spatial overhaul

On 2026-10-30 the user asked for a 5x larger world, a full spatial simulation, the full tech
catalogue, and real items. This document is the plan and its running status. It is updated as
phases land.

## Decisions (from the user)

| | |
|---|---|
| World | 800 x 400 cells (5x each way). One cell is one 16-unit art tile; a person is about one tile tall. |
| Placement | Full spatial sim. People have tile positions. Buildings, fields and objects are sim entities on tiles, sited by bands' own decisions. They persist, decay and burn. |
| Tech | The full civilisation catalogue (assets/civilisation/catalogue.json), foraging to space. Emergent, with no eras. |
| Items | Real persistent goods: gathered, crafted by recipe, stored, hauled, traded, spoiled. People own and use their tools and clothes. |
| Population | About 10x today (~120k people), within 16 GB. |

These rules stay:
- Determinism: seed + ActionLog replays exactly, and 1, 4 or 16 threads give the same history.
- "LLM proposes, sim disposes": utility AI does routine siting and crafting; the LLM keeps the pivotal choices.

## Scale

- `WorldMap.Scale` = W / 160 (5 at 800 wide). Every rule written in "old cells" multiplies by it:
  migration radius, contact distance, budding, disasters, god-power radii and sea ranges.
- World generation works in normalised coordinates, so the land keeps its shape at any size. Two things are per-cell:
  - Runoff per cell is multiplied by `CellArea` (1 / Scale²), so river discharge keeps its meaning.
  - The coast is shallow water within 2 cells of land.
- Ecology capacity per cell = old capacity x CellArea x `SimConfig.CapacityScale`.
- **Home range:** a band's people work tiles within `Band.Range` cells of camp (4 at full scale), not just the camp cell.

## Spatial model

- `Agent.Site`: the tile someone works today, chosen with the activity (forage at the best of a few
  sampled tiles in range, hunt where game is, fish at water, farm their fields, build at a site, craft at a
  workstation). Camp is `Band.Cell`. The renderer walks people between the two.
- Harvests in the parallel band phase read a start-of-day snapshot of the stocks. Each band keeps its
  own running deductions, and the writes are committed in order by `Later()`. Results stay
  thread-count independent even when ranges overlap.
- **Structures** are entities (`Simulation.Structures`, id -> `Structure`) with a footprint on the tile
  grid (`WorldMap.StructureAt`). States: planned, building, complete, damaged, ruined, burned.
  - Bands site new ones in the sequential decisions phase, so there are no occupancy races.
  - Builders add labour during band days, and materials come from the band's own stores.
- **Fields** are 1-tile structures holding a crop and its growth stage. Farmers sow, tend and harvest them.
- **Flora state:** `WorldMap.Cleared` (felled, burned or cleared tiles) regrows slowly. The renderer rebakes dirty chunks.

## Economy

- `Resources`: every stored good (ids match `civilisation/stored_*`). Each has its food value,
  spoil rate, weight and category.
- `Inventory`: quantities by resource. A band's stores live in its storage structures; a person carries a little.
- `Item`: owned tools and clothing pieces with wear. Clothing ids are wardrobe parts, so what someone
  wears is what is drawn.
- `Recipes`: inputs -> outputs, with the technique, workstation and tool needed and the labour taken.
- **Food:** eating draws food value from carried goods and then the stores, the most perishable first.
  Cooking, drying, smoking and storing change both value and spoilage.

## Technology

- `TechSet` (256 bits) replaces the 64-bit mask. Techs are data, with domain, difficulty, value and
  prerequisites, plus the materials or environment that make discovery likely.
- Each tech unlocks structures, recipes, items, herds or crops. The catalogue's capabilities map to techs
  from foraging through stonecraft, agriculture, metallurgy, mechanisation, electrification, information,
  advanced and space.
- Entries with art are built and drawn. Entries still marked `planned` in the catalogue exist in the sim
  and draw with the nearest available art.

## Phases

1. Scale: 5x world, home ranges, site choice, snapshot harvests, rescaled distances and capacity, chunked 16 px rendering.
2. TechSet and the full tech table.
3. Resources, inventories, items, recipes, food rework.
4. Structures, construction, siting, fields, flora state, decay.
5. Fauna species, hunting yields, domestication and herds.
6. Rendering from sim entities; inspector UI for goods, buildings and herds.
7. Balance at ~120k people, memory check, tests, docs.

## Status (2026-10-30)

- [x] 1 Scale: 800 x 400 world; home ranges; shared-stock writes deferred (no snapshot copies needed); rescaled distances.
- [x] 2 Tech: `TechSet` (256 bits) and 101 techniques from stone tools to interplanetary travel, each with the
  surroundings that suggest it; teaching and coming of age respect prerequisites.
- [x] 3 Economy: 107 goods; inventories for bands and people; 140 wardrobe items owned as gear (warmth, protection,
  prestige, fighting power, job bonuses, wear); 41 recipes; spoilage, firewood, clutter; food shared at feasts.
- [x] 4 Structures: 92 kinds planned, sited on open land, cleared (felling trees and breaking boulders for logs
  and stone), built with materials and labour by priority, aged, repaired, abandoned, adopted and reclaimed by the
  wild; fields sown, tended and harvested; villages grow cores and bud off daughter villages.
- [x] 5 Fauna: herds (species by land, capture on hunts, breeding to pen capacity, milk/eggs/wool, slaughter);
  hunting yields meat, hides, bone, antler and fur by biome.
- [x] 6 Rendering: terrain from the sim (felled woods, quarried boulders, fields by crop and growth, built-on cells,
  per-chunk rebakes); structures by kind and state; people dressed in their own gear with the tool they use;
  herds by their pens; inspector lists built works, stores, animals and each person's possessions.
- [ ] 7 Balance (2026-10-31):
  - **Growth fixed.** The ~5k plateau came from seed 42's start on a small island with the continents 120-150 cells away. Peoples now start on the large landmasses (`SeedPopulation`), sea range grows with sails, sailing and shipbuilding, field yields went up 2.5x, spoilage came down, and nomads carry their food first when they move camp. Seed 42 now reaches ~16k by year 250, ~28k by 275 and ~40-47k by 300-325, still growing.
  - **Memory:** about 35 KB per person including GC headroom (1.4 GB at 40k), so ~120k people needs roughly 4-5 GB.
  - **Speed:** see the sim README's performance notes. Per-day cost grows with population: the band day is parallel, while band decisions, the commit and disasters are sequential.
  - Still open: a full 500-year run to ~120k, and the tuning that follows.
