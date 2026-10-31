

import json
import os
import re
import signal
import subprocess
import time
import urllib.request

from . import MODELS_DIR, RUN_DIR
from .config import Plan, ServerPlan
from .install import find_server

STATE = RUN_DIR / "servers.json"

def build_cmd(exe, plan: Plan, s: ServerPlan) -> list[str]:
    cmd = [str(exe), "-hf", s.hf, "--host", plan.host, "--port", str(s.port),
           "-c", str(s.ctx), "-np", str(s.slots), "--alias", s.role, "--no-mmproj"]
    if s.device == "gpu":
        cmd += ["-ngl", str(plan.gpu_layers)]
        if plan.gpu_device:
            cmd += ["--device", plan.gpu_device]
    else:

        cmd += ["--device", "none", "-ngl", "0", "-t", str(s.threads), "-tb", str(s.threads)]
    if s.kind == "embed":
        cmd += ["--embedding", "--pooling", "last", "-ub", "2048", "-b", "2048"]
    else:
        cmd += ["-fa", "on", "-ctk", plan.cache_type, "-ctv", plan.cache_type,
                "--reasoning-budget", "0",
                "--slot-save-path", str(RUN_DIR / "slots" / s.role)]
    return cmd + s.extra_args

def base_url(plan: Plan, s: ServerPlan) -> str:
    return f"http://{plan.host}:{s.port}"

def healthy(url: str) -> bool:
    try:
        with urllib.request.urlopen(url + "/health", timeout=2) as r:
            return r.status == 200
    except Exception:
        return False

def _read_state() -> dict:
    return json.loads(STATE.read_text()) if STATE.exists() else {}

def serve(plan: Plan, timeout_s: int = 3600) -> None:
    exe = find_server()
    if not exe:
        raise SystemExit("llama-server not found. Run `python lab.py install` first.")
    (RUN_DIR / "logs").mkdir(parents=True, exist_ok=True)
    MODELS_DIR.mkdir(parents=True, exist_ok=True)
    env = dict(os.environ, LLAMA_CACHE=str(MODELS_DIR))
    state = _read_state()

    for s in plan.servers.values():
        url = base_url(plan, s)
        if healthy(url):
            print(f"[{s.role}] already running at {url}")
            continue
        (RUN_DIR / "slots" / s.role).mkdir(parents=True, exist_ok=True)
        log = RUN_DIR / "logs" / f"{s.role}.log"
        cmd = build_cmd(exe, plan, s)
        flags = subprocess.CREATE_NEW_PROCESS_GROUP if os.name == "nt" else 0
        proc = subprocess.Popen(cmd, stdout=open(log, "w", encoding="utf-8"),
                                stderr=subprocess.STDOUT, env=env, creationflags=flags)
        state[s.role] = {"pid": proc.pid, "url": url, "model": s.model,
                         "device": s.device, "log": str(log), "cmd": cmd}
        print(f"[{s.role}] starting {s.model} on {s.device} (pid {proc.pid}), log: {log}")
    STATE.write_text(json.dumps(state, indent=2))

    print("Waiting for servers (first run downloads the models; this can take a while)...")
    deadline = time.time() + timeout_s
    pending = {r for r in plan.servers}
    while pending and time.time() < deadline:
        for role in sorted(pending):
            if healthy(state[role]["url"] if role in state else base_url(plan, plan.servers[role])):
                print(f"[{role}] ready")
                pending.discard(role)
            elif role in state and not _alive(state[role]["pid"]):
                tail = _tail(state[role]["log"], 15)
                raise SystemExit(f"[{role}] exited during startup. Last log lines:\n{tail}")
        time.sleep(2)
    if pending:
        raise SystemExit(f"Timed out waiting for: {', '.join(sorted(pending))}")

def stop() -> None:
    state = _read_state()
    for role, info in state.items():
        if _alive(info["pid"]):
            os.kill(info["pid"], signal.SIGTERM)
            print(f"[{role}] stopped (pid {info['pid']})")
    STATE.unlink(missing_ok=True)

BUF_RE = re.compile(r"(\S+)\s+(model|KV|compute) buffer size\s*=\s*([\d.]+) MiB")

def status() -> None:
    state = _read_state()
    if not state:
        print("No servers started by lab.py.")
        return
    for role, info in state.items():
        up = "ready" if healthy(info["url"]) else ("starting" if _alive(info["pid"]) else "dead")
        print(f"[{role}] {info['model']} on {info['device']} at {info['url']}: {up}")
        if os.name == "nt" and _alive(info["pid"]):
            vram, ram = _win_memory(info["pid"])
            print(f"    VRAM {vram:.2f} GB, RAM {ram:.2f} GB (RAM includes the memory-mapped model file)")
            continue
        totals: dict[str, float] = {}
        for dev, _kind, mib in BUF_RE.findall(_tail(info["log"], 10**6)):
            dev = "CPU" if dev.upper().startswith("CPU") else dev
            totals[dev] = totals.get(dev, 0) + float(mib)
        for dev, mib in totals.items():
            print(f"    {dev}: {mib / 1024:.2f} GB allocated (weights + KV + compute)")

def _win_memory(pid: int) -> tuple[float, float]:

    ps = (f"$c=(Get-Counter '\\GPU Process Memory(pid_{pid}_*)\\Dedicated Usage' -EA SilentlyContinue)"
          f".CounterSamples|Measure-Object CookedValue -Sum;"
          f"\"$($c.Sum) $((Get-Process -Id {pid}).WorkingSet64)\"")
    out = subprocess.run(["powershell", "-NoProfile", "-Command", ps], capture_output=True, text=True).stdout
    try:
        vram, ram = (float(x or 0) for x in out.split())
    except ValueError:
        return 0.0, 0.0
    return vram / 2**30, ram / 2**30

def _alive(pid: int) -> bool:
    if os.name == "nt":
        out = subprocess.run(["tasklist", "/FI", f"PID eq {pid}", "/NH"],
                             capture_output=True, text=True).stdout
        return str(pid) in out
    try:
        os.kill(pid, 0)
        return True
    except OSError:
        return False

def _tail(path: str, n: int) -> str:
    try:
        with open(path, encoding="utf-8", errors="replace") as f:
            return "".join(f.readlines()[-n:])
    except FileNotFoundError:
        return ""
