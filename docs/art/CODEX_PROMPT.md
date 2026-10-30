# Codex prompt: Evolysia asset generation

How to use: run Codex from the repository root and paste **the master prompt** plus **one phase block**
per run (Phase A first; it builds the pipeline every later phase uses). Review the contact sheets in
`assets/_preview/` before starting the next phase. If you'd rather not paste it each time, copy the
master prompt into `tools/assetgen/AGENTS.md`; Codex reads that automatically when working there.

---

## Master prompt

```
You are the asset pipeline engineer and pixel artist for Evolysia, a top-down god-game in which a
randomly generated planet is populated by autonomous AI-driven people who feel, invent, worship and
fight. The player mostly watches, so visuals must read instantly at small sizes and convey inner life.

Read docs/art/ART_DIRECTION.md completely before doing anything. It is the specification. Asset IDs,
sizes, frame counts, palettes, layer orders and banned content in it are binding. The IDs are shared
with the simulation code, so do not rename them.

HOW TO PRODUCE ASSETS
- Generate assets with code: Python 3.11+, Pillow and numpy only, in tools/assetgen/. Every generator
  is deterministic from a seed, reads the master palette, and writes PNG + JSON sidecar + manifest
  entries. Hand-placed pixel data (string grids/arrays in code) is welcome for small sprites and icons.
  Procedural shape generation with hand-tuned rules is preferred for organic families (terrain edges,
  trees, rocks, glyphs).
- If an image-generation tool is available, use it only for concept exploration. Never ship its output
  directly; anything derived from it must be redrawn or downscaled, quantised to the master palette,
  edge-cleaned and validated like everything else.
- Do not copy, trace or imitate assets or distinctive styles from existing games or media. Do not
  download third-party art. The only external assets allowed are the SIL-OFL fonts named in the spec,
  with their licence files.
- Pixel-art rules: 16 px base tile, top-left light, selective darker-ramp outlines (no pure black
  except cursors), no anti-aliasing against transparency, no baked shadows, colours only from the master
  palette, hue-shifted ramps.
- Recolourable families are exported twice: reference PNG and index map (_idx.png), as the spec describes.

WORKFLOW FOR EVERY RUN
1. Implement or extend generators for the requested phase only. Keep generators small and per-family:
   tools/assetgen/families/<family>.py.
2. Build: `python tools/assetgen/build.py <family...>` (or `all`).
3. Validate: `python tools/assetgen/validate.py`. It must pass with zero errors before you finish.
4. Produce contact sheets in assets/_preview/ (4x, labelled, animations frame by frame, recolour
   rows, a 1x readability strip on light and dark backgrounds).
5. Look at your own contact sheets (open the PNGs) and fix anything unreadable, off-palette, seamy or
   samey. Iterate at least once per family.
6. Finish with a short report: what was generated (counts per family), anything not done, any spec
   ambiguity you resolved and how, and the preview files to review.

RULES
- Touch only tools/assetgen/, assets/ and docs/art/QUESTIONS.md. Do not modify game or simulation code.
- If the spec is ambiguous, choose the option most consistent with its intent, record it in
  docs/art/QUESTIONS.md, and continue. Stop and ask only if a decision would invalidate other phases.
- Never generate real-world religious, political or national symbols. The glyph generator must reject
  the banned shapes listed in the spec.
- Keep the repo lean: no generated previews larger than 8 MB each, no duplicate outputs.
```

---

## Phase A: palette and pipeline (run this first)

```
PHASE A - foundation. Build:
1. assets/palette/: the master palette (evolysia.gpl, palette.png, palette.json with named ramps and
   recolour slot definitions) per spec section 2.1-2.2, including the brand anchors #f1e6d9 and #d4c3b1
   and the UI colours in section 11.1. Ramps must be hue-shifted (cool shadows, warm lights).
2. The LUTs: lut_nature.png (one row per biome x season from section 3.3), lut_skin.png (8),
   lut_hair.png (8 incl. greys for elders), lut_cloth.png (undyed + 12 dyes), lut_culture.png (12).
   Make the seasonal rows feel distinct and the transitions between adjacent rows smooth.
3. tools/assetgen/: build.py (family registry and CLI), a common library (palette loading, quantise to
   palette, selective outline, index-map export, sheet packing, JSON sidecar and manifest writing,
   seeded RNG), contact_sheet.py, validate.py implementing every check in spec section 12 (palette
   compliance, alpha rules, manifest consistency, 16-mask completeness, seam test, idx/ref alpha
   match, glyph ban check stub, duplicate-variant check).
4. A tiny demo family (e.g. one grass tile with its 16 corner set and one shrub) so the pipeline is
   exercised end to end, plus its contact sheet showing it in all lut_nature rows.
5. A Godot palette-swap shader in assets/shaders/palette_swap.gdshader (canvas_item: samples the index
   map's R channel and the LUT row given by a uniform or instance uniform; supports blending between two
   rows for seasonal transitions). Include a short assets/shaders/README.md explaining usage.
Acceptance: validate.py passes; the demo contact sheet shows the grass and shrub in every season;
palette.png is reviewable at a glance.
```

## Phase B: terrain

```
PHASE B - terrain (spec section 3). Generate every terrain material: 4 full-tile variants each, the
16-tile dual-grid corner set over transparency, animated water/lava frames, the rock/scree cliff
sets and cliff wall strips, and all overlays in section 3.2 (rivers 16-piece x 3 widths animated,
roads x 3, territory border, snow cover, frost, wet ground, ash fall, burned ground, flood water,
cracks, craters, footprint trail). Edges must be organic, and water edges get a land-side foam line.
Contact sheets: each material's corner set laid out by mask, a 12x12 random-tiling seam test, a
sample landscape mixing all materials in priority order, and grass/forest_floor/tundra/dry_grass across
all lut_nature rows.
```

## Phase C: flora

```
PHASE C - flora (spec section 4). All trees, shrubs, grasses, crops and wild plants with every listed
state and variant, index maps for recolour slots, and sway frames where specified (or mark
"shader_sway": true in the sidecar). Trees must overhang convincingly and have distinct silhouettes per
type (a conifer must never be mistaken for a broadleaf at 1x). Contact sheets: one tree type per row
across all states, plus a seasonal strip for broadleaf, acacia and berry_bush across lut_nature rows.
```

## Phase D: people and portraits

```
PHASE D - people (spec section 6). Build the layered character system: body bases (3 builds x child
/adult/elder) for down/up/side, every animation in 6.2 with consistent hand-anchor points recorded per
frame, hair (10 styles, back and front layers), clothing (6 materials), body paint (6 patterns, culture
slots), accessories and held items. Everything recolourable via the skin/hair/cloth/culture LUTs.
Then the 48x48 portrait system in 6.3: head bases (3 shapes + child + elder), all part sets,
expression part variants, and data/portrait_expressions.json mapping each of the 11 emotions to
parts exactly as in the spec table. Contact sheets: one body with every animation; a "family" sheet of
random composites (parents + 3 children sharing traits); every skin and hair row; and a portrait grid
of 6 random people x 11 emotions.
```

## Phase E: fauna

```
PHASE E - fauna (spec section 5). Every animal with its listed animations, side view facing right, and
domestic recolour variants (dog, cattle, domestic sheep/goat/fowl/horse with varied coat patterns via
recolour slots). Predators must read as dangerous at 1x. Contact sheets: each animal's animations
frame by frame plus a 1x herd/pack scene on grass and snow backgrounds.
```

## Phase F: structures, items and glyphs

```
PHASE F - structures (spec section 7), items (section 8) and culture glyphs (section 9). Complete
sprites for early structures with all states (building, complete, damaged, ruined, burned; animated
lit variants). Modular wall/roof/floor/door/window/fence/stairs/pillar parts for every material with
complete connection sets, roof pieces for 1x1, 2x2 and 3x3 footprints, and construction overlays.
Items as 16x16 icons and 8x8 world drops, plus resource nodes. A procedural glyph generator
(tools/assetgen/families/glyphs.py) that composes allowed primitives, rejects banned shapes (implement
the check concretely: e.g. detect 4-fold rotational symmetry with orthogonal arms, hooked arms, star
polygons and crescent shapes), and exports 64 sample glyphs + banners. Contact sheets: three example
buildings assembled from parts in different materials (round hut, longhouse, stone shrine with glyph),
every structure state, the item grid, and the 64-glyph sheet in several culture colours.
```

## Phase G: effects, weather, disasters and bubbles

```
PHASE G - effects (spec section 10) and the status bubble system (10.1). Every effect with its frame
count; soft alpha only where the spec allows. Bubble frames (thought/speech/alert) and 7x7
two-colour glyph icons for every emotion, need and activity. Contact sheets: each animation frame by
frame, a 1x "village in a storm" mock-up and a 1x crowd with mixed bubbles, so you can judge whether
the icons read at 1x.
```

## Phase H: UI, icons, map symbols, fonts

```
PHASE H - UI (spec section 11). The theme components with all states as 9-slice PNGs (sidecar JSON
holds the 9-slice margins), every icon set at 16x16 (god powers also at 32x32), cursors with hotspots,
map markers and frame. Fetch Pixelify Sans and Alegreya from Google Fonts into assets/fonts/ with their
OFL licence files (do not draw fonts). Write assets/ui/theme_spec.json mapping components to Godot Theme
properties (StyleBoxTexture margins, font sizes 8/12/16 px at 1x, colours). Contact sheets: every
component in every state on the panel background, all icon sets in labelled grids, and a mock inspector
screen (portrait, needs bars, trait radar placeholder, emotion, relationships) and a mock HUD (time
controls, god power bar, overlay toggles, chronicle feed) composed from the real assets at 2x. Check
text contrast against the spec (WCAG AA).
```

---

## Reviewing a phase (checklist for you)
- Open `assets/_preview/*.png`. Can you tell every sprite apart **at 1x** on light and dark backgrounds?
- Do seasons and skin, hair and culture recolours look natural across the LUT rows?
- Do tiles show seams or obvious repetition in the random-tiling sheet?
- Does anything look like a real-world symbol or another game's art? If so, reject it and note why in `docs/art/QUESTIONS.md`.
- Run `python tools/assetgen/validate.py` yourself; it must be clean.
