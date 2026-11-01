import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
STAGE = ROOT / "build/windows"


def copy(source, destination):
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)


def verify(stage):
    for relative in ("Evolysia.exe", "Evolysia.pck", "LICENSE", "ART-LICENSE.md",
                     "THIRD_PARTY_NOTICES.md", "godot-notices.json", "assets/manifest.json",
                     "licenses/dotnet/LICENSE.TXT", "licenses/dotnet/THIRD-PARTY-NOTICES.TXT"):
        if not (stage / relative).is_file():
            raise RuntimeError(f"Missing release file: {relative}")
    data = list(stage.glob("data_*"))
    if len(data) != 1 or not (data[0] / "coreclr.dll").is_file():
        raise RuntimeError("The Windows package must contain one self-contained .NET runtime")
    manifest = json.loads((stage / "assets/manifest.json").read_text())
    expected = {p.relative_to(ROOT / "assets").as_posix() for p in (ROOT / "assets").rglob("*")
                if p.is_file() and "_preview" not in p.parts and p.suffix in (".png", ".json", ".gpl", ".ttf", ".txt")}
    actual = {p.relative_to(stage / "assets").as_posix() for p in (stage / "assets").rglob("*") if p.is_file()}
    if actual != expected:
        raise RuntimeError(f"Artwork package mismatch: {len(expected - actual)} missing, {len(actual - expected)} extra")
    for relative in sorted(expected):
        source = ROOT / "assets" / relative
        exported = stage / "assets" / relative
        if hashlib.sha256(source.read_bytes()).digest() != hashlib.sha256(exported.read_bytes()).digest():
            raise RuntimeError(f"Artwork changed during packaging: {relative}")
    forbidden = {".git", ".godot", ".claude", ".codex", ".agents", "__pycache__", "_preview", "models", "bin", "results"}
    for path in stage.rglob("*"):
        if set(path.relative_to(stage).parts) & forbidden or path.suffix.lower() in (".pdb", ".gguf", ".pyc") or path.name == "local.toml":
            raise RuntimeError(f"Local-only file in package: {path}")
    print(f"Package verified: {len(manifest['assets'])} asset entries; self-contained Windows x64")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--godot")
    parser.add_argument("--no-export", action="store_true")
    parser.add_argument("--verify", type=Path)
    args = parser.parse_args()
    if args.verify:
        verify(args.verify.resolve())
        return
    STAGE.mkdir(parents=True, exist_ok=True)
    if not args.no_export:
        if not args.godot:
            parser.error("--godot is required unless --no-export is used")
        result = subprocess.run(
            [str(Path(args.godot).resolve()), "--headless", "--path", str(ROOT / "game"),
             "--export-release", "Windows x64", str(STAGE / "Evolysia.exe")],
            cwd=ROOT, capture_output=True, text=True,
        )
        (ROOT / "build/export.log").write_text(result.stdout + result.stderr, encoding="utf-8")
        if result.returncode or "ERROR:" in result.stdout + result.stderr:
            raise RuntimeError("Godot export failed; see build/export.log")
    for source in (ROOT / "assets").rglob("*"):
        if source.is_file() and "_preview" not in source.parts and source.suffix in (".png", ".json", ".gpl", ".ttf", ".txt"):
            copy(source, STAGE / "assets" / source.relative_to(ROOT / "assets"))
    lab = ROOT / "tools/llm_lab"
    for relative in ("lab.py", "README.md"):
        copy(lab / relative, STAGE / "tools/llm_lab" / relative)
    for directory, suffixes in (("lablib", (".py",)), ("config", (".toml",)), ("data", (".json",))):
        for source in (lab / directory).rglob("*"):
            if source.is_file() and source.suffix in suffixes:
                copy(source, STAGE / "tools/llm_lab" / source.relative_to(lab))
    for name in ("LICENSE", "ART-LICENSE.md", "THIRD_PARTY_NOTICES.md"):
        copy(ROOT / name, STAGE / name)
    version = re.search(r'config/version="([^"]+)"', (ROOT / "game/project.godot").read_text(encoding="utf-8"))[1]
    notes = (ROOT / f"docs/releases/v{version}.md").read_text(encoding="utf-8")
    notes = notes.replace(f"(v{version}-validation.md)", f"(https://github.com/TAS-69/Evolysia/blob/v{version}/docs/releases/v{version}-validation.md)")
    (STAGE / "README.md").write_text(notes, encoding="utf-8", newline="\n")
    copy(ROOT / "game/branding/godot-notices.json", STAGE / "godot-notices.json")
    copy(ROOT / "tools/llm_lab/README.md", STAGE / "tools/llm_lab/README.md")
    lab_readme = STAGE / "tools/llm_lab/README.md"
    lab_readme.write_text(lab_readme.read_text(encoding="utf-8").replace("(../../docs/wiki/Local-models.md)", "(https://github.com/TAS-69/Evolysia/wiki/Local-models)"), encoding="utf-8", newline="\n")
    data = next(STAGE.glob("data_*"))
    runtime = json.loads((data / "Evolysia.Game.runtimeconfig.json").read_text())["runtimeOptions"]["includedFrameworks"][0]["version"]
    packages = Path(os.environ.get("NUGET_PACKAGES", str(Path.home() / ".nuget/packages")))
    runtime_package = packages / "microsoft.netcore.app.runtime.win-x64" / runtime
    for name in ("LICENSE.TXT", "THIRD-PARTY-NOTICES.TXT"):
        copy(runtime_package / name, STAGE / "licenses/dotnet" / name)
    verify(STAGE)
    version = re.search(r'config/version="([^"]+)"', (ROOT / "game/project.godot").read_text())[1]
    output = ROOT / "build/releases"
    output.mkdir(exist_ok=True)
    archive = output / f"Evolysia-{version}-windows-x64.zip"
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as bundle:
        for path in sorted(STAGE.rglob("*")):
            if path.is_file():
                info = zipfile.ZipInfo("Evolysia/" + path.relative_to(STAGE).as_posix(), (2026, 10, 31, 0, 0, 0))
                info.compress_type = zipfile.ZIP_DEFLATED
                info.external_attr = 0o100644 << 16
                bundle.writestr(info, path.read_bytes(), compress_type=zipfile.ZIP_DEFLATED, compresslevel=9)
    with archive.open("rb") as file:
        digest = hashlib.file_digest(file, "sha256").hexdigest()
    (output / "SHA256SUMS.txt").write_text(f"{digest}  {archive.name}\n", encoding="utf-8", newline="\n")
    print(f"Created {archive.name}: {archive.stat().st_size:,} bytes\nSHA256 {digest}")


if __name__ == "__main__":
    main()
