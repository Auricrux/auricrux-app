#!/usr/bin/env python3
"""Generative construction_god suite against live product auricrux-fca (GGUF path)."""
from __future__ import annotations

import json
import time
import urllib.error
import urllib.request
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
SUITE = REPO / "eval" / "construction_god_suite_v1.json"
ALIASES = REPO / "eval" / "keyword_aliases_v1.json"
REPORTS = REPO / "eval" / "reports"
BASE = "https://auricrux.futurecontractorsofamerica.com"
THRESHOLD = 80.0


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
    suite = json.loads(SUITE.read_text(encoding="utf-8"))
    cases = suite["cases"]
    aliases = load_aliases()
    results = []
    passed = 0
    print(f"GGUF generative suite against {BASE} ({len(cases)} cases)", flush=True)

    for i, case in enumerate(cases):
        body = json.dumps(
            {
                "query": case["query"],
                "thinkingMode": 1,
                "searchScope": 2,
                "sessionId": f"gguf-{i}",
                "conversationHistory": [],
            }
        ).encode("utf-8")
        req = urllib.request.Request(
            f"{BASE}/api/chat?model=auricrux-fca",
            data=body,
            headers={"Content-Type": "application/json"},
            method="POST",
        )
        content = ""
        ok = False
        try:
            with urllib.request.urlopen(req, timeout=180) as resp:
                data = json.loads(resp.read().decode("utf-8"))
                content = data.get("content") or data.get("Content") or ""
                ok = bool(str(content).strip())
        except Exception as exc:  # noqa: BLE001 — record and continue suite
            content = f"HTTP_ERROR: {exc}"

        keywords = case.get("expectedKeywords") or []
        matched = [k for k in keywords if keyword_match(content, k, aliases)]
        need = max(1, (len(keywords) + 1) // 2)
        case_pass = ok and len(matched) >= need
        if case_pass:
            passed += 1
        mark = "PASS" if case_pass else "FAIL"
        print(f"[{mark}] {case['id']} {len(matched)}/{len(keywords)}", flush=True)
        results.append(
            {
                "id": case["id"],
                "category": case.get("category"),
                "passed": case_pass,
                "keywordsTotal": len(keywords),
                "keywordsMatched": len(matched),
                "matched": matched,
                "excerpt": content[:280],
            }
        )

    rate = round(100.0 * passed / len(cases), 1) if cases else 0.0
    suite_passed = rate >= THRESHOLD
    stamp = time.strftime("%Y-%m-%d")
    report = {
        "suiteId": suite["suiteId"],
        "mode": "gguf-generative-product-chat",
        "baseUrl": BASE,
        "model": "auricrux-fca",
        "runAtUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "keywordAliasPath": str(ALIASES) if ALIASES.is_file() else None,
        "totalCases": len(cases),
        "passedCases": passed,
        "passRatePercent": rate,
        "passThresholdPercent": THRESHOLD,
        "suitePassed": suite_passed,
        "cases": results,
    }
    REPORTS.mkdir(parents=True, exist_ok=True)
    json_path = REPORTS / f"construction_god_suite_gguf_generative_{stamp}.json"
    md_path = REPORTS / f"construction_god_suite_gguf_generative_{stamp}.md"
    json_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    md_path.write_text(
        f"# Construction god suite — GGUF generative ({stamp})\n\n"
        f"- Base: {BASE}\n"
        f"- Model: auricrux-fca\n"
        f"- Keyword aliases: {'enabled' if aliases else 'none'}\n"
        f"- Result: {passed}/{len(cases)} ({rate}%) — "
        f"{'PASS' if suite_passed else 'FAIL'} at >= {THRESHOLD}%\n"
        f"- JSON: {json_path}\n",
        encoding="utf-8",
    )
    print(f"Wrote {json_path}", flush=True)
    print(f"Suite {'PASS' if suite_passed else 'FAIL'} ({rate}%)", flush=True)
    return 0 if suite_passed else 1


if __name__ == "__main__":
    raise SystemExit(main())
