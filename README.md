# HolyChat

Self-hosted, end-to-end encrypted messaging app for a small, closed
group of friends. Built on the Matrix protocol (Synapse homeserver),
with a custom backend for invite-based registration and admin
management.

## Why

Third-party chat platforms are, by design, a trust exercise: you
hand your conversations to a company and hope they keep their
promises about privacy. The EU's regulatory direction on messaging
(the ongoing "Chat Control" debate) is a reminder that those
promises can shift under legal or political pressure - even if
today's version of the law (Chat Control 1.0, passed July 2026)
only covers voluntary scanning by platforms that already don't use
end-to-end encryption, and the version that could reach encrypted
apps (Chat Control 2.0 / CSAR) hasn't passed.

HolyChat sidesteps that trust exercise entirely: we run our own
server, use the same battle-tested, audited encryption family as
Signal (Matrix's Olm/Megolm), and never ask for a phone number or
email - so there's no account tied to a legal identity to hand over
in the first place. No message content is ever accessible to
anyone but the devices in the conversation, us included.

## Architecture

| Layer | What it is | Where |
|---|---|---|
| Messaging protocol & encryption | Matrix (Synapse homeserver) | `barreiroserver`, Docker, behind Cloudflare Tunnel at `matrix.holychat.cc` |
| Backend API | Auth (JWT + optional TOTP), invites, user management | `backend/HolyChat.Api` (ASP.NET Core + EF Core + SQL Server) |
| Client app | Flutter (Android + iOS) | `frontend/holychat_app` |

The Flutter app talks to **two** separate backends:
- The **Synapse homeserver** directly (via the Matrix Dart SDK) for
  everything chat-related: rooms, messages, encryption.
- **HolyChat.Api** for everything else: login, invites, account
  approval/admin actions.

Federation is disabled — this Synapse instance only ever talks to
itself. No messages leave our own server.

## Repository layout

```
backend/HolyChat.Api/      ASP.NET Core backend
frontend/holychat_app/     Flutter client
```

Each has its own `SETUP_*.md` files documenting individual features
as they were built.

## Status

Actively in development. Core backend (auth, invites, user
management, TOTP 2FA) is done. Frontend has login, Matrix session
handling, and a basic conversations/chat screen. See the repo's
Issues for what's tracked next.

## Local development

**Backend:** requires .NET 9 SDK and SQL Server (Developer edition
works). See `backend/HolyChat.Api/SETUP_*.md` for step-by-step setup
of each feature.

**Frontend:** requires Flutter SDK, Android toolchain (Rust +
Visual Studio Build Tools are also needed to compile
`flutter_vodozemac`'s native encryption bindings). See
`frontend/holychat_app/SETUP_*.md`.

Both connect to the same Synapse instance at
`https://matrix.holychat.cc`.