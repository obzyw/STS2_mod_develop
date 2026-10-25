"""Collect our tested plugins and selected reports; excludes third-party mod binaries, saves and raw logs."""
import argparse
import hashlib
import json
import os
import shutil
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--game", type=Path, required=True)
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
own_mods = ["SilentMovementCompat", "SilentEffectsCompat", "WatcherDivinityPose", "WatcherStanceTransitions"]
files = []
for name in own_mods:
    source = args.game / "mods" / name
    destination = root / "portable" / "mods" / name
    destination.mkdir(parents=True, exist_ok=True)
    for path in source.iterdir():
        if path.is_file():
            shutil.copy2(path, destination / path.name)
            files.append({"path": (destination / path.name).relative_to(root).as_posix(), "sha256": hashlib.sha256(path.read_bytes()).hexdigest().upper()})
(root / "portable" / "files.json").write_text(json.dumps(files, ensure_ascii=False, indent=2), encoding="utf-8")

app = Path(os.environ["APPDATA"]) / "SlayTheSpire2"
local = Path(os.environ["LOCALAPPDATA"]) / "SlayTheSpire2"
settings = [("appdata", p) for p in [
    "mod_configs/DebilitateRedKnife.animations.json", "mod_settings/MoreNecrobinderAnimations.json",
    "mod_settings/MoreSilentAnimations.json", "MeleeAttack_settings.json", "mod_settings/RegentAnimations.json",
    "WatcherReimagined/settings.json", "mods/YUWatcherForms/settings.json", "watcher_mod_settings.json",
]] + [("localappdata", "RegentFX/ritsu_interop_state.json")]
records = []
for location, relative in settings:
    path = (app if location == "appdata" else local) / relative
    if path.exists():
        # This list only contains visual settings, never account settings.save or profile saves.
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        filename = relative.replace("/", "__")
        (root / "profiles" / "author-visual-settings" / filename).write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
        records.append({"root": location, "relativePath": relative, "file": filename})
(root / "profiles" / "author-visual-settings" / "index.json").write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding="utf-8")

reports = [
    "native-compatibility-20261022-232341/compatibility-probe.json",
    "differential-validation.json", "native-misery-20261022-232939/validation.json",
    "native-compatibility-20261023-201759/silent-validation.json",
    "native-compatibility-20261023-212049/effects-validation.json",
    "native-compatibility-20261023-221150/effects-validation.json",
    "native-compatibility-20261024-102544/regent-validation.json",
    "native-compatibility-20261024-233858/watcher-pose-validation.json",
    "native-compatibility-20261025-000927/stance-transition-validation.json",
    "watcher-menu-20261025-002825/menu-result.json", "display-stability-deployment.json",
]
for relative in reports:
    source = root / "artifacts" / relative
    if not source.exists():
        continue
    data = source.read_text(encoding="utf-8-sig")
    data = data.replace(str(args.game).replace("\\", "\\\\"), "<GAME>")
    data = data.replace(os.environ["USERPROFILE"].replace("\\", "\\\\"), "<USERPROFILE>")
    destination = root / "evidence" / relative.replace("/", "__")
    destination.write_text(data, encoding="utf-8")
print(f"Collected {len(files)} own runtime files, {len(records)} visual preference files; no saves or third-party mod DLLs.")
