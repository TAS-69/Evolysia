# Evolysia game client

A Godot 4.7 **.NET** project that hosts the simulation from `../sim` and lets you watch and inspect the
world (800 x 400 cells). Zoomed in, it draws the original artwork from `../assets` at the density the zoom calls for:
16 px per cell, then 32 and 64 px as you close in (civilisation `render_contract.json`: up to 24 screen px per cell,
up to 48, beyond). Everything shown comes from the simulation: the woods people have felled, the boulders they've
broken, their fields and crops, every structure they've built, their animals, and each person's own clothes and
tools. Zoomed out, and for the data overlays, it shows a smooth procedural map.

## Run it
Install Godot 4.7.2 .NET. A local download may be kept at `Godot_v4.7.2-stable_mono_win64`; it is ignored by Git. The standard engine cannot run C#. See the [source build guide](../docs/wiki/Building-and-testing.md).

- **Editor:** open `Godot_v4.7.2-stable_mono_win64.exe`, choose Import, select `game/project.godot`, then press F5.
- **Directly:**
  ```bash
  ./Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64.exe --path game
  ```

## A new world
A plain launch (and **New world…** in the top bar) opens the new-world screen:
- **Seed:** type one or **Roll**.
- **Planet:** *Terra* (balanced continents, every climate), *Ignis* (hot, dry, volcano-studded), *Cryos* (cold, ice-bound) or *Thalassia* (an ocean world of small islands). The map preview regenerates as you choose.
- **Peoples:** 1 to 6. Each is its own people, with its own tongue, ways and colour, and starts as two bands.
- **Scatter at random** puts them in good land as far apart as the land allows. **Place on the map** lets you click the preview to set each one down (click again to remove). Begin waits until all are placed.

Options after `--`:

| flag | effect |
|---|---|
| `--seed N` | world seed; with any flag the game skips the new-world screen |
| `--planet Terra\|Ignis\|Cryos\|Thalassia` | planet type |
| `--tribes N` | peoples at the start (1-6) |
| `--setup` | open the new-world screen anyway |
| `--fold inspector,chronicle,voices` | start with those panels collapsed |
| `--speed N` | speed index (0 pause, 1-6 = 1× to 25×) |
| `--days N` | fast-forward N days before showing (e.g. `54000` = 150 years) |
| `--overlay Biome\|Elevation\|Temperature\|Rainfall\|Food\|Cultures` | starting map overlay |
| `--select band\|agent` | open the inspector on the largest band, or on its most eventful adult |
| `--zoom Z` | camera zoom (0.2-16; tile art fades in from 1.1, camps appear from 1.8, 32 px art from 3, 64 px from 6) |
| `--demo village` | (screenshots) the selected band settles with iron-age know-how and two years to build |
| `--focus x,y` | centre the camera on a cell |
| `--screenshot path.png` | capture after start-up, then quit (used for automated checks) |

## Controls
Right/middle-drag or WASD to pan, mouse wheel to zoom, left-click a band or any land to inspect.
Space pauses, 1-6 set the speed: 1×, 2×, 3×, 5×, 10×, 25× (12 to 300 days per second; how fast the top speeds really run depends on population). Tab cycles overlays, Esc closes the inspector.
Links in the chronicle and inspector jump to that band or person. Click a panel's heading (or its arrow) to collapse it to a strip; click again to open it.

## What's on screen
The planet **wraps east-west**, as the simulation does: pan forever in either direction and the far coasts and sea routes continue seamlessly. Terrain, markers, routes and effects are drawn on three copies, and the camera folds back into one planet-width, so the jump is invisible.

- **Planet, zoomed in:** landscape tiles at 16, 32 or 64 px, baked in 512 px chunks in the background around the view (a chunk rebakes when its patch of world changes). Each cell's biome, soil patch and shore pick one of 56 materials, blended on a dual grid. Water and lava animate. Rivers use the connection sets, sized by flow. Plants are scattered by biome (flora family at 16 px, civilisation detail art above) and coloured from the cell they stand on, through the nature LUT for its biome and season. Felled trees, quarried boulders and built-on cells show as gaps; fields show their crop and growth. Plants rooted in front of a person, animal or building are drawn over it.
- **Planet, zoomed out:** colour comes from biome, today's plant growth and the season, plus hillshading, rivers and ash. Coastlines are smoothed from interpolated elevation.
- **Bands, zoomed in:** every structure the simulation has sited, drawn by kind and state: dwellings composited from regional wall, floor and roof pieces (with scaffolding while they go up, damage, ruins and burns after); hearths that burn while there is firewood; stores showing what the band holds; workshops, shrines, pens, palisades that join up, boats and carts. The selected village, at 64 px, shows its houses roofless. Every member walks between home and the tile they work today, in the wardrobe pieces they own, holding the tool they use, animated by what they are doing. Herds graze round their pens; wild animals keep their distance.
- **Bands, zoomed out:** circles are wandering bands, squares are farming villages. Colour is culture, size is population, and sick bands turn yellow-green.
- **Both:** notable events flash as rings. Names are drawn crisply in screen space without overlapping.
- **Sea routes:** dashed arcs. Recent voyages are bright and older ones fade into a map of where people went. Lost voyages are red and end at sea with an ×, and first landings are ringed in gold. Select a band to see the shores its scouts have sighted across the water.
- **Look:** refined dark earthy (`Ui.cs`): translucent rounded panels, a slim top bar with a segmented speed control, and small colour cues in the text. Women are rose, men blue, children gold. Roles and today's activity are tinted by kind of work (food, making, spirit, people, mind). Goods are tinted by kind (food, material, fuel, metal, made, medicine), buildings by role, and peoples by their culture's hue. Section headings are ochre.
- **Chronicle:** the simulation's notable events, newest first, colour-coded by category (the same 8 categories as the art guide's icons).
- **Inspector:**
  - *Bands:* culture, leader, food and shelter, what they've built (and what's going up), their stores and animals, know-how, beliefs and how widely they're held, neighbours, members.
  - *People:* temperament and character now, with how far life has moved each trait from birth; feelings, needs, fears, and fears they've faced down; confidence, and domains they gave up after being shamed; beliefs, relationships and grudges, family links, life memories.
  - *Land:* climate, water, food stocks, hazards.

## God powers
The bar under the top bar holds your powers in three groups: **kind** ones in gold (Rain, Drought, Bless, Fertile, Heal, Game), **wrath** in red (Bolt, Fire, Flood, Quake, Blizzard, Plague, Meteor, Beasts, Curse), and **the word** in blue (Dream, Message).
- **Area powers:** pick one, see its reach as a ring at the cursor, and click the map to unleash it. Shift-click keeps it armed; Esc cancels. Acts land immediately, even while paused.
- **Person powers:** Curse and Dream act on the person selected in the inspector. **Dream** opens a dialog: choose what the dream urges (worship me, leave this place, make peace, seek new things, fear nothing, love) and optionally the words they hear. The kind decides the effect; your words are what they remember and repeat.

- **Message:** your own words, left at a place you click: **written on the ground** (weathers away in about 60 years), **carved in a standing stone** (a real structure; it lasts while it stands) or **sent as a vision** to every band near. Bands that come within reach read it once. Each adult takes it their own way, by temperament and whether they can read: as *scripture* (it becomes a belief, and may make you their god), as a *vision* (awe), as a literal *command* (peace, war, stay, go, sail, build an altar, learn, pray, matched from your words; only the literate), *dismissed*, or with *dread*. With Minds on, the leader's choice of reading goes to the model in character. It is the strongest and rarest power: the first can be given only after 250 years, then once every 250 years. The button counts down the years.

**They notice you.** Witnessed wonders build *awe*. When it is high enough, a seer names you, each people in their own tongue: *"… the Watcher Above"* if you have been kind, *"… the Hand in the Sky"* if cruel. A dreamer you visit can become your prophet.
- **Prayers:** believers in trouble pray for rain, food, healing, children or protection. Open prayers sit at the top of the chronicle with the days left to answer them.
- **Answering:** use the matching power on their band, or strike their enemies for protection. Answered prayers deepen faith and raise the priest's standing.
- **Ignoring:** silence breeds doubt, and faith in you fades like any belief unless something renews it.
- **What it sets off:** each act is announced in the chronicle, and believers' revelations, prayers, thanks and dreams are voiced by the narrator.
- **Replay:** every act is logged with its day (`Simulation.ActionLog`), so seed plus action and accepted-decision logs replay the same history.

Demo: `--demo gods` heals and blesses the largest band until it names you, then sends its leader a peace dream.

## Minds (LLM-driven choices)
With **Minds** on (top bar) and a model running, pivotal choices are made by the model, in character:
- **Responses to rejection:** conform, double down, withdraw, resent, leave.
- **How a leader greets a foreign people:** welcome, keep wary, raid.
- **How a leader reads your message:** scripture, vision, command, dismiss, dread.

How it works:
- **The shortlist:** the simulation first narrows each choice to the three options that person's temperament makes plausible. The model can only pick from those.
- **Brooding:** the person broods for 10 days. If no answer comes in time, instinct (the trait-weighted roll) decides.
- **Holding time:** at speeds up to 5×, time pauses for up to 6 s when a choice is about to fall to instinct. At 10× and 25× instinct always rules.
- **What you see:** the chosen option, the thought behind it and what they say appear in Voices and in the person's Inner voice. A late answer is discarded so voices never contradict what happened.
- **Determinism:** choices are logged inputs (`Simulation.DecisionLog`), so worlds stay replayable.
- **Watching it:** the top bar shows how many choices are *brooding*. Screenshot flags: `--speed 2 --select voice --wait-choices N`.

## Voices (LLM narration)
A local model voices notable moments: pivots after rejection, myths, inventions, schisms, overcome fears, killings and notable deaths.
- **Starting it:** start the model with the **Start model** button in the top bar, or run `python tools/llm_lab/lab.py serve`. This uses your lab profile (default `lite`: Qwen3.5-4B on GPU, 0.8B helper on CPU). The top bar shows *Voices on/offline*; hover it for throughput.
- **Where voices appear:** the **Voices** panel (inner thought in italics, speech in quotes, new beliefs and rituals highlighted), speech bubbles over the band on the map, and an **Inner voice** section in the person inspector.
- **What gets voiced:** a bounded, prioritised queue (`Evolysia.Narration.Narrator`). Schisms and myths outrank routine moments. Whoever you have selected (+4), their band (+2) and anything on screen (+1) jump the queue. Stale moments lose priority, and the least important are dropped. Coverage depends on server throughput. At high speeds the bounded queue skips more moments.
- Ordinary narration describes decided outcomes. With **Minds** enabled, valid choices for pending pivotal decisions can change the simulation; those accepted choices must be included when replaying a history. Late or invalid replies fall back to instinct.
- Add `--select voice --wait-voices N` to the screenshot flags to capture voices.

## Structure (`src/`)
| file | role |
|---|---|
| `Main.cs` | scene root: wiring, selection, input, screenshot mode |
| `SimHost.cs` | owns `Simulation`; steps days on the main thread within a 20 ms/frame budget |
| `WorldView.cs` | procedural planet texture and overlays (zoomed out) |
| `Art.cs` | reads `../assets`: sidecar JSON, reference PNGs, slot-index maps, LUTs |
| `TileBake.cs`, `TileView.cs`, `shaders/tiles.gdshader` | the planet in tile art at 16/32/64 px: chunks baked in the background to palette-slot indices (and plant roots), coloured per cell (season, snow, shade, culture tint, animated water) by the shader |
| `Settlements.cs` | snapshots the sim for the bake (fields, built-on cells, timber and stone left); regional building styles |
| `Camps.cs` | structures and people from the sim: building composites, stores with contents, people between home and work |
| `People.cs` | wardrobe compositor: 12 layers with per-piece dyes, any of 20 animations, at 16/32/64 px |
| `Wildlife.cs` | wild herds, predators and fish from the ecology; each band's own herds |
| `Sprites.cs` | world sprites, enlarged and LUT-recoloured textures |
| `BandLayer.cs`, `BandLabels.cs` | y-sorted camps (with plants in front), or markers; sea routes and event pings; screen-space labels |
| `CameraRig.cs` | pan and zoom, clamped to the planet |
| `Hud.cs`, `Ui.cs`, `Describe.cs` | the interface (top bar, powers, collapsible panels, dream and message dialogs), its theme and colour tags, chronicle and inspector BBCode |
| `SetupScreen.cs` | the new-world screen: seed, planet, peoples, map preview, placing tribes |
| `Palette.cs` | zoomed-out biome colours, final UI colours, culture hues |
| `shaders/pixel.gdshader` | keeps pixel art crisp at non-integer zoom |
| `TargetOverlay.cs` | the armed power's reach at the cursor, and bursts where acts land |
| `NarrationHost.cs` | feeds moments to the narrator with focus priorities, delivers voices, can start the lab servers |

## Next
- Art still missing at some densities is enlarged from 16 px (rivers, early structures such as totems and storage pits, crops other than grain); cliffs, territory borders and roads aren't drawn yet.
- Spatially index structures for drawing when worlds hold tens of thousands of them.
- Optionally step the simulation on a worker thread. Each day already runs across cores (`SimConfig.Threads`, cores minus 2 in the game), so the UI stays responsive without it.
- Swap power-bar text for the final icon set (art guide section 11.3), and add Raise/Lower land, Possess and Follow.
