#!/usr/bin/env bash
# Build and run the Document OCR Function App locally.
# Usage: scripts/run-functions.sh [--no-build] [-- <extra args passed to func start>]

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." &>/dev/null && pwd)"
PROJECT_DIR="$REPO_ROOT/src/DocumentOcr.Processor"

BUILD=1
EXTRA_ARGS=()
while [[ $# -gt 0 ]]; do
    case "$1" in
        --no-build) BUILD=0; shift ;;
        --) shift; EXTRA_ARGS=("$@"); break ;;
        *) EXTRA_ARGS+=("$1"); shift ;;
    esac
done

command -v dotnet >/dev/null || { echo "ERROR: .NET 10 SDK ('dotnet') not found in PATH." >&2; exit 1; }
command -v func   >/dev/null || { echo "ERROR: Azure Functions Core Tools v4 ('func') not found in PATH." >&2; exit 1; }

if [[ ! -f "$PROJECT_DIR/local.settings.json" ]]; then
    echo "WARN: $PROJECT_DIR/local.settings.json is missing. Copy local.settings.json.template and fill it in." >&2
fi

cd "$PROJECT_DIR"
if [[ "$BUILD" -eq 1 ]]; then
    echo "==> dotnet build $PROJECT_DIR"
    dotnet build
fi

echo "==> func start ${EXTRA_ARGS[*]:-}"
exec func start "${EXTRA_ARGS[@]}"
