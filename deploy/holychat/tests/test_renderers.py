"""Non-production regression checks for the HolyChat configuration renderers."""
from pathlib import Path
import subprocess
import sys
import unittest

import yaml

ROOT = Path(__file__).resolve().parents[1]


def render(name: str, content: str):
    return subprocess.run(
        [sys.executable, str(ROOT / name)],
        input=content,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )


class DeploymentRendererTests(unittest.TestCase):
    def test_compose_removes_public_synapse_port(self):
        old = """
services:
  synapse:
    image: matrixdotorg/synapse:latest
    ports:
      - "8008:8008"
    volumes:
      - ./data/synapse:/data
  synapse-db:
    image: postgres:16
    volumes:
      - ./data/postgres:/var/lib/postgresql/data
"""
        result = render("render_compose.py", old)
        self.assertEqual(result.returncode, 0, result.stderr)
        new = yaml.safe_load(result.stdout)["services"]
        self.assertNotIn("ports", new["synapse"])
        self.assertEqual(new["holychat-proxy"]["ports"], [
            "127.0.0.1:8008:8080",
            "127.0.0.1:18008:8081",
        ])

    def test_refuses_unknown_synapse_port_layout(self):
        old = """
services:
  synapse:
    image: matrixdotorg/synapse:latest
    ports: ["8448:8448"]
  synapse-db:
    image: postgres:16
"""
        result = render("render_compose.py", old)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("Unexpected published port", result.stderr)

    def test_homeserver_disables_federation_without_changing_keys(self):
        before = """server_name: holychat.cc
registration_shared_secret: FAKE_TEST_VALUE_NOT_A_REAL_SECRET
listeners:
  - port: 8008
    type: http
    x_forwarded: true
    resources:
      - names: [client, federation]
        compress: false
"""
        result = render("render_homeserver.py", before)
        self.assertEqual(result.returncode, 0, result.stderr)
        new = yaml.safe_load(result.stdout)
        self.assertEqual(new["registration_shared_secret"], "FAKE_TEST_VALUE_NOT_A_REAL_SECRET")
        self.assertFalse(new["send_federation"])
        self.assertFalse(new["enable_registration"])
        self.assertFalse(new["allow_guest_access"])
        self.assertEqual(new["listeners"][0]["resources"][0]["names"], ["client"])

    def test_rejects_different_server(self):
        before = """server_name: someoneelse.org
registration_shared_secret: fake
listeners:
  - resources:
      - names: [client, federation]
"""
        self.assertNotEqual(render("render_homeserver.py", before).returncode, 0)


if __name__ == "__main__":
    unittest.main()
