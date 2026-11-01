<p align="center">
  <img src="evolysia-logo.svg" alt="Evolysia" width="580">
</p>

<p align="center"><strong>A living world. A watchful god. People with other ideas.</strong></p>

<p align="center">
  <a href="https://github.com/TAS-69/Evolysia/releases">Download</a> ·
  <a href="https://github.com/TAS-69/Evolysia/wiki">Wiki</a> ·
  <a href="https://github.com/TAS-69/Evolysia/issues">Report a bug</a>
</p>

Evolysia is a top-down god-game about autonomous people and the worlds they make. Start with scattered bands on a generated planet, watch them learn, settle, trade and quarrel, then decide how much divine intervention is a reasonable amount. The people may disagree.

**Current source: `0.2.0-dev`.** Model integration is required for gameplay. Councils generate plans that govern work, construction, experiments, migration and relationships; people remember their own reasons. Nobody is handed a manual: bands learn what their work, buildings and products do by trying them, and find new techniques only through experiments of their own devising. Manufacturing, power, staffed services, roads, terrain shaping, possession, physical cargo, specialist interiors and named saves are connected. This is still a development build with incomplete systems and changing save formats.

The published **`0.1.0-alpha.1`** is the earlier prototype. Its [release notes](docs/releases/v0.1.0-alpha.1.md) describe that download; the documentation below describes current source.

![The new-world screen](docs/images/new-world.png)

## Start playing

1. Build current source using the instructions below, or use [Releases](https://github.com/TAS-69/Evolysia/releases) for the earlier alpha.
2. For current source, start a compatible model with Python 3.11+: `python tools/llm_lab/lab.py bootstrap --helper off --embed off`. The first run installs the local runtime and downloads weights.
3. Launch the game. Choose a seed, planet and one to six peoples. Begin unlocks after the model completes a structured handshake.

Exported Windows packages include their .NET runtime. Current gameplay also needs a running model endpoint; local bootstrap requires Python, disk space and sufficient memory. **World → Model connection** changes the endpoint and model name. A lost connection holds time at every speed. The renderer requires a Vulkan-capable GPU; other platforms and graphics backends remain unverified.

## What's here

- Four planet types: temperate **Terra**, volcanic **Ignis**, frozen **Cryos** and island-covered **Thalassia**, on an 800 × 400 world that wraps east-west.
- Individual genomes, families, needs, emotions, learned fears and changing personalities. People inherit traits, form relationships, invent techniques and sometimes take rejection very personally.
- Migrating bands, emerging cultures, belief, leadership, conflict, farming, herding and dangerous sea journeys.
- Finite timber, stone and ore; 107 goods, 146 gear definitions, 40 processing recipes, 130 structure definitions and 106 techniques. Availability depends on knowledge, surroundings and materials.
- Zoom from a planet map to modular people and settlements, with 16, 32 and 64 pixel artwork densities where available. Clothing pieces have separate main and trim dyes; regional building materials and selected settlements' roof cutaways add detail.
- Twenty-one god powers, including rain, healing, disasters, dreams, rare written messages, plant growth, terrain shaping and possession. Inspect people and bands, follow links in their chronicle and see how interventions are remembered.
- Required generative councils choose labour, personal goals, experiments, construction, sharing, diplomacy and leadership. They can propose community divisions, mutually agreed unions and named building layouts with accessible fixtures. Invalid proposals change nothing.
- Physical stacks in backpacks and individual containers, with lineage, capacity, spoilage and loading reservations. Model-ordered cargo jobs move workers and vehicles along routes. Daily gathering and some trade still use aggregate deliveries.
- Ordered manufacturing stages actual inputs and labour; generators burn fuel, solar panels produce power and batteries retain it. Staffed clinics, teaching, research, ceremonies, governance and guards have practical effects.
- Roads require clearance, materials and labour, then improve freight speed. Terrain shaping updates drainage and habitats; possession lets you move and gather as one adult while their needs continue.
- Versioned saves retain random streams, memories, plans, possessions and cargo in transit. Named slots, quick save, rotating autosaves and a save browser keep worlds separate. Settings include interface scale and reduced motion.
- Follow a person, inspect their portrait, and choose build, facial features, garment styles and independent dyes. Requested clothing still needs to be made.

![A settlement up close](docs/images/settlement.png)

## Controls

| Action | Control |
|---|---|
| Pan | WASD, arrow keys, right-drag or middle-drag |
| Zoom towards the pointer | Mouse wheel |
| Inspect / use an armed power | Left-click |
| Pause / resume | Space |
| Select speed | 1–6: 1×, 2×, 3×, 5×, 10×, 25× |
| Cycle map overlay | Tab |
| Cancel a power / close selection | Esc |
| Collapse a panel | Click its heading |
| Quick save / saved worlds | F5 / F9 |
| Follow selection | F |
| Appearance, settings, saved worlds and model connection | World menu |
| While possessing an adult | WASD/arrows move; E forage; C chop; Q quarry; R rest; X release |

## Build from source

Use **Godot 4.7.2 .NET**, a .NET SDK capable of building `net8.0`, and **.NET SDK 10** for the runner and tests. The standard Godot download cannot run the C# client. Engine binaries are not stored in this repository.

```powershell
dotnet build game/Evolysia.Game.csproj
dotnet test sim/Evolysia.Sim.slnx -c Release
```

Import `game/project.godot` in the .NET editor and press F5. The client reads the sibling `assets` directory directly. See [Building and testing](docs/wiki/Building-and-testing.md) for export and headless instructions.

Asset generation requires Python 3.11+, Pillow and NumPy:

```powershell
python tools/assetgen/build.py all
python tools/assetgen/validate.py
```

The repository includes 3,614 validated manifest entries across eleven families. Contact sheets regenerate in `assets/_preview` and are excluded from downloads. [Asset pipeline documentation](tools/assetgen/README.md).

## Alpha limits

Multiplayer, possession, complete magic and space gameplay, final UI assets, procedural culture glyphs, roads and cliff rendering remain unfinished. Specialist layouts are physical rooms with paid fixtures; many political, ceremonial and advanced institutions still lack their own complete service loops. Transport supports explicit container jobs, rather than routing every economic exchange. Some close views enlarge base sprites, and portraits do not mirror every expanded wardrobe piece.

Higher speed settings are targets. Model latency can dominate progression, especially with many bands; large populations can also run slower. Tests of the rules alone do not establish long-run model quality or late-game balance. [Development status](docs/DEVELOPMENT.md) tracks completed integration and remaining work. Save compatibility between development builds is not guaranteed.

## Licence

Code and documentation text: [MIT](LICENSE), copyright © 2026 **TAS-69**. Original artwork, palettes, logo and branding: [all rights reserved](ART-LICENSE.md), with permission for playing official builds and sharing gameplay footage. Third-party dependencies retain [their own licences](THIRD_PARTY_NOTICES.md).
