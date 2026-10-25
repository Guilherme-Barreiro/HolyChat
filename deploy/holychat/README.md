# HolyChat | Synapse deployment hardening

The production Synapse is already running at `/srv/holychat` on barreiroserver. This directory is **not** intended as an independent Docker Compose installation; its installer transforms the existing Compose and homeserver.yaml after making backups.

## Observed configuration (25 October 2026)
- Synapse 1.158.0; PostgreSQL 16; Cloudflare Tunnel active
- Synapse Docker mapping `0.0.0.0:8008:8008`, UFW inactive
- Public API status: Matrix client HTTP 200, shared-secret registration HTTP 200, federation HTTP 200
- Existing secret in `/srv/holychat/data/synapse/homeserver.yaml` (do not print it)
- No dedicated backup directory observed

## Installation (on server, after checkout/pull)
```bash
cd /home/barreiro/HolyChat
sudo bash deploy/holychat/apply.sh
sudo bash deploy/holychat/verify.sh
```

**Important:** This installation has not been executed on the production server. Check the changes in the PR and confirm access to a working backup/restore method beforehand. The installer performs a PostgreSQL logical dump and archives Synapse data including signing keys, preserves the original Compose, and aborts if preflight checks fail. It recreates the Synapse container briefly; existing messages and PostgreSQL data are not intentionally modified.

The installer changes host published ports to `127.0.0.1:8008` (public client-only Nginx reverse proxy) and `127.0.0.1:18008` (private administration), makes the Synapse container internal to Docker, and disables Matrix federation at listener level plus outbound federation. It will not rotate any existing credentials or pull an upgraded Synapse image.

**Cloudflare route precondition:** `matrix.holychat.cc` must point to `http://localhost:8008` **on this host**. Do not use this installer if cloudflared runs in a different network namespace or relies on a separate host interface. The 18008 port MUST NEVER be published by Cloudflare.

### Using private administration from Windows
```powershell
ssh -N -L 18008:127.0.0.1:18008 barreiro@barreiroserver
```
Configure `Synapse__AdminBaseUrl=http://127.0.0.1:18008` privately in the backend and `Synapse__RegistrationSharedSecret` through a local secret store. Do not commit that secret.

### Rollback
Backup paths will be printed during install. Restore the **configuration files** from the corresponding backup if necessary and recreate only Synapse, then verify access. Do not restore the PostgreSQL dump over a live database. Keep off-host copies of backups and test restoration on an isolated system.

### Limitations
- Blocking public admin and federation routes does not enforce MFA for direct Matrix logins.
- Synapse session revocation upon HolyChat suspension must be implemented independently.
- Registration can leave an orphaned Matrix account if the SQL transaction fails.
- The scripts are for the observed two-service layout, not general-purpose installers.
- The real production validation, encrypted messaging tests and backup restoration test remain to be done.
