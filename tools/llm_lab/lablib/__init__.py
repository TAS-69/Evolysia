from pathlib import Path

LAB_DIR = Path(__file__).resolve().parent.parent
CONFIG_DIR = LAB_DIR / "config"
DATA_DIR = LAB_DIR / "data"
BIN_DIR = LAB_DIR / "bin"
MODELS_DIR = LAB_DIR / "models"
RUN_DIR = LAB_DIR / ".run"
RESULTS_DIR = LAB_DIR / "results"
LOCAL_CONFIG = LAB_DIR / "local.toml"

ROLES = ("main", "helper", "embed")
