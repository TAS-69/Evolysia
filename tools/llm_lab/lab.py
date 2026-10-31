

import argparse
import sys

from lablib import config, evals, install, report, servers
from lablib.client import Endpoint

def add_overrides(p):
    g = p.add_argument_group("overrides (beat local.toml and the profile)")
    g.add_argument("--profile", help="lite | balanced | full | cpu")
    g.add_argument("--main-model")
    g.add_argument("--main-device", choices=["gpu", "cpu"])
    g.add_argument("--helper", help="model key, tiny | small | medium, or off")
    g.add_argument("--helper-device", choices=["gpu", "cpu"])
    g.add_argument("--helper-threads", type=int)
    g.add_argument("--embed", help="model key or off")
    g.add_argument("--embed-device", choices=["gpu", "cpu"])
    g.add_argument("--embed-threads", type=int)

def get_plan(a):
    keys = ["profile", "main_model", "main_device", "helper", "helper_device", "helper_threads",
            "embed", "embed_device", "embed_threads"]
    return config.resolve({k: getattr(a, k, None) for k in keys})

def endpoints(plan, mock):
    return {r: Endpoint(r, servers.base_url(plan, s), mock=mock) for r, s in plan.servers.items()}

def ask(prompt, options, default):
    shown = "/".join(o.upper() if o == default else o for o in options)
    while True:
        v = input(f"{prompt} [{shown}]: ").strip().lower() or default
        if v in options:
            return v

def cmd_configure(a):
    pf = config.load_profiles()
    models = config.load_models()
    print("Profiles:")
    for name, p in pf["profiles"].items():
        print(f"  {name:<9} {p['description']}")
    prof = ask("Profile", list(pf["profiles"]), "lite")
    chat = [k for k, m in models.items() if m["kind"] == "chat"]
    base = pf["profiles"][prof]
    out = {"profile": prof}

    print("\nMain mind (dialogue/decisions). Models:", ", ".join(chat))
    mm = ask("Main model", chat, base["main"]["model"])
    md = ask("Main device", ["gpu", "cpu"], base["main"]["device"])
    out["main"] = {"model": mm, "device": md}

    print("\nHelper sub-agent (naming, tagging, memory summaries). Sizes:")
    for k, v in pf["helper_sizes"].items():
        print(f"  {k:<7} {v:<14} ~{models[v]['size_gb']} GB  {models[v]['notes']}")
    hb = base.get("helper", {}).get("model", "off")
    hdef = next((k for k, v in pf["helper_sizes"].items() if v == hb), "off")
    hs = ask("Helper size", list(pf["helper_sizes"]) + ["off"], hdef)
    if hs == "off":
        out["helper"] = {"model": "off"}
    else:
        hd = ask("Helper device (cpu keeps VRAM free)", ["cpu", "gpu"], "cpu")
        out["helper"] = {"model": pf["helper_sizes"][hs], "device": hd}
        if hd == "cpu":
            t = input(f"Helper CPU threads [{config.default_threads()}]: ").strip()
            out["helper"]["threads"] = int(t) if t.isdigit() else config.default_threads()

    ed = ask("\nEmbeddings for memory retrieval", ["cpu", "gpu", "off"], "cpu")
    out["embed"] = {"model": "off"} if ed == "off" else {"model": "qwen3-embed-0.6b", "device": ed}

    config.write_local(out)
    print(f"\nSaved {config.LOCAL_CONFIG}\n")
    print(config.describe(config.resolve()))

def cmd_plan(a):
    plan = get_plan(a)
    print(config.describe(plan))
    if a.commands:
        exe = install.find_server() or "llama-server"
        print("\nCommands:")
        for s in plan.servers.values():
            print("  " + " ".join(servers.build_cmd(exe, plan, s)))

def cmd_install(a):
    install.install(a.backend, tag=a.tag, dry_run=a.dry_run)

def cmd_serve(a):
    plan = get_plan(a)
    print(config.describe(plan) + "\n")
    servers.serve(plan)

def cmd_eval(a):
    plan = get_plan(a)
    if not a.mock:
        down = [r for r, s in plan.servers.items() if not servers.healthy(servers.base_url(plan, s))]
        if down:
            sys.exit(f"Not running: {', '.join(down)}. Start them with `python lab.py serve` "
                     f"(same flags), or use --mock.")
    res = evals.run_all(endpoints(plan, a.mock), plan, quick=a.quick, samples=a.samples,
                        n_random=a.random_personas, fixed_temp=a.temp, do_bench=not a.no_bench)
    res["mock"] = a.mock
    jp, mp = report.save(res)
    print()
    for grade, label, v, good in report.verdicts(res):
        print(f"  {grade:<5} {label:<45} {v}  (target >= {good})")
    for role, rows in res.get("bench", {}).items():
        best = max(rows, key=lambda b: b["agg_tokens_per_s"])
        print(f"  {role} throughput: {best['agg_tokens_per_s']} tok/s, {best['calls_per_min']} calls/min "
              f"at concurrency {best['concurrency']}")
    print(f"\nWrote {mp}\n      {jp}")

def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)

    sub.add_parser("configure", help="interactive: choose profile and sub-agent resources").set_defaults(fn=cmd_configure)

    p = sub.add_parser("plan", help="show resolved config and memory estimate")
    add_overrides(p)
    p.add_argument("--commands", action="store_true", help="also print llama-server command lines")
    p.set_defaults(fn=cmd_plan)

    p = sub.add_parser("install", help="download prebuilt llama.cpp")
    p.add_argument("--backend", choices=["vulkan", "rocm", "cpu"], default="vulkan",
                   help="vulkan works on AMD/NVIDIA/Intel GPUs and can still run CPU-only servers")
    p.add_argument("--tag", help="pin a specific llama.cpp build, e.g. b12005 (default: newest)")
    p.add_argument("--dry-run", action="store_true", help="show which file would be downloaded")
    p.set_defaults(fn=cmd_install)

    p = sub.add_parser("serve", help="start llama-server for each role")
    add_overrides(p)
    p.set_defaults(fn=cmd_serve)

    p = sub.add_parser("eval", help="run the evaluation suite")
    add_overrides(p)
    p.add_argument("--mock", action="store_true", help="no servers; random outputs (pipeline test)")
    p.add_argument("--quick", action="store_true", help="8 personas x 5 scenarios")
    p.add_argument("--samples", type=int, default=3, help="repeats per persona/scenario (1 is noisy)")
    p.add_argument("--random-personas", type=int, default=4)
    p.add_argument("--temp", type=float, help="fixed temperature (default: per-persona from novelty seeking)")
    p.add_argument("--no-bench", action="store_true")
    p.set_defaults(fn=cmd_eval)

    sub.add_parser("status", help="show running servers and real memory use").set_defaults(fn=lambda a: servers.status())
    sub.add_parser("stop", help="stop servers started by lab.py").set_defaults(fn=lambda a: servers.stop())
    sub.add_parser("compare", help="table of all saved runs").set_defaults(fn=lambda a: print(report.compare()))

    sys.stdout.reconfigure(line_buffering=True)
    a = ap.parse_args()
    a.fn(a)

if __name__ == "__main__":
    main()
