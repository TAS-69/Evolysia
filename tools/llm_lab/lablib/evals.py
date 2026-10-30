"""The eval suite: personality fidelity (main), helper tasks, embeddings, throughput."""

import itertools
import json
import math
import random
import re
import statistics
import time
from collections import Counter
from concurrent.futures import ThreadPoolExecutor

from . import DATA_DIR
from .client import Endpoint, cosine
from .personas import ARCHETYPES, Persona, random_personas

EMOTIONS = ["joy", "fear", "anger", "sadness", "shame", "pride", "disgust", "surprise", "love", "calm", "envy"]

SYSTEM_MAIN = (
    "You voice one person living in a harsh early-human world simulation. Stay fully in character. "
    "People here can be selfish, cowardly, cruel, dishonest, stubborn, superstitious or foolish: play the "
    "character exactly as described, not as a helpful or polite assistant. Their personality should drive "
    "the choice. Reply only with the requested JSON."
)
SYSTEM_HELPER = "You are a fast utility module inside a world simulation. Reply only with the requested JSON."
QUERY_PREFIX = "Instruct: Given a question a person asks themself, retrieve the most relevant memory\nQuery: "


def load_json(name):
    return json.loads((DATA_DIR / name).read_text(encoding="utf-8"))


def _pmap(fn, items, workers):
    with ThreadPoolExecutor(max_workers=max(1, workers)) as ex:
        return list(ex.map(fn, items))


# ---- 1. personality fidelity --------------------------------------------------

def decision_schema(option_ids):
    return {
        "type": "object",
        "properties": {
            "thought": {"type": "string", "maxLength": 240},
            "action": {"type": "string", "enum": option_ids},
            "says": {"type": "string", "maxLength": 200},
            "emotion": {"type": "string", "enum": EMOTIONS},
            "intensity": {"type": "integer", "minimum": 1, "maximum": 10},
        },
        "required": ["thought", "action", "says", "emotion", "intensity"],
        "additionalProperties": False,
    }


def decision_messages(p: Persona, sc: dict, options: list[dict]):
    fmt = lambda s: s.replace("{name}", p.name)
    mem = "\n".join(f"- {fmt(m)}" for m in sc.get("memories", [])) or "- (nothing relevant)"
    opts = "\n".join(f"- {o['id']}: {o['desc']}" for o in options)
    user = (f"{p.sheet()}\n\nRelevant memories:\n{mem}\n\nSituation: {fmt(sc['situation'])}\n\n"
            f"Options:\n{opts}\n\nDecide what {p.name} does. JSON fields: thought ({p.name}'s inner voice, "
            f"first person, 1-2 sentences), action (one option id), says (what {p.name} says aloud, may be "
            f"empty), emotion, intensity (1-10).")
    return [{"role": "system", "content": SYSTEM_MAIN}, {"role": "user", "content": user}]


def run_fidelity(ep: Endpoint, personas, scenarios, samples, workers, fixed_temp=None, seed=1):
    jobs = []
    for (pi, p), sc, k in itertools.product(enumerate(personas), scenarios, range(samples)):
        rng = random.Random(f"{seed}-{pi}-{sc['id']}-{k}")
        opts = sc["options"][:]
        rng.shuffle(opts)  # avoid position bias
        jobs.append((p, sc, k, opts, rng.randint(0, 2**31)))

    def one(job):
        p, sc, k, opts, s = job
        r = ep.chat(decision_messages(p, sc, opts), decision_schema([o["id"] for o in opts]),
                    temperature=fixed_temp if fixed_temp is not None else p.temperature(),
                    max_tokens=320, seed=s)
        d = r.data or {}
        return {"persona": p.name, "archetype": p.archetype, "traits": p.centred(), "scenario": sc["id"],
                "kind": sc["kind"], "sample": k, "chosen": d.get("action") if r.schema_ok else None,
                "thought": d.get("thought", ""), "says": d.get("says", ""), "emotion": d.get("emotion"),
                "json_ok": r.json_ok, "schema_ok": r.schema_ok, "error": r.error,
                "latency_s": round(r.latency_s, 3), "completion_tokens": r.completion_tokens}

    return _pmap(one, jobs, workers)


def _affinities(sc, traits):
    return {o["id"]: sum(w * traits[t] for t, w in o["load"].items()) for o in sc["options"]}


def score_fidelity(records, scenarios):
    by_id = {s["id"]: s for s in scenarios}
    valid = [r for r in records if r["chosen"]]
    pct, top1, by_kind = [], [], {}
    for r in valid:
        aff = _affinities(by_id[r["scenario"]], r["traits"])
        a = aff[r["chosen"]]
        others = [v for k, v in aff.items() if k != r["chosen"]]
        below = sum(v < a - 1e-9 for v in others) + 0.5 * sum(abs(v - a) <= 1e-9 for v in others)
        p = below / len(others)
        pct.append(p)
        top1.append(a >= max(aff.values()) - 1e-9)
        by_kind.setdefault(r["kind"], []).append(p)

    # Do different personas actually choose differently? (normalised entropy per scenario)
    ent = []
    for sid, sc in by_id.items():
        picks = [r["chosen"] for r in valid if r["scenario"] == sid]
        if len(picks) > 1:
            c = Counter(picks)
            h = -sum(n / len(picks) * math.log(n / len(picks)) for n in c.values())
            ent.append(h / math.log(min(len(sc["options"]), len(picks))))

    # "Too nice" check: do selfish personas pick antisocial options more than kind ones?
    anti = {sid: {o["id"] for o in sc["options"] if o["load"].get("CO", 0) <= -0.5} for sid, sc in by_id.items()}
    rate = lambda rs: round(sum(r["chosen"] in anti[r["scenario"]] for r in rs) / len(rs), 3) if rs else None
    pool = [r for r in valid if anti[r["scenario"]]]
    selfish = rate([r for r in pool if r["traits"]["CO"] <= -0.4])
    kind = rate([r for r in pool if r["traits"]["CO"] >= 0.4])

    n_opts = statistics.mean(len(s["options"]) for s in scenarios)
    mean = lambda xs: round(statistics.mean(xs), 3) if xs else None
    return {
        "calls": len(records),
        "json_valid_rate": round(sum(r["json_ok"] for r in records) / len(records), 3),
        "schema_valid_rate": round(len(valid) / len(records), 3),
        "errors": sum(1 for r in records if r["error"]),
        "fidelity_percentile": mean(pct),          # 0.5 = random, 1.0 = always best-fitting option
        "fidelity_ci95": round(1.96 * statistics.stdev(pct) / math.sqrt(len(pct)), 3) if len(pct) > 1 else None,
        "top_choice_rate": mean(top1),
        "top_choice_chance": round(1 / n_opts, 3),
        "fidelity_by_kind": {k: mean(v) for k, v in sorted(by_kind.items())},
        "differentiation": mean(ent),             # 0 = everyone picks the same, 1 = maximally varied
        "antisocial_rate_selfish": selfish,
        "antisocial_rate_kind": kind,
        "nice_bias_gap": round(selfish - kind, 3) if selfish is not None and kind is not None else None,
        "mean_latency_s": mean([r["latency_s"] for r in records]),
        "mean_completion_tokens": mean([r["completion_tokens"] for r in records]),
    }


# ---- 2. voice distinctness (uses embeddings when available) --------------------

def score_voices(records, embed_ep: Endpoint | None):
    groups = {}
    for r in records:
        if r["sample"] == 0 and r["says"].strip():
            groups.setdefault(r["scenario"], []).append(r["says"])
    sims = []
    for lines in groups.values():
        if len(lines) < 2:
            continue
        if embed_ep:
            vecs = embed_ep.embed(lines)
            sims += [cosine(a, b) for a, b in itertools.combinations(vecs, 2)]
        else:
            sets = [set(re.findall(r"[a-z']+", l.lower())) for l in lines]
            sims += [len(a & b) / max(1, len(a | b)) for a, b in itertools.combinations(sets, 2)]
    if not sims:
        return {"voice_distinctness": None}
    return {"voice_distinctness": round(1 - statistics.mean(sims), 3),
            "voice_metric": "1 - mean embedding cosine" if embed_ep else "1 - mean word Jaccard"}


# ---- 3. helper sub-agent tasks ------------------------------------------------

def run_helper(ep: Endpoint, tasks, workers):
    cats = tasks["categories"]
    msg = lambda u: [{"role": "system", "content": SYSTEM_HELPER}, {"role": "user", "content": u}]

    def classify(t):
        s = {"type": "object", "required": ["category", "importance"], "additionalProperties": False,
             "properties": {"category": {"type": "string", "enum": cats},
                            "importance": {"type": "integer", "minimum": 1, "maximum": 10}}}
        r = ep.chat(msg(f"Classify this event from a village simulation.\nCategories: {', '.join(cats)}.\n"
                        f"Importance to the people involved: 1 trivial, 5 notable, 10 life-changing.\n"
                        f"Event: {t['text']}"), s, 0.2, 40)
        d = r.data if r.schema_ok else {}
        lo, hi = t["importance"]
        return {"ok": r.schema_ok, "lat": r.latency_s, "toks": r.completion_tokens, "task": "classify",
                "input": t["text"], "output": d,
                "correct": d.get("category") == t["category"], "imp_ok": lo <= d.get("importance", -1) <= hi}

    def name(t):
        s = {"type": "object", "required": ["name", "meaning"], "additionalProperties": False,
             "properties": {"name": {"type": "string", "maxLength": 20},
                            "meaning": {"type": "string", "maxLength": 80}}}
        r = ep.chat(msg(f"Invent a place name for {t['feature']}. The language builds words from sounds like: "
                        f"{', '.join(t['syllables'])}. The name must be ONE word (no spaces, no 'The'), "
                        f"2-4 syllables. Give the name and what it means."), s, 0.9, 60)
        n = (r.data or {}).get("name", "") if r.schema_ok else ""
        letters = n.replace("'", "").replace("-", "")
        return {"ok": r.schema_ok, "lat": r.latency_s, "toks": r.completion_tokens, "task": "name",
                "input": t["feature"], "output": r.data,
                "valid_name": letters.isalpha() and 3 <= len(letters) <= 14,
                "phonology": any(sy in n.lower() for sy in t["syllables"])}

    def summarize(t):
        s = {"type": "object", "required": ["summary", "trust_change", "tags"], "additionalProperties": False,
             "properties": {"summary": {"type": "string", "maxLength": 160},
                            "trust_change": {"type": "integer", "minimum": -5, "maximum": 5},
                            "tags": {"type": "array", "maxItems": 3, "items": {"type": "string", "enum": cats}}}}
        r = ep.chat(msg(f"Turn this conversation into a one-sentence memory. trust_change: how much the other "
                        f"person should now trust {t['about']} (-5 betrayed .. 5 loyal friend). "
                        f"tags: up to 3 of: {', '.join(cats)}.\n\n{t['dialogue']}"),
                    s, 0.3, 90)
        d = r.data if r.schema_ok else {}
        tc = d.get("trust_change", 0)
        return {"ok": r.schema_ok, "lat": r.latency_s, "toks": r.completion_tokens, "task": "summarize",
                "input": t["about"], "output": d,
                "sign_ok": tc != 0 and (tc > 0) == (t["trust_sign"] > 0)}

    c = _pmap(classify, tasks["classify"], workers)
    n = _pmap(name, tasks["names"], workers)
    s = _pmap(summarize, tasks["summarize"], workers)
    allr = c + n + s
    rate = lambda rs, k: round(sum(bool(r[k]) for r in rs) / len(rs), 3)
    return {
        "calls": len(allr),
        "schema_valid_rate": rate(allr, "ok"),
        "classify_accuracy": rate(c, "correct"),
        "importance_in_range": rate(c, "imp_ok"),
        "names_valid": rate(n, "valid_name"),
        "names_phonology_match": rate(n, "phonology"),
        "summary_trust_sign": rate(s, "sign_ok"),
        "mean_latency_s": round(statistics.mean(r["lat"] for r in allr), 3),
        "items": [{k: r[k] for k in ("task", "input", "output")} for r in allr],
    }


# ---- 4. embedding retrieval -----------------------------------------------------

def run_retrieval(ep: Endpoint, tasks):
    mem = tasks["retrieval"]["memories"]
    qs = tasks["retrieval"]["queries"]
    t0 = time.perf_counter()
    mv = ep.embed(mem)
    qv = ep.embed([QUERY_PREFIX + q["q"] for q in qs])
    elapsed = time.perf_counter() - t0
    hits = sum(max(range(len(mv)), key=lambda i: cosine(v, mv[i])) == q["gold"] for v, q in zip(qv, qs))
    return {"retrieval_top1": round(hits / len(qs), 3),
            "texts_per_s": round((len(mem) + len(qs)) / elapsed, 1)}


# ---- 5. throughput ---------------------------------------------------------------

def bench_chat(ep: Endpoint, levels, scenario):
    p = ARCHETYPES[1]
    msgs = decision_messages(p, scenario, scenario["options"])
    schema = decision_schema([o["id"] for o in scenario["options"]])
    out = []
    for c in levels:
        n = max(4, 2 * c)
        t0 = time.perf_counter()
        rs = _pmap(lambda i: ep.chat(msgs, schema, 0.9, 200, seed=i), range(n), c)
        wall = time.perf_counter() - t0
        lats = sorted(r.latency_s for r in rs)
        toks = sum(r.completion_tokens for r in rs)
        out.append({"concurrency": c, "requests": n, "errors": sum(1 for r in rs if r.error),
                    "agg_tokens_per_s": round(toks / wall, 1),
                    "calls_per_min": round(60 * n / wall, 1),
                    "p50_latency_s": round(lats[len(lats) // 2], 2),
                    "p95_latency_s": round(lats[min(len(lats) - 1, int(len(lats) * 0.95))], 2)})
    return out


# ---- driver ------------------------------------------------------------------------

def run_all(eps: dict, plan, quick=False, samples=1, n_random=4, fixed_temp=None, do_bench=True, log=print):
    scen = load_json("scenarios.json")["scenarios"]
    tasks = load_json("helper_tasks.json")
    personas = ARCHETYPES + random_personas(n_random)
    if quick:
        scen, personas = scen[:5], ARCHETYPES
    main, helper, embed = eps["main"], eps.get("helper"), eps.get("embed")
    main_slots = plan.servers["main"].slots
    res = {"profile": plan.profile, "models": {r: s.model for r, s in plan.servers.items()},
           "devices": {r: s.device for r, s in plan.servers.items()}, "quick": quick, "samples": samples}

    log(f"[main] personality fidelity: {len(personas)} personas x {len(scen)} scenarios x {samples} samples")
    recs = run_fidelity(main, personas, scen, samples, main_slots, fixed_temp)
    res["fidelity"] = score_fidelity(recs, scen)
    res["fidelity"].update(score_voices(recs, embed))
    res["examples"] = [r for r in recs if r["sample"] == 0 and r["archetype"] != "random"][:16]

    hp = helper or main
    hslots = plan.endpoint_for("helper").slots
    log(f"[{hp.role}] helper tasks (classify / name / summarize)")
    res["helper"] = run_helper(hp, tasks, hslots)
    res["helper"]["served_by"] = hp.role

    if embed:
        log("[embed] memory retrieval")
        res["embed"] = run_retrieval(embed, tasks)

    if do_bench:
        log("[main] throughput")
        lv = sorted({1, 2, 4, main_slots} & set(range(1, main_slots + 1)))
        res["bench"] = {"main": bench_chat(main, lv, scen[0])}
        if helper:
            log("[helper] throughput")
            res["bench"]["helper"] = bench_chat(helper, sorted({1, plan.servers['helper'].slots}), scen[0])
    return res
