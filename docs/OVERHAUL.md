# Spatial simulation

The current client and simulation use an 800 × 400 planet. A cell corresponds to one logical sixteen-unit art tile. Distances derived from the original 160 × 80 model scale through `WorldMap.Scale`; ecological capacity scales through `CellArea`.

## Ownership and daily work

`Simulation` owns people, bands, structures, cultures, ecology and history. The renderer reads that state. A person's `Site` records today's work tile; the client animates travel between home and work. Harvest deductions from overlapping band ranges are committed in fixed order to keep thread-count-independent results.

`Band.Stores` and `Agent.Pack` hold quantities by good. Gear is owned by people and carries wear. A rendered storage object shows representative goods from band stores; the contents are not independently located persistent stacks. Per-container inventory identities, loading reservations and full transport logistics remain unfinished.

## Construction and land

Structures have IDs, definitions, footprints, band ownership and condition. Bands plan and site projects, clear land, obtain materials and build with labour. State progresses through planned, building, complete, damaged, ruined and burned. Portable shelters can move; abandoned works may be adopted or reclaimed.

Timber, loose stone, boulders and ore are world resources. Felling, quarrying and construction change the landscape. Fields track crop, growth and yield. Pens support herds. The client rebakes dirty terrain chunks and draws surviving structures and owned gear.

## Definitions and rendering

The simulation defines 106 techniques, 107 goods, 146 gear items, 40 processing recipes and 90 structure kinds. The separate art catalogue includes additional concepts and planned coverage. Neither count establishes a complete modern or future civilisation loop.

The client chooses 16, 32 or 64 pixel art densities by screen tile size. The detailed terrain, wardrobe and civilisation kit cover those densities; other assets fall back to base sprites. Regional visual styles do not automatically alter each structure's physical recipe. Some simulation definitions have no dedicated runtime visual.

## Validation and remaining work

The alpha includes tested economy rules, construction occupancy, inherited psychology, powers, narration choices and cross-thread repeatability. The complete asset manifest passes palette, metadata, animation and family contract checks. See [the alpha validation record](releases/v0.1.0-alpha.1-validation.md) for exact release evidence.

Long-run balance, very large populations, memory ceilings, structure drawing at extreme scale, independent storage/cargo, complete specialist institutions and final visual coverage remain open. Earlier exploratory population and memory figures are not supported performance guarantees.
