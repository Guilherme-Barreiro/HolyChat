"""Adapt the observed HolyChat Compose without exposing its environment or secret values."""
import sys
import yaml

c = yaml.safe_load(sys.stdin.read())
if not isinstance(c, dict):
    raise SystemExit("Invalid Compose YAML")
services = c.get("services")
if not isinstance(services, dict) or "synapse" not in services or "synapse-db" not in services:
    raise SystemExit("Expected Synapse and PostgreSQL services")
if "holychat-proxy" in services:
    raise SystemExit("Proxy already exists; refusing to overwrite")
synapse = services["synapse"]
ports = synapse.get("ports")
if not isinstance(ports, list) or len(ports) != 1:
    raise SystemExit("Unexpected Synapse port count")
p = ports[0]
if isinstance(p, int):
    valid = p == 8008
elif isinstance(p, str):
    valid = p in ("8008:8008", "0.0.0.0:8008:8008", "[::]:8008:8008")
elif isinstance(p, dict):
    valid = str(p.get("published")) == "8008" and int(p.get("target", 0)) == 8008
else:
    valid = False
if not valid:
    raise SystemExit("Unexpected published port; refusing to edit")

synapse.pop("ports")
synapse["pull_policy"] = "never"
services["holychat-proxy"] = {
    "image": "nginx:1.28-alpine",
    "pull_policy": "never",
    "restart": "unless-stopped",
    "depends_on": {"synapse": {"condition": "service_healthy"}},
    "ports": ["127.0.0.1:8008:8080", "127.0.0.1:18008:8081"],
    "volumes": ["./proxy/default.conf:/etc/nginx/conf.d/default.conf:ro"],
}
print(yaml.safe_dump(c, sort_keys=False, allow_unicode=True))
