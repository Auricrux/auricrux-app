#!/usr/bin/env python3
"""Atlas health smoke for CI. Never prints the connection string."""

from __future__ import annotations

import os
import sys


def main() -> int:
    conn = os.environ.get("ATLAS_CONNECTION_STRING", "").strip()
    db_name = os.environ.get("ATLAS_DATABASE", "auricrux").strip() or "auricrux"

    if not conn:
        print("atlas_health configured=false reason=missing_secret")
        return 1

    # Redact before any accidental logging
    safe_len = len(conn)
    print(f"atlas_health secret_present=true secret_len={safe_len}")

    try:
        from pymongo import MongoClient
        from pymongo.errors import PyMongoError
    except ImportError as exc:
        print(f"atlas_health configured=true healthy=false reason=pymongo_missing detail={exc}")
        return 1

    try:
        client = MongoClient(conn, serverSelectionTimeoutMS=8000, connectTimeoutMS=8000)
        client.admin.command("ping")
        db = client[db_name]
        collections = sorted(db.list_collection_names())
        print(
            "atlas_health configured=true healthy=true "
            f"database={db_name} collection_count={len(collections)}"
        )
        # Print only collection names — never documents
        if collections:
            print("atlas_health collections=" + ",".join(collections[:40]))
        return 0
    except PyMongoError as exc:
        # Do not include connection string fragments
        msg = type(exc).__name__
        print(f"atlas_health configured=true healthy=false reason={msg}")
        return 2
    except Exception as exc:  # noqa: BLE001
        print(f"atlas_health configured=true healthy=false reason={type(exc).__name__}")
        return 2


if __name__ == "__main__":
    sys.exit(main())
