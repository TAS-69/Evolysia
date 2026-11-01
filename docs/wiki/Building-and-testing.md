# Building and testing

## Source requirements

- Godot **4.7.2 .NET**, rather than the standard editor.
- A .NET SDK with `net8.0` build support for the client and libraries; **SDK 10** for the runner and test projects, which target `net10.0`.
- Python **3.11+**, Pillow and NumPy for artwork regeneration. Local model bootstrap uses the Python standard library. Gameplay requires a compatible running endpoint.

Clone [the repository](https://github.com/TAS-69/Evolysia), keep its layout intact, and run these commands from its root:

```powershell
dotnet build game/Evolysia.Game.csproj
dotnet test sim/Evolysia.Sim.slnx -c Release
```

Import `game/project.godot` into the .NET editor and press F5. The game reads `../assets` in the source layout and `assets` beside the executable in an exported distribution.

## Headless simulation

```powershell
dotnet run -c Release --project sim/src/Evolysia.Sim.Runner -- --seed 42 --width 160 --height 80 --tribes 1 --bands 1 --days 2 --save build/world.evo --out build/runs/seed42
```

The runner writes world/end maps, yearly statistics, agents, deaths, chronicle text and narratable moments. Use `--save PATH` and `--load PATH` for resumable worlds; reports alone are not saves. A model endpoint is required by default. `--rules-only` explicitly selects engine diagnostics without model integration. Additional options include `--planet`, `--tribes`, `--width`, `--height`, `--bands`, `--band-size`, `--profile`, `--trace`, `--narrate` and `--minds`. See the argument parser in `Program.cs` for values; unknown options return an error.

## Artwork

```powershell
python tools/assetgen/build.py all
python tools/assetgen/validate.py
```

You can build selected families instead of `all`. Every family writes reference PNGs, index maps where applicable, JSON metadata and manifest entries. Review regenerated contact sheets before committing changed artwork. Stable IDs and palette-slot indices are shared contracts.

## Vegetation colour regression

Build the client, then run the render check with the Godot .NET console executable:

```powershell
Godot_console.exe --path game res://verification/NaturePaletteCheck.tscn
```

This requires a working graphics device. It compares actual terrain-shader colours with foreground vegetation redraws across trees, shrubs, rocks, multiple densities and seasonal blends. All 48 cases must pass within one 8-bit colour value per channel. The verification scene is excluded from exported game resources.

## Branding

```powershell
python tools/release/branding.py --godot C:/path/to/Godot_console.exe
```

This rasterises the official SVG without redrawing it, creates a proportionally padded Windows icon and extracts licence notices from that engine. The brand image is separate from the sprite palette contract.

## Windows release

Install the matching **4.7.2 .NET export templates**, or extract their Windows debug/release x64 executable templates into `build/cache/templates` as referenced by `game/export_presets.cfg`. The template archive used for the first alpha has SHA-256 `92f8681e349ef1f90891b792da95e3b2b0bd1ed610b78018c58feb2d87e15a9d`.

```powershell
python tools/release/package.py --godot C:/path/to/Godot_console.exe
```

The packager exports the game, stages its complete artwork, copies the model-lab source and licence notices, verifies the package and creates a ZIP plus SHA-256 sums in `build/releases`. Local models, downloaded server binaries, configuration, previews, editor files and debug symbols are excluded. Build staging is ignored by Git.

Before publishing, run the simulation suite and asset validator, inspect actual menu and settlement screenshots, and launch the packaged executable from a directory that does not contain the source project's `game` folder. See the release's validation record for what was actually tested.
