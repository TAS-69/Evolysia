import argparse
import subprocess
from pathlib import Path

from PIL import Image


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--godot", required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    subprocess.run(
        [str(Path(args.godot).resolve()), "--headless", "--path", str(root / "game"),
         "--script", str(root / "tools/release/branding.gd")],
        cwd=root, check=True,
    )
    logo = Image.open(root / "game/branding/evolysia-logo.png").convert("RGBA")
    logo.thumbnail((240, 240), Image.Resampling.LANCZOS)
    icon = Image.new("RGBA", (256, 256), "#1e1813")
    icon.alpha_composite(logo, ((256 - logo.width) // 2, (256 - logo.height) // 2))
    icon.save(root / "game/branding/evolysia.ico", sizes=[(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])


if __name__ == "__main__":
    main()
