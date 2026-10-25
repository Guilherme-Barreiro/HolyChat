#!/usr/bin/env bash
set -Eeuo pipefail
umask 077
BASE=/srv/holychat
PKG="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE="$BASE/docker-compose.yml"
HOME_YAML="$BASE/data/synapse/homeserver.yaml"
BACKUPS="$BASE/backups"
[[ "$EUID" -eq 0 ]] || { echo "Executa com sudo"; exit 1; }
for f in "$COMPOSE" "$HOME_YAML" "$PKG/nginx.conf" "$PKG/render_compose.py" "$PKG/render_homeserver.py"; do
  [[ -f "$f" ]] || { echo "Falta: $f"; exit 1; }
done
for cmd in docker python3 curl tar gzip sha256sum; do command -v "$cmd" >/dev/null; done
[[ ! -e "$BASE/proxy/default.conf" ]] || { echo "Proxy ja existe, nao substituir"; exit 1; }
[[ "$(systemctl is-active cloudflared)" == active ]] || { echo "Cloudflared nao esta activo"; exit 1; }
container="$(docker compose -f "$COMPOSE" ps -q synapse)"
db="$(docker compose -f "$COMPOSE" ps -q synapse-db)"
[[ -n "$container" && -n "$db" ]] || { echo "Contentores nao encontrados"; exit 1; }
curl -fsS --max-time 15 -o /dev/null "https://matrix.holychat.cc/_matrix/client/versions"
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT
docker exec -i "$container" python3 -c "$(cat "$PKG/render_compose.py")" < "$COMPOSE" > "$STAGE/docker-compose.yml"
docker exec -i "$container" python3 -c "$(cat "$PKG/render_homeserver.py")" < "$HOME_YAML" > "$STAGE/homeserver.yaml"
docker compose -f "$STAGE/docker-compose.yml" --project-directory "$BASE" config --quiet
docker pull nginx:1.28-alpine
docker run --rm --network "$(docker inspect "$container" --format '{{range $name,$network := .NetworkSettings.Networks}}{{println $name}}{{end}}' | head -1)" -v "$PKG/nginx.conf:/etc/nginx/conf.d/default.conf:ro" nginx:1.28-alpine nginx -t
mkdir -p "$BACKUPS"; chmod 700 "$BACKUPS"
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
backup="$BACKUPS/$stamp"
mkdir -m 700 "$backup"
cp -p "$COMPOSE" "$backup/docker-compose.yml"
cp -p "$HOME_YAML" "$backup/homeserver.yaml"
[[ ! -f "$BASE/.env" ]] || cp -p "$BASE/.env" "$backup/.env"
docker exec "$db" sh -ec 'exec pg_dumpall -U "${POSTGRES_USER:-postgres}" --quote-all-identifiers' | gzip -1 > "$backup/postgres.sql.gz"
gzip -t "$backup/postgres.sql.gz"
tar -czf "$backup/synapse-data.tar.gz" -C "$BASE" data/synapse
tar -tzf "$backup/synapse-data.tar.gz" >/dev/null
sha256sum "$backup/postgres.sql.gz" "$backup/synapse-data.tar.gz" > "$backup/SHA256SUMS"
echo "Backup: $backup"
changed=0
restore() {
  # The new proxy owns host port 8008. Stop and remove it BEFORE
  # restoring the original Compose, otherwise the old Synapse fails
  # to bind the port and rollback silently leaves Matrix unavailable.
  trap - ERR
  local failed=0
  echo "Falha durante a actualizacao. A repor a configuracao anterior..." >&2
  docker compose -f "$COMPOSE" rm -f -s holychat-proxy || failed=1
  cp -p "$backup/docker-compose.yml" "$COMPOSE" || failed=1
  cp -p "$backup/homeserver.yaml" "$HOME_YAML" || failed=1
  if [[ "$failed" == 0 ]]; then
    docker compose -f "$COMPOSE" up -d --no-deps --pull never --force-recreate synapse || failed=1
  fi
  if [[ "$failed" == 0 ]]; then
    local available=0
    for i in $(seq 1 60); do
      if curl --max-time 5 -fsS -o /dev/null http://127.0.0.1:8008/_matrix/client/versions; then
        available=1
        break
      fi
      sleep 2
    done
    [[ "$available" == 1 ]] || failed=1
  fi
  if [[ "$failed" == 0 ]]; then
    rm -f "$BASE/proxy/default.conf"
    rmdir "$BASE/proxy" 2>/dev/null || true
    echo "Configuracao reposta; Synapse voltou a responder na porta 8008." >&2
  else
    echo "ATENCAO: a reversao automatica nao ficou validada!" >&2
    echo "Verificar o backup em: $backup" >&2
    echo "Nao restaurar a base de dados sobre uma instancia em funcionamento." >&2
  fi
}
trap 'if [[ "$changed" == 1 ]]; then restore; fi' ERR
mkdir -p "$BASE/proxy"
cp "$PKG/nginx.conf" "$BASE/proxy/default.conf"
cp "$STAGE/docker-compose.yml" "$COMPOSE"
cp "$STAGE/homeserver.yaml" "$HOME_YAML"
changed=1
docker compose -f "$COMPOSE" up -d --no-deps --pull never --force-recreate synapse
for i in $(seq 1 60); do
  state="$(docker inspect "$(docker compose -f "$COMPOSE" ps -q synapse)" --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' 2>/dev/null || true)"
  [[ "$state" == healthy ]] && break
  sleep 2
done
[[ "$state" == healthy ]] || { echo "Synapse indisponivel"; exit 1; }
docker compose -f "$COMPOSE" up -d --no-deps --pull never holychat-proxy
bash "$PKG/verify.sh" --assert
changed=0
trap - ERR
echo "Concluido. Backup guardado em $backup."
