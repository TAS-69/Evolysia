# Local model lab

Local runtime bootstrap, required generative planning and evaluation tools for Evolysia. Python 3.11+ is required for the lab, which uses the standard library. Current gameplay needs a compatible running model endpoint.

From the repository root:

```powershell
python tools/llm_lab/lab.py eval --mock --quick
python tools/llm_lab/lab.py configure
python tools/llm_lab/lab.py plan --commands
python tools/llm_lab/lab.py install --backend vulkan
python tools/llm_lab/lab.py bootstrap --helper off --embed off
```

`configure` writes ignored machine-specific choices to `local.toml`. `plan` shows the selected profile, role placement and approximate memory use. `install` downloads a llama.cpp build; Vulkan, ROCm and CPU backends are selectable. The first `serve` can download model weights into `models`. Review upstream licences and available disk, RAM and VRAM first. Models and server binaries are not included in the alpha.

## Roles and profiles

The main role handles consequential councils, personal intentions, cargo, designs and pivotal choices; an optional helper handles short background tasks; an embedding role supports retrieval evaluation. Profiles `lite`, `balanced`, `full` and `cpu` live in `config/profiles.toml`; model identifiers and estimates live in `config/models.toml`.

Override profile settings with `--main-model`, `--main-device`, `--helper`, `--helper-device`, `--helper-threads`, `--embed`, `--embed-device` and `--embed-threads`. Use the same overrides for `serve` and `eval`. Estimates vary with context size, concurrency, quantisation and backend; `status` reports the running servers.

## Evaluation

```powershell
python tools/llm_lab/lab.py eval --quick
python tools/llm_lab/lab.py compare
python tools/llm_lab/lab.py status
python tools/llm_lab/lab.py stop
```

The evaluation measures structured output, trait-consistent responses to authored scenarios, differentiation, helper tasks, retrieval and throughput. Read differentiation alongside fidelity: random choices can look varied without fitting a personality.

Mock mode only verifies the evaluation pipeline with synthetic responses. Chance-level scores and unrealistic throughput are expected; they do not measure model quality. Real evaluation requires online servers and appropriate hardware. Results are written to ignored `results` files.

The game expects the main endpoint on localhost port 8081. Bootstrap installs a missing runtime before launching the configured service. A successful structured completion is required before Begin. Councils govern ordinary work and major decisions; invalid replies receive corrective feedback. Loss of the model holds world time at every speed. Cosmetic narration uses a separate bounded queue and yields to consequential planning. The optional helper and embedding roles support lab evaluation; the game can run with the main role alone.

See [Local models](../../docs/wiki/Local-models.md) for player setup and limitations.
