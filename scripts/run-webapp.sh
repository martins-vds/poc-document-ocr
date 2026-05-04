#!/usr/bin/env bash
# Build and run the Document OCR Blazor Web App locally.
# Usage: scripts/run-webapp.sh [--no-build] [--urls <url>] [-- <extra args passed to dotnet run>]

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." &>/dev/null && pwd)"
PROJECT_DIR="$REPO_ROOT/src/DocumentOcr.WebApp"

BUILD=1
URLS=""
EXTRA_ARGS=()
while [[ $# -gt 0 ]]; do
    case "$1" in
        --no-build) BUILD=0; shift ;;
        --urls)     URLS="$2"; shift 2 ;;
        --) shift; EXTRA_ARGS=("$@"); break ;;
        *) EXTRA_ARGS+=("$1"); shift ;;
    esac
done

command -v dotnet >/dev/null || { echo "ERROR: .NET 10 SDK ('dotnet') not found in PATH." >&2; exit 1; }

if [[ ! -f "$PROJECT_DIR/appsettings.Development.json" ]]; then
    echo "WARN: $PROJECT_DIR/appsettings.Development.json is missing. Copy appsettings.Development.json.template and fill it in." >&2
fi

cd "$PROJECT_DIR"
if [[ "$BUILD" -eq 1 ]]; then
    echo "==> dotnet build $PROJECT_DIR"
    dotnet build
fi

DOTNET_ARGS=(run --no-build)
if [[ "$BUILD" -eq 0 ]]; then
    DOTNET_ARGS=(run --no-build)
else
    DOTNET_ARGS=(run --no-build)
fi
if [[ -n "$URLS" ]]; then
    DOTNET_ARGS+=(--urls "$URLS")
fi

echo "==> dotnet ${DOTNET_ARGS[*]} ${EXTRA_ARGS[*]:-}"
exec dotnet "${DOTNET_ARGS[@]}" "${EXTRA_ARGS[@]}"
