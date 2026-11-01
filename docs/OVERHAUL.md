# Spatial simulation

The client and simulation use an 800 × 400 cylindrical planet. One cell is one logical sixteen-unit art tile; distance and ecological capacity scale from the original 160 × 80 model through `WorldMap.Scale` and `CellArea`.

## Authority and decisions

`Simulation` owns people, bands, ecology, structures, physical stock, memories and history. The renderer reads that state. Ordinary gameplay requires model-generated councils and pivotal choices. Requests carry world-local IDs; validated replies govern work, projects, research, diplomacy, leadership and other bounded actions. Interventions expire affected plans and stale replies cannot apply.

Random streams, chronological events and accepted plans survive saves. Background model requests receive immutable snapshots and apply replies on the simulation thread. Physiological needs, ecology, ownership and resource conservation remain authoritative rules. Rules-only diagnostic runs explicitly disable the model requirement; the game does not expose that as a fallback.

## Stock and transport

Inventories own persistent stacks, split lineage, bounded merge records, reservations and nested containers. `Band.Stores` aggregates its owned storage once; packs and individual fixtures retain their own stock. Explicit cargo jobs move workers and vehicles between containers and can resume after loading a save.

Routine deposits and some economic exchanges remain aggregate paths. Full traffic, theft, vehicle footprint clearance and sea-freight economies remain unfinished. A drawing of a ship is still cheaper than operating one.

## Construction

Projects clear land and consume instance material bills and labour. Thirty-one specialist layouts instantiate separately owned, paid fixtures. Generated named layouts validate known templates, dimensions, fixture access and costs. Tropical/wetland substitutions change actual construction inputs, rather than only appearance.

The simulation currently has 106 techniques, 107 goods, 146 gear definitions, 40 processing recipes and 130 structure definitions. Those counts do not establish complete modern, future, magical or political systems.

## Rendering and verification

The renderer chooses 16, 32 or 64 pixel art density from screen tile size. Modular outfits have independent main/trim dyes; the editor requests real manufactured garments. Portraits combine age, emotion, scars and inherited facial features. Other art families may enlarge their base sprites.

[Development status](DEVELOPMENT.md) lists the remaining systems and verification workflow. Historical alpha evidence remains in its own release records. Population targets, memory projections and exploratory timings are not supported performance guarantees.
