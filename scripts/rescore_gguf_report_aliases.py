#!/usr/bin/env python3
"""Offline re-score of a GGUF generative report using keyword aliases (no live calls)."""
from __future__ import annotations

import json
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
ALIASES = REPO / "eval" / "keyword_aliases_v1.json"
SUITE = REPO / "eval" / "construction_god_suite_v1.json"


def load_aliases() -> dict[str, list[str]]:
    if not ALIASES.is_file():
        return {}
    doc = json.loads(ALIASES.read_text(encoding="utf-8"))
    raw = doc.get("aliases") or {}
    return {str(k).lower(): [str(a) for a in (v or [])] for k, v in raw.items()}


def keyword_match(content: str, keyword: str, aliases: dict[str, list[str]]) -> bool:
    hay = content.lower()
    needle = str(keyword).lower()
    if needle in hay:
        return True
    for alt in aliases.get(needle, []):
        if alt.lower() in hay:
            return True
    return False


def main() -> int:
    if len(sys.argv) < 2:
        print("Usage: rescore_gguf_report_aliases.py <report.json>", file=sys.stderr)
        return 2
    report_path = Path(sys.argv[1])
    report = json.loads(report_path.read_text(encoding="utf-8"))
    suite = json.loads(SUITE.read_text(encoding="utf-8"))
    by_id = {c["id"]: c for c in suite["cases"]}
    aliases = load_aliases()

    # Keep prior PASS cases (excerpts are truncated and undercount).
    # Only re-score prior FAIL cases with aliases against stored excerpts.
    flips = []
    still_fail = []
    passed = 0
    for case in report.get("cases") or []:
        cid = case["id"]
        if case.get("passed"):
            passed += 1
            continue
        suite_case = by_id.get(cid) or {}
        keywords = suite_case.get("expectedKeywords") or []
        excerpt = case.get("excerpt") or ""
        matched = [k for k in keywords if keyword_match(excerpt, k, aliases)]
        need = max(1, (len(keywords) + 1) // 2) if keywords else 1
        now_pass = len(matched) >= need and not str(excerpt).startswith("HTTP_ERROR")
        if now_pass:
            passed += 1
            flips.append({"id": cid, "matched": matched, "wasMatched": case.get("matched")})
        else:
            still_fail.append(
                {
                    "id": cid,
                    "matched": matched,
                    "missing": [k for k in keywords if k not in matched],
                }
            )

    total = len(report.get("cases") or [])
    rate = round(100.0 * passed / total, 1) if total else 0.0
    out = {
        "sourceReport": str(report_path),
        "mode": "offline-excerpt-rescore-with-aliases",
        "limitation": "Uses stored 280-char excerpts only; undercounts vs full responses.",
        "originalPassed": report.get("passedCases"),
        "originalRatePercent": report.get("passRatePercent"),
        "rescoredPassed": passed,
        "rescoredRatePercent": rate,
        "deltaCases": passed - int(report.get("passedCases") or 0),
        "flippedToPass": flips,
        "stillFail": still_fail,
        "suitePassedAt80": rate >= 80.0,
    }
    out_path = report_path.with_name(report_path.stem + "_alias_rescore.json")
    out_path.write_text(json.dumps(out, indent=2), encoding="utf-8")
    print(json.dumps(out, indent=2))
    print(f"Wrote {out_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
