

import os
import tomllib
from dataclasses import dataclass, field

from . import CONFIG_DIR, LOCAL_CONFIG, ROLES

@dataclass
class ServerPlan:
    role: str
    model: str
    hf: str
    kind: str
    device: str
    port: int
    ctx: int
    slots: int
    threads: int | None
    size_gb: float
    kv_gb: float
    extra_args: list[str] = field(default_factory=list)

    @property
    def est_gb(self) -> float:

        return round(self.size_gb + self.kv_gb + 0.4, 1)

@dataclass
class Plan:
    profile: str
    description: str
    host: str
    gpu_layers: int
    gpu_device: str
    cache_type: str
    servers: dict[str, ServerPlan]

    def endpoint_for(self, role: str) -> ServerPlan | None:

        if role in self.servers:
            return self.servers[role]
        if role == "helper":
            return self.servers.get("main")
        return None

    def totals(self) -> dict[str, float]:
        out = {"gpu": 0.0, "cpu": 0.0}
        for s in self.servers.values():
            out[s.device] += s.est_gb
        return {k: round(v, 1) for k, v in out.items()}

def _load(path):
    with open(path, "rb") as f:
        return tomllib.load(f)

def load_models() -> dict:
    return _load(CONFIG_DIR / "models.toml")["models"]

def load_profiles() -> dict:
    return _load(CONFIG_DIR / "profiles.toml")

def load_local() -> dict:
    return _load(LOCAL_CONFIG) if LOCAL_CONFIG.exists() else {}

def default_threads() -> int:

    return max(2, (os.cpu_count() or 4) // 4)

def resolve(cli: dict | None = None) -> Plan:

    cli = {k: v for k, v in (cli or {}).items() if v is not None}
    models = load_models()
    pf = load_profiles()
    local = load_local()

    name = cli.get("profile") or local.get("profile") or "lite"
    if name not in pf["profiles"]:
        raise SystemExit(f"Unknown profile '{name}'. Options: {', '.join(pf['profiles'])}")
    prof = pf["profiles"][name]
    defaults = pf["defaults"]
    sizes = pf.get("helper_sizes", {})

    servers = {}
    for role in ROLES:
        spec = dict(prof.get(role, {"model": "off"}))
        spec.update(local.get(role, {}))
        if f"{role}_model" in cli:
            spec["model"] = cli[f"{role}_model"]
        if role in cli:
            spec["model"] = cli[role]
        for key in ("device", "threads"):
            if f"{role}_{key}" in cli:
                spec[key] = cli[f"{role}_{key}"]

        model = sizes.get(spec.get("model", "off"), spec.get("model", "off"))
        if model == "off":
            continue
        if model not in models:
            raise SystemExit(f"Unknown model '{model}' for {role}. Options: {', '.join(models)}")
        m = models[model]
        if role == "embed" and m["kind"] != "embed":
            raise SystemExit(f"{model} is not an embedding model")
        if role != "embed" and m["kind"] != "chat":
            raise SystemExit(f"{model} is not a chat model")

        device = spec.get("device", "gpu")
        if device not in ("gpu", "cpu"):
            raise SystemExit(f"{role}.device must be gpu or cpu, got '{device}'")
        ctx = int(spec.get("ctx", 8192))
        servers[role] = ServerPlan(
            role=role,
            model=model,
            hf=m["hf"],
            kind=m["kind"],
            device=device,
            port=int(defaults["ports"][role]),
            ctx=ctx,
            slots=int(spec.get("slots", 2)),
            threads=int(spec["threads"]) if spec.get("threads") else (default_threads() if device == "cpu" else None),
            size_gb=float(m["size_gb"]),
            kv_gb=round(float(m["kv_gb_per_8k"]) * ctx / 8192, 2),
            extra_args=list(spec.get("extra_args", [])),
        )

    if "main" not in servers:
        raise SystemExit("A main model is required.")
    return Plan(
        profile=name,
        description=prof.get("description", ""),
        host=defaults["host"],
        gpu_layers=int(defaults["gpu_layers"]),
        gpu_device=local.get("gpu_device", defaults.get("gpu_device", "")),
        cache_type=defaults["cache_type"],
        servers=servers,
    )

def describe(plan: Plan) -> str:
    lines = [f"Profile: {plan.profile} - {plan.description}", ""]
    lines.append(f"  {'role':<7} {'model':<18} {'device':<6} {'ctx':>6} {'slots':>5} {'threads':>7} {'~GB':>5}")
    for s in plan.servers.values():
        lines.append(
            f"  {s.role:<7} {s.model:<18} {s.device:<6} {s.ctx:>6} {s.slots:>5} "
            f"{(s.threads or '-')!s:>7} {s.est_gb:>5}"
        )
    if "helper" not in plan.servers:
        lines.append("  helper  (off - helper tasks use main)")
    t = plan.totals()
    lines += ["", f"  Estimated VRAM: ~{t['gpu']} GB   Estimated system RAM: ~{t['cpu']} GB",
              "  (rough; run `lab.py status` after `serve` for real buffer sizes)"]
    return "\n".join(lines)

def write_local(values: dict) -> None:

    out = []
    if "profile" in values:
        out.append(f'profile = "{values["profile"]}"\n')
    for role in ROLES:
        sect = values.get(role)
        if not sect:
            continue
        out.append(f"\n[{role}]")
        for k, v in sect.items():
            out.append(f'{k} = "{v}"' if isinstance(v, str) else f"{k} = {v}")
    LOCAL_CONFIG.write_text("# Written by `lab.py configure`. Overrides config/profiles.toml.\n"
                            + "\n".join(out) + "\n", encoding="utf-8")
