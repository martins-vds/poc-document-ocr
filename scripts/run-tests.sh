#!/usr/bin/env bash
# Build and run the Document OCR unit-test suite.
# Usage: scripts/run-tests.sh [--no-build] [--filter <expr>] [--coverage]
#                             [-- <extra args passed to dotnet test>]

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." &>/dev/null && pwd)"
TEST_PROJECT="$REPO_ROOT/tests/DocumentOcr.Tests.csproj"

BUILD=1
FILTER=""
COVERAGE=0
EXTRA_ARGS=()
while [[ $# -gt 0 ]]; do
    case "$1" in
        --no-build) BUILD=0; shift ;;
        --filter)   FILTER="$2"; shift 2 ;;
        --coverage) COVERAGE=1; shift ;;
        --) shift; EXTRA_ARGS=("$@"); break ;;
        *) EXTRA_ARGS+=("$1"); shift ;;
    esac
done

command -v dotnet >/dev/null || { echo "ERROR: .NET 10 SDK ('dotnet') not found in PATH." >&2; exit 1; }

cd "$REPO_ROOT"

if [[ "$BUILD" -eq 1 ]]; then
    echo "==> dotnet build $TEST_PROJECT"
    dotnet build "$TEST_PROJECT"
fi

TEST_ARGS=("$TEST_PROJECT")
[[ "$BUILD" -eq 1 ]] && TEST_ARGS+=(--no-build)
[[ -n "$FILTER" ]] && TEST_ARGS+=(--filter "$FILTER")
if [[ "$COVERAGE" -eq 1 ]]; then
    TEST_ARGS+=(--collect "XPlat Code Coverage")
fi

echo "==> dotnet test ${TEST_ARGS[*]} ${EXTRA_ARGS[*]:-}"
exec dotnet test "${TEST_ARGS[@]}" "${EXTRA_ARGS[@]}"
