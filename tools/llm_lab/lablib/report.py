"""Save results and turn the numbers into a plain verdict."""

import json
import time

from . import RESULTS_DIR

# (metric path, good threshold, ok threshold, higher is better)
CHECKS = [
    ("fidelity.schema_valid_rate", 0.98, 0.9, "Valid structured output"),
    ("fidelity.fidelity_percentile", 0.72, 0.62, "Acts in character (0.5 = random)"),
    ("fidelity.differentiation", 0.55, 0.4, "Different personas choose differently"),
    ("fidelity.nice_bias_gap", 0.3, 0.15, "Selfish personas act selfish vs kind ones"),
    ("helper.classify_accuracy", 0.8, 0.65, "Helper: event classification"),
    ("helper.summary_trust_sign", 0.8, 0.6, "Helper: memory summary trust direction"),
    ("helper.names_valid", 0.8, 0.6, "Helper: usable invented names"),
    ("embed.retrieval_top1", 0.8, 0.6, "Embeddings: memory retrieval"),
]


def _get(res, path):
    cur = res
    for k in path.split("."):
        if not isinstance(cur, dict) or k not in cur:
            return None
        cur = cur[k]
    return cur


def verdicts(res):
    out = []
    for path, good, ok, label in CHECKS:
        v = _get(res, path)
        if v is None:
            continue
        grade = "GOOD" if v >= good else ("OK" if v >= ok else "WEAK")
        out.append((grade, label, v, good))
    return out


def summary_md(res) -> str:
    m = res["models"]
    lines = [f"# LLM lab run: {res['profile']}", "",
             "| role | model | device |", "|---|---|---|"]
    lines += [f"| {r} | {m[r]} | {res['devices'][r]} |" for r in m]
    if res.get("mock"):
        lines += ["", "**MOCK RUN**: random outputs, numbers show the chance baseline only."]
    lines += ["", "## Verdict", "", "| | check | value | target |", "|---|---|---|---|"]
    lines += [f"| {g} | {label} | {v} | >= {good} |" for g, label, v, good in verdicts(res)]
    f = res["fidelity"]
    lines += ["", "## Personality fidelity (main)", "",
              f"- calls: {f['calls']}, errors: {f['errors']}, mean latency {f['mean_latency_s']} s, "
              f"~{f['mean_completion_tokens']} tokens/call",
              f"- fidelity {f['fidelity_percentile']} +/- {f.get('fidelity_ci95')} (95% CI); "
              f"top choice rate {f['top_choice_rate']} (chance {f['top_choice_chance']})",
              f"- by scenario kind: {f['fidelity_by_kind']}",
              f"- antisocial choice rate: selfish personas {f['antisocial_rate_selfish']}, "
              f"kind personas {f['antisocial_rate_kind']}",
              f"- voice distinctness: {f.get('voice_distinctness')} ({f.get('voice_metric', '')})"]
    lines += ["", f"## Helper tasks (served by {res['helper']['served_by']})", "",
              "\n".join(f"- {k}: {v}" for k, v in res["helper"].items() if k not in ("served_by", "items"))]
    if "embed" in res:
        lines += ["", "## Embeddings", "", "\n".join(f"- {k}: {v}" for k, v in res["embed"].items())]
    for role, rows in res.get("bench", {}).items():
        lines += ["", f"## Throughput: {role}", "",
                  "| concurrency | tok/s (total) | calls/min | p50 s | p95 s | errors |", "|---|---|---|---|---|---|"]
        lines += [f"| {b['concurrency']} | {b['agg_tokens_per_s']} | {b['calls_per_min']} | "
                  f"{b['p50_latency_s']} | {b['p95_latency_s']} | {b['errors']} |" for b in rows]
    lines += ["", "## Sample outputs", ""]
    for ex in res.get("examples", [])[:8]:
        lines.append(f"- **{ex['persona']}** ({ex['archetype']}) / {ex['scenario']} -> `{ex['chosen']}`: "
                     f"*{ex['thought']}* \"{ex['says']}\"")
    return "\n".join(lines) + "\n"


def save(res) -> tuple:
    RESULTS_DIR.mkdir(parents=True, exist_ok=True)
    stem = time.strftime("%Y%m%d-%H%M%S") + f"_{res['profile']}_{res['models']['main']}"
    if res.get("mock"):
        stem += "_mock"
    base, n = stem, 1
    while (RESULTS_DIR / f"{stem}.json").exists():
        n += 1
        stem = f"{base}_{n}"
    jp, mp = RESULTS_DIR / f"{stem}.json", RESULTS_DIR / f"{stem}.md"
    jp.write_text(json.dumps(res, indent=2), encoding="utf-8")
    mp.write_text(summary_md(res), encoding="utf-8")
    return jp, mp


def compare() -> str:
    rows = []
    for p in sorted(RESULTS_DIR.glob("*.json")):
        r = json.loads(p.read_text(encoding="utf-8"))
        f, h = r["fidelity"], r["helper"]
        tps = max((b["agg_tokens_per_s"] for b in r.get("bench", {}).get("main", [])), default=None)
        rows.append(f"| {p.stem} | {r['models']['main']} | {r['models'].get('helper', '-')}"
                    f"@{r['devices'].get('helper', '-')} | {f['fidelity_percentile']}"
                    f"{' +/- ' + str(f['fidelity_ci95']) if f.get('fidelity_ci95') else ''} | {f['differentiation']} | "
                    f"{f['nice_bias_gap']} | {f['schema_valid_rate']} | {h['classify_accuracy']} | "
                    f"{h['summary_trust_sign']} | {tps} |")
    head = ("| run | main | helper | fidelity | differentiation | nice-gap | schema | classify | trust-sign | "
            "main tok/s |\n|---|---|---|---|---|---|---|---|---|---|")
    return head + "\n" + "\n".join(rows) if rows else "No results yet."
