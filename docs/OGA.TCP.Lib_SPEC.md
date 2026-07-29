# OGA.TCP.Lib — Specification

**Project:** OGA.TCP.Lib
**Short description:** A message-based TCP connectivity library for .NET, providing framed JSON message exchange, channels, large-message chunking, keepalive, and client registration between processes.
**Status:** Draft
**Revision:** 1
**Template Revision:** 3
**Created:** 2026-07-26T00:00:00Z
**Related Documents:** references/SPEC_TEMPLATE_R3.md, references/IMPLEMENTATION_GUIDE_TEMPLATE_R1.md, ../README.md

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Functional Requirements](#2-functional-requirements)
3. [Non-Functional Requirements](#3-non-functional-requirements)
4. [Constraints and Technology Decisions](#4-constraints-and-technology-decisions)
5. [Architectural Overview](#5-architectural-overview)
6. [Data Model](#6-data-model)
7. [Solution Structure](#7-solution-structure)
8. [Key Interfaces and Contracts](#8-key-interfaces-and-contracts)
9. [Protocols and Flows](#9-protocols-and-flows)
10. [API Surface](#10-api-surface)
11. [Infrastructure](#11-infrastructure)
12. [Key Decisions](#12-key-decisions)
13. [Open Items](#13-open-items)

---

## 1. Project Overview

### 1.1 Background

OGA.TCP.Lib is an in-service library used for message-based TCP connectivity between processes. It is published as two NuGet packages — OGA.TCP.Lib (client) and OGA.TCP.Server.Lib (server) — built from a common set of Visual Studio Shared Projects and compiled into multiple .NET target frameworks (client: NET Framework 4.5.2 through NET 8; server: NET 5 through NET 8). Packages are versioned and published by a Jenkins pipeline to a private BaGet feed.

The library predates, and is a sibling of, a separate WebSocket library maintained by the same author. During an earlier harmonization effort, some WebSocket source files were pasted into this repository; they are not used by this library and are slated for removal (see OI-01). The two libraries deliberately share a common session-layer design: a message envelope, registration handshake, channels, chunking, and keepalive semantics.

This specification is being reverse-engineered from the working code as the baseline for a phased improvement effort: a code review, added test coverage, consumer documentation, a .NET 8 compile target, and a designed binary-transmission capability. Because the library is in live service, all work is incremental, wire-compatible where possible, and captured here before implementation.

### 1.2 Purpose

The library exists so that applications can exchange typed, routed messages over plain TCP between processes with minimal ceremony: a consumer implements or instantiates a client, points it at a listening server, and sends/receives POCOs without writing framing, serialization, reconnect, or connection-tracking logic.

### 1.3 Scope

This specification covers:

- The client library (OGA.TCP.Lib): the abstract session-layer client, the TCP transport client, channel adapters, reconnect/backoff behavior, and client-side configuration.
- The server library (OGA.TCP.Server.Lib): the per-connection endpoint, the TCP listener, and the connection manager that tracks registered clients and their properties.
- The shared wire protocol: framing, the message envelope, internal message types, the registration handshake, keepalive, and the large-message chunking protocol.
- The library-version (LibVersion) scheme governing client/server compatibility.

WebSocket connectivity is explicitly excluded (see §1.7).

### 1.4 Problem Statement

The problem is sufficiently described by §1.1 and §1.2. The heading is retained to show the section was considered. (In brief: raw TCP offers a byte stream, not messages; every consuming application would otherwise re-implement framing, serialization, typed dispatch, keepalive, reconnect, and server-side connection tracking.)

### 1.5 Goals

The library lets two processes hold a long-lived, message-oriented TCP connection that behaves like a typed message bus. A sender hands the library a POCO; the receiver gets that POCO's JSON with enough envelope metadata (message type name, channel, scope) to route it to the right handler. Named channels can be added at runtime via channel adapters, so independent features of an application can share one connection ("share the pipe") without interfering with one another.

The connection is self-maintaining. The client runs a connection loop that establishes the connection, performs a registration handshake (so the server can track who is connected, from what device and process, with what capabilities), exchanges application-level ping/pong keepalives, detects dead peers on both sides, and reconnects with exponential backoff and jitter when the transport drops. Waits and timeouts for the various failure modes are configurable.

Messages larger than the frame limit are transparently chunked into a sequence of ordinary envelope messages and reassembled on the far side, so one large transfer does not monopolize the connection and other channels can interleave traffic.

### 1.6 User Requirements

**UR-01 — Typed message exchange.** A consuming application SHALL be able to send a serializable object to its peer and have the peer receive it with the original type name available for dispatch.

**UR-02 — Named channels.** A consuming application SHALL be able to register named channel handlers (channel adapters) at runtime, and messages tagged with a channel SHALL be delivered to that channel's handler.

**UR-03 — Self-maintaining connection.** A client SHALL maintain its connection without consumer intervention: reconnecting with exponential backoff and jitter on loss, and surfacing connection-state changes via delegates.

**UR-04 — Client registration.** The server SHALL track each connected client, and the client SHALL identify itself at connect time (connection id, user id, device id, process id, runtime id, library version, and other properties) so server-side logic can enumerate and address known connections.

**UR-05 — Keepalive and dead-peer detection.** Both ends SHALL detect an unresponsive peer within a configurable interval, using application-level ping/pong messages, with keepalive optionally disabled for debugging.

**UR-06 — Large-message chunking.** The library SHALL transparently split messages exceeding the maximum frame size into chunks and reassemble them at the receiver, so large transfers share the pipe with other channels.

**UR-07 — Configurable failure-mode timing.** A consumer SHALL be able to configure the delays and timeouts governing connect attempts, registration replies, keepalive intervals, dead-peer declaration, and chunk-receiver staleness.

**UR-08 — Multi-framework availability.** The client library SHALL be consumable from NET Framework 4.5.2, NET Framework 4.8, NET Standard 2.1, and NET 5/6/7/8 applications; the server library from NET 5/6/7/8 applications.

### 1.7 Non-Goals

- **WebSocket connectivity.** Handled by a separate WebSocket library. WebSocket source files present in this repository (`WSClient_v1_Abstract.cs`, `WSEndpoint.cs`, `WSConnectionMgr_Base.cs`) are unused here and pending removal (OI-01).
- **Broker/queue semantics.** The library is point-to-point client/server connectivity; it is not a message broker, has no persistence, and offers no delivery guarantees beyond the TCP session.
- **Transport security.** The library does not itself provide TLS or message encryption; securing the channel is the deployment's concern.

### 1.8 Glossary

- **Envelope (MessageEnvelope)** — The JSON wire wrapper carrying every message: id, sent-time, message type name, payload (as a JSON string), scope, channel, reply-to, and property strings. See §9 (pending).
- **Channel** — A named routing tag on an envelope; delivered to the matching channel adapter or channel delegate on the receiver.
- **Scope** — An envelope routing field for connection-scoped behaviors (e.g., loopback modes).
- **Channel adapter** — A consumer-side handler object bound to a channel name, registerable at runtime.
- **Registration** — The handshake in which a client sends a ConnRegisterDTO identifying itself, and the server replies with a ConnRegisterReplyDTO carrying the server-assigned connection id.
- **Chunking** — The library's scheme for splitting an oversized message into ChunkStart/Chunk/ChunkEnd envelope messages and reassembling them at the receiver.
- **LibVersion** — The wire-protocol/client-behavior version ("1" or "2") declared by a client at registration; governs feature expectations between client and server. Defined in `LibVersions.cs`.
- **Connection loop** — The client's long-running async loop that connects, registers, monitors, and reconnects.
- **Receive loop (cReceiveLoop)** — The shared TCP frame-reading state machine: 4-byte length prefix, then payload bytes, then dispatch of the JSON string.

### 1.9 Spec Conventions

This spec adheres to the conventions defined in `SPEC_TEMPLATE.md` at the revision identified by the `Template Revision` field in this spec's title block. See the Conventions section of that document for identifier rules, number stability, terminal item disposition (tombstoning), requirement language, table usage, revision-bump triggers, the forward-looking-reference principle, and other conventions governing this spec.

The conventions are not duplicated here. The template is the single source of truth for the rules a spec follows; this spec references the template revision it was authored under, and migrations to newer template revisions are deliberate.

---

## 2. Functional Requirements

Pending: to be authored during the reverse-engineering pass over the session layer, server endpoint, chunking, registration, and channel-adapter functionality (OI-14). The functional areas to be covered are enumerated in §1.6.

---

## 3. Non-Functional Requirements

Pending: to be authored during the reverse-engineering pass (OI-14). Known candidate topics: the 1 MiB frame ceiling and chunking throughput; reconnect timing envelopes; logging via the OGA logging conventions; thread-safety guarantees of the send path; observability counters in `cEndpoint_Metrics`.

### 3.1 Performance

Pending (OI-14).

### 3.2 Reliability and Availability

Pending (OI-14).

### 3.3 Security

Not applicable beyond §1.7's exclusion of transport security: the library trusts its host environment, carries an auth-level enum and auth-token hook for consumers, and imposes no trust boundary of its own. To be confirmed during the review pass (OI-14).

### 3.4 Observability

Pending (OI-14).

### 3.5 Accessibility

Not applicable: this is a headless connectivity library with no user interface.

### 3.6 Regulatory and Compliance

Not applicable: the library carries application payloads opaquely and imposes no data handling of its own; regulatory posture belongs to consuming applications.

---

## 4. Constraints and Technology Decisions

### 4.1 Tech Stack

C# on .NET, multi-targeted (client: net452, net48, netstandard2.1, net5.0, net6.0, net7.0, net8.0; server: net5.0, net6.0, net7.0, net8.0) from Visual Studio Shared Projects. JSON serialization via Newtonsoft.Json. Logging via NLog through OGA logging conventions. Build/publish via Jenkins to a private BaGet feed; versioning via GitVersion with `+semver:` commit-message bumps. Full inventory and rationale to be completed during the reverse-engineering pass (OI-14).

### 4.2 Library Dependencies

**Newtonsoft.Json 13.x** — Role: serialization of the MessageEnvelope and all payload DTOs. Rationale: required for net452 reach; the wire format is Newtonsoft-shaped. Substitution would be a wire-compatibility risk and is not planned.

**NLog 5.x** — Role: logging backend used via OGA.SharedKernel logging. 

**OGA.SharedKernel 3.x** — Role: shared logging/base conventions across OGA libraries. (Not referenced by the netstandard2.1 client target.)

**Server-only:** NUlid (connection id generation), OGA.Common.Lib.NetCore (async semaphore and common utilities), OpenTelemetry (metrics/tracing hooks).

Versions to be confirmed and rationale expanded during the reverse-engineering pass (OI-14).

### 4.3 Environment Constraints

The library runs wherever its target frameworks run; Jenkins builds produce win and linux runtime outputs. No other environment assumptions are known at this revision; to be confirmed (OI-14).

### 4.4 Repository Structure

```
OGA.TCP.Lib gh/
  OGA.TCP.Lib/                          main solution folder
    OGA.TCP.ClientServerShared_SP/      shared project: envelope, framing, chunking, serializer, metrics
    OGA.TCP.Lib_SP/                     shared project: client session layer, channel adapters
    OGA.TCP.Server.Lib_SP/              shared project: server endpoint, listener, connection manager
    OGA.TCP_Test_SP/                    shared project: client-side tests
    OGA.TCP.Server.Lib_Tests_SP/        shared project: server-side tests
    Testing_CommonHelpers_SP/           shared project: test helpers (incl. forked TESTINGSRVR_* server copies)
    OGA.TCP.Lib_NET452 ... _NET8/       client target csprojs importing the shared projects
    OGA.TCP.Server.Lib_NET5 ... _NET8/  server target csprojs
    *_Test / *_Tests projects           per-TFM test csprojs
    OGA.TCP.Lib.nuspec                  client package definition
    OGA.TCP.Server.Lib.nuspec           server package definition
    Jenkinsfile                         build/version/publish pipeline
    scratchdev/                         scratch console project (see OI-09)
  OGA.TCP.Lib_forNET452Test/            isolated solution for NET452 MSTest compatibility
  docs/                                 this spec and related documents
    references/                         document templates
```

### 4.5 Non-Requirements (Explicit Exclusions)

- **No rewrite.** The library is in live service; improvements are incremental and preserve the public API surface and wire compatibility unless a change is explicitly decided in a KD.
- **No serializer migration.** No move from Newtonsoft.Json to System.Text.Json in this effort; the wire format stays Newtonsoft-shaped.
- **No new transports.** Beyond the planned binary-transmission capability (OI-13, OI-39), no additional transports are in scope.

---

## 5. Architectural Overview

Pending: to be authored during the reverse-engineering pass (OI-14). In brief, for orientation: an abstract session-layer client (`Client_v1_Abstract`) is specialized by a TCP transport client (`TCPClient_v1_Abstract` → `TCPClient_v1_Impl`); the server mirrors this with a per-connection `Endpoint_Abstract` → `TCPEndpoint`, fed by a `cListener` accept loop, with `ConnectionMgr_Abstract`-derived managers tracking registered connections. Both sides share the envelope, framing (`cReceiveLoop`), and chunking code from the shared project.

### 5.1 Tiers and Components

Pending (OI-14).

### 5.2 Data Flow

Pending (OI-14).

---

## 6. Data Model

Pending: to be authored during the reverse-engineering pass (OI-14). Principal types known at this revision: `MessageEnvelope`, `ConnRegisterDTO`/`ConnRegisterReplyDTO`, chunking DTOs (`ChunkStartDTO`, `ChunkDTO`, `ChunkEndDTO`, plus unused Ack/Request/Cancel variants — see OI-15), `ClientInfo`, `ConnectionEntry_v1`.

### 6.1 Entity Overview

Pending (OI-14).

### 6.2 Schema

Pending (OI-14).

### 6.3 Identifiers

Pending (OI-14). Known at this revision: client generates a GUID connection id at connect; server assigns the authoritative connection id (NUlid-based) in the registration reply; envelope MsgIds are integer strings from a process-global interlocked counter.

### 6.4 Versioning and Soft Delete

Not applicable in the persistence sense: the library holds no durable state. Wire-protocol versioning (LibVersion) is a protocol concern, covered in §9 (pending).

### 6.5 Data Retention

Not applicable: the library persists nothing. In-memory lifecycle concerns (stale chunk receivers, unregistered-connection reaping) are runtime behaviors, covered in §9 (pending).

---

## 7. Solution Structure

Pending: to be authored during the reverse-engineering pass (OI-14). The shared-project composition is outlined in §4.4; the reference rules between shared projects and target csprojs will be documented here, including which shared projects each package imports.

### 7.1 Libraries and Projects Overview

Pending (OI-14).

### 7.2 Library Boundary Rationale

Pending (OI-14).

### 7.3 Reference Rules

Pending (OI-14).

---

## 8. Key Interfaces and Contracts

Pending: to be authored during the reverse-engineering pass (OI-14). Known seams: `Client_v1_Abstract.RawTransportSend` (transport abstraction), `IChannelAdapter` (channel handling), delegate surfaces for message/status/binary-frame events, `ConnectionMgr_Abstract` hooks.

### 8.1 Internal Interfaces

Pending (OI-14).

### 8.2 External Contracts

Pending (OI-14). The wire protocol itself is the library's principal external contract; it is versioned by LibVersion and must remain backward compatible across this improvement effort.

### 8.3 DTOs and Wire Types

Pending (OI-14).

---

## 9. Protocols and Flows

Pending: to be authored during the reverse-engineering pass (OI-14). Protocols to document: TCP framing (4-byte little-endian length prefix + UTF-8 JSON envelope, 1 MiB ceiling), envelope semantics, registration handshake, keepalive ping/pong, large-message chunking, LibVersion 1 vs 2 differences, and the planned binary-transmission protocol once designed (OI-13).

### 9.1 Protocols

Pending (OI-14).

### 9.2 Architectural Data Flows

Pending (OI-14).

### 9.3 User Workflow Narration

Not applicable: the library has no end-user workflows; consumer-facing usage flows belong to the implementation guide (OI-16).

---

## 10. API Surface

Not applicable in the HTTP sense: this is a library distributed as NuGet packages; its surface is the public API of its types, to be documented in §8 (pending, OI-14) and in the consumer implementation guide (OI-16).

---

## 11. Infrastructure

Not applicable as deployment topology: this is a NuGet-distributed library; deployment shape is the consumer's concern. Build/publish infrastructure (Jenkins pipeline, GitVersion, BaGet feed) is part of this repository's operational surface and its known issues are captured in OI-08.

---

## 12. Key Decisions

### KD-01 — Packages ship Debug-configuration binaries with PDBs

**Decision.** The published NuGet packages are built in Debug configuration (Debug/DebugWin/DebugLinux), and PDB files are included in the packages.

**Rationale.** Runtime logging across the OGA libraries includes class/file/line-number metadata, which is resolved from PDBs. Shipping Debug builds with PDBs keeps that metadata accurate in live service. No suitable alternative for obtaining logging metadata without PDBs has been identified.

**Alternatives considered.** Release-configuration builds also emit PDBs, but JIT optimization (inlining, reordering) degrades the accuracy of file/line resolution; Debug builds keep line fidelity at the cost of optimization.

**Consequences.** Build pipeline stages and nuspec file entries intentionally reference Debug output paths; this is not a defect. Any future change to Release packaging would need to re-validate logging metadata quality first.

### KD-02 — Tests run outside the build/publish pipeline

**Decision.** The build/publish Jenkins job does not run unit or integration tests. Tests are executed in a separate Jenkins job, and during development.

**Rationale.** The publish pipeline stays focused on versioning, build, packaging, and feed publication; test execution has its own job with its own cadence, and the test suites are also exercised during development.

**Consequences.** Publication is not gated by the test suites; regressions must be caught by the dev-time runs and the separate test job. The absence of a `dotnet test` stage in the Jenkinsfile is intentional, not a defect.

### KD-03 — TCP frames carry a five-byte preamble: length plus frame type

**Decision.** The TCP frame becomes a five-byte preamble — the existing 4-byte little-endian body length followed by a 1-byte frame type — then the body. The length counts body bytes only (the preamble is excluded). Consecutive frames on one connection may carry different types. Frame type values are enumerated in a static registry class in the shared project (`OGA.TCP.ClientServerShared_SP`), each value documented narratively; values are only ever added, never repurposed. Value 0 is reserved as invalid (a zeroed buffer must not read as a valid frame), 1 identifies a JSON `MessageEnvelope` body, 2 identifies a binary message body (KD-04), and 123 (0x7B) is permanently reserved as the legacy-framing detector (KD-07). The registry class is `OGA.TCP.FrameTypes` in `OGA.TCP.ClientServerShared_SP\FrameTypes.cs`.

**Rationale.** The current frame has no type discriminator — the receiver's only interpretation is "UTF-8 JSON envelope" — so binary has nowhere to live. A type byte supplies the discrimination WebSocket gets natively from its Text/Binary opcode, while leaving a JSON frame's body byte-for-byte identical to today's traffic, so the entire session layer above the framing is unchanged. One byte yields 256 frame kinds, making the registry itself the future extension mechanism (compact headers, control frames) without another wire migration.

**Alternatives considered.** A sentinel/negative length as the discriminator (rejected: overloads the length semantics and reads poorly in diagnostics); folding the type byte into the length count (rejected: explicit preamble is easier to reason about and log); base64 binary inside the existing envelope (rejected by the owner: ~33% bandwidth cost, worsened by JSON escaping).

**Consequences.** This is a wire-format break with current peers. Per the owner, no mixed old/new client–server pair will exist — both ends of a conversation are upgraded together — so no on-wire negotiation or fallback is designed; the LibVersion registration mechanism remains available if a guard is later wanted (OI-39). The receive state machine in `cReceiveLoop` must be reworked (the natural moment to resolve OI-20 through OI-23), and the `RawTransportSend` seam on `Client_v1_Abstract` changes shape, which touches the WebSocket library through the shared base.

### KD-04 — Routing metadata stays at the message layer; binary bodies carry an envelope-like header

**Decision.** The frame preamble carries no routing information. JSON frame bodies remain today's `MessageEnvelope`, unchanged. A binary frame body is `[Int32 header length][UTF-8 JSON header][raw payload bytes]`, where the header carries the same routing fields the envelope carries — channel, scope, message type, message id, correlation props — and the payload follows as raw bytes outside the JSON.

**Rationale.** Routing is a session-layer concern consumed by dispatch, not by the read pump; keeping it in the message preserves the byte-identical JSON path and feeds one dispatch code path from either body type. Placing the payload outside the JSON removes any base64/escaping tax. Because the layout above the transport framing is self-contained, the WebSocket library can reuse the identical binary body layout inside its native binary frames (which need no preamble), keeping the message model shared between the two libraries even though the TCP preamble is TCP-only.

**Alternatives considered.** A shared routing header at the frame level (rejected: restructures every live JSON message, and the WebSocket transport has no frame preamble to put it in); a binary-encoded compact header via `cCustom_Serializer` (rejected for the initial version: header overhead is noise against typical binary payload sizes, JSON keeps messages debuggable, and the codec currently carries defects per OI-32 — the frame-type registry leaves room for a compact-header frame type later if profiling justifies it).

**Consequences.** The binary header needs its own DTO (envelope-minus-Data shape). Dispatch, channel adapters, and correlation handling extend to accept a byte-payload variant fed by the same routing fields (OI-13). Whether a channel carries JSON or binary is the consumer's convention; the library routes both without enforcement.

### KD-05 — Chunking is rebuilt as a byte-based, frame-type-agnostic splitter

**Decision.** The chunking layer is rebuilt to operate on the encoded frame body as opaque bytes, below any interpretation of those bytes. When a message's encoded body (of either frame type) exceeds the size cap, the sender splits the bytes; the receiver reassembles them and re-injects `(innerFrameType, reassembledBytes)` into the top of the normal receive-processing path, making a chunked message indistinguishable from one that arrived in a single frame.

Chunk **control** messages — `ChunkStartDTO`, `ChunkEndDTO`, `ChunkCancelDTO` — are JSON envelopes (internal messages), reshaped to carry an explicit `TransferId`, byte-true sizes (`TotalSize`, chunk size, chunk count), and the **inner frame type** of the original message, so the receiver interprets the reassembled bytes correctly. Chunk **data** rides binary messages: the routing header identifies the chunk-data message type and carries the `TransferId` and offset; the payload is the raw byte slice. `ChunkDTO` (the JSON string-slice carrier) retires. The protocol has no ack or retransmission: TCP and WebSocket are reliable ordered transports, and backpressure exists because the sender awaits each frame through the write semaphore.

Reassembly lives in the session layer at the existing chunking intercept step, keyed by `TransferId`. Completeness is enforced at ChunkEnd by byte count and chunk count. Cancel is wired into the intercept (it is dead code today). Concurrent transfers on one connection interleave, keyed by `TransferId`; traffic from other channels interleaves between chunk frames (the share-the-pipe property); chunks within a transfer are strictly in-order, with the offset check serving as a corruption guard rather than a reordering mechanism. A reassembled body that itself parses to chunk messages is rejected as a protocol error.

**Rationale.** Byte-based splitting makes the worst existing chunking defects unexpressible rather than fixed: the char-vs-byte cap overrun, the surrogate-pair corruption, and the double-escaping expansion (OI-18, OI-19) all stem from slicing strings. Carrying data slices as binary messages avoids any base64 tax. Keeping the mechanism entirely at the message layer means one implementation serves both transports — over WebSocket, the identical chunk messages ride native Binary frames — and the TCP receive loop stays chunking-unaware. There is no wire legacy to honor: no live WebSocket deployment chunks, and TCP chunking is encapsulated inside the endpoint/client abstraction with both ends of each use case updated together.

**Alternatives considered.** A dedicated chunk-data frame type in the registry (rejected: WebSocket has no frame registry, so it would need a message-layer discriminator anyway — the same question answered at different layers per transport, recreating the hand-mirrored-drift pattern; and registry values are permanent, the wrong place for session-protocol concepts that may evolve). All-binary-native control messages (rejected: control is tiny and per-transfer so there is nothing to save, and under KD-04 a metadata-only binary message degenerates into an envelope-equivalent while abandoning the existing internal-message machinery). Base64 chunk data inside JSON envelopes (rejected: the encoding tax chunking exists to avoid). An ack/request handshake (rejected: adds no correctness on reliable ordered transports).

**Consequences.** `LargeMsgSender`/`LargeMsgReceiver` are rebuilt byte-based; the Start/End/Cancel DTOs are reshaped; `ChunkDTO` retires; `ChunkAckDTO`/`ChunkRequestDTO` remain outside the protocol (their reserved-vs-deleted disposition stays with OI-15). The OI-18 and OI-19 defects are resolved by replacement rather than patching. The chunk-size arithmetic measures the real encoded chunk-message size rather than assuming fixed headroom. The rebuilt classes are part of the common-elements candidate set (OI-40).

### KD-06 — Three size limits govern messaging: frame, chunk payload, and transfer

**Decision.** The single `MaxMessageSize` is replaced by three per-instance limits:

- **`MaxFrameSize`** (default 1 MiB) — the most bytes one frame body may carry, applying to both frame types. Enforced byte-true on the send path (refuse, or chunk, anything whose encoded body exceeds it) and on the receive path (a declared length above it is fatal, as today). Post-chunking, this is the interleave quantum: the longest one frame can hold the pipe before another channel's frame gets a turn. The mutable static `CONST_MAX_MessageSize` retires to a compiled default; the effective value is per-instance configuration.
- **`MaxChunkPayloadSize`** (default: derived, fit-to-frame) — the most payload bytes one chunk-data message may carry. Defaults to what fits under `MaxFrameSize` after the measured chunk-message overhead; configurable lower so bulk transfers use smaller frames than ordinary messages, giving latency-sensitive channels more frequent interleave slots. The library clamps it so a chunk frame can never exceed `MaxFrameSize`.
- **`MaxTransferSize`** (default 64 MiB) — the most total bytes a chunked message may reassemble to; the actual message-size ceiling in the chunked world, and the receiver's memory defense. Enforced at ChunkStart (an over-declared `TotalSize` rejects the transfer with a Cancel) and re-checked during accumulation (receipt exceeding the declaration is a protocol error), so an under-declaring sender cannot bypass the gate.

Relationships are enforced, not merely documented: `MaxChunkPayloadSize` is clamped to fit `MaxFrameSize`; `MaxTransferSize` must be at least `MaxFrameSize`. All three carry expressive XML documentation stating what each limit protects and how to coordinate the values, and the same content appears in this spec's §9 (when authored) and in the consumer implementation guide (OI-16).

**Rationale.** Once chunking works, the frame cap stops being the message-size limit and becomes a fairness knob; the transfer cap takes over as the true ceiling and closes the unbounded-reassembly hole (OI-19); the chunk-payload knob exists specifically for the owner's share-the-pipe goal — tuning bulk-transfer granularity independently of the ordinary-message cap.

**Consequences.** Limits are local policy in this version: each side enforces its own receive limits, and mismatches surface as transfer-time Cancels rather than at send time — stated plainly in the implementation guide. Negotiating the more-constrained set at registration is deferred to OI-41. Defaults are initial policy values the owner may tune.

### KD-07 — The dual-frame protocol is LibVersion 3; the frame-type byte is the mismatch detector

**Decision.** The dual-frame framing (KD-03 through KD-06) ships as **LibVersion 3**. `LibVersions.cs` gains a documented v3 entry describing its capabilities (five-byte frame preamble, dual JSON/binary messaging, rebuilt chunking, the KD-06 limits); clients announce it via the existing `tcplibver` registration prop, and the server-side validation range extends to accept it. The receive loop treats an unrecognized frame-type byte as **fatal**: the connection is torn down with a specific error identifying the byte, since on the no-mixed-pairs posture an unknown type can only mean stream corruption or a mismatched deployment, and both should die loudly at the first detectable byte.

Additionally, the `FrameTypes` registry permanently reserves value **123 (0x7B, the `{` character) as invalid-with-meaning**: a legacy pre-preamble peer's first frame body always begins with `{`, so a v3 receive loop that reads 0x7B in the type position reports "legacy JSON framing detected" rather than a generic corruption error — deterministic, human-readable diagnosis of the one mismatch most likely to occur in the wild.

**Rationale.** The LibVersion mechanism exists for exactly this and costs one registry entry. But version negotiation rides *inside* frames, so it cannot guard across a framing break: a v2 peer and a v3 peer misparse each other from the first frame, before any registration message is ever interpreted. The frame-type byte is therefore the real wild-mismatch detector — a v2 client against a v3 server dies immediately and diagnosably on the reserved 0x7B; a v3 client against a v2 server fails registration (its preamble byte corrupts the JSON parse) and cycles on the registration-reply timeout, which is the owner's accepted early-fail behavior for a scenario that is not supposed to occur. The version entry's primary roles are documentation, same-framing capability gating, and rejection of a v3 client by an older server that validates the announced range.

**Consequences.** `LibVersions.cs` (TCP) gets the v3 entry; the corresponding `WSLibVersions.cs` in the WebSocket library gets its own v3 entry when that library adopts the shared elements (OI-42). Server registration validation accepts [1..3]. The dead external doc links in `LibVersions.cs` (OI-10) are superseded by documenting version semantics in the file itself and in §9. Server-side capability announcement in the registration reply remains with OI-41.

---

## 13. Open Items

### OI-01 — Backport WebSocket class organization to OGA.WSClient_Base, then remove the files

`WSClient_v1_Abstract.cs` (client SP), `WSEndpoint.cs` and `WSConnectionMgr_Base.cs` (server SP) are unused in this library; WebSocket connectivity lives in the separate OGA.WSClient_Base library. These three files hold the owner's latest attempt at layering WebSocket transport over the `Client_v1_Abstract` type. The owner will backport how these three classes are organized into OGA.WSClient_Base; after the backport, the files are deleted from this repository. Until then, reviews, tests, and documentation in this effort disregard these files. Note: the recent binary-frame commit (a9f960d) touched `WSEndpoint.cs` as well as the shared base classes; the base-class binary-frame members are unaffected by this removal.

### OI-02 — Inverted keepalive pong-timeout comparison ⚠ NEEDS YOUR REVIEW

**Intended function (confirmed with owner).** The client keepalive takes credit for all received traffic: every received message of any type refreshes `LastReceivedTime` (`TCPClient_v1_Abstract.CALLBACK_Receiver_Message_Received`, line ~811), so a ping is sent only when the connection has been quiet for `Cfg_KeepAliveInterval` (check at `Client_v1_Abstract.cs` line ~1781 — correct). A received pong resets the in-flight flag `_keepAliveStatus` (line ~3204 — correct). `LastReceivedTime` is deliberately the timeout baseline, rather than ping-send time, so that any received traffic proves remote-end liveness and defers dead-peer declaration. The take-credit architecture works as designed and SHALL be preserved.

**The flaw.** The last-resort branch — ping in flight, nothing received at all — has its comparison inverted (`Send_KeepAlive_IfNeeded_Async`, line ~1756): `ctime.CompareTo(LastReceivedTime.AddSeconds(Cfg_KeepAlive_ReplyMaxDuration)) < 0` declares the connection dead when the current time is still *within* the reply window, and returns "still waiting" on every tick once the window has passed. With default timing (interval 15 s, reply window 20 s, connection-loop tick ~5 s), the in-flight branch is first evaluated at roughly the deadline, so in practice: (a) a truly silent remote end is never declared dead by the keepalive — connection loss is only detected by other means (send failures, receive-loop errors); and (b) in the narrow case where a loop tick lands inside the reply window with a ping outstanding and no pong yet, the connection would be declared dead prematurely.

**Recommendation.** Invert the comparison (`ctime > LastReceivedTime + Cfg_KeepAlive_ReplyMaxDuration` → dead), keeping `LastReceivedTime` as the baseline per the intended take-credit semantics. Note the post-fix semantics: dead is declared when *total silence* exceeds `Cfg_KeepAlive_ReplyMaxDuration`, which gives the server a meaningful reply window only when `Cfg_KeepAlive_ReplyMaxDuration > Cfg_KeepAliveInterval + loop tick`; the defaults (20 vs. 15 + 5) sit exactly on that boundary.

**Risk assessment (owner-reviewed).** The fix is client-local decision logic: no wire change, no client/server version coordination. The server answers pings unconditionally (`Endpoint_Abstract.Process_InternalMessage`, "ping" branch, line ~2361 — no configuration gate), so healthy servers are unaffected. Behavior changes per consuming application only as it adopts the updated package, giving a naturally incremental rollout; `Cfg_Disable_KeepAlive` remains a per-consumer escape hatch. Connections newly dropped by the fix are half-open/zombie connections that today sit undetected indefinitely — dropping them is the designed behavior. Residual exposures: (a) a server whose receive/dispatch path is blocked longer than the reply window by a consumer handler will now cause client reconnect cycles (previously tolerated silently); (b) configurations with `Cfg_KeepAliveInterval >= Cfg_KeepAlive_ReplyMaxDuration` leave only one loop tick of pong grace — addressed by the guard below.

**Agreed fix scope.** The fix release SHALL include:
1. The comparison inversion, preserving `LastReceivedTime` as the timeout baseline.
2. A configuration-relationship guard or floor ensuring the pong deadline falls no earlier than a couple of loop ticks after the ping was sent.
3. XML doc comments on the relevant delay properties (`Cfg_KeepAliveInterval`, `Cfg_KeepAlive_ReplyMaxDuration`, `Cfg_Connected_InnerLoop_Delay`) explaining how the intervals coordinate and the required relationship between them.
4. Corresponding delay-coordination guidance in the consumer implementation guide (OI-16).
5. A unit test for the client-side pong-timeout path. Existing keepalive tests in `TCPClient_v1_Tests.cs` cover the server-side dead-client timeout and keepalive-disable request only; no test exercises the client's pong-timeout branch. The new test rigs an endpoint to swallow pings and asserts the client declares the connection lost within the configured window (this test fails against current code, anchoring the regression).

Status: items 1, 2, 3, and 5 are implemented in commit 8b8df28, which has been pushed and is in the automated build. The regression test fails against the prior logic and passes with the fix. Item 4 is carried by OI-16. Remaining before closure: owner confirmation that the release build published successfully and the full test suites pass in the owner's test environment (the client suite binds an environment-specific address and cannot run elsewhere).

### OI-03 — MessageEnvelope.ToLogString defects ⚠ NEEDS YOUR REVIEW

`MessageEnvelope.ToLogString()` prints `MsgId` on the `Message_Type` line (copy/paste slip), and every `"label = " + value ?? ""` line null-coalesces the already-non-null concatenation result rather than the value (operator precedence). Log-only impact; recommendation: fix the field reference and parenthesize the coalesces.

### OI-04 — async void chunk processing

`ProcessChunkingMessage` is `async void` on both client and server sides. Exceptions thrown in it are unobservable by callers and can crash the process; completion cannot be awaited. The review pass identified concrete reachable paths: a duplicate `ChunkStart` (or the un-removed receiver of OI-19) makes `Dictionary.Add` throw at `Client_v1_Abstract.cs:2973`, and the client dispatches the reassembled message inline at `:3082`, so a consumer handler's exception escapes into the async-void machinery. Both are remotely triggerable. Correctness also currently depends on the `AcceptChunk*` calls completing synchronously — introducing a real `await` in them would let frame N+1 be processed before frame N, which the receiver's strict in-order offset check would then reject. Candidate fix: `async Task` with an explicitly observed continuation, or restructure the call site.

### OI-05 — Test coverage gaps

Areas with no direct test coverage: the chunking helpers (`LargeMsgSender`, `LargeMsgReceiver`) in isolation, chunk-cancel/timeout/prune paths, `ExpBackoff_wJitter`, `cBuffer`, `cEndpoint_Metrics`, `ConnectionMgr_Abstract` in isolation, and `cCustom_Serializer` (one test for ~1,580 lines). The binary-frame members added in a9f960d are also untested.

The review pass sharpened the priority: **an end-to-end large-message test over a real TCP connection, in both directions, would have caught OI-18** (client-to-server chunked transfers never complete; chunk frames exceed the frame cap). That test is the first one to write, before the OI-18 fix, so it anchors the regression the same way the keepalive test anchors OI-02. Other high-value additions suggested by the findings: a message containing non-BMP characters large enough to chunk (OI-19 surrogate splitting), a payload that is quote-dense enough to expose the char-vs-byte escaping margin (OI-18b), registration props containing colons and quotes (OI-29), runtime channel-adapter registration while traffic flows (OI-25), and a dispose-during-active-receive race (OI-20). Note the existing client suite binds a hardcoded LAN address, so new tests should use loopback to stay portable (see OI-38).

### OI-06 — TESTINGSRVR_* forked server copies have drifted ⚠ NEEDS YOUR REVIEW

`Testing_CommonHelpers_SP` contains forked copies of the server classes (`TESTINGSRVR_Endpoint_Abstract`, plus listener/endpoint/model copies) declared into the real `OGA.TCP.Server` namespaces, because client test targets include frameworks the server library does not support. Client/server compatibility is therefore validated against a snapshot rather than against the shipping server code.

This is a test-fidelity concern only — there is no packaging exposure. `Testing_CommonHelpers_SP` is imported exclusively by test projects (all client and server test csprojs, plus the isolated NET452 test solution); no library target project imports it, so the forked copies never reach either published package.

The drift is no longer hypothetical. The binary-frame receive capability added to the real `Endpoint_Abstract` in commit a9f960d was never mirrored into the fork: `Process_ReceivedBinaryFrame_from_Client`, `Cfg_BinaryFrameHandling_IsFatal`, and `OnBinaryFrameReceived` appear 4, 4, and 6 times respectively in the real class and **zero** times in the fork (fork 2,489 lines vs. real 2,572). Chunking, keepalive/silence detection, chunk-cancel handling, and connection-entry population are still present in both. Practical consequence: client-side binary-frame behavior cannot be exercised against the fork at all, which matters directly for OI-13 and OI-36.

Options to assess: re-sync the fork and add a scripted diff check to catch future divergence; consolidate so the tests build the real server sources for the frameworks that support them; or accept the drift, document the fork as a frozen v1/v2-protocol reference server, and test newer capabilities only in the server test projects.

### OI-07 — (Closed)

### OI-08 — (Closed by KD-02)

### OI-09 — (Cancelled)

### OI-10 — Dead documentation links

`LibVersions.cs` and possibly other file headers link to a wiki (`wiki.galaxydump.com` / Atlassian) for LibVersion semantics. Verify the links, and in any case capture the LibVersion 1 vs 2 semantics in §9 of this spec so the spec is self-sufficient (folds into OI-14).

### OI-11 — cCustom_Serializer breadth vs. actual usage

Usage is now established: repository-wide, the only production callers of `cCustom_Serializer` are `Serialize_Integer32` (frame-length writers in `TCPClient_v1_Abstract` and `TCPEndpoint`) and `Deserialize_Integer32` (the frame-length reader in `cReceiveLoop`), plus the `size_of_Int32` constant. Every other type — bool, short, long, float, double, DateTime, Guid, Version, string, string[] — is referenced only from within the class and its own test file. So roughly 95% of the codec is unused by the wire protocol, and several of its unused paths are defective (OI-32). Decide its disposition: keep and repair it as the designated primitive codec for the binary work (OI-13), trim it to the Int32 methods actually in use, or leave as-is with its status documented. Relevant to OI-13 and OI-05.

### OI-12 — (Closed)

### OI-13 — Binary data transmission capability: consumer-facing scope ⚠ NEEDS YOUR INPUT

The consumer-facing half of the binary work, paired with the wire-level design in OI-39. Decisions needed before implementation:

- **Payload model.** Minimum viable is an opaque `byte[]` sent and received per channel, with the consumer owning any framing or typing inside it. The richer option is typed binary payloads — a declared message type name accompanying the bytes, so binary messages dispatch through the same type-routing consumers already use for JSON.
- **API shape.** Whether binary send is a distinct method (`SendBinary_to_Endpoint(byte[], channel, ...)`) or an overload of the existing send entry point, and whether receive arrives on the existing `OnBinaryFrameReceived` delegate, on the channel adapters (a new `AcceptIncomingBinary` member on `IChannelAdapter`), or both.
- **Channel semantics.** Whether a channel may carry both JSON and binary messages, or a channel is declared one or the other at registration.
- **Size and chunking.** Whether binary messages participate in the chunking layer (see OI-18/OI-19 — chunking must be repaired and made byte-based before it can carry binary), or whether binary is initially capped at one frame.

Outcome recorded as KDs plus §8 (API surface) and §9 (protocol) content.

### OI-39 — Dual JSON/binary messaging: remaining design questions ⚠ NEEDS YOUR INPUT

The framing and routing-placement decisions are made (KD-03, KD-04). Established context that informs the remainder: the base-class binary receive plumbing (`OnBinaryFrameReceived` et al.) survives the OI-01 WebSocket-file removal and is the contract the TCP implementation should connect to (OI-36); `cReceiveLoop` currently hands out a decoded `string` only, so the reworked receive loop needs a byte-payload delegate alongside it; and the owner has confirmed no mixed old/new peer pairs will exist, so no on-wire negotiation or fallback is designed.

Remaining questions to settle before implementation:

1. **Chunking participation.** Settled by KD-05: both frame types chunk, via the byte-based splitter.
2. **Frame size caps.** Settled by KD-06: one frame cap for both types, plus chunk-payload and transfer limits.
3. **Version guard.** Settled by KD-07: LibVersion 3, fatal unknown frame types, 0x7B reserved as the legacy-framing detector.
4. **Implementation scoping.** Whether the framing change is bundled with the `cReceiveLoop` rework that resolves OI-20 through OI-23 (recommended: the read state machine is being rewritten either way), and how the `RawTransportSend` seam change is coordinated with the WebSocket library's shared base.

The consumer-facing API surface (send methods, adapter contract, channel semantics) is OI-13. Outcomes recorded as KDs and §9 protocol content.

### OI-14 — Complete the reverse-engineered spec sections

Author the pending sections of this spec from the code: §2 Functional Requirements, §3 NFRs, §5 Architecture, §6 Data Model, §7 Solution Structure, §8 Interfaces/Contracts, §9 Protocols and Flows (framing, envelope, registration, keepalive, chunking, LibVersion 1 vs 2). This is the "reverse out a design spec" goal of the improvement effort and is expected to run alongside the code-review pass.

### OI-15 — Unused chunking DTOs

Established during the review pass: `ChunkAckDTO` and `ChunkRequestDTO` are empty classes with no senders or handlers. `ChunkCancelDTO` is different — it **is** sent (by `LargeMsgSender` on cancellation or mid-sequence send failure) but is not intercepted on either receive side, so it reaches consumer dispatch as a bogus application message and its handler branches are dead (see OI-19). KD-05 wires Cancel into the rebuilt protocol and keeps Ack/Request out of it (reliable ordered transports need neither). Remaining decision: whether the two excluded DTOs stay as reserved surface or are deleted.

### OI-16 — Author the consumer implementation guide

Produce the consumer-facing usage documentation from `references/IMPLEMENTATION_GUIDE_TEMPLATE_R1.md`, covering client setup (`TCPClient_v1_Impl`), server setup (`TCPConnMgr_wListener` + `TCPEndpoint`), channel adapters, configuration knobs for failure-mode timing — including guidance on coordinating the delay intervals (keepalive interval, pong reply window, connection-loop tick; see OI-02) and the size limits (frame, chunk payload, transfer; see KD-06, including the local-policy posture that limit mismatches surface as transfer-time cancels) — and chunking/keepalive behavior a consumer should understand. As part of this work, the repository README SHALL gain a high-level overview of the library (both nuspecs point their `releaseNotes` at the README, and both packages embed it as their package readme, so it carries the packages' front-page description). Blocked behind OI-14 far enough that the protocol facts it cites are settled.

### OI-17 — (Closed by §13)

### OI-18 — Chunking is non-functional over TCP ⚠ NEEDS YOUR REVIEW

Two independent defects mean the large-message chunking feature cannot work over the TCP transport as shipped. Both were verified directly in code.

**(a) The server keys receivers by the wrong identifier.** `Endpoint_Abstract.cs:1968` passes the *envelope* id (`me.MsgId`) into `ProcessChunkingMessage`, and the receiver is stored under it (`:2131`) and looked up by it (`:2163`, `:2220`, `:2288`). But every frame gets a fresh envelope id — `Client_v1_Abstract.cs:2242` assigns `me.MsgId = GetNextMessageId()` inside the per-frame send — so the id under which the receiver was stored never matches any subsequent chunk frame. The client side keys by the logical transfer id `dto.MsgId` (`Client_v1_Abstract.cs:2973`, `:3005`, `:3062`), which is correct. Net effect: **client-to-server chunked transfers never complete**; every chunk logs "Received message chunk without a chunk start message", the message is lost silently, and an orphaned receiver accumulates per attempt until the 120-second prune. Server-to-client transfers work.

**(b) Chunk frames exceed the frame cap.** The chunking threshold and chunk size are character-based (`Client_v1_Abstract.cs:2105`, `:2118` — `MaxMessageSize - 1024` = 1,047,552 chars), but each chunk's `Data` slice is JSON-escaped twice before hitting the wire — once serializing `ChunkDTO`, again as the envelope's `Data` string — and only 1024 bytes of headroom exist. Because the chunked payload is itself JSON, it is quote-dense, and each `"` expands to `\\\"`. A measured worst case produced a 1,591,749-byte frame against the 1,048,576-byte cap. `cReceiveLoop.cs:1097` treats an oversized frame as fatal, so the feature tears down the connection it exists to protect. Note the send-side size guard is deliberately skipped when chunking is enabled (`:2220-2237`), so nothing else catches it. The same char-vs-byte confusion also lets a *non-chunked* message just under the threshold exceed the cap after single-level escaping.

Resolution path decided: these defects are resolved by replacement rather than patching — the KD-05 chunking rebuild removes both failure classes by construction. This item stays open as the defect record until the rebuild ships. Until then, consumers should be told the practical message ceiling is a single frame.

### OI-19 — Chunking data-integrity and lifecycle defects ⚠ NEEDS YOUR REVIEW

Beyond OI-18, the chunking subsystem carries defects that produce silent corruption or leaks once the transfer path works:

- **Surrogate-pair splitting.** `LargeMsgSender.cs:353` slices with `Substring` on UTF-16 code units, so a chunk boundary can split a surrogate pair. The lone surrogate survives JSON serialization and is replaced with U+FFFD at UTF-8 encoding, so a non-BMP character (e.g. an emoji in user content) silently becomes two replacement characters. Character counts are preserved, so offsets still line up and nothing reports an error.
- **Client never removes a completed receiver** (`Client_v1_Abstract.cs:3062-3084`; the server does, at `Endpoint_Abstract.cs:2238`). The reassembled message stays pinned for up to `Cfg_ReceiverTimeout`; a duplicate `ChunkEnd` re-dispatches the entire message a second time; and a reused MsgId makes `Dictionary.Add` throw inside an `async void` (see OI-04) — remotely triggerable.
- **No completeness validation.** `LargeMsgReceiver.cs:155-191` never compares accumulated bytes to `MessageSize` or chunk count to `ChunkCount`, so if any chunk was rejected, the End message dispatches a truncated payload as if complete.
- **ChunkCancel is sent but never handled.** The intercept lists comment out `ChunkCancelDTO` (`Client_v1_Abstract.cs:2793`, `Endpoint_Abstract.cs:1960`), so a cancel frame falls through to the *application* dispatch as a bogus message type, and the cancel-handling branches on both sides are unreachable. Sender-side aborts therefore never tear down the peer's receiver.
- **Sender ignores the send result of the Start and End frames** (`LargeMsgSender.cs:245`, `:279`), so a failed End frame reports overall success while the message silently evaporates at the receiver.
- **Unbounded receiver growth.** Declared `MessageSize`/`ChunkCount` are never enforced, and each chunk refreshes the idle timer, so a peer can stream indefinitely under one MsgId.
- **`Determine_Chunk_Count` divides by the wrong constant** (`LargeMsgSender.cs:372`, `:384` use `CONST_MAX_MessageSize` instead of `MaxChunkSize`), understating `ChunkCount` — latent today, but it would poison the completeness check that fixes the third item above.

Resolution path decided: resolved by the KD-05 rebuild rather than patched individually. This item stays open as the defect record until the rebuild ships.

### OI-20 — Receive-loop teardown can crash the process ⚠ NEEDS YOUR REVIEW

In `cReceiveLoop.cs`, the try/catch around the read callback ends at line 874; all frame-processing code after it (through `Process_Received_MessageBuffer`) runs unprotected on an IO-completion thread. `CloseDown()` concurrently disposes and nulls the buffer (`:388-396`) with no synchronization against an in-flight callback. A read completing while the owner disposes the loop — or while the frame-read timeout fires and calls `CloseDown` — dereferences the nulled buffer at `:979` or `:1424`, throwing a `NullReferenceException` on a threadpool thread with no handler, which terminates the process. Candidate fix: a close/callback gate (lock or interlocked state) plus wrapping the post-`EndRead` processing.

### OI-21 — Receive loop caps throughput at a few messages per second ⚠ NEEDS YOUR REVIEW

`cReceiveLoop.cs` blocks a threadpool thread with `Thread.Sleep(100)` on every read-callback entry (`:687`), before every body-read queue (`:519`), and in `CloseDown` (`:383`). A single message therefore costs roughly 300 ms of blocked thread time (length callback + body queue + body callback), limiting a connection to roughly 3–10 messages per second and holding a blocked thread per active connection. The sleeps appear to exist to let a cancellation token settle before disposal; a proper CTS lifecycle would remove the need. This is a scalability ceiling on a live service and worth measuring before and after.

### OI-22 — Frame validation gaps

`cReceiveLoop.cs:1097` sanity-checks the frame length only against `MaxMessageSize`; a negative length passes and dies later via an incidental `ArgumentOutOfRangeException` from `BeginRead` rather than the check. Separately, `FrameReadTimeout` bounds only a single `BeginRead` and is re-armed on every partial read, and the length-read phase has no timeout at all — so a peer dripping one byte every few seconds holds a connection and its buffer indefinitely (slowloris). Candidate fixes: add `tempint < 0` to the sanity check, and add a whole-frame deadline distinct from the per-read one.

### OI-23 — Frame-read timeout races

`cReceiveLoop.Arm_FrameReadTimeout` (`:619-666`) awaits `Task.Delay` on a token, then re-reads `this._readcts` — which may by then be a *new* CTS belonging to a later frame — and declares the connection `Lost` on a healthy connection. The same block swallows an `ObjectDisposedException` from a concurrently disposed CTS, silently leaving that frame with no read timeout. Candidate fix: capture the CTS (or a generation counter) in the local scope the timeout task closes over.

### OI-24 — Client shutdown ordering and dispose do not wait ⚠ NEEDS YOUR REVIEW

Two related defects in the client lifecycle:

- `Client_v1_Abstract.cs:479` calls `Stop_Async().GetAwaiter()` without `GetResult()`, so `Dispose` returns immediately while teardown continues on another thread — and `Logger` is nulled underneath it. The same fire-and-forget pattern appears at `TCPClient_v1_Abstract.cs:733`, `:798`, `:849` (benign only because the TCP override completes synchronously) and in the server at `Endpoint_Abstract.cs:371`, `:1040`.
- `Stop_Async` closes the transport *first* and cancels `_cts` last, after ~200 ms of delays (`:574-656`). In that window the connection loop can observe `!IsConnected`, treat it as connection loss, and start a full reconnect — including re-registration with the server — after the consumer called Stop. Candidate fix: cancel `_cts` before closing the transport.

### OI-25 — Non-thread-safe dictionaries on hot paths ⚠ NEEDS YOUR REVIEW

Three plain `Dictionary` instances are mutated and enumerated from different threads with no synchronization:

- `_ChannelMessageHandlers` (client): written by `Add_ChannelAdapter`/`Remove_ChannelHandler`/`Close_ChannelAdapters` (`Client_v1_Abstract.cs:685`, `:735`, `:745`), read by dispatch on the receive thread (`:3430`). Registering a channel adapter at runtime while traffic flows can throw or corrupt the table — and runtime channel registration is an advertised feature (UR-02).
- `_largemsgreceivers` on both sides: mutated on the receive thread, enumerated by the prune on the connection-loop thread (`Client_v1_Abstract.cs:2973`/`:3142`; `Endpoint_Abstract.cs:2131`/`:2304`). A chunk arriving during a prune throws inside the loop, killing the prune pass (client) or tearing down the connection (server).

Candidate fix: `ConcurrentDictionary` or a shared lock across all call sites per collection.

### OI-26 — Server can leak connection slots indefinitely ⚠ NEEDS YOUR REVIEW

Three findings compound into a resource leak on the server:

- A client can send registration prop `"keepalive":"off"`, which sets `Cfg_Disable_KeepAlive` on the endpoint (`Endpoint_Abstract.cs:2610-2644`) and thereby skips the silence check (`:734`). There is no server-side way to refuse the request.
- `ConnectionMgr_Abstract`'s reapers (`Purge_OldUnregisteredConnections`, `Purge_LostConnections`) are invoked **only** from `CloseDown()` (`:128-143`) — no timer or background loop calls them anywhere in the library.
- An endpoint disposed directly (rather than through a manager purge) never fires `DispatchConnectionClosed` — `Stop_Async` clears the delegate instead (`:476-567`) — so its entry stays in the manager's dictionary.

Together: a silent client that opted out of keepalive holds its slot, its endpoint task, and its dictionary entry for the life of the process. Deciding whether the reapers should be scheduled (and whether clients may disable keepalive at all) is an owner call.

### OI-27 — Listener fragility ⚠ NEEDS YOUR REVIEW

`cListener.Accept_Callback` responds to a `SocketException` by calling `CloseDown_Listener()` without re-arming (`:677-704`). `EndAcceptTcpClient` throws `SocketException` for a single aborted inbound connection — a client sending RST between connect and accept, which is routine — so one bad handshake permanently stops the server from accepting connections. Compounding it: the listener is single-use (`Start_Listener` requires `Initialized` state and `CloseDown_Listener` nulls the delegates), and `TCPConnMgr_wListener.cs:212` discards `Start_Listener`'s return value, so a failed bind (port in use) is reported to the host as a successful startup. Also: the backlog is hardcoded to 10, and port-in-use is detected by matching an English exception-message prefix (`:780`) rather than `SocketError.AddressAlreadyInUse`.

### OI-28 — Endpoint can run orphaned from its connection manager

`TCPConnMgr_wListener.cs:269-295` wraps `AddConnection` in a swallow-all catch and then starts the endpoint regardless. If `AddConnection` throws (or is overridden to throw), the endpoint runs, registers, and serves traffic while absent from `_connections` — invisible to queries and counts, never closed by `CloseDown()`, with its delegates unwired.

### OI-29 — Registration prop parsing is fragile

Server-side prop parsing (`Endpoint_Abstract.cs:2561-2676`) splits each prop on **every** `:` and reads `parts[1]`, so any value containing a colon is silently truncated — a `runtimeid` of `node:4222` is stored as `node`. Keys are matched with `ToLower().Contains(...)`, so a prop whose key merely contains a known substring is hijacked (`"rapid"` matches `pid`; `"languagepack"` overwrites Language), and correctness depends on the order the branches are checked (`appid` contains `pid` and only works because it is tested first). On the client side, props are built as hand-concatenated JSON fragments with no escaping (`Client_v1_Abstract.cs:2011-2044`), so a quote or backslash in `RuntimeId` produces a malformed prop. Additionally, re-registration re-initializes the parsed locals and writes them unconditionally (`:2525-2536`, `:2783-2787`), so a later registration that omits `runtimeid`/`language`/libver silently wipes or downgrades the recorded values. Fixing the parse without breaking live clients means keeping the existing wire format but splitting on the first colon only and matching keys exactly.

### OI-30 — Registration completion is fire-and-forget

`Endpoint_Abstract.cs:2811-2835` sends the registration reply and fires `DispatchConnectionRegistered` on a `Task.Run` after `Process_InternalMessage` has already returned success. If the reply send fails, the connection stays up but the manager is never told the connection registered, so no routing layer knows about a connection the endpoint considers live. A second registration arriving before the task runs is compared against the not-yet-updated `ClientInfo.ConnectionId` and can be rejected as a fatal mismatch.

### OI-31 — A raw-message tap disables the session layer

On both sides, setting the raw-message delegate causes `Process_ReceivedMessage` to return before `Process_InternalMessage` runs (`Client_v1_Abstract.cs:2727-2733`; mirrored server-side). Consequences on the client: pings are never answered, pongs never clear the keepalive flag, and registration replies are never processed — so with the default `Cfg_ConnectionWaitsforRegistrationReply`, the client recycles its connection every `Cfg_RegistrationReplyTimeout` forever. The XML docs do not warn about this. Decide whether internal messages should be processed before the tap, or whether the behavior should simply be documented as "raw tap replaces the session layer."

### OI-32 — cCustom_Serializer correctness defects

Independent of how widely it is used (OI-11), the codec has real defects: the embedded-length string methods store `string.Length` (char count) as the prefix while writing UTF-8 bytes, so any non-ASCII string round-trips truncated and misaligns the rest of the stream (`:144-171`, and the string-array metadata at `:1570`); `Deserialize_Version` passes recovered values positionally into a constructor with different parameter order, swapping Build and Revision, and throws on 2- or 3-part versions (`:559-562` vs `:1239-1245`); `size_of_short` is 4 rather than 2 (`:11`), so short round-trips misreport consumed bytes; the big-endian path reverses bytes **in place** (mutating the caller's buffer) and even reverses UTF-8 text, which has no endianness; and `cMultiString_MetaData.Deserialize` allocates an array from an unvalidated wire-supplied count before its bounds check (`:1466-1499`). Since production traffic only uses the Int32 codec, none of this is live-service urgent — but it bears directly on OI-11's disposition and on OI-13, which may want a working primitive codec.

### OI-33 — Metrics and telemetry defects

`Sent_Message_Count` is permanently zero on both sides: `TCPEndpoint.cs:354` and `TCPClient_v1_Abstract.cs:605` increment through a `Metrics` getter that returns a fresh copy each call. The same getters dereference the receive loop without a null check, so reading `Metrics` before the first connection throws. In `cReceiveLoop.cs:1444`, `Last_Received_Message_Time` is set from `DateTime.Now` while every sibling write uses `UtcNow`. And all three OpenTelemetry span names lose their suffix to `??` precedence (`Endpoint_Abstract.cs:1323`, `:1835`, `:2862`), so every span is named just `tcpsocket`/`tcp` instead of the three intended distinct names.

### OI-34 — Unbounded task spawning on ping and loopback paths

Each received ping spawns a `Task.Run` to send the pong (`Endpoint_Abstract.cs:2376-2384`), and each message spawns one per loopback echo (`:1887`, `:1937`), all queueing on the write semaphore. A client sending faster than the server's synchronous write drains accumulates unbounded queued tasks and buffered payloads, with no cap, coalescing, or drop policy. A pending-pong flag would bound the keepalive case.

### OI-35 — Client connection loop blocks threadpool threads

The connection loop uses `ExpBackoff_wJitter.Delay` (a `Thread.Sleep` loop) at eight call sites despite an available `DelayAsync`, and `WaitforCondition` uses `SpinWait.SpinUntil` for up to the full registration-reply timeout (`Client_v1_Abstract.cs:1646`). One client instance can therefore tie up a threadpool thread for the whole backoff or registration window; many client instances in one process can starve the pool. `WaitforCondition`'s `scaninterval` parameter is computed and then ignored.

### OI-36 — Binary-frame plumbing is inert on the TCP transport

`Process_ReceivedBinaryFrame_from_Client` (`Endpoint_Abstract.cs:2000-2058`) is only ever called from the WebSocket endpoint, and `cReceiveLoop` has no binary-frame concept — its only payload delegate hands out a decoded string. So on TCP, `OnBinaryFrameReceived` never fires and `Cfg_BinaryFrameHandling_IsFatal` has no effect: a consumer wiring a binary handler on a TCP endpoint silently gets nothing. This is the starting point for OI-39 rather than a defect to fix in isolation — the framing design decides what this plumbing should connect to.

### OI-37 — Dead code and minor defects (batch)

Collected low-severity items, each small and independent:

- `cEndpoint_Ping_Tracking` is confirmed dead — zero references repository-wide outside the shared-project include. It is public API, so removal is a minor breaking change; decide keep-or-remove. (If revived it needs fixes: a 20 ms coercion of its delay setters, a copy/paste log message in `Disable()`, an unknown-state branch that returns `None` instead of `CloseConnection`, and local-time timestamps.)
- The `res == -1` fatal-recycle path in `TCPClient_v1_Abstract.cs:829-859` is unreachable: `Process_ReceivedMessage` maps every negative internal-message result to `0` (`Client_v1_Abstract.cs:2752-2763`), so a garbled registration reply is only logged.
- `Close_ChannelAdapters` (`Client_v1_Abstract.cs:745-757`) removes the adapter *after* calling its `Close()`; a consumer adapter that throws from `Close()` leaves the collection unchanged and the loop refetches the same element forever — an infinite loop inside `Dispose`.
- `Start_Async` has no already-started guard (`:541-552`); a second call orphans the first `CancellationTokenSource` and runs two connection loops against one client.
- `Stop_Async` nulls all consumer delegates and closes all channel adapters (`:586-590`, `:647-649`), so a Stop-then-Start cycle silently loses every handler the consumer registered.
- A failed ping *send* returns success (`:1832-1841`), so only pong timeout recycles the connection.
- `ExpBackoff_wJitter` clamps `JitterHeight` above 1.0 to **0** rather than 1.0 (`:31-37`), silently disabling jitter for a caller asking for maximum; its `maxRetries` constructor argument is stored but never enforced; and `Delay()` returns success even when cancelled.
- `_instance_counter++` in `cReceiveLoop.cs:161` is not atomic despite the field being `volatile`, so concurrent accepts can share an InstanceId in logs.
- Log-string precedence bugs of the same shape as OI-03 exist in `ClientInfo.ToLogString` and the chunking DTOs' `ToLogString`.
- `cListener`'s `SendTimeout` floor is dead code (missing `else`, `:62-67`).
- Explicit JSON nulls for `MessageType`/`Scope` cause an NRE that drops the message (`Endpoint_Abstract.cs:1872`, `:1875`).
- Several `cReceiveLoop` error paths request a `Closed` transition from `Open`, which the state machine forbids, producing a spurious "state change prevented" error log on every such path and losing the intended `Error` classification.

### OI-42 — Propagate LibVersion 3 into the WebSocket library's version registry

The WebSocket library (OGA.WSClient_Base) maintains its own `WSLibVersions.cs` documenting versions and capabilities for the WebSocket transport; both libraries currently stand at version 2. When the WebSocket library adopts the shared dual-frame elements (OI-40), its `WSLibVersions.cs` SHALL gain the corresponding version-3 entry documenting the adopted capabilities (binary message body layout, rebuilt chunking, limits), mirroring the TCP library's `LibVersions.cs` v3 entry from KD-07. Recorded so the propagation is not forgotten; owner executes it in the WebSocket library's own repository at that phase.

### OI-41 — Negotiated size limits and capability exchange at registration

Under KD-06, the size limits are local policy: each side enforces its own receive limits, and a sender exceeding the receiver's `MaxTransferSize` discovers it via the transfer-time Cancel rather than up front. The owner's suggestion, deferred to a later phase: exchange limits at registration so the two ends operate on the more constrained set — the client announcing its receive limits in its registration props, the server announcing its own in the reply's currently-empty `Props`, and each sender honoring the peer's receive limits so oversize sends fail fast at the call site instead of mid-transfer. This dovetails with the version-guard/capability-announcement question (OI-39): the registration reply's `Props` is the natural single mechanism for the server to declare limits, supported frame types, and other capabilities. Held until the dual-frame work is functioning; when taken up, decide what is announced, what is enforced versus advisory, and how config asymmetry (send vs. receive limits) is expressed.

### OI-40 — Publishable common-elements library for cross-library reuse

The dual-frame design work is producing elements that are deliberately transport- and library-neutral: the common messaging-host interface that decouples the channel-adapter substrate from `Client_v1_Abstract` (so client, server, TCP, and WebSocket endpoints can all host the same adapters), the relocated adapter family, the binary message body layout, and the rebuilt chunking classes. The separate OGA.WSClient_Base library is intended to consume these same elements; today, sharing between the two libraries happens by manually harmonized code copies, which is the drift mechanism already seen in OI-06 and in the keepalive fix backport.

Plan for a published library (nuget package) of the common elements that both this library and the WebSocket library reference, replacing copy-based sharing for the shared substrate. Deliberately deferred: the elements are being designed and proven inside this repository first, and the extraction/packaging boundary gets decided once something is functioning here. When taken up, decisions needed: package naming and namespace (transport-neutral), which of the existing `ClientServerShared_SP` contents belong in it versus staying TCP-specific (e.g., the frame-type registry and `cReceiveLoop` are TCP-only), and the release-coordination model between three packages on one Jenkins/GitVersion pipeline pattern.

### OI-38 — Client test suite is bound to one environment

`TCPClient_v1_Tests` and its sibling client test classes bind the hardcoded LAN address `192.168.70.103`, so the suite only runs on the owner's test machine. The `KeepAlive_Tests` class added with OI-02 uses loopback instead and runs anywhere. Consider moving the existing suites to loopback (or a configurable host) so tests are runnable on any dev machine and in any future automated environment. Low risk — test-only change — but it touches a large number of test methods, so it is worth doing deliberately rather than alongside a functional fix.

---

*End of document.*
