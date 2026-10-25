#!/usr/bin/env bash
set -euo pipefail
assert="${1:-}"
ok=1
check() {
  local name="$1" url="$2" expected="$3" actual
  actual="$(curl --max-time 15 -sS -o /dev/null -w '%{http_code}' "$url" 2>/dev/null || true)"
  printf '%-23s HTTP %s (esperado %s)\n' "$name" "$actual" "$expected"
  [[ "$actual" == "$expected" ]] || ok=0
}
check cliente-publico https://matrix.holychat.cc/_matrix/client/versions 200
check admin-publico https://matrix.holychat.cc/_synapse/admin/v1/register 403
check federacao-publica https://matrix.holychat.cc/_matrix/federation/v1/version 403
check cliente-local http://127.0.0.1:8008/_matrix/client/versions 200
check admin-local http://127.0.0.1:8008/_synapse/admin/v1/register 403
check admin-privado http://127.0.0.1:18008/_synapse/admin/v1/register 200
[[ "$assert" != --assert || "$ok" == 1 ]]
