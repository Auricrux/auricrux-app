"""Probe Google Play Developer API; set GITHUB_OUTPUT ready=true when uploads are allowed."""
from __future__ import annotations

import json
import os
import sys

import google.auth.transport.requests
import requests
from google.oauth2 import service_account

PACKAGE_NAME = os.environ.get("PACKAGE_NAME", "com.fca.auricrux")
SA_JSON = os.environ.get("GOOGLE_PLAY_SERVICE_ACCOUNT_JSON", "")
GITHUB_OUTPUT = os.environ.get("GITHUB_OUTPUT", "")


def main() -> int:
    ready = "false"
    if SA_JSON and PACKAGE_NAME:
        info = json.loads(SA_JSON)
        creds = service_account.Credentials.from_service_account_info(
            info,
            scopes=["https://www.googleapis.com/auth/androidpublisher"],
        )
        creds.refresh(google.auth.transport.requests.Request())
        url = (
            "https://androidpublisher.googleapis.com/androidpublisher/v3/"
            f"applications/{PACKAGE_NAME}/edits"
        )
        resp = requests.post(
            url,
            headers={"Authorization": f"Bearer {creds.token}"},
            json={},
            timeout=60,
        )
        if resp.status_code == 200:
            ready = "true"
        print(f"Play API probe for {PACKAGE_NAME}: {resp.status_code}")
    else:
        print("Play probe skipped: missing PACKAGE_NAME or service account JSON")

    if GITHUB_OUTPUT:
        with open(GITHUB_OUTPUT, "a", encoding="utf-8") as handle:
            handle.write(f"ready={ready}\n")

    print(f"ready={ready}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
