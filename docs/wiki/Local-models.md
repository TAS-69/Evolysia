# Optional local voices and choices

The game runs without a model server. The optional narration layer adds thoughts, speech and interpretations of important moments. A bounded queue prioritises the selected person, their band and visible events, dropping stale work when necessary.

With **Minds** enabled and a server online, certain pivotal decisions may be offered to the model. The simulation supplies a shortlist and validates the chosen option. Invalid, late or missing answers fall back to instinct. Ordinary narration describes outcomes; accepted pivotal choices can change the simulation. At 10× and 25×, time does not wait for decisions.

## Set up

Install Python 3.11+ and review the model and runtime licences before downloading optional components. The lab uses the Python standard library.

```powershell
python tools/llm_lab/lab.py configure
python tools/llm_lab/lab.py plan --commands
python tools/llm_lab/lab.py install --backend vulkan
python tools/llm_lab/lab.py serve
```

Use `install --help` to check available backends. Profiles and model identifiers live in `tools/llm_lab/config`. The first server launch can download large weight files. Memory estimates are approximate and depend on model, context, concurrency and backend.

The game expects the main endpoint at `127.0.0.1:8081`; the helper normally uses port 8082. **Start model** launches the lab with the local Python installation. It does not install Python or select/download a runtime for you. Keep the supplied `tools/llm_lab` folder beside a packaged game if using that button.

## Checks

```powershell
python tools/llm_lab/lab.py eval --mock --quick
python tools/llm_lab/lab.py eval --quick
python tools/llm_lab/lab.py compare
```

Mock mode checks the evaluation plumbing with synthetic responses. Its chance-level behaviour and extremely high throughput are not measures of model quality or game performance. Real evaluation requires running servers and suitable hardware. No model weights, server binaries, personal configuration or benchmark results are distributed with the alpha.
