#!/usr/bin/env bash
# Idempotent agent bootstrap for Cursor Cloud / local VMs.
# Installs .NET 10 SDK (if missing), terraform, and PATH wiring.
set -euo pipefail

DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
TF_VERSION="${TF_VERSION:-1.9.8}"
BIN_DIR="$HOME/.local/bin"
mkdir -p "$DOTNET_ROOT" "$BIN_DIR"

export PATH="$DOTNET_ROOT:$BIN_DIR:$PATH"

need_dotnet=1
if command -v dotnet >/dev/null 2>&1; then
  if dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
    need_dotnet=0
  fi
fi

if [[ "$need_dotnet" -eq 1 ]]; then
  echo "[bootstrap] installing .NET 10 SDK into $DOTNET_ROOT"
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$DOTNET_ROOT"
fi

if ! command -v terraform >/dev/null 2>&1; then
  echo "[bootstrap] installing terraform $TF_VERSION"
  arch=$(uname -m)
  case "$arch" in
    x86_64|amd64) tf_arch=amd64 ;;
    aarch64|arm64) tf_arch=arm64 ;;
    *) echo "unsupported arch: $arch"; exit 1 ;;
  esac
  curl -fsSL "https://releases.hashicorp.com/terraform/${TF_VERSION}/terraform_${TF_VERSION}_linux_${tf_arch}.zip" -o /tmp/terraform.zip
  python3 - <<'PY'
import zipfile
zipfile.ZipFile('/tmp/terraform.zip').extract('terraform', '/tmp')
PY
  install -m 0755 /tmp/terraform "$BIN_DIR/terraform"
fi

# Persist PATH for interactive shells in this VM
profile_line='export PATH="$HOME/.dotnet:$HOME/.local/bin:$PATH"; export DOTNET_ROOT="$HOME/.dotnet"'
for f in "$HOME/.bashrc" "$HOME/.profile"; do
  touch "$f"
  if ! grep -q 'DOTNET_ROOT="$HOME/.dotnet"' "$f" 2>/dev/null; then
    echo "$profile_line" >> "$f"
  fi
done

echo "[bootstrap] dotnet=$(dotnet --version 2>/dev/null || true)"
echo "[bootstrap] terraform=$(terraform version -json 2>/dev/null | python3 -c 'import sys,json; print(json.load(sys.stdin)["terraform_version"])' 2>/dev/null || terraform version | head -1)"
echo "[bootstrap] done"
