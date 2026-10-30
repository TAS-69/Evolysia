# Evolysia - Art Direction & Asset Specification

Status: v1 (2026-10-30). Source of truth for every graphic in the game. The IDs in this document
are shared with the simulation code (biomes, emotions, event categories, needs, traits, actions), so
renaming one is a code change, not just an art change.

---

## 1. What the art must do

Evolysia is a god-game: a randomly generated planet where thousands of autonomous people live, feel,
invent, worship and fight. The player mostly **watches**. So the art must:

1. **Stay readable at a glance and at scale.** Thousands of agents and a whole continent on screen. Silhouette and value contrast matter more than detail.
2. **Show inner life.** Emotions, fear, faith and culture must be visible through emotion bubbles, portraits, body paint and banners, not only in menus.
3. **Change with the world.** Seasons, biomes, disasters, cultures and technology all change how things look. Recolouring and modular parts beat thousands of hand-made variants.
4. **Look handcrafted, never copied.** The style is our own. Do not imitate any existing game's assets or distinctive look.

### Style in one line
**"Living diorama": warm, earthy, top-down oblique pixel art.** The ground is seen from directly above.
Upright things (people, trees, buildings) show their front face and a little of their top. Palettes are
naturalistic and slightly desaturated. **Life gets saturated accents**: people's clothing and culture
colours, fire, divine powers, emotion icons. The player's eye should always find the living things first.

### Mood words
earthy, handmade, woven, carved, sun-warmed, weathered, hopeful, fragile, folkloric (but not real-world folklore).

### Never
- Recreate or trace assets, characters or distinctive styles from existing games or media.
- Use real-world religious, political or national symbols: crosses, crescents, stars of David, pentagrams, ankh, om, yin-yang, swastika or any rotated hooked-cross shape, flags, brand marks. Every culture and faith in Evolysia is fictional, and its symbols are procedurally generated (section 9).
- Bake text into images (it can't be localised). The only exception is preview/contact sheets.
- Use anti-aliasing against transparency, blur, gradients in sprites (shaders may add glow), or JPEG.
- Show gore. Death is shown with a fallen pose and a fade, plus an optional small dark-brown stain decal behind a setting.

---

## 2. Technical foundation

| Spec | Value |
|---|---|
| Base tile | **16 x 16 px** |
| Rendering | Integer zoom (1x-6x) in Godot 4, nearest-neighbour filtering, no mipmaps |
| File format | PNG-32 (RGBA), sRGB, straight (not premultiplied) alpha, alpha strictly 0 or 255 except the soft-alpha families in section 2.4 |
| Light | Key light from the **top-left**. Highlights on top/left faces, shade on bottom/right |
| Outline | 1 px **selective outline**: a darker shade of the adjacent ramp, never pure black. Pure-black outlines are allowed only on UI cursors |
| Shadows | **Not baked.** The engine draws ground shadows (ellipse by size class: S/M/L/XL) so day/night and the sun angle can move them |
| Max colours | ~16 per sprite (excluding transparency); every colour must come from the master palette |
| Animation | Horizontal strips, one row per direction, frame size constant within a sheet |
| Pivot | Characters/animals: bottom-centre of the feet. Trees/props/buildings: bottom-centre of the footprint. The engine Y-sorts by pivot |

### 2.1 Master palette
- One master palette: `assets/palette/evolysia.gpl`, plus `palette.png` (swatches) and `palette.json` (named ramps).
- About 96-128 colours, organised as **ramps** of 3-6 shades going dark to light. Hues shift slightly as they lighten: shadows lean cool/purple and highlights lean warm/yellow.
- Required ramp families: `soil`, `sand`, `rock`, `basalt`, `snow`, `ice`, `water`, `grass`, `foliage`, `bark`, `wood`, `thatch`, `hide`, `mudbrick`, `stone`, `clay`, `metal_copper`, `metal_iron`, `fire`, `lava`, `ash`, `skin` (8 tones x 5 shades), `hair` (8 colours x 4 shades), `cloth` dyes (12 x 4 shades), `culture` (12 hues x 3 shades), `ui` (section 10), `divine` (gold-white glow), `blight` (sickly yellow-green), `shadow`.
- **Brand anchors**, taken from the logo: bone `#f1e6d9`, parchment `#d4c3b1`. They are the UI text and highlight colours.

### 2.2 Recolouring: indexed variants
Seasons, biomes, skin, hair, clothing dyes and culture colours are applied **at runtime by a palette-lookup shader**, not by drawing variants. Recolourable families are exported twice:

1. `<id>.png`: a **reference** colouring (temperate summer; average skin, brown hair, undyed cloth), used for review and as a fallback.
2. `<id>_idx.png`: an **index map**. R = ramp-slot index (0-255), G = 0, B = 0, A = alpha. Non-recolourable pixels use reserved slots that map to fixed colours.

Slots are defined in `palette.json` (e.g. `foliage_0..4`, `grass_0..4`, `skin_0..4`, `hair_0..3`, `cloth_a_0..3`, `cloth_b_0..3`, `culture_0..2`). The lookup textures (LUTs) are:

- `lut_nature.png`: one row per **biome palette x season** (section 3.3).
- `lut_skin.png` (8 rows), `lut_hair.png` (8 rows), `lut_cloth.png` (12 dyes + undyed), `lut_culture.png` (12 rows).

Each LUT row maps slot index to colour. A sprite drawn once can then appear in every season, biome, skin tone and culture.

### 2.3 Folders, naming, metadata
```
assets/
  palette/            evolysia.gpl, palette.png, palette.json, lut_*.png
  terrain/            <material>.png / _idx.png / .json
  overlays/           rivers, roads, borders, snow_cover, decals
  flora/  fauna/  people/  portraits/  structures/  items/  effects/  icons/  ui/  map/
  manifest.json       every asset: id, family, file(s), frame size, frames, directions, fps, pivot, tags
  _preview/           contact sheets (4x, labelled) - review only, not shipped
tools/assetgen/       generators, validator, contact-sheet builder
```
- IDs are `snake_case`, family-prefixed in the manifest (`flora.conifer_mature`), and file names match the ID.
- Each sheet has a sidecar `.json`: `{"frame": [w,h], "pivot": [x,y], "directions": ["down","up","side"], "anims": {"walk": {"row": 0, "frames": 4, "fps": 8}}, "recolor_slots": [...]}`.

### 2.4 Soft-alpha exceptions
Partial alpha is allowed only for: cloud and storm shadows, fog, smoke, rain and snow particles, glow and divine effects, weather overlays, and UI drop-shadows. Everything else is hard-edged.

---

## 3. World and terrain

### 3.1 Terrain materials (tiles)
Tiles are **materials**. A biome is a classification that picks the materials, flora and palette row.

| id | notes |
|---|---|
| `deep_water` | animated, 4 frames, slow swell |
| `water` | animated, 4 frames |
| `shallow_water` | animated, 4 frames; seabed faintly visible |
| `sand` | beaches, deserts |
| `grass` | recolourable via `grass` slots |
| `dry_grass` | steppe, savanna |
| `forest_floor` | leaf litter, needles (recolourable) |
| `jungle_floor` | dense, darker, roots |
| `tundra` | moss/lichen patches |
| `dirt` | bare soil, trampled ground |
| `mud` | wetlands, after floods |
| `marsh` | wet mud + water pockets |
| `rock` | mountains, cliffs |
| `scree` | gravel slopes |
| `snow` | |
| `ice` | glaciers, frozen lakes |
| `basalt` | volcanic plains |
| `lava` | animated, 4 frames, emissive |
| `ash` | after eruptions or fires (also an overlay) |
| `salt_flat` | dry lake beds |
| `tilled_soil` | farms, 2 variants (furrows N-S / E-W) |
| `path` | trampled path (also an overlay) |

**Per material:**
- 4 plain full-tile variants (the engine picks one at random per cell).
- A **16-tile corner set** for blending: a "dual-grid" set where each tile covers the corners of four cells, indexed by a 4-bit corner mask (TL=1, TR=2, BR=4, BL=8). Draw it **over transparency** so it layers on whatever lies beneath.
- **Layer priority** (lowest to highest): deep_water < water < shallow_water < marsh < mud < sand < salt_flat < dirt < tilled_soil < dry_grass < grass < tundra < forest_floor < jungle_floor < scree < rock < basalt < ash < snow < ice < lava. A higher material's edges overlap lower ones.
- Edges must look organic (irregular, 1-3 px wobble), not straight. Water edges get a 1 px foam/wet-sand highlight on the land side.
- Elevation: a 16-tile **cliff** corner set for `rock` and `scree` faces, plus a south-facing cliff wall strip (16x16, 3 variants) to show height steps.

### 3.2 Overlays
| id | spec |
|---|---|
| `river` | 16-piece connection set (4-bit N/E/S/W mask) x 3 widths (stream, river, great river), animated 4 frames |
| `road` | 16-piece set x 3 types: trampled path, packed dirt road, stone road |
| `territory_border` | 16-piece corner set, white index, culture-tinted, dashed |
| `snow_cover` | 16-piece corner set laid over any land material (seasonal accumulation), plus a "light dusting" full-tile variant |
| `frost` / `wet_ground` / `ash_fall` / `burned_ground` / `flood_water` | full-tile overlays with 3 variants each; `flood_water` animated 4 frames |
| `crack` | earthquake decals, 4 variants (16 and 32 px) |
| `crater` | 32x32 and 48x48 decals |
| `footprint_trail` | optional, 4 variants |

### 3.3 Biomes -> palette rows
Biome IDs (shared with the simulation): `ice_sheet`, `tundra`, `boreal_forest`, `cold_desert`, `temperate_grassland`,
`temperate_forest`, `temperate_rainforest`, `shrubland`, `hot_desert`, `savanna`,
`tropical_seasonal_forest`, `tropical_rainforest`, `wetland`, `alpine`, `volcanic`, plus water biomes
`ocean`, `deep_ocean`, `lake`, `coast`.

- `lut_nature.png` has one row per **biome x season**. Seasons are `spring`, `summer`, `autumn`, `winter` for temperate and cold biomes, and `wet` and `dry` for tropical ones. The engine blends between adjacent rows over time, so the colour change is gradual.
- Seasonal intent:
  - Spring: fresh yellow-greens and blossom accents.
  - Summer: deep greens.
  - Autumn: ochre, rust and crimson foliage, with grass shifting to straw.
  - Winter: desaturated, cool, deciduous foliage gone (section 4), snow from the overlay.
  - Dry: straw, dust, pale.
  - Wet: lush, saturated.
- **Climate-driven tints** (shader uniforms, not assets): drought (desaturate + warm), volcanic winter (grey + cool), plague years (slight sickly cast).

---

## 4. Flora
All flora is recolourable (`foliage`, `bark`, `grass`, `flower` slots). Pivot is at the base. Trees **overhang** their tile.

| id | size (px) | variants | states |
|---|---|---|---|
| `conifer` | 16x32 | 4 | sapling(16x16), young, mature, old, dead, burned, snow_capped (extra layer) |
| `broadleaf` | 16x32 / 32x32 (old) | 4 | sapling, young, mature, old, **bare** (winter), dead, burned |
| `rainforest_giant` | 32x48 | 3 | young, mature, burned |
| `jungle_tree` | 32x32 | 4 | young, mature, burned |
| `palm` | 16x32 | 3 | young, mature, burned |
| `acacia` | 32x32 | 3 | young, mature, bare(dry season), burned |
| `shrub` | 16x16 | 4 | normal, dry, burned |
| `berry_bush` | 16x16 | 3 | empty, flowering, fruiting (fruit = `cloth`-style accent slot, tintable) |
| `cactus` | 16x16 / 16x32 | 3 | normal, flowering |
| `tall_grass` | 16x16 | 4 | animated sway 4 frames |
| `reeds` | 16x16 | 3 | animated sway 4 frames |
| `flowers` | 16x16 | 4 | tintable bloom colour |
| `mushrooms` | 16x16 | 3 | |
| `stump`, `fallen_log` | 16x16 | 2 each | |
| `wild_grain`, `wild_tuber`, `wild_gourd`, `wild_fiber` | 16x16 | 2 each | growing, ripe (these are what agents domesticate) |
| `crop_grain`, `crop_tuber`, `crop_gourd`, `crop_fiber` | 16x16 | 1 each | seedling, growing, ripe, harvested, withered |

Wind sway: trees get a 2-frame idle (1 px canopy shift) or the shader handles sway. Mark which in the manifest.

---

## 5. Fauna
Animals are **side view** (facing right; the engine mirrors for left), used for all movement directions.
Domesticated animals are recolour variants (`hide`/`hair` slots) with more varied coat patterns.

| id | frame (px) | anims (frames) | notes |
|---|---|---|---|
| `rabbit` | 8x8 | idle 2, walk 4, run 4, dead 1 | |
| `fowl` | 8x8 | idle 2, walk 4, flap 2, dead 1 | wild bird -> domestic fowl |
| `bird_flock` | 16x16 | fly 4 | 3-5 birds, ambient |
| `fish_school` | 16x16 | swim 4 | dark shapes under water |
| `goat`, `sheep` | 16x16 | idle 2, walk 4, run 4, eat 2, dead 1 | wild + domestic |
| `deer` | 16x16 | idle 2, walk 4, run 4, eat 2, dead 1 | |
| `boar` | 16x16 | idle 2, walk 4, run 4, eat 2, attack 3, dead 1 | |
| `elk` | 16x24 | idle 2, walk 4, run 4, eat 2, dead 1 | antlers |
| `bison` | 24x16 | idle 2, walk 4, run 4, eat 2, attack 3, dead 1 | -> domestic `cattle` |
| `horse` | 24x16 | idle 2, walk 4, run 4, eat 2, dead 1 | wild + domestic |
| `camel` | 24x24 | idle 2, walk 4, dead 1 | deserts |
| `wolf` | 16x16 | idle 2, walk 4, run 4, attack 3, howl 2, dead 1 | -> domestic `dog` |
| `bear` | 24x16 | idle 2, walk 4, run 4, attack 3, stand 2, dead 1 | |
| `big_cat` | 24x16 | idle 2, walk 4, run 4, attack 3, dead 1 | |
| `crocodile` | 24x8 | idle 2, crawl 4, attack 3, dead 1 | wetlands |
| `snake` | 16x8 | idle 2, slither 4, strike 2 | |

Predators must read as dangerous at 1x: darker values and a clear head and teeth silhouette.

---

## 6. People
Up to thousands on screen. Each person is assembled at runtime from **layers** chosen by their genome,
age, culture and technology, so children visibly resemble their parents.

### 6.1 Sprite spec
- Frame **16x16**. An adult figure is about **11 px** tall, a child about **8 px**, an elder about **10 px** (slight stoop).
- Directions: `down`, `up`, `side` (right; mirrored for left).
- Builds: `slim`, `average`, `stout`, with 1 px differences in width. Ages: `child`, `adult`, `elder`.
- **Layer order** (back to front): `hair_back` -> `body` (skin slots) -> `clothing` (cloth slots) -> `body_paint` (culture slots, drawn over skin and under clothing edges) -> `hair_front` -> `accessory` (headdress, necklace) -> `held_item`.
- Variety:
  - Hair: 10 styles (shaved, short, cropped, long, braided, topknot, mane, tied-back, locks, child tuft), with an elder variant through the hair LUT (grey/white).
  - Clothing by technology/material: `none` (loincloth), `hide_wrap`, `fur_cloak` (cold), `woven_tunic`, `dyed_robe`, `ceremonial` (shaman/priest, culture-tinted).
  - Body paint: 6 patterns.
  - Accessories: feather band, bone necklace, beads, mask (ritual), helmet (bone/leather).
- Held items (separate 16x16 layer, aligned to a hand-anchor point per frame in the sheet JSON): spear, stone axe, digging stick, basket, torch, bow, net, staff, bundle (carry), infant (carried).

### 6.2 Animations (frames, all 3 directions unless noted)
| anim | frames | fps | sim action ids |
|---|---|---|---|
| `idle` | 2 | 3 | idle, wait |
| `walk` | 4 | 8 | walk, migrate |
| `run` | 4 | 12 | flee, chase |
| `gather` | 3 | 6 | forage, harvest |
| `chop` | 3 | 8 | chop |
| `dig` | 3 | 6 | dig, mine, farm |
| `attack` | 3 | 10 | hunt, fight |
| `carry` | 4 | 8 | haul (walk with bundle) |
| `build` | 3 | 6 | build |
| `craft` | 3 | 6 | craft, cook (seated, side only) |
| `eat` / `drink` | 2 | 4 | eat, drink |
| `talk` | 2 | 4 | talk (hand gesture) |
| `pray` | 2 | 3 | pray, ritual |
| `dance` | 4 | 8 | celebrate |
| `mourn` | 2 | 3 | mourn (kneeling) |
| `sleep` | 2 | 2 | sleep (lying, side only) |
| `swim` | 4 | 6 | swim (head and shoulders only) |
| `play` | 4 | 8 | children only |
| `dead` | 1 | - | dead (lying, side only) |

### 6.3 Portraits (inspector, family tree, chronicle)
- **48x48** layered portraits, shown at 2x/3x. Head and shoulders, 3/4 view facing left.
- Layers: `bg` (culture-tinted vignette) -> `shoulders` (clothing material) -> `head` (3 shapes x skin slots) -> `ears` -> `eyes` (6 shapes, 5 iris colours) -> `brows` (4 shapes) -> `nose` (5) -> `mouth` -> `facial_hair` (4) -> `hair` (10 styles front+back) -> `scars` (3: cheek, brow, missing-ear) -> `paint` (culture pattern) -> `accessory`. **Child** and **elder** have their own head bases (proportions, wrinkles).
- **Expressions** are combinations of parts, defined in data, so all 11 emotions are covered without separate faces:

| emotion | brows | eyes | mouth |
|---|---|---|---|
| `calm` | neutral | normal | neutral |
| `joy` | raised | normal (crinkled) | smile |
| `pride` | neutral | narrowed | smirk |
| `love` | raised | soft (half-closed) | small smile |
| `surprise` | raised high | wide | open_round |
| `fear` | raised + pinched | wide | grimace |
| `anger` | furrowed | narrowed | open_shout |
| `disgust` | furrowed one side | narrowed | sneer |
| `sadness` | sad tilt | downcast | frown |
| `shame` | sad tilt | closed/averted | pressed |
| `envy` | furrowed | side-glance | pout |

Scars come from simulation events (predator attacks, battles), so trauma is visible in the portrait.

---

## 7. Structures
Technology is emergent, so structures are tagged by **material**, not by era. Buildings are made of **modular parts**, because agents design buildings from parts. A few complete "landmark" sprites cover common early structures.

### 7.1 Simple and early structures (complete sprites, states: building, complete, damaged, ruined, burned)
`windbreak` 16x16 - `lean_to` 16x16 - `hide_tent` 16x24 - `pit_house` 32x24 - `fire_pit` 16x16 (unlit and lit animated 4 frames) -
`drying_rack` 16x16 - `storage_pit` 16x16 - `granary` 16x24 - `kiln` 16x24 (lit animated) - `smelter` 16x24 (lit animated) -
`well` 16x16 - `animal_pen` (fence pieces, see 7.2) - `burial_mound` 16x16 - `cairn` 16x16 - `standing_stone` 16x24 -
`totem` 16x32 (carvings culture-tinted) - `altar` 16x16 - `dock` (pieces) - `raft` 16x16 - `canoe` 16x16 - `bridge` (pieces).

### 7.2 Modular parts (tiles, 16-piece connection sets where they connect)
- `wall_<material>`: materials `wattle`, `wood`, `mudbrick`, `stone`, `fired_brick`, `palisade`. Connection set (N/E/S/W mask) with a south-facing front face 16x16 and a top cap.
- `roof_<material>`: materials `hide`, `thatch`, `bark`, `wood_shingle`, `clay_tile`. 16-tile corner sets drawn as roof planes, plus ridge, gable and conical (round-hut) pieces for 1x1, 2x2 and 3x3 footprints.
- `floor_<material>`: `earth`, `reed_mat`, `plank`, `flagstone`.
- `door_<material>`, `window_<material>`, `fence_<material>` (wood, wattle, stone), `stairs`, `pillar`.
- Construction overlays: `scaffold`, `foundation_outline`, `rubble`, `scorch`.
- **Religious buildings** are composed from parts plus a **culture glyph** (section 9) placed on the roof peak, door or banner. There is no fixed temple design.

---

## 8. Items and resources
16x16 icons (also used in the UI) and 8x8 world drop sprites.

- Food: `berries`, `nuts`, `roots`, `grain`, `meat_raw`, `meat_cooked`, `fish`, `fruit`, `honey`, `bread`, `dried_meat`, `milk`, `eggs`.
- Materials: `wood`, `log`, `stone`, `flint`, `clay`, `hide`, `fur`, `fiber`, `reed`, `bone`, `antler`, `copper_ore`, `tin_ore`, `iron_ore`, `copper_ingot`, `bronze_ingot`, `iron_ingot`, `salt`, `ochre` (pigment), `charcoal`.
- Crafted: `stone_tool`, `spear`, `bow`, `arrow`, `axe`, `pick`, `hoe`, `net`, `basket`, `pot`, `cloth`, `rope`, `jewelry`, `idol`, `medicine`, `drum`, `flute`.
- World resource nodes: `boulder`, `rock_outcrop`, `flint_nodule`, `clay_pit`, `ore_vein` (tinted copper/tin/iron), `salt_deposit`, `obsidian`, `cave_entrance` 16x16 / 32x32, `hot_spring` animated, `fresh_spring` animated, `beehive`.

---

## 9. Culture glyphs (procedural)
Each culture and faith gets a generated glyph: 12x12, drawn as strokes on a 6x6 construction grid.

- **Allowed primitives:** dot, ring, arc, spiral, wave, zigzag, ray burst, chevron, eye, hand, leaf, horn, sun disc, moon disc (full only), animal-track, concentric rings, diamond lattice. Symmetric or asymmetric.
- **Banned:** any cross with equal or near-equal arms; latin cross; hooked/rotated-hook crosses (swastika family) in any orientation; 5- and 6-point stars; crescent with or without a star; ankh; pentagram; yin-yang; om; any letter from a real alphabet. The generator **must** run a rejection check against these.
- Glyphs are white-indexed and tinted via `lut_culture`. They are used on banners (16x24, animated sway 2 frames), body paint, totems, roofs, the map and the UI.

---

## 10. Effects, weather and disasters
| id | spec |
|---|---|
| `fire_s/m/l` | 8x8 / 16x16 / 16x32, 6 frames, emissive |
| `smoke` | soft alpha, 16x32, 8 frames |
| `embers` | particles 2x2 |
| `rain`, `heavy_rain`, `snow`, `hail` | particle sprites 1x4 / 2x6 / 2x2 / 2x2; a screen-space overlay for heavy rain |
| `fog` | tileable 64x64 soft-alpha overlay |
| `cloud_shadow` | 64x32 soft-alpha blobs, 4 variants |
| `lightning` | 16x64 / 16x96, 3 frames, plus a flash (engine) |
| `tornado` | 24x48, 6 frames |
| `dust_storm` | tileable 64x64 overlay, 4 frames |
| `lava_flow` | animated overlay, 4 frames |
| `volcanic_plume` | 32x64, 8 frames, plus an ash particle |
| `tsunami_front` | 16x16 tileable wave crest, 4 frames |
| `meteor` | 16x16 head + trail, 4 frames, plus impact burst 32x32 (6 frames) |
| `splash`, `ripple` | 16x16, 4 frames |
| `dust_puff` | 8x8, 4 frames (construction, running) |
| `plague_miasma` | soft particles in `blight` colours |
| `locust_swarm` | dot particles |
| `birth_sparkle` | 8x8, 4 frames, warm |
| `death_fade` | 8x8, 4 frames, a gentle rising wisp (neutral, no religious iconography) |
| `harvest_sparkle` | 8x8, 4 frames |
| `divine_bless` / `divine_curse` / `dream_ripple` / `power_target` | the player's god powers; `divine` colours for bless, deep violet for curse, soft cyan rings for dream, 16-32 px, 6 frames |
| `selection_ring` | 16x8, 4 frames |
| `path_dot` | 4x4 |

### 10.1 Status bubbles (above heads)
- A **10x10 bubble frame** (thought/speech/alert variants) with **7x7 glyph icons** inside.
- Emotions (one each): `joy`, `fear`, `anger`, `sadness`, `shame`, `pride`, `disgust`, `surprise`, `love`, `calm`, `envy`.
- Needs: `hungry`, `thirsty`, `tired`, `cold`, `scared`.
- Activities: `talking` (squiggle, no letters), `idea` (spark), `praying`, `conflict` (crossed sticks), `sick`, `injured`, `mourning`, `romance`, `teaching`, `building`.
- Icons must read as **pure silhouettes** at 7x7 in two colours.

---

## 11. UI
UI art is pixel art at native resolution, scaled 2x/3x by the engine.

### 11.1 Theme: "carved wood and woven cloth"
- Panels are **deep umber/charcoal**: bg `#1e1813`, panel `#2a211a`, raised `#3a2e24`, border `#5a4636`.
- Text and highlights are **bone** `#f1e6d9` and **parchment** `#d4c3b1`.
- Accents: **ochre** `#c8913a` (primary action), **moss** `#6f8f4e` (positive), **terracotta** `#b5553c` (danger/negative), **river blue** `#4f86a8` (info), **divine gold** `#f2d27a` (god powers).
- Corners and borders hint at **carving and stitching**: notched corners, a 1 px stitched inner line. Keep ornament to the corners; content areas stay calm.
- Contrast: body text against panel must meet **WCAG AA (4.5:1)**. Bone on panel does.

### 11.2 Components (9-slice unless noted; states: normal, hover, pressed, disabled, focused, and toggled for toggles)
`panel_main`, `panel_inner`, `panel_inset`, `tooltip`, `window_frame` + `title_bar` + `close_button`, `button_primary`,
`button_secondary`, `button_icon` (24x24), `toggle`, `tab` (active/inactive), `scrollbar_track` + `scrollbar_grabber`, `slider_track` +
`slider_knob`, `checkbox`, `radio`, `text_field`, `dropdown` + `dropdown_item`, `progress_frame` + `progress_fill_mask` (fill colour set in engine),
`divider` (plain + ornamental), `corner_ornament` (4), `badge` (count), `notification_toast`, `minimap_frame`, `portrait_frame` (48+border),
`node_frame` (family tree / relationship graph), `chart_frame`, `speed_indicator`.

### 11.3 Icons, 16x16 (and a 32x32 version for god powers)
- **God powers:** `rain`, `drought`, `lightning`, `earthquake`, `wildfire`, `flood`, `blizzard`, `meteor`, `plague`, `bless_harvest`, `fertility`, `heal`, `curse`, `dream_whisper`, `spawn_animal`, `spawn_plant`, `raise_land`, `lower_land`, `possess`, `follow`.
- **Time:** `pause`, `play`, `fast_2`, `fast_4`, `fast_max`, `step`.
- **Map overlays:** `elevation`, `temperature`, `rainfall`, `biome`, `population`, `culture`, `religion`, `technology`, `relationships`, `fear`, `resources`.
- **Event categories** (chronicle; shared with the simulation): `danger`, `social`, `discovery`, `loss`, `spiritual`, `conflict`, `trade`, `birth`.
- **Needs:** `hunger`, `thirst`, `energy`, `warmth`, `safety`, `social`.
- **Traits** (radar-chart labels): `novelty_seeking`, `harm_avoidance`, `reward_dependence`, `persistence`, `self_directedness`, `cooperativeness`, `self_transcendence`.
- **Emotions:** the 11 from section 10.1, drawn at 16x16.
- **Relationships:** `partner`, `parent`, `child`, `sibling`, `friend`, `rival`, `enemy`, `mentor`, `follower`.
- **Roles** (emergent professions): `gatherer`, `hunter`, `fisher`, `farmer`, `herder`, `builder`, `crafter`, `healer`, `shaman`, `leader`, `trader`, `warrior`, `scout`, `elder`, `child`.
- **General:** `settings`, `save`, `load`, `camera`, `chronicle` (book), `family_tree`, `culture`, `religion`, `tech`, `map`, `close`, `info`, `warning`, `search`, `filter`, `pin`, `watch` (eye), `random_seed` (dice), `undo`, `screenshot`.
- **Cursors** (16x16 at 1x, hotspot in JSON): `pointer`, `hand`, `inspect`, `power_target`, `follow`, `forbidden`, `drag`.

### 11.4 Map view (planet/strategic)
The planet map is rendered by shaders from world data (no painted map). Assets needed:

- **Settlement markers:** `camp`, `hamlet`, `village`, `town` (12x12).
- **Event markers:** `ruin`, `shrine`, `battle`, `discovery`, `disaster`, `migration_arrow` (9-slice-able stroke), `culture_banner` (glyph-tinted).
- A `map_frame` 9-slice in the style of carved wood and stitched parchment.

### 11.5 Fonts
Fonts are **not drawn by the generator**. Use open-licensed (SIL OFL) fonts from Google Fonts, and include the licence file:

- **UI / labels:** *Pixelify Sans*.
- **Long reading text** (chronicle, agent thoughts, legends): *Alegreya* (serif, storybook feel) or *Alegreya Sans*.

The logo stays the provided SVG (`evolysia-logo.svg`). Do not redraw it.

---

## 12. Quality bar and validation
Every asset must pass `tools/assetgen/validate.py`:

1. Every pixel colour is in the master palette (reference PNGs). Index PNGs use only defined slots.
2. Alpha is 0 or 255, except the families listed in section 2.4.
3. Sizes, frame counts and directions match the manifest. The pivot and hand anchors are inside the frame.
4. Corner and connection sets are complete (all 16 masks), and edges tile seamlessly (an automated seam test with random tilings).
5. Index maps and reference maps have identical alpha.
6. Glyphs pass the banned-symbol check (section 9).
7. Readability check: the contact sheet includes a **1x view on a mid-grass background and on a dark background**. Sprites must be identifiable as silhouettes.
8. No two variants of the same asset are pixel-identical.

Every family also ships a **contact sheet** in `assets/_preview/`: 4x scale, labelled, with animations laid out frame by frame, and recolourable assets shown across several LUT rows (e.g. all seasons for one tree, all skin tones for one body).
