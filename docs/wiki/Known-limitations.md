# Known limitations

Current source is `0.2.0-dev`; the released `0.1.0-alpha.1` has different capabilities. Development is still very much in the "interesting things happen" stage.

- A compatible running model is mandatory. Slow or invalid replies hold progression at every speed. Two requests run concurrently; large societies and long campaigns need further performance and behaviour testing.
- F5 writes a quick save; F9 opens named and automatic slots. Autosaves rotate three files by simulated days. Compatible optional fields migrate older development worlds, but required schema changes can still make a save incompatible.
- Windows x64 is the checked client target. Other platforms, GPUs and rendering backends need testing. There is no multiplayer or complete campaign. Settings cover scale, reduced motion and common display options; keyboard/controller accessibility remains incomplete.
- Manufacturing orders, fuel/solar power, batteries, refrigeration and six staffed service kinds are implemented. Complete future economies, magic, space travel, laws, taxation, markets and deeper institutions remain unfinished. Solar output uses a climate/temperature approximation.
- Containers have persistent stacks and reservations. Explicit cargo jobs move physical stock, workers and vehicles. Routine harvest deposits, crafting inputs and some trade remain aggregate exchanges. Road surfaces improve freight speed and routes recheck obstacles. Traffic arbitration, full vehicle footprints while moving, comprehensive theft and end-to-end sea freight need more work.
- The outfit editor chooses available garment definitions and independent dyes, skin tones and hairstyles. Clothing still needs knowledge, inputs and manufacture. Expanded wardrobe pieces are not all mirrored in the portrait atlases.
- Roads, cliffs and the specified fonts are connected. Final interface icons/effects, procedural culture glyphs and some dedicated structure visuals remain unfinished. Some families enlarge base sprites at close zoom.
- Scripted physiological and ecological rules remain authoritative. Generative councils supply goals and decisions, rather than replacing conservation, illness, reproduction or all social mechanics.
- Possession supports one adult and basic local movement, rest, foraging, chopping and quarrying. It still advances in simulation cells and days, rather than providing a complete direct-control action game.
- Reviewed settings, save and appearance screenshots now close cleanly. Long interactive sessions and other graphics drivers still need testing.

[Development status](https://github.com/TAS-69/Evolysia/blob/main/docs/DEVELOPMENT.md) tracks implementation and remaining work. For bugs, include version, OS, GPU, endpoint/model, seed, planet, year, population, speed and reproduction steps. Include a save when useful, after checking it for private player messages.
