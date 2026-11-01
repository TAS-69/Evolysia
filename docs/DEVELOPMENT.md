# Development status

Current source: **0.2.0-dev**. The published `0.1.0-alpha.1` remains a separate historical build. Catalogue coverage and completed gameplay are tracked separately.

## Connected systems

- Required structured model handshake, connection settings and local bootstrap. Time holds through missing councils, outages, expired plans and invalid replies at every speed.
- Councils choose labour, personal goals, remembered reflections, relationships, sharing, projects, research, diplomacy, elections, viable divisions and reciprocal unions. Family ties, trust, grudges, beliefs, clothing requests and recent experiences inform their observations.
- **Discovery and lore instead of a manual.** Councils are never told what work, buildings, products or techniques do. Each band keeps lore built from its own experience:
  - traditions (founders know foraging, hunting, rest and company);
  - what a day of each kind of work actually brought, used and cost, gathered over the first days it is tried;
  - what a building did the first time one stood, and what a product took and gave the first time it was made;
  - techniques, with the ideas they bring to mind (names only);
  - clues and dead ends.

  Untried work, buildings and products are marked untried, and the model must try them to learn. There is no research menu: new techniques come only from experiments the model proposes in the people's terms, an action (heat, strike, weave, plant, tame…) on up to three things they can perceive (clay, fire, reeds, animals, the dead, the sky…). These are judged against hidden ways into each technique (`Agents/Discovery.cs`). Experiments take real work; research buildings speed them. Outcomes come back as lore: a discovery, a near miss naming something missing or in the way, or a dead end. People occasionally notice clues at work. Knowledge can die with the people who hold it.
  - Crafters without an order only tinker. A sitting leader keeps office until death or a council decision; a death triggers a succession council.
- Full-text messages and dreams, literacy gating, renewed councils after interventions and rejection of replies based on replaced requests.
- Named generated rooms with validated dimensions, accessible fixtures, material costs and labour. Thirty-one specialist layouts connect to 130 structure definitions; regional bamboo/reed substitutions retain the actual material bill.
- Explicit manufacturing orders for all 40 processing recipes and 146 gear definitions, subject to techniques and stations. Work stages real inputs, consumes labour and power, handles blocked outputs and returns surviving materials on cancellation.
- Fuel-consuming generators, solar generation, battery capacity/reserves and powered refrigeration/manufacturing. Solar output currently uses climate and temperature as a daylight approximation.
- Staffed care, teaching, research, worship, governance and defence. Treatment consumes supplies; pupils learn known techniques with prerequisites; research takes sustained work; ceremonies propagate fictional doctrine; governance affects trust; guards improve raid defence. These remain compact service models, rather than complete legal, market or religious systems.
- Persistent stock identities, split lineage, bounded merge records, nested containers, capacity, reservations and spoilage. Discarded surplus remains recoverable on the ground and is rendered where it lies.
- Foot and vehicle cargo jobs with vehicle collection, movement, unloading, cancellation and transit saves. Repeated journeys wear paths; ordered dirt/stone roads require clearance, materials and labour. Completed roads improve freight speed; carriers recheck blocked routes.
- Terrain raising/lowering recalculates drainage, coasts, landmasses, habitats and water access. Flooded buildings are ruined; displaced camps seek nearby land. Grow restores native plants/trees within habitat limits.
- Possession of one adult, with movement, rest, foraging, chopping and quarrying. Position, health, fatigue, inventory and finite local stock remain authoritative. A distant person cannot draw food or services from camp. The community still requires model councils.
- Checksummed atomic world saves, named slots, separate quick save, three rotating autosaves, a load/delete browser and a legacy-save import button. Optional tagged fields allow compatible older development saves to migrate; incompatible required changes still fail explicitly.
- Settings for autosave interval, interface scale, reduced motion, fullscreen, object shadows and world labels.
- Per-garment style/main/trim selection, skin/hair choices, build and available facial features, including clean-shaven selection. Clothing requests require manufacture. Portraits reflect age, expression, facial inheritance and scars, with a separate cosmetic random stream.
- Existing cliff and road artwork connected at native densities; specified Pixelify Sans and Alegreya Sans fonts included with original OFL notices. Hidden orphaned controls and temporary rendering resources cleaned up in reviewed client exits.

## Remaining work

- Complete magic, space travel and all modern/future production and service chains.
- Richer laws, taxation, markets, political institutions, regional construction rules and sustained cultural/religious development.
- Fully routed routine harvesting, crafting inputs and trade; theft, traffic arbitration, moving vehicle footprint clearance and thoroughly tested sea-freight economies.
- Additional directly controlled activities, precise movement inside a simulation cell and full specialist work through possession.
- Procedural fictional culture glyphs with banned-shape rejection, remaining interface/effect families and dedicated structure visuals. Some families still enlarge base art at close zoom.
- Full expanded wardrobe/hairstyle support in portraits and a larger independently drawn cosmetic catalogue.
- Population-scale model throughput, long campaigns, economic balance, memory ceilings and supported hardware targets.
- Broader platform/backend testing, keyboard/controller accessibility and remaining menu polish.

## Verification

```powershell
dotnet test sim/Evolysia.Sim.slnx -c Release
dotnet build game/Evolysia.Game.csproj
python tools/assetgen/validate.py
```

Rules-only tests explicitly select `RequireModel=false`; this is a diagnostic mode. Use a real structured-output endpoint separately, inspect accepted/rejected plans and actual work, then reopen a saved world in the client. Synthetic replies cannot establish model quality.

The [November validation record](releases/v0.2.0-dev-2026-11-01-validation.md) separates this pass from the [previous development validation](releases/v0.2.0-dev-validation.md). No new public release is implied by local builds.
