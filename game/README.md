# Evolysia game client (step 3)

A Godot 4.7 **.NET** project that hosts the simulation from `../sim` and lets you watch and inspect the
world. The art is procedural placeholder until the Codex asset pipeline (`docs/art/`) delivers sprites
and tiles.

## Run it
The .NET build of Godot is in the repo root (`Godot_v4.7.2-stable_mono_win64`). The standard build next to it can't run C#.

- **Editor:** open `Godot_v4.7.2-stable_mono_win64.exe`, choose Import, select `game/project.godot`, then press F5.
- **Directly:**
  ```bash
  ./Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64.exe --path game -- --seed 42
  ```

Options after `--`:

| flag | effect |
|---|---|
| `--seed N` | world seed (also settable in the top bar) |
| `--days N` | fast-forward N days before showing (e.g. `54000` = 150 years) |
| `--overlay Biome\|Elevation\|Temperature\|Rainfall\|Food\|Cultures` | starting map overlay |
| `--select band\|agent` | open the inspector on the largest band, or on its most eventful adult |
| `--zoom Z` | camera zoom (0.6-10) |
| `--screenshot path.png` | capture after start-up, then quit (used for automated checks) |

## Controls
Right/middle-drag or WASD to pan, mouse wheel to zoom, left-click a band or any land to inspect.
Space pauses, 1-4 set the speed (2 / 10 / 60 / 360 days per second), Tab cycles overlays, Esc closes the inspector.
Links in the chronicle and inspector jump to that band or person.

## What's on screen
The planet **wraps east-west**, as the simulation does: pan forever in either direction and the far coasts and sea routes continue seamlessly. Terrain, markers, routes and effects are drawn on three copies, and the camera folds back into one planet-width, so the jump is invisible.

- **Planet:** colour comes from biome, today's plant growth and the season (dry seasons, autumn colour, snow and sea ice follow real temperature), plus hillshading, rivers and ash after eruptions. Coastlines are smoothed from interpolated elevation.
- **Bands:** circles are wandering bands, squares are farming villages. Colour is culture, size is population, and sick bands turn yellow-green. Notable events flash as rings. Names are drawn crisply in screen space without overlapping.
- **Sea routes:** dashed arcs. Recent voyages are bright and older ones fade into a map of where people went. Lost voyages are red and end at sea with an ×, and first landings are ringed in gold. Select a band to see the shores its scouts have sighted across the water.
- **Chronicle:** the simulation's notable events, newest first, colour-coded by category (the same 8 categories as the art guide's icons).
- **Inspector:**
  - *Bands:* culture, leader, food and shelter, know-how, beliefs and how widely they're held, neighbours, members.
  - *People:* temperament and character now, with how far life has moved each trait from birth; feelings, needs, fears, and fears they've faced down; confidence, and domains they gave up after being shamed; beliefs, relationships and grudges, family links, life memories.
  - *Land:* climate, water, food stocks, hazards.

## God powers
The bar under the top bar holds your powers: **kind** ones in gold (Rain, Bless, Fertile, Heal, Game), **wrath** in red (Drought, Bolt, Fire, Flood, Quake, Blizzard, Plague, Meteor, Beasts, Curse), and **Dream** in blue.
- **Area powers:** pick one, see its reach as a ring at the cursor, and click the map to unleash it. Shift-click keeps it armed; Esc cancels. Acts land immediately, even while paused.
- **Person powers:** Curse and Dream act on the person selected in the inspector. **Dream** opens a dialog: choose what the dream urges (worship me, leave this place, make peace, seek new things, fear nothing, love) and optionally the words they hear. The kind decides the effect; your words are what they remember and repeat.

**They notice you.** Witnessed wonders build *awe*. When it is high enough, a seer names you, each people in their own tongue: *"… the Watcher Above"* if you have been kind, *"… the Hand in the Sky"* if cruel. A dreamer you visit can become your prophet.
- **Prayers:** believers in trouble pray for rain, food, healing, children or protection. Open prayers sit at the top of the chronicle with the days left to answer them.
- **Answering:** use the matching power on their band, or strike their enemies for protection. Answered prayers deepen faith and raise the priest's standing.
- **Ignoring:** silence breeds doubt, and faith in you fades like any belief unless something renews it.
- **What it sets off:** each act is announced in the chronicle, and believers' revelations, prayers, thanks and dreams are voiced by the narrator.
- **Replay:** every act is logged with its day (`Simulation.ActionLog`), so seed plus log replays the same history.

Demo: `--demo gods` heals and blesses the largest band until it names you, then sends its leader a peace dream.

## Minds (LLM-driven choices)
With **Minds** on (top bar) and a model running, pivotal choices are made by the model, in character:
- **Responses to rejection:** conform, double down, withdraw, resent, leave.
- **How a leader greets a foreign people:** welcome, keep wary, raid.

How it works:
- **The shortlist:** the simulation first narrows each choice to the three options that person's temperament makes plausible. The model can only pick from those.
- **Brooding:** the person broods for 10 days. If no answer comes in time, instinct (the trait-weighted roll) decides.
- **Holding time:** at speeds up to 3×, time pauses for up to 6 s when a choice is about to fall to instinct. At 4× instinct always rules.
- **What you see:** the chosen option, the thought behind it and what they say appear in Voices and in the person's Inner voice. A late answer is discarded so voices never contradict what happened.
- **Determinism:** choices are logged inputs (`Simulation.DecisionLog`), so worlds stay replayable.
- **Watching it:** the top bar shows how many choices are *brooding*. Screenshot flags: `--speed 2 --select voice --wait-choices N`.

## Voices (LLM narration)
A local model voices notable moments: pivots after rejection, myths, inventions, schisms, overcome fears, killings and notable deaths.
- **Starting it:** start the model with the **Start model** button in the top bar, or run `python tools/llm_lab/lab.py serve`. This uses your lab profile (default `lite`: Qwen3.5-4B on GPU, 0.8B helper on CPU). The top bar shows *Voices on/offline*; hover it for throughput.
- **Where voices appear:** the **Voices** panel (inner thought in italics, speech in quotes, new beliefs and rituals highlighted), speech bubbles over the band on the map, and an **Inner voice** section in the person inspector.
- **What gets voiced:** a bounded, prioritised queue (`Evolysia.Narration.Narrator`). Schisms and myths outrank routine moments. Whoever you have selected (+4), their band (+2) and anything on screen (+1) jump the queue. Stale moments lose priority, and the least important are dropped. At 1×-2× nearly everything is voiced; at 4× only the big moments are.
- **Narration never changes the simulation.** The model voices outcomes the simulation already decided, so worlds stay deterministic.
- Add `--select voice --wait-voices N` to the screenshot flags to capture voices.

## Structure (`src/`)
| file | role |
|---|---|
| `Main.cs` | scene root: wiring, selection, input, screenshot mode |
| `SimHost.cs` | owns `Simulation`; steps days on the main thread within a 20 ms/frame budget |
| `WorldView.cs` | procedural planet texture and overlays |
| `BandLayer.cs`, `BandLabels.cs` | band markers and event pings; screen-space labels |
| `CameraRig.cs` | pan and zoom, clamped to the planet |
| `Hud.cs`, `Describe.cs` | UI theme (art guide section 11.1 colours), chronicle and inspector BBCode |
| `Palette.cs` | placeholder biome colours, final UI colours, culture hues |
| `TargetOverlay.cs` | the armed power's reach at the cursor, and bursts where acts land |
| `NarrationHost.cs` | feeds moments to the narrator with focus priorities, delivers voices, can start the lab servers |

## Next
- Swap the procedural map for the Codex tiles and the palette-swap shader (art guide sections 2.2 and 3).
- Optionally step the simulation on a worker thread. Each day already runs across cores (`SimConfig.Threads`, cores minus 2 in the game), so the UI stays responsive without it.
- Swap power-bar text for the Codex icon set (art guide section 11.3), and add Raise/Lower land, Possess and Follow.
