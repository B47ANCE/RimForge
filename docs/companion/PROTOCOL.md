# Protocol v1

Transport is newline-delimited UTF-8 JSON over `RimForge.Agent.v1`.
Maximum envelope size: 1 MiB.

Message types:
- `rimforge.hello`
- `rimforge.hello.accepted` (reserved for duplex transport milestone)
- `rimforge.heartbeat`
- `rimforge.session.ended`

Every envelope contains protocol version, message type, GUID message ID,
session ID, UTC timestamp, and an object payload.
