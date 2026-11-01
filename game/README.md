# Evolysia game client

Godot 4.7.2 **.NET** client for the sibling simulation and artwork. Current source is `0.2.0-dev`; its model integration is mandatory. The earlier published alpha has different capabilities.

## Run

```powershell
dotnet build game/Evolysia.Game.csproj
python tools/llm_lab/lab.py bootstrap --helper off --embed off
./Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64.exe --path game
```

The engine directory is an example local installation, excluded from Git. Import `game/project.godot` in a Godot .NET editor to run there. The standard editor cannot execute C#.

The new-world screen waits for a real structured model handshake. Choose a seed, planet and one to six peoples; scatter them or place them on the preview, then Begin. **Start model** launches local bootstrap through Python. **World → Model connection** saves an alternative compatible endpoint and model name. A missing connection or invalid plan holds time at every speed.

## Play

Pan with WASD/arrows or right/middle drag, zoom towards the pointer with the wheel, and click to inspect. Space pauses; 1–6 choose speed; Tab cycles overlays; Esc disarms a power or clears selection. The World menu opens setup, saves, model settings and the selected person's outfit editor. F follows the selected person/band; manual panning releases it.

F5 saves one local slot at `user://world.evo`; F9 loads it paused. Saves retain stock, designs, reflections and cargo in transit. Keep backups of important worlds: development schema changes can invalidate older saves. There is no autosave/slot browser yet.

The outfit editor independently chooses garment styles and main/trim dyes, skin/hair tones and hairstyle. New styles request actual manufactured clothing rather than granting free gear. The portrait combines age, emotion, inherited features and scars; expanded wardrobe coverage is incomplete.

The renderer reads the simulation's world resources, structures, fixtures, possessions and container contents. Detailed families provide 16, 32 and 64 pixel densities; some others enlarge base art. Selecting a band at the closest density exposes supported interiors. Thirty-one specialist layouts and generated plans build actual paid fixtures; institution-specific services are not all complete.

## Review options

Arguments after `--` skip the setup screen unless `--setup` is supplied:

| Option | Effect |
|---|---|
| `--seed N`, `--planet NAME`, `--tribes N` | Initial world |
| `--setup` | Show new-world screen |
| `--load --save-file PATH` | Restore a review save |
| `--select agent` or `--select band` | Select a demonstration subject |
| `--zoom N`, `--focus X,Y` | Review camera |
| `--outfit` | Open the selected person's editor |
| `--speed N` | Pause at 0, speed settings 1–6 |
| `--screenshot PATH` | Capture and quit after loading |
| `--wait-minds N` | Wait for accepted model plans/decisions before capture |
| `--save-on-shot` | Save alongside the review capture |
| `--fold inspector,chronicle,voices` | Collapse panels |

`--days N` is limited by mandatory planning and cannot bypass it for offline fast-forwarding. Screenshot waiting has a four-minute ceiling. Runtime warnings during automated shutdown remain a known issue.

See [Development status](../docs/DEVELOPMENT.md), [Required model connection](../docs/wiki/Local-models.md) and [Building/testing](../docs/wiki/Building-and-testing.md).
