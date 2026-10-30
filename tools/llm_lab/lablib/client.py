"""Minimal OpenAI-compatible client for llama-server, with a mock mode for offline testing."""

import hashlib
import json
import math
import random
import re
import time
import urllib.error
import urllib.request
from dataclasses import dataclass

THINK_RE = re.compile(r"<think>.*?</think>", re.S)
FENCE_RE = re.compile(r"^```(?:json)?\s*|\s*```$")


@dataclass
class ChatResult:
    text: str = ""
    data: dict | None = None
    json_ok: bool = False
    schema_ok: bool = False
    prompt_tokens: int = 0
    completion_tokens: int = 0
    latency_s: float = 0.0
    error: str | None = None


class Endpoint:
    def __init__(self, role: str, url: str | None, mock: bool = False, mock_seed: int = 0):
        self.role, self.url, self.mock = role, url, mock
        self._rng = random.Random(mock_seed)

    def __repr__(self):
        return f"Endpoint({self.role}, {'mock' if self.mock else self.url})"

    # ---- chat ---------------------------------------------------------------
    def chat(self, messages, schema=None, temperature=0.7, max_tokens=300, seed=None) -> ChatResult:
        t0 = time.perf_counter()
        if self.mock:
            data = mock_from_schema(schema or {"type": "string"}, self._rng)
            text = json.dumps(data)
            res = ChatResult(text=text, prompt_tokens=sum(len(m["content"]) // 4 for m in messages),
                             completion_tokens=len(text) // 4)
        else:
            body = {"model": self.role, "messages": messages, "temperature": temperature,
                    "max_tokens": max_tokens, "chat_template_kwargs": {"enable_thinking": False}}
            if seed is not None:
                body["seed"] = seed
            if schema:
                body["response_format"] = {"type": "json_schema",
                                           "json_schema": {"name": "out", "strict": True, "schema": schema}}
            try:
                out = _post(self.url + "/v1/chat/completions", body)
            except Exception as e:  # network / server error: record, don't crash the run
                return ChatResult(error=str(e), latency_s=time.perf_counter() - t0)
            text = out["choices"][0]["message"].get("content") or ""
            usage = out.get("usage", {})
            res = ChatResult(text=text, prompt_tokens=usage.get("prompt_tokens", 0),
                             completion_tokens=usage.get("completion_tokens", 0))
        res.latency_s = time.perf_counter() - t0
        if schema:
            try:
                res.data = json.loads(FENCE_RE.sub("", THINK_RE.sub("", res.text).strip()))
                res.json_ok = True
                res.schema_ok = validate(res.data, schema)
            except json.JSONDecodeError:
                pass
        return res

    # ---- embeddings ---------------------------------------------------------
    def embed(self, texts: list[str]) -> list[list[float]]:
        if self.mock:
            return [_hash_embed(t) for t in texts]
        out = _post(self.url + "/v1/embeddings", {"model": self.role, "input": texts})
        vecs = [d["embedding"] for d in sorted(out["data"], key=lambda d: d["index"])]
        return [_normalise(v) for v in vecs]


def _post(url, body, timeout=300):
    req = urllib.request.Request(url, data=json.dumps(body).encode(), method="POST",
                                 headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return json.load(r)
    except urllib.error.HTTPError as e:
        raise RuntimeError(f"HTTP {e.code}: {e.read().decode(errors='replace')[:300]}") from None


def _normalise(v):
    n = math.sqrt(sum(x * x for x in v)) or 1.0
    return [x / n for x in v]


def cosine(a, b) -> float:
    return sum(x * y for x, y in zip(a, b))


def _hash_embed(text, dims=64):
    v = [0.0] * dims
    for w in re.findall(r"[a-z]+", text.lower()):
        v[int(hashlib.md5(w.encode()).hexdigest(), 16) % dims] += 1
    return _normalise(v)


# ---- tiny JSON-schema subset (what our schemas use) --------------------------

def validate(x, s) -> bool:
    t = s.get("type")
    if "enum" in s and x not in s["enum"]:
        return False
    if t == "object":
        if not isinstance(x, dict) or any(k not in x for k in s.get("required", [])):
            return False
        return all(validate(x[k], sub) for k, sub in s.get("properties", {}).items() if k in x)
    if t == "array":
        return (isinstance(x, list) and len(x) <= s.get("maxItems", 10**9)
                and all(validate(i, s.get("items", {})) for i in x))
    if t == "string":
        return isinstance(x, str) and len(x) <= s.get("maxLength", 10**9)
    if t in ("integer", "number"):
        ok = isinstance(x, int) if t == "integer" else isinstance(x, (int, float))
        return (ok and not isinstance(x, bool)
                and s.get("minimum", -math.inf) <= x <= s.get("maximum", math.inf))
    return True


def mock_from_schema(s, rng):
    if "enum" in s:
        return rng.choice(s["enum"])
    t = s.get("type")
    if t == "object":
        return {k: mock_from_schema(v, rng) for k, v in s.get("properties", {}).items()}
    if t == "array":
        return [mock_from_schema(s.get("items", {}), rng) for _ in range(rng.randint(1, 2))]
    if t == "integer":
        return rng.randint(int(s.get("minimum", 0)), int(s.get("maximum", 10)))
    if t == "number":
        return rng.uniform(s.get("minimum", 0), s.get("maximum", 1))
    return rng.choice(["Mock words here.", "Another mock line.", "Ka ru tem.", "We go now."])
