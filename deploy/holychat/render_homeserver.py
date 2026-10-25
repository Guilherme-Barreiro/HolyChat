"""Conservative edits only: refuse unfamiliar listener structures and preserve secrets."""
import re
import sys
import yaml

original = sys.stdin.read()
config = yaml.safe_load(original)
if not isinstance(config, dict) or config.get("server_name") != "holychat.cc":
    raise SystemExit("Unexpected Matrix server name")
if not config.get("registration_shared_secret") and not config.get("registration_shared_secret_path"):
    raise SystemExit("Shared-secret registration not configured")

for key in ("send_federation", "enable_registration", "allow_guest_access"):
    if key in config and config[key] not in (None, False):
        raise SystemExit(f"Unexpected explicit {key}: refusing to change")

# This pattern deliberately matches only the exact listener format seen on the server.
pattern = r"(?m)^([ \t]*-\s*names:\s*)\[\s*client\s*,\s*federation\s*\]([ \t]*(?:#.*)?)$"
changed, count = re.subn(pattern, r"\1[client]\2", original)
if count != 1:
    raise SystemExit("Unexpected listener shape; manual review required")

for key in ("send_federation", "enable_registration", "allow_guest_access"):
    matches = list(re.finditer(rf"(?m)^{re.escape(key)}:\s*[^\n]*$", changed))
    if len(matches) > 1:
        raise SystemExit(f"Duplicate root configuration entry: {key}")
    if matches:
        changed = changed[:matches[0].start()] + f"{key}: false" + changed[matches[0].end():]
    else:
        changed += f"\n{key}: false\n"

updated = yaml.safe_load(changed)
for key in ("send_federation", "enable_registration", "allow_guest_access"):
    if updated.get(key) is not False:
        raise SystemExit(f"Failed to disable {key}")
for listener in updated.get("listeners", []):
    for resource in listener.get("resources", []):
        if "federation" in resource.get("names", []):
            raise SystemExit("Federation listener still present")
assert updated.get("server_name") == config.get("server_name")
assert updated.get("registration_shared_secret") == config.get("registration_shared_secret")
sys.stdout.write(changed)
