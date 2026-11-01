import hashlib
import json
from pathlib import Path
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
DIRECTORY = ROOT / "assets/fonts"
ALLOWED = {"PixelifySans.ttf", "AlegreyaSans-Regular.ttf", "AlegreyaSans-Bold.ttf", "AlegreyaSans-Italic.ttf", "pixelifysans-OFL.txt", "alegreyasans-OFL.txt"}


def main():
    registry = json.loads((DIRECTORY / "sources.json").read_text(encoding="utf-8"))
    for item in registry["fonts"] + registry["licenses"]:
        if item["file"] not in ALLOWED or not item["source"].startswith("https://raw.githubusercontent.com/google/fonts/"):
            raise ValueError("Only the specified Google Fonts sources are allowed")
        path = DIRECTORY / item["file"]
        if path.is_file() and hashlib.sha256(path.read_bytes()).hexdigest() == item["sha256"]:
            continue
        with urllib.request.urlopen(item["source"], timeout=60) as response:
            data = response.read(2_000_000)
        if hashlib.sha256(data).hexdigest() != item["sha256"]:
            raise ValueError(f"Font checksum mismatch: {path.name}")
        path.write_bytes(data)
    print("Verified four pinned font files and their two original OFL licences.")


if __name__ == "__main__":
    main()
