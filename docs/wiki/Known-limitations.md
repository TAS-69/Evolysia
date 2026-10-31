# Known limitations

This is an initial alpha, not a finished civilisation sandbox.

- **No save/load.** Closing the game or beginning a new world discards the session. The headless runner's CSV reports are analysis exports, not resumable saves.
- Windows x64 is the packaged target. Other platforms, GPUs and graphics backends need testing.
- There is no multiplayer, direct unit control, complete campaign, in-game character editor or comprehensive settings/accessibility screen.
- The simulation catalogue contains advanced technology, but full modern/future societies, magic systems, space travel gameplay and every specialist institution are incomplete.
- Inventories track quantities by good; rendered container contents are representative. Persistent stacks, independent container storage and comprehensive cargo logistics remain work in progress.
- Some structure kinds lack dedicated runtime visuals. Much of the detailed art kit is an integration resource rather than currently reachable gameplay. Portraits, final UI assets, procedural culture glyphs, roads and cliff rendering are unfinished.
- Simulation speeds are targets. Growing populations, terrain baking and large structure lists can lower frame rate and time progression. Population targets and memory projections are not supported hardware guarantees.
- Optional narration depends on a separately installed runtime, compatible model and available memory. Replies can be slow or inconsistent. Bounded pivotal choices fall back to instinct when necessary.
- Automated screenshot shutdown currently reports resource cleanup warnings. These are recorded separately from simulation test and asset validation results.

When [reporting a bug](https://github.com/TAS-69/Evolysia/issues), include the version, OS, GPU, seed, planet, approximate year, population, speed and steps to reproduce. Include whether Minds or local voices were active. Please keep private model paths and machine-specific configuration out of public reports.
