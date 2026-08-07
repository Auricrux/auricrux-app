import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
APP_DIR = ROOT / "apps" / "auricrux-mobile"
LOG_DIR = ROOT / "artifacts" / "tmp"
LOG_DIR.mkdir(parents=True, exist_ok=True)

steps = [
    ("npm_install", ["npm.cmd", "install"]),
    ("eas_android_build", ["npx.cmd", "eas-cli", "build", "-p", "android", "--profile", "preview", "--non-interactive"]),
    ("expo_export_web", ["npx.cmd", "expo", "export", "--platform", "web"]),
    ("firebase_deploy", ["npx.cmd", "firebase-tools", "hosting:channel:deploy", "live", "--project", "auricrux-mobile-prod"]),
]

summary = []
for name, cmd in steps:
    proc = subprocess.run(
        cmd,
        cwd=str(APP_DIR),
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
    )
    (LOG_DIR / f"{name}.out.log").write_text(proc.stdout or "", encoding="utf-8")
    (LOG_DIR / f"{name}.err.log").write_text(proc.stderr or "", encoding="utf-8")
    summary.append({
        "step": name,
        "code": proc.returncode,
        "cmd": cmd,
        "stdoutLog": str(LOG_DIR / f"{name}.out.log"),
        "stderrLog": str(LOG_DIR / f"{name}.err.log"),
    })
    if proc.returncode != 0:
        break

summary_path = LOG_DIR / "auricrux_mobile_release_summary.json"
summary_path.write_text(json.dumps(summary, indent=2), encoding="utf-8")
print(summary_path)
print(json.dumps(summary, indent=2))
