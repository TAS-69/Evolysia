

import json
import platform
import re
import shutil
import tarfile
import urllib.request
import zipfile
from pathlib import Path

from . import BIN_DIR

RELEASES_API = "https://api.github.com/repos/ggml-org/llama.cpp/releases"
POINTER = BIN_DIR / "llama-server.json"
EXE = "llama-server.exe" if platform.system() == "Windows" else "llama-server"

def find_server() -> Path | None:

    if POINTER.exists():
        p = Path(json.loads(POINTER.read_text())["path"])
        if p.exists():
            return p
    on_path = shutil.which("llama-server")
    return Path(on_path) if on_path else None

def _asset_matcher(backend: str):

    system, machine = platform.system(), platform.machine().lower()
    arch = "arm64" if machine in ("arm64", "aarch64") else "x64"
    os_tag = {"Windows": "win", "Linux": "ubuntu", "Darwin": "macos"}.get(system)
    if not os_tag:
        raise SystemExit(f"Unsupported OS: {system}")
    if os_tag == "macos" or (os_tag == "ubuntu" and backend == "cpu"):
        exact = re.compile(rf"^llama-b\d+-bin-{os_tag}-{arch}\.(zip|tar\.gz)$")
        return lambda name: bool(exact.match(name))
    return lambda name: (name.startswith("llama-b") and f"-bin-{os_tag}-{backend}" in name
                         and re.search(rf"-{arch}\.(zip|tar\.gz)$", name) is not None)

def install(backend: str = "vulkan", tag: str | None = None, dry_run: bool = False) -> Path:

    url = f"{RELEASES_API}/tags/{tag}" if tag else f"{RELEASES_API}?per_page=20"
    with urllib.request.urlopen(url, timeout=30) as r:
        releases = json.load(r)
    match = _asset_matcher(backend)
    for release in [releases] if tag else releases:
        assets = [a for a in release["assets"] if match(a["name"])]
        if assets:
            break
    else:
        raise SystemExit(f"No llama.cpp build found for backend '{backend}' on this OS.")
    asset = assets[0]
    size_mb = asset["size"] / 1e6
    print(f"llama.cpp {release['tag_name']}: {asset['name']} ({size_mb:.0f} MB)")
    if dry_run:
        return Path()

    dest = BIN_DIR / f"{release['tag_name']}-{backend}"
    archive = BIN_DIR / asset["name"]
    BIN_DIR.mkdir(parents=True, exist_ok=True)
    print(f"Downloading to {archive} ...")
    urllib.request.urlretrieve(asset["browser_download_url"], archive)

    dest.mkdir(parents=True, exist_ok=True)
    if archive.name.endswith(".zip"):
        with zipfile.ZipFile(archive) as z:
            z.extractall(dest)
    else:
        with tarfile.open(archive) as t:
            t.extractall(dest, filter="data")
    archive.unlink()

    exe = next(dest.rglob(EXE), None)
    if not exe:
        raise SystemExit(f"{EXE} not found in {dest}")
    POINTER.write_text(json.dumps({"path": str(exe), "tag": release["tag_name"], "backend": backend}))
    print(f"Installed: {exe}")
    return exe
