# Art and customisation

Evolysia uses warm, earthy pixel art with selective outlines, light from the top-left and runtime palette lookup. The official SVG logo is used unchanged in the repository and rasterised proportionally for the menu and startup splash. The Godot startup image is replaced; engine licence notices remain included.

## Zoom and outfits

The logical art grid stays sixteen units per cell. The detailed civilisation, wardrobe and landscape families provide 16, 32 and 64 pixel densities. The renderer chooses 16 up to 24 screen pixels per cell, 32 up to 48, then 64. Other families fall back to their existing base artwork.

Wardrobe data defines 185 part choices in twelve layers, 32 preset outfits and twenty animation types. Individual pieces expose separate main and trim dye choices from thirteen cloth rows. Skin, hair and culture selections remain independent. The renderer uses inherited appearance and owned equipment; there is **no player-facing outfit selection editor yet**. The expanded wardrobe is not fully mirrored in the portrait atlases.

Buildings use modular floors, walls, roofs and props. At the closest density, selecting a band exposes roof cutaways for supported houses. The art kit contains specialist room examples, regional styles, storage components, vehicles and advanced concepts. The game does not instantiate every example or catalogue variation.

## Pipeline

The checked-in manifest contains 3,614 entries:

| Family | Entries |
|---|---:|
| Palette | 6 |
| Terrain | 48 |
| Overlays | 19 |
| Flora | 72 |
| Fauna | 105 |
| People | 140 |
| Portraits | 14 |
| Structures | 93 |
| Civilisation | 2,013 |
| Wardrobe | 720 |
| Landscape | 384 |

Generators use Python 3.11+, Pillow and NumPy, deterministic seeds and the master palette. Reference PNGs, index maps, metadata and manifest entries are validated together. Contact sheets live in `assets/_preview`, include native readability strips and are excluded from game downloads.

The specification is [ART_DIRECTION.md](https://github.com/TAS-69/Evolysia/blob/main/docs/art/ART_DIRECTION.md). Recorded decisions are in [QUESTIONS.md](https://github.com/TAS-69/Evolysia/blob/main/docs/art/QUESTIONS.md). Portrait integration, the final interface, procedural culture glyphs and several visual families remain unfinished. Palette validation does not claim that a missing family already exists.
