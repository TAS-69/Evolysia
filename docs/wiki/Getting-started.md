# Getting started

Download the Windows x64 archive from [Releases](https://github.com/TAS-69/Evolysia/releases), extract it completely and run `Evolysia.exe`. Keep `assets`, the managed data folder and `Evolysia.pck` alongside the executable. Exported packages include .NET. Version 0.2.0-alpha.1 requires a running model: follow [Required model connection](Local-models). Local bootstrap needs Python 3.11+; a separately hosted compatible endpoint is also supported.

Use a Vulkan-capable GPU. This alpha has been checked on Windows; Linux, macOS, integrated GPUs and alternative rendering backends have not been release-tested.

The opening screen lets you choose a numeric seed and a planet:

| Planet | Character |
|---|---|
| Terra | Balanced continents and varied climates |
| Ignis | Hot, dry and volcanic |
| Cryos | Cold, ice-bound and short summers |
| Thalassia | Mostly ocean, with small islands |

Choose one to six peoples. Each starts with its own culture and language, split into two bands. **Scatter at random** chooses suitable land. **Place on the map** lets you place their starting positions; click a marker again to remove it. Placement is adjusted to liveable land by the simulation.

After the model status shows ready, select **Begin**, zoom towards a band and left-click to inspect it. Space pauses the world, which is useful when reading a particularly complicated family situation.

**World → New world** replaces the current world after Begin. Save first with F5 or create a named slot in World → Saved worlds. F9 opens the save browser; loading starts paused. Three autosaves rotate every thirty simulated days by default; Settings can change or disable the interval. Saves include possessions, memories, work orders and cargo in transit. The Back button returns without replacing the world. Development saves may not survive future schema changes.

The same seed generates the same starting world. Later history also depends on player actions and accepted model plans. Generated plans and text are not guaranteed to repeat.
