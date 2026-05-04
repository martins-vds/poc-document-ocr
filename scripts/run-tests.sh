#!/usr/bin/env bash
# Build and run the Document OCR test suites.
#
# Usage: scripts/run-tests.sh [--unit | --integration | --all (default)]
#                             [--no-build] [--filter <expr>] [--coverage]
#                             [-- <extra args passed to dotnet test>]

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." &>/dev/null && pwd)"
UNIT_PROJECT="$REPO_ROOT/tests/DocumentOcr.UnitTests/DocumentOcr.UnitTests.csproj"
INTEGRATION_PROJECT="$REPO_ROOT/tests/DocumentOcr.IntegrationTests/DocumentOcr.IntegrationTests.csproj"

SCOPE="all"
BUILD=1
FILTER=""
COVERAGE=0
EXTRA_ARGS=()
while [[ $# -gt 0 ]]; do
    case "$1" in
        --unit)        SCOPE="unit"; shift ;;
        --integration) SCOPE="integration"; shift ;;
        --all)         SCOPE="all"; shift ;;
        --no-build)    BUILD=0; shift ;;
        --filter)      FILTER="$2"; shift 2 ;;
        --coverage)    COVERAGE=1; shift ;;
        --) shift; EXTRA_ARGS=("$@"); break ;;
        *) EXTRA_ARGS+=("$1"); shift ;;
    esac
done

command -v dotnet >/dev/null || { echo "ERROR: .NET 10 SDK ('dotnet') not found in PATH." >&2; exit 1; }

cd "$REPO_ROOT"

case "$SCOPE" in
    unit)        TARGETS=("$UNIT_PROJECT") ;;
    integration) TARGETS=("$INTEGRATION_PROJECT") ;;
    all)         TARGETS=("$UNIT_PROJECT" "$INTEGRATION_PROJECT") ;;
esac

for project in "${TARGETS[@]}"; do
    if [[ "$BUILD" -eq 1 ]]; then
        echo "==> dotnet build $project"
        dotnet build "$project"
    fi

    TEST_ARGS=("$project")
    [[ "$BUILD" -eq 1 ]] && TEST_ARGS+=(--no-build)
    [[ -n "$FILTER" ]] && TEST_ARGS+=(--filter "$FILTER")
    if [[ "$COVERAGE" -eq 1 ]]; then
        TEST_ARGS+=(--collect "XPlat Code Coverage")
    fi

    echo "==> dotnet test ${TEST_ARGS[*]} ${EXTRA_ARGS[*]:-}"
    dotnet test "${TEST_ARGS[@]}" "${EXTRA_ARGS[@]}"
done
