# Detailed civilisation art

Close views use artwork authored at 64 pixels per tile, with 32 and 16 pixel density levels. The logical world grid stays at 16 units per cell. Existing asset IDs, files and palette slots remain unchanged. This additive sample supplies art and integration contracts; it does not implement civilisation gameplay.

```
python tools/assetgen/build.py civilisation wardrobe landscape
python tools/assetgen/validate.py
```

The civilisation pack has 671 visual components across 2,013 sheets and 2,076 frame images: sixteen floor/ground surfaces, twenty-three wall materials with sixteen masks each, twelve roof surfaces, twenty-four container layers, 107 stored-item forms, sixty-six fixtures, one static person reference, sixty-six nature/animal references and eleven vehicles. Every sheet has reference/index PNGs and a sidecar. No palette colours were added. Separate wardrobe and landscape families provide the animated modular characters and detailed world materials.

## Zoom and coordinates

`assets/civilisation/render_contract.json` records the scale rules. Density is the pixel width of a logical 16-unit cell, not a change to world size. A 7x6 room occupies the same area at every level. Use `logical_frame` for visual extent and `placement_footprint` for occupied ground. Trees have a 16x32 visual extent over a 16x16 footprint.

Multiply logical coordinates by `density / 16`. Flooring `logical_pivot * density / 16` gives the exported pixel anchor. Preserve world positions and inventory identities when switching density. Address animation columns by frame width; trees are taller than they are wide.

The 64 pixel source has authored plank joints, weaving, stone facets, foliage clusters, machinery and individual grain marks. Lower levels use nearest-neighbour reduction with repeating surface borders reconciled afterward. Magnifying close artwork preserves crisp pixels but adds no further geometry. Pixel-art camera views should snap to integer magnification of the selected density. Planet views need world-data aggregation and markers rather than rendering every item.

## Open buildings and specialist spaces

`blueprints.json` provides thirty-one furnished samples spanning camps and homes, food storage, artisan workshops, industry, research, medicine, councils, royal halls, ritual and ceremonial spaces, magical workshops, defences, markets, rural work and transport. Floors, walls, doors, roofs, equipment, containers and people remain separate. Roofs can be hidden per room; close previews cut away the southern wall. Doors have open and closed visuals. Workers in room previews use the appropriate editable wardrobe preset when that family is built.

Equipment and capabilities identify specialist spaces. A loom creates a weaving workspace; an anvil and forge create a metalworking workspace. Rooms can share equipment or change use without replacing a baked building image. Layout checks reserve an entrance, exclude occupied equipment cells from walking space, and verify connected floor and equipment access. These review checks do not implement navigation.

Roof samples currently cover rectangles. Detailed ridge/gable/conical pieces, irregular boundaries, multiple rooms, construction, damage, ruins and burning remain pending. Existing low-resolution early structures keep their five required conditions.

`regional_styles.json` adds nine resource/climate preferences and 243 material/layout variants of enclosed rooms. Construction input categories cover wood, wattle, reeds, hide, earth, adobe, several stones, snow/ice, fired materials, metals and advanced composites. These are input categories and process requirements, not quantitative simulation recipes. Region preferences are suggestions based on resources and climate; imported materials and player choices can override them. The variant metadata also supports mirrored layouts without duplicating PNGs.

## Storage and transport

`storage_profiles.json` defines baskets, chests, backpacks, handcarts, grain bins, crates, pallets and cargo pods. Draw the container back, occupied slots from back to front, an optional closed lid/cover, then the front rim or latch. Slots may contain mixed resource types. Closed covers hide contents without removing them. Cart/pallet covers are removable boards. Equipped backpacks draw behind the body at the shared origin and pivot.

Visible slots are an art budget, not kilograms, volume or stack capacity. The simulation must own persistent item/stack IDs, quantities, ownership, capacity, compatibility, reservations and transfers. Empty slots draw nothing; overflow selects representative stacks without discarding inventory. Localisable quantity labels belong in the UI.

Transport moves a container transform and its attached contents. `inventory_example.json` follows six persistent grain-stack IDs, totalling sixty grain, through a granary, cart, backpack and chest. The example drives a review sheet and checks identity/quantity conservation. It does not execute transfers in the game. Runtime pickup, loading, equipping, unloading, theft, spoilage, destruction and save/load must update authoritative item locations exactly once, then let rendering read that state.

`vehicle_profiles.json` declares container attachments separately from the vehicle artwork. Cargo ship and vehicle base PNGs contain no baked payload. Attach each persistent container at its declared anchor, then redraw base pixels inside any foreground hull polygon to occlude the load correctly. The transport cargo sheet shows empty and loaded examples using the same independent container sprites and inventory renderer.

Hearths, kilns, forges and ovens have unlit visuals. Seven active fixtures use four frames at 6 fps. Fire slots are marked emissive. Smoke, ground shadows, cast light, heat and fuel consumption remain engine responsibilities. Eleven static transport references cover rafts, canoes, rowing boats, sailing/cargo ships, wagons, sleds, bicycles, trucks, rovers and hover carriers; propulsion and loading animations remain pending.

## Individually coloured outfits

The wardrobe family contains 185 part selections in twelve layers and 32 editable outfit presets. It exports all twenty original action animations, their original frame counts/FPS/action IDs, all supported age/build/direction profiles, and densities 16, 32 and 64. The clothing group contains separate legwear, footwear, tops, outerwear and armour. Hair, exposed-skin paint, headwear, accessories and held tools/weapons remain separate. Presets include specialist work, rural, travel, court, royal, ceremonial, defence, industrial, environmental and magical clothing. A preset is a starting selection, not a character class or equipment lock.

Each piece has its own `main` and `trim` selections, independently choosing any of the thirteen cloth LUT rows. Apply those rows only to the piece's `cloth_a` and `cloth_b` groups. Skin, hair and culture selections remain independent. Natural metal pixels retain their material ramps; painted/cloth parts and straps expose dye channels. The sixteen-colour limit is checked per exported component; a player-selected layered outfit can combine additional colours from independently dyed pieces.

Read `assets/wardrobe/customisation.json` for part inventories, presets, hairstyle pairings, layer order and dye rules. Shaved and omitted equipment use null selections. Body paint is clipped to the visible skin after clothing, outerwear and armour. The new family preserves the original major people layer order, with clothing sublayers inside that group. Its pose anchors come from the existing people rig; side mirroring must transform pixels, pivots and hand anchors together. Held items rotate around the grip when needed to fit an action frame.

Wardrobe atlases pack whole animation strips into up to eight columns. The manifest keeps a compact summary, including `strip_count`; load the sidecar for profiles, variants, row lookups, pivots and hand anchors. Resolve `row_lookup[variant][profile]` separately for each density. For physical strip `r`, frame `f` starts at `x = (r % strip_columns) * frame_width * frames + f * frame_width`, `y = (r // strip_columns) * frame_height`. Identical physical strips share storage, including details that collapse at distant densities or parts hidden in a pose. Do not reuse another density's row lookup. New wardrobe styles are not yet added to the existing portrait atlases.

## Detailed world materials

The landscape family exports 56 materials across 384 sheets: four plain variants and a complete sixteen-mask corner set per material, both furrow orientations for tilled soil, four frames for water/lava, and eight types of exposed rock/ice face with corner sets and three south-wall variants. All three densities are exported. The original terrain files and material ordering remain unchanged.

New materials include loam, peat, silt, clays, black soil, sands, dunes, gravels, bogs, marshes, mudflats, forest litter, moss/lichen, frozen ground and several rock types. `priority_anchor` retains the original material family ordering; `priority_offset` breaks ties within that family. Use plain variants in homogeneous mask-15 areas to reduce repetition. Lower-density corner geometry is recalculated on the same logical field so all compatible edges still agree exactly after detail reduction. These surface and cliff assets do not replace the existing river/road/weather overlay family.

## Civilisation coverage

`catalogue.json` groups requirements from foraging through space technology, plus specialist, regional, natural-resource and landscape coverage. These organise production work without imposing era locks on emergent technology. Materials, capabilities and resources determine what people can build.

Coverage includes food, water, shelter, care, sanitation, workshops, storage, power, transport, education, communication, culture, recycling, automation, advanced medicine and off-world life support. Entries explicitly distinguish sample art/layouts, art supplied by the landscape family, and planned work. A catalogue entry does not indicate a working building or completed animation set.

The civilisation person's standalone sprite remains a static scale reference; the wardrobe supplies the animated modular replacement. Detailed fauna references are static, and full motion/state sets, seasonal/growth flora states, detailed overlay/effect sets, portrait wardrobe synchronisation, irregular roofs and building conditions remain pending. Most advanced catalogue requirements remain planned. The existing families continue supplying their complete original-resolution sets.

## Review and checks

Start with `civilisation_zoom.png`, `civilisation_overview.png`, `civilisation_storage.png` and `civilisation_inventory_handoff.png` in `assets/_preview/`. Per-room sheets include a 4x cutaway, smaller densities and roof-covered views. Object galleries provide native grass/dark/silhouette strips. Other sheets show every active frame, wall mask, surface, outdoor sample and skin/hair/cloth/culture/nature LUT selections.

Review `wardrobe_outfits_1.png`, `wardrobe_outfits_2.png`, `wardrobe_dyes.png` and the wardrobe part/action sheets for character customisation. `civilisation_regions.png`, `civilisation_resources.png` and the transport sheets cover regional construction and goods. `landscape_random_tilings.png` and the per-material sheets show terrain, cliffs, variants and every animated frame.

Validation covers the inventory, palette/index agreement, hard alpha, colour budgets, density transforms, unique animation frames, compatible wall edges, seeded random tilings, surface seams, container fill changes, covered-resource occlusion, mixed inventories, persistent-item conservation, room access and the 8 MB preview limit. Game camera, rendering, navigation, jobs, production chains, inventories and logistics are outside this asset-only change.
