#!/usr/bin/env python3
"""Provision the local Cosmos DB emulator with the database and containers
required by DocumentOcr.

The Azure CLI ``az cosmosdb`` commands target the ARM control plane, which
the emulator does not implement, so this script talks directly to the
emulator's data-plane REST API.

Invoked by ``.devcontainer/post-create.sh`` with the following environment
variables:

- ``COSMOS_ENDPOINT`` — emulator gateway URL (e.g. ``https://127.0.0.1:8081``)
- ``COSMOS_KEY`` — well-known emulator master key (base64)
- ``COSMOS_DATABASE`` — database id to create
- ``COSMOS_CONTAINERS`` — whitespace-separated list of ``name:/partitionKey``
  specs (e.g. ``ProcessedDocuments:/identifier Operations:/id``)
"""
from __future__ import annotations

import base64
import datetime
import hashlib
import hmac
import json
import os
import ssl
import sys
import urllib.error
import urllib.parse
import urllib.request

ENDPOINT = os.environ["COSMOS_ENDPOINT"]
KEY = os.environ["COSMOS_KEY"]
DB = os.environ["COSMOS_DATABASE"]
CONTAINERS = [
    tuple(spec.split(":", 1))
    for spec in os.environ["COSMOS_CONTAINERS"].split()
]

# The emulator uses a self-signed certificate; trust is established at the
# system level by the calling shell script. Disable verification here so the
# script also works before/without that trust import.
_ctx = ssl.create_default_context()
_ctx.check_hostname = False
_ctx.verify_mode = ssl.CERT_NONE


def _auth_header(verb: str, resource_type: str, resource_id: str, date: str) -> str:
    text = f"{verb.lower()}\n{resource_type.lower()}\n{resource_id}\n{date.lower()}\n\n"
    sig = base64.b64encode(
        hmac.new(base64.b64decode(KEY), text.encode("utf-8"), hashlib.sha256).digest()
    ).decode()
    return urllib.parse.quote(f"type=master&ver=1.0&sig={sig}", safe="")


def _request(
    verb: str,
    path: str,
    resource_type: str,
    resource_id: str,
    body: dict | None = None,
    extra_headers: dict | None = None,
) -> tuple[int, bytes]:
    date = datetime.datetime.now(datetime.UTC).strftime("%a, %d %b %Y %H:%M:%S GMT")
    headers = {
        "Authorization": _auth_header(verb, resource_type, resource_id, date),
        "x-ms-date": date,
        "x-ms-version": "2018-12-31",
        "Accept": "application/json",
        "Content-Type": "application/json",
    }
    if extra_headers:
        headers.update(extra_headers)
    data = json.dumps(body).encode("utf-8") if body is not None else None
    req = urllib.request.Request(f"{ENDPOINT}{path}", data=data, headers=headers, method=verb)
    try:
        with urllib.request.urlopen(req, context=_ctx, timeout=30) as resp:
            return resp.status, resp.read()
    except urllib.error.HTTPError as e:
        return e.code, e.read()


def _log(message: str) -> None:
    print(f"[post-create]   {message}")


def main() -> int:
    status, body = _request("POST", "/dbs", "dbs", "", {"id": DB})
    if status in (201, 409):
        _log(f"database '{DB}': {'created' if status == 201 else 'already exists'}")
    else:
        _log(f"ERROR creating database: HTTP {status} {body!r}")
        return 1

    for name, pk in CONTAINERS:
        container_body = {"id": name, "partitionKey": {"paths": [pk], "kind": "Hash"}}
        status, resp = _request(
            "POST",
            f"/dbs/{DB}/colls",
            "colls",
            f"dbs/{DB}",
            container_body,
            extra_headers={"x-ms-offer-throughput": "400"},
        )
        if status in (201, 409):
            _log(
                f"container '{name}' (pk={pk}): "
                f"{'created' if status == 201 else 'already exists'}"
            )
        else:
            _log(f"ERROR creating container '{name}': HTTP {status} {resp!r}")
            return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
