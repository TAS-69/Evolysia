# Evolysia LLM lab (step 1)

Decide which **local** models are good enough to voice Evolysia's agents, on the hardware you have,
before any game code depends on them. Pure Python 3.11+, standard library only.

## The setup it tests

| role | job | default home |
|---|---|---|
| **main** | dialogue, decisions, reflections, pivot moments | GPU |
| **helper** (sub-agent) | event tagging, place/person names, memory summaries | **CPU** |
| **embed** | memory retrieval | **CPU** |

Helpers and embeddings get short prompts and give short answers, so a CPU handles them fine, and keeping
them off the GPU leaves VRAM for the main mind. Every role can be moved to `gpu`/`cpu`, resized, or
turned `off`. With the helper off, its tasks go to the main model.

| profile | for | main | helper | ~VRAM | ~RAM |
|---|---|---|---|---|---|
| `lite` (default) | 8-12 GB GPU | Qwen3.5-4B | Qwen3.5-0.8B on CPU | ~4 GB | ~2 GB |
| `balanced` | 16 GB GPU | Qwen3.5-9B | Qwen3.5-2B on CPU | ~7 GB | ~3 GB |
| `full` | 24 GB GPU | Gemma 4 26B-A4B (MoE) | Qwen3.5-2B on CPU | ~19 GB | ~3 GB |
| `cpu` | no usable GPU | Qwen3.5-4B on CPU | off | 0 | ~5 GB |

The memory figures are rough estimates. On Windows, `lab.py status` reads real per-server VRAM/RAM from
the GPU performance counters. Measured `lite` on an RX 7900 XTX: main 3.4 GB VRAM; helper 0.8 GB RAM and
embed 1.0 GB RAM, with no VRAM.
If the machine has an integrated GPU as well, check `llama-server --list-devices` and set
`gpu_device = "Vulkan0"` (or whichever is the discrete card) in `local.toml` if needed.
Helper sizes: `tiny` (0.8B), `small` (2B), `medium` (4B). Edit `config/models.toml` to add models.

## Quick start

```bash
cd tools/llm_lab
python lab.py eval --mock --quick   # offline pipeline check, no downloads (scores = chance baseline)
python lab.py configure             # pick profile, helper size, CPU/GPU, threads -> local.toml
python lab.py plan --commands       # what will run + memory estimate
python lab.py install               # llama.cpp Vulkan build (~40 MB); --backend rocm|cpu, --tag bNNNNN
python lab.py serve                 # first run downloads the GGUFs into models/
python lab.py eval                  # full suite -> results/*.md + *.json
python lab.py status                # real VRAM/RAM per server
python lab.py stop
```

One-off overrides work on `plan`, `serve` and `eval`, for example:
`--profile balanced --helper tiny --helper-device cpu --helper-threads 8 --embed off --main-model gemma-4-e4b`.
Use the same overrides for `serve` and `eval`. To compare models, stop the servers, serve with a different
`--main-model`, run `eval` again, then `python lab.py compare`.

## What it measures

**Main mind: personality fidelity.** 12 personas (8 extreme archetypes plus 4 random ones) face 10
situations: fear, rejection and pivot moments, starvation ethics, omens, conformity, novelty, ambition.
Each option carries trait weights, so the scenario data says which option each personality *should* lean
towards.
- `fidelity_percentile` (where the chosen option ranks for that persona): 0.5 = random, 1.0 = always the best fit. A perfect oracle scores 0.999; aim for 0.72 or more.
- `differentiation`: whether personas choose differently. Random choices also score high here, so read it together with fidelity.
- `nice_bias_gap`: how much more often selfish personas pick antisocial options than kind ones. This catches the "every agent is a saint" failure of instruction-tuned models.
- `schema_valid_rate`: JSON is grammar-constrained by llama-server, so this should be about 1.0.
- `voice_distinctness`: whether different personas sound different (embedding distance between their spoken lines).

**Helper:** event classification, importance ratings, invented names that follow a culture's sound
palette, and summarising a conversation into a memory with the right trust direction.

**Embeddings:** whether the right memory comes back for questions an agent asks itself.

**Throughput:** total tokens/s and calls/min at 1, 2, 4 and N parallel requests. Calls/min is the
budget that sets how many agents can be "LLM-awake" at once.

## Notes

- Thinking/reasoning is switched off (`--reasoning-budget 0`, `enable_thinking: false`). Agents need fast, short answers.
- Option order is shuffled on every call, so position bias isn't counted as personality.
- `eval` runs 3 samples per persona/scenario (360 calls) by default. A single sample moved fidelity by about 0.08 between identical runs. Check the `+/-` 95% CI before calling one model better than another.
- Every helper prompt must list the allowed values. Grammar constraints force valid output, but they can't make the model choose well among options it was never shown.
- Per-persona temperature = 0.6 + 0.5 x novelty seeking. Use `--temp` to fix it.
- The CPU servers use `--device none`, so nothing runs on the GPU, prompt processing included.
- Scenario and task data lives in `data/` and is easy to extend; the scoring reads the trait weights automatically.
