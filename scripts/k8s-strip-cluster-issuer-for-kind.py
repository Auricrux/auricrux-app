#!/usr/bin/env python3
"""Strip cert-manager ClusterIssuer docs from k8s-ingress.yaml for kind dry-run."""
from __future__ import annotations

import re
from pathlib import Path

text = Path("k8s-ingress.yaml").read_text(encoding="utf-8")
docs = re.split(r"(?m)^---\s*$", text)
keep = [doc.strip() for doc in docs if doc.strip() and "kind: ClusterIssuer" not in doc]
Path("/tmp/k8s-ingress-no-crd.yaml").write_text("\n---\n".join(keep) + "\n", encoding="utf-8")
print(f"kept {len(keep)} docs (ClusterIssuer stripped for kind)")
