#!/usr/bin/env bash
# Provisions the local Azurite and Cosmos DB emulators with the queues,
# blob containers, and Cosmos containers expected by DocumentOcr.
#
# Safe to re-run: every operation is idempotent. Invoked automatically by
# the devcontainer postCreateCommand and can be re-run manually with:
#     bash .devcontainer/post-create.sh
set -euo pipefail

# --- Well-known emulator credentials -----------------------------------------
# Azurite ships with a fixed dev account and key; both are public, well-known
# values intended ONLY for local development and never used in production.
AZURITE_CONN="DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;"
COSMOS_ENDPOINT="https://127.0.0.1:8081"
COSMOS_KEY="C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="

QUEUE_NAME="pdf-processing-queue"
BLOB_CONTAINERS=("uploaded-pdfs" "processed-documents")
COSMOS_DATABASE="DocumentOcrDb"
# Container name -> partition key path (must match production schema).
COSMOS_CONTAINERS=(
    "ProcessedDocuments:/identifier"
    "Operations:/id"
)

log() { printf '[post-create] %s\n' "$*"; }

# --- Wait for Azurite --------------------------------------------------------
log "Waiting for Azurite (blob endpoint http://127.0.0.1:10000)..."
for i in {1..60}; do
    if curl -sf -o /dev/null "http://127.0.0.1:10000/devstoreaccount1?comp=list" \
        || curl -s -o /dev/null -w '%{http_code}' "http://127.0.0.1:10000/" | grep -qE '^(200|400|403)$'; then
        log "Azurite is responsive."
        break
    fi
    sleep 2
    if [[ $i -eq 60 ]]; then
        log "ERROR: Azurite did not become ready in time."
        exit 1
    fi
done

# --- Wait for Cosmos emulator -----------------------------------------------
log "Waiting for Cosmos DB emulator (https://127.0.0.1:8081)..."
for i in {1..120}; do
    if curl -sk -o /dev/null -w '%{http_code}' "${COSMOS_ENDPOINT}/_explorer/emulator.pem" | grep -qE '^(200|404)$'; then
        log "Cosmos emulator is responsive."
        break
    fi
    sleep 3
    if [[ $i -eq 120 ]]; then
        log "WARNING: Cosmos emulator did not respond; container creation will be skipped."
        SKIP_COSMOS=1
    fi
done

# --- Trust the Cosmos emulator certificate (best-effort) --------------------
if command -v update-ca-certificates >/dev/null 2>&1; then
    if curl -sk "${COSMOS_ENDPOINT}/_explorer/emulator.pem" -o /tmp/cosmos-emulator.pem 2>/dev/null \
        && [[ -s /tmp/cosmos-emulator.pem ]]; then
        sudo cp /tmp/cosmos-emulator.pem /usr/local/share/ca-certificates/cosmos-emulator.crt 2>/dev/null || \
            cp /tmp/cosmos-emulator.pem /usr/local/share/ca-certificates/cosmos-emulator.crt 2>/dev/null || true
        sudo update-ca-certificates >/dev/null 2>&1 || update-ca-certificates >/dev/null 2>&1 || true
        log "Imported Cosmos emulator certificate into the system trust store."
    fi
fi

# --- Ensure az CLI is present ------------------------------------------------
if ! command -v az >/dev/null 2>&1; then
    log "ERROR: Azure CLI not found. Cannot provision Azurite resources."
    exit 1
fi

# --- Create blob containers + queue in Azurite ------------------------------
for container in "${BLOB_CONTAINERS[@]}"; do
    log "Creating blob container: ${container}"
    az storage container create \
        --name "${container}" \
        --connection-string "${AZURITE_CONN}" \
        --only-show-errors >/dev/null
done

log "Creating queue: ${QUEUE_NAME}"
az storage queue create \
    --name "${QUEUE_NAME}" \
    --connection-string "${AZURITE_CONN}" \
    --only-show-errors >/dev/null

# --- Create Cosmos database + containers ------------------------------------
if [[ "${SKIP_COSMOS:-0}" != "1" ]]; then
    log "Provisioning Cosmos database '${COSMOS_DATABASE}' and containers..."
    # The Azure CLI `az cosmosdb` commands target the ARM control plane, which
    # the emulator does not implement. Delegate to a small Python helper that
    # talks directly to the emulator's data-plane REST API.
    COSMOS_ENDPOINT="${COSMOS_ENDPOINT}" \
    COSMOS_KEY="${COSMOS_KEY}" \
    COSMOS_DATABASE="${COSMOS_DATABASE}" \
    COSMOS_CONTAINERS="${COSMOS_CONTAINERS[*]}" \
    python3 "$(dirname "$0")/provision-cosmos.py"
fi

log "Provisioning complete."
log "Azurite:  ${AZURITE_CONN}"
log "Cosmos:   ${COSMOS_ENDPOINT} (key: well-known emulator key)"
