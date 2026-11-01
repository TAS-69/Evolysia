# Required model connection

Current source (`0.2.0-dev`) requires a working generative model for ordinary gameplay. The published `0.1.0-alpha.1` predates this requirement. Opening the game does not download anything; **Start model** explicitly runs the local bootstrap.

## Set up

Install Python 3.11+. From the repository root:

```powershell
python tools/llm_lab/lab.py configure
python tools/llm_lab/lab.py bootstrap --helper off --embed off
python tools/llm_lab/lab.py status
```

Bootstrap installs a missing llama.cpp runtime, then starts the configured main model. First use can download several gigabytes of weights. Runtime/backend choices and machine-specific overrides live in the lab; profiles include GPU and CPU options. Model weights and runtime binaries retain their upstream licences and are excluded from game packages.

The default endpoint is `http://127.0.0.1:8081`. **World → Model connection** saves another HTTP(S) endpoint and model name. Supply the server root, without `/v1/chat/completions`. The current client expects an endpoint that accepts chat completions with strict JSON Schema and does not require an API key. It sends world observations, memories and player messages to that endpoint.

Begin waits for an actual structured completion. Connection status is visible in the top bar. If the model disconnects, times out or returns an invalid plan, the world waits at every speed. There is no timed fallback to instinct in normal gameplay. Reconnect or change the endpoint to continue; saving remains available while waiting.

## What the model controls

Every fifteen simulated days by default, each band requests a council. Up to six rotating people receive individual work, goals, thoughts, speech and reflections. The remaining population follows the generated labour shares. Illness, exhaustion and physical feasibility still constrain actions.

Councils choose available construction projects and experiments, sharing policy, settlement and migration, relationships and neighbouring diplomacy. They may elect an adult leader, split off a viable community, or propose a union requiring reciprocal consent. They can design a named building from a known template, arranging known fixtures around accessible corridors. Construction consumes materials and labour; proposed objects are not free inventory.

**Discovery and lore.** Councils see their band's lore, not the game's rules:
- traditions;
- what each kind of work has actually brought;
- what their buildings and products turned out to do;
- the techniques they know and the ideas these bring to mind;
- clues and dead ends.

Anything untried is marked untried. To find a new technique, a council proposes up to two experiments: an action (heat, strike, shape, weave, tie, plant, tame, mark, sing…) on one to three things from `can_work_with`, with the idea behind it. People assigned to experiment work on it over the following days. The simulation judges the attempt against hidden ways into each technique and reports back a discovery, a near miss (what seemed missing or in the way) or a dead end. Occasionally someone notices a clue during ordinary work. The model never chooses a technique by name.

Cargo orders name actual workers, source and destination containers, goods, quantities and optional vehicles. Stocks, reservations, capacity, ownership and routes must validate before a plan is accepted. Invalid plans are rejected before any of their effects are applied; a retry receives corrective feedback.

Personal reflections persist in saves and return in later prompts. Interventions invalidate affected councils and stale replies. Messages enter council knowledge only after being encountered, with readable text gated by literacy or a vision. Dreams supply their actual words and urge. These observations can influence later plans; they do not guarantee obedience.

Pivotal social and first-contact decisions also use the model. Physics, ecology, births, resource conservation and legal action constraints remain simulation rules. The model composes choices within those rules; generated text cannot create arbitrary technologies, powers or goods.

## Throughput and checks

Two consequential requests run concurrently. Planning latency can dominate game speed; population-scale generative play and centuries of balanced model behaviour are not established. A capable structured-output model is essential. Smaller models can need several retries for geometry or logistics.

```powershell
python tools/llm_lab/lab.py eval --mock --quick
python tools/llm_lab/lab.py eval --quick
dotnet run -c Release --project sim/src/Evolysia.Sim.Runner -- --width 160 --height 80 --tribes 1 --bands 1 --days 2 --out build/runs/model-check
```

Mock evaluation checks the evaluation plumbing, not gameplay intelligence. The runner exports accepted plans and rejected-reply diagnostics into its ignored output directory. Keep personal messages and machine-specific configuration out of public reports.
