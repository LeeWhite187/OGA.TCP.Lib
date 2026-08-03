# OGA.TCP.Lib — Specification

**Project:** OGA.TCP.Lib
**Short description:** A message-based TCP connectivity library for .NET, providing framed JSON message exchange, channels, large-message chunking, keepalive, and client registration between processes.
**Status:** In Implementation — Release 1 shipped; Release 2 (LibVersion 3 wire break) built and under verification
**Revision:** 3
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

Messages larger than the frame limit are transparently chunked and reassembled on the far side, so one large transfer does not monopolize the connection and other channels can interleave traffic.

Beginning with LibVersion 3 (KD-03 through KD-09), the library carries two kinds of messages over one connection: the JSON envelope messaging described above, and binary messages — raw byte payloads routed by the same channel/scope/correlation metadata, with no encoding tax. Whether a given channel carries JSON or binary is declared per channel; which to use for which message types is the consuming application's choice. All session machinery (registration, keepalive, chunk control) rides JSON.

### 1.6 User Requirements

**UR-01 — Typed message exchange.** A consuming application SHALL be able to send a serializable object to its peer and have the peer receive it with the original type name available for dispatch.

**UR-02 — Named channels.** A consuming application SHALL be able to register named channel handlers (channel adapters) at runtime, and messages tagged with a channel SHALL be delivered to that channel's handler.

**UR-03 — Self-maintaining connection.** A client SHALL maintain its connection without consumer intervention: reconnecting with exponential backoff and jitter on loss, and surfacing connection-state changes via delegates.

**UR-04 — Client registration.** The server SHALL track each connected client, and the client SHALL identify itself at connect time (connection id, user id, device id, process id, runtime id, library version, and other properties) so server-side logic can enumerate and address known connections.

**UR-05 — Keepalive and dead-peer detection.** Both ends SHALL detect an unresponsive peer within a configurable interval, using application-level ping/pong messages, with keepalive optionally disabled for debugging.

**UR-06 — Large-message chunking.** The library SHALL transparently split messages exceeding the maximum frame size into chunks and reassemble them at the receiver, so large transfers share the pipe with other channels.

**UR-07 — Configurable failure-mode timing.** A consumer SHALL be able to configure the delays and timeouts governing connect attempts, registration replies, keepalive intervals, dead-peer declaration, and chunk-receiver staleness.

**UR-08 — Multi-framework availability.** The client library SHALL be consumable from NET Framework 4.5.2, NET Framework 4.8, NET Standard 2.1, and NET 5/6/7/8 applications; the server library from NET 5/6/7/8 applications.

**UR-09 — Binary message exchange.** A consuming application SHALL be able to send and receive raw binary payloads over the same connection and channels as JSON messages, without base64 or other encoding expansion, from LibVersion 3 onward.

### 1.7 Non-Goals

- **WebSocket connectivity.** Handled by a separate WebSocket library. WebSocket source files present in this repository (`WSClient_v1_Abstract.cs`, `WSEndpoint.cs`, `WSConnectionMgr_Base.cs`) are unused here and pending removal (OI-01).
- **Broker/queue semantics.** The library is point-to-point client/server connectivity; it is not a message broker, has no persistence, and offers no delivery guarantees beyond the TCP session.
- **Transport security.** The library does not itself provide TLS or message encryption; securing the channel is the deployment's concern.

### 1.8 Glossary

- **Envelope (MessageEnvelope)** — The JSON wire wrapper carrying every message: id, sent-time, message type name, payload (as a JSON string), scope, channel, reply-to, and property strings. See §9.
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

Requirements describe the system as designed: current shipping behavior where unchanged, and the KD-03 through KD-09 design where it supersedes it. Requirements that only take effect at LibVersion 3 say so.

### 2.1 Connection Lifecycle

**FR-01 — Self-maintaining connection loop.** The client SHALL maintain its connection through a long-running connection loop that establishes the transport, performs registration, monitors health, and reconnects on loss, without consumer intervention.

**FR-02 — Reconnect backoff.** When connection attempts fail or a connection is lost, the client SHALL delay reconnection using exponential backoff with optional jitter, bounded by the configured maximum delays (network-loss, startup-retry, and post-connect-failure delays).

**FR-03 — Single connection-lost event.** The client SHALL raise the connection-lost delegate exactly once per established connection's loss, and the connected delegate on each successful (re)establishment.

**FR-04 — Orderly shutdown.** When stopped or disposed, the client SHALL cancel its connection loop before closing the transport, so no reconnection or re-registration can occur after a stop; disposal SHALL complete the teardown before returning.

**FR-05 — Session readiness gating.** The client SHALL gate application sends on session readiness (`AllowSend`): transport connected, post-connect work complete, and — when configured to wait — a validated registration reply received.

### 2.2 Registration

**FR-06 — Client registration.** On each connection, the client SHALL send a registration message carrying its connection id, user id, device id, and property strings (loopback mode, keepalive mode, process id, runtime id; LibVersion-dependent additions per §9).

**FR-07 — Server-assigned connection identity.** The server SHALL assign each connection a server-generated unique connection id (ULID) and return it in the registration reply, echoing the previously registered id so the client can discard stale replies; the client SHALL adopt the server-assigned id.

**FR-08 — Registration validation.** The server SHALL require non-empty connection id and device id, SHALL treat connection id and device id as immutable for the life of a connection, and SHALL validate the announced LibVersion against its supported range, closing the connection on violation.

**FR-09 — Re-registration.** The server SHALL accept repeated registrations over one connection to update user context (e.g., login/logout), preserving previously supplied optional properties when a later registration omits them.

**FR-10 — Client tracking.** The server SHALL track each connection's metadata (ClientInfo: connection time, ids, app id/version, language, runtime id, LibVersion, auth level) and expose connection queries by connection id, user id, and device id through the connection manager.

### 2.3 Messaging

**FR-11 — Typed JSON message exchange.** The system SHALL deliver serialized objects wrapped in a message envelope carrying the payload's type name, a message id, sent-time, channel, scope, and correlation properties, and SHALL dispatch received messages with the type name available for consumer routing.

**FR-12 — Binary message exchange (v3).** The system SHALL deliver raw byte payloads as binary messages carrying the same routing metadata (channel, scope, message type, message id, correlation) in a routing header, with the payload bytes uncoded on the wire.

**FR-13 — Internal machinery rides JSON.** All session-machinery messages — registration, keepalive ping/pong, and chunk control — SHALL be JSON messages; the binary message path SHALL carry only application payloads and chunk data.

**FR-14 — Implicit keepalive dismissal.** The receiver SHALL accept zero-length frames and empty-payload messages as implicit liveness signals, refreshing receive timestamps without dispatching to consumers.

**FR-15 — Raw-message tap.** When a consumer assigns the raw-message delegate, the endpoint SHALL hand every received frame (frame type and raw bytes) to that delegate exclusively, bypassing all internal processing and dispatch; this is a dumb-pipe mode in which the consumer owns the session semantics.

### 2.4 Channels

**FR-16 — Runtime channel registration.** Consumers SHALL be able to register and remove named channel handlers at runtime, either as channel adapters or as wrapped delegates, on both client and server endpoints, safely while traffic is flowing.

**FR-17 — Channel kind discipline (v3).** Each channel SHALL be declared as JSON-kind or binary-kind at registration; delivery SHALL verify the arriving message's kind against the channel's declaration and reject mismatches with a logged error.

**FR-18 — Dispatch routing.** Messages carrying a channel SHALL be delivered to the matching registered handler by exact channel-name lookup; messages without a channel SHALL be delivered to the endpoint's no-channel fallback delegate for their kind (JSON: `OnMessageReceived`; binary: `OnBinaryFrameReceived`); channeled messages with no registered handler SHALL be dropped with a logged error.

**FR-19 — Channel metrics.** Channel adapters SHALL maintain functioning per-channel receive/sent/bad counters.

### 2.5 Keepalive

**FR-20 — Take-credit keepalive.** The client SHALL send a keepalive ping only after the connection has been quiet for the keepalive interval, counting any received message as proof of remote liveness; a received pong SHALL clear the in-flight state.

**FR-21 — Ping reply.** Each endpoint SHALL answer a received ping with a pong, unconditionally.

**FR-22 — Dead-connection declaration.** With a ping in flight, the client SHALL declare the connection dead when nothing has been received for the configured reply window, floored at two connection-loop ticks after the ping was sent, and SHALL recycle the connection.

**FR-23 — Server-side silence detection.** The server SHALL declare a client dead after the configured silence timeout when client chatter is required; a client MAY request keepalive-exempt status at registration, which disables the server's silence check for that connection.

### 2.6 Chunking (v3 design, per KD-05/KD-06)

**FR-24 — Transparent chunking of oversized messages.** When a message's encoded body exceeds the frame limit, the sender SHALL split the bytes into chunk messages and the receiver SHALL reassemble and process them as if the message had arrived in one frame, for both frame types, preserving the original frame type.

**FR-25 — Pipe sharing.** Chunk transmission SHALL interleave with other traffic: concurrent transfers are keyed by transfer id, other channels' messages may be sent between chunk frames, and chunk payload size is independently tunable below the frame limit.

**FR-26 — Transfer integrity.** The receiver SHALL validate completeness at end-of-transfer (accumulated bytes equal declared size; chunk count equals declared count) and SHALL discard incomplete transfers rather than dispatching truncated payloads; chunks within a transfer are strictly in-order, with violations treated as protocol errors.

**FR-27 — Transfer cancellation.** The sender SHALL announce cancellation (on token cancellation or mid-sequence send failure) with a cancel message, and the receiver SHALL tear down the corresponding transfer state upon receiving it.

**FR-28 — Transfer limits.** The receiver SHALL reject transfers whose declared size exceeds the configured maximum transfer size, and SHALL abort transfers whose accumulated bytes exceed the declaration.

**FR-29 — Stale-transfer pruning.** The receiver SHALL discard in-progress transfers idle beyond the configured receiver timeout.

### 2.7 Server Connection Management

**FR-30 — Listener accept flow.** The server listener SHALL accept TCP connections and spawn a per-connection endpoint, registered with the connection manager and started independently, without one connection's handling blocking the accept of others.

**FR-31 — Connection reaping.** The connection manager SHALL provide purges for unregistered connections older than the unregistered TTL and for lost connections, and hooks for notifying an external mapping service of registrations and closures.

### 2.8 Framing and Versioning

**FR-32 — Frame typing (v3).** Every TCP frame SHALL carry the five-byte preamble (4-byte little-endian body length, 1-byte frame type from the `FrameTypes` registry); receivers SHALL treat unknown frame-type values as fatal, reporting the reserved legacy-framing value (123) with its specific diagnosis.

**FR-33 — Version announcement.** The client SHALL announce its LibVersion at registration via the transport's libver property; the server SHALL validate it against its supported range and record it in the connection's metadata.

---

## 3. Non-Functional Requirements

### 3.1 Performance

**NFR-01 — Non-blocking receive path.** The receive path SHALL NOT block threadpool threads with fixed sleeps or spin-waits; per-message processing cost SHALL be bounded by actual work, not built-in delays. (The current `cReceiveLoop` violates this — OI-21 — and the Release 2 rework resolves it.)

**NFR-02 — Serialized sends, bounded waits.** All sends on a connection SHALL be serialized through one write gate so frames never interleave mid-frame; waiting delays in the connection loop SHOULD be async rather than thread-blocking (current violations tracked in OI-35).

**NFR-03 — Chunk overhead proportionality.** Chunking overhead SHALL be proportional to metadata size, not payload size: no base64 or escaping expansion of chunk payloads (KD-05).

No numeric throughput or latency targets are committed at this revision; the Release 2 rework SHOULD be benchmarked before/after (OI-21) to establish a baseline worth committing to.

### 3.2 Reliability and Availability

**NFR-04 — No crash from remote input or teardown races.** Malformed messages, invalid frames, and connection teardown concurrent with in-flight receives SHALL never terminate the host process; failures are contained to the connection (current violations: OI-20, OI-04).

**NFR-05 — Contained message failures.** A malformed or undeliverable message SHALL be logged and dropped without closing the connection, except framing-level violations (oversized, negative, or unknown-type frames), which are fatal to the connection by design.

**NFR-06 — Deterministic corruption detection.** Stream corruption and framing mismatches SHALL be detected at the earliest decodable byte (frame length sanity, frame-type validity) and reported distinctly from application errors (KD-07).

**NFR-07 — At-most-once connection-loss signaling.** Consumers SHALL be able to rely on single-fire connection-loss notification for idempotent-free recovery logic.

### 3.3 Security

Not applicable beyond §1.7's exclusion of transport security: the library trusts its host environment, carries an auth-level enum and auth-token hook for consumers, and imposes no trust boundary of its own. The review pass confirmed this posture, with one qualification: the framing and limit checks (§9.1.1, KD-06) are the library's only hardening against hostile peers, and deployments exposing listeners beyond trusted networks should treat transport security and peer authentication as their own responsibility (see also OI-26 on the keepalive-exemption resource exposure).

### 3.4 Observability

**NFR-08 — Contextual logging.** All log lines SHALL carry class name, instance id, and method context (the established `_classname:InstanceId::method` convention), with file/line metadata supplied by the Debug+PDB packaging posture (KD-01).

**NFR-09 — Accurate counters.** Exposed metrics (`cEndpoint_Metrics` counters and timestamps, channel adapter counters) SHALL reflect actual traffic; all timestamps SHALL be UTC (current violations: OI-33, OI-37).

**NFR-10 — Distinct trace spans.** The server's OpenTelemetry activities SHALL emit their three distinct span names (send, receive, dispatch) with correlation-id tags (current violation: OI-33).

### 3.5 Accessibility

Not applicable: this is a headless connectivity library with no user interface.

### 3.6 Regulatory and Compliance

Not applicable: the library carries application payloads opaquely and imposes no data handling of its own; regulatory posture belongs to consuming applications.

---

## 4. Constraints and Technology Decisions

### 4.1 Tech Stack

C# on .NET, multi-targeted (client: net452, net48, netstandard2.1, net5.0, net6.0, net7.0, net8.0; server: net5.0, net6.0, net7.0, net8.0) from Visual Studio Shared Projects — one source body compiled per target framework (§7). JSON serialization via Newtonsoft.Json (a wire-format commitment, not merely a library preference). Logging via NLog through the OGA.SharedKernel logging conventions. TCP transport built directly on `TcpClient`/`TcpListener`/`NetworkStream`. Build/publish via Jenkins to a private BaGet feed; versioning via GitVersion with `+semver:` commit-message bumps (KD-01, KD-02 govern packaging and test posture). The net452/net48 targets constrain language/API choices: no default interface methods, no Span-based APIs in shared code, and conditional compilation only via the `#if (NET452 || NET48)` pattern already in use.

### 4.2 Library Dependencies

**Newtonsoft.Json 13.0.3** — Role: serialization of the MessageEnvelope, all DTOs, and (v3) the binary routing header. Rationale: required for net452 reach; the wire format is Newtonsoft-shaped with default settings (property names exactly as declared in C#). Substitution would be a wire-compatibility risk and is not planned (§4.5).

**NLog 5.2.8** — Role: logging backend, consumed via `OGA.SharedKernel.Logging_Base` and per-instance logger references.

**OGA.SharedKernel 3.7.1** — Role: shared logging/base conventions across the OGA library family. (Not referenced by the netstandard2.1 client target, which carries only Newtonsoft and NLog.)

**Server-only:** **NUlid 1.7.2** (server-side connection id generation — the ULID `WSId`), **OGA.Common.Lib.NetCore 3.10.3** (async semaphore used by the server send gate, common utilities), **OpenTelemetry 1.5.1** (the three trace activities in the endpoint; §3.4).

### 4.3 Environment Constraints

The library runs wherever its target frameworks run; Jenkins builds produce win and linux runtime outputs, and the packages ship both plus ref assemblies. The library assumes nothing about its network environment beyond raw TCP reachability: no TLS (§1.7), no proxy awareness, no DNS behavior beyond `TcpClient`'s. All supported runtimes are little-endian, which the wire format relies on (§9.1).

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

The library is layered so that transport specifics (how bytes move) are separated from session semantics (what messages mean), and session semantics are shared between client and server — and, by design, with the sibling WebSocket library.

```
                    CLIENT SIDE                              SERVER SIDE
  consumer ──────────────────────────────────────────────────────────────────── consumer
      │ delegates / channel adapters                              │
  ┌───────────────────────────┐                  ┌────────────────────────────────┐
  │ Client_v1_Abstract        │   session layer  │ Endpoint_Abstract (per-conn)   │
  │  loop, registration,      │   (shared        │  registration handling,        │
  │  keepalive, chunking,     │    concepts &    │  silence detection, chunking,  │
  │  dispatch                 │    substrate)    │  dispatch                      │
  └───────────┬───────────────┘                  └───────────┬────────────────────┘
  ┌───────────┴───────────────┐                  ┌───────────┴────────────────────┐
  │ TCPClient_v1_Abstract     │  transport layer │ TCPEndpoint                    │
  │  → TCPClient_v1_Impl      │                  │  ← cListener accept loop       │
  │  TcpClient + cReceiveLoop │                  │  TcpClient + cReceiveLoop      │
  └───────────────────────────┘                  └────────────────────────────────┘
                                                 ┌────────────────────────────────┐
                                                 │ ConnectionMgr_Abstract         │
                                                 │  → TCPConnMgr_wListener        │
                                                 │  registry, queries, reaping    │
                                                 └────────────────────────────────┘
```

### 5.1 Tiers and Components

**Session layer (shared concepts).** `Client_v1_Abstract` and `Endpoint_Abstract` are deliberate mirrors: each owns registration, keepalive, chunk interception, and message dispatch for its side. They share the envelope, DTOs, chunking helpers, and (post-KD-08) the channel substrate — `IChannelAdapter`, the common host interface, and the `ChannelDispatcher` — from the shared project. The abstract client is transport-agnostic by design: the sibling WebSocket library derives its client from the same base (§1.1), which is why transport-specific behavior lives strictly below this layer.

**TCP transport layer.** `TCPClient_v1_Abstract` (concrete-ready as `TCPClient_v1_Impl`) and `TCPEndpoint` bind the session layer to `TcpClient`/`NetworkStream`. Both delegate frame reading to `cReceiveLoop` — the TCP-only construct that supplies what raw TCP lacks and WebSocket framing provides natively: message boundaries and (v3) a frame-type discriminator. The receive loop is frame-type agnostic: it parses and validates the preamble and hands `(frameType, bytes)` upward; it never interprets bodies.

**Server connection management.** `cListener` wraps `TcpListener`, accepting connections and handing each `TcpClient` to `TCPConnMgr_wListener`, which constructs a `TCPEndpoint`, registers it in `ConnectionMgr_Abstract`'s registry (keyed by the server-generated connection id), wires closure/registration hooks, and starts it. The manager exposes connection queries, purge operations, and virtual hooks for an external client-mapping service. `TCPConnectionMgr_Base` is the listener-less variant for externally accepted connections.

**Consumer surface.** Consumers interact through: lifecycle (`Start_Async`/`Stop_Async`/`Dispose`), send methods (JSON object send; v3 binary send), delegates (message received, binary received, connection lost, status change, raw tap), and channel registration (adapters or wrapped delegates). §8 and §10 detail the surface.

### 5.2 Data Flow

**Send (client → server, JSON).** Consumer calls `SendMessage_to_Endpoint(obj, channel, scope, cid)` → payload serialized; envelope built (MsgId, type name, Data, routing) → envelope serialized to UTF-8 → if oversized, diverted to the chunking layer (§9.1.5) → `RawTransportSend` prepends the frame preamble → single `NetworkStream` write under the write semaphore. The server's send path mirrors this exactly.

**Receive (either side).** `cReceiveLoop` accumulates the preamble, validates length and frame type, accumulates the body, and invokes the message-received callback with `(frameType, bytes)` → the transport class stamps `LastReceivedTime` and forwards to the session layer's `Process_ReceivedMessage` → five ordered steps: raw-tap handoff (exclusive, if assigned); internal-message handling (JSON only: ping/pong, registration); empty-message dismissal; chunk interception (control messages and chunk-data, with completed transfers re-injected at the top of this method); dispatch — channel lookup through the dispatcher, kind check, delivery to the adapter or the no-channel fallback.

**Connection establishment.** Client loop: create transport → connect → wire receive machinery → send registration → (optionally) await validated reply → `AllowSend` = true, connected delegate fires. Server: accept → endpoint start → receive loop up → registration arrives as an ordinary message → reply sent, manager notified → connection appears in queries as registered.

---

## 6. Data Model

The library holds no durable state; its data model is the set of wire types and in-memory tracking records. Wire schemas are external contracts (§8.2) and live in §6.2; their protocol usage is narrated in §9.

### 6.1 Entity Overview

**MessageEnvelope** — the JSON wire wrapper for every JSON message. Created per send, consumed per receive, never persisted. **BinaryMessageHeader (v3)** — the envelope-minus-Data sibling that fronts a binary message body; same routing semantics, payload externalized. **ConnRegisterDTO / ConnRegisterReplyDTO** — the registration handshake pair; created per registration exchange. **Chunk control DTOs (v3)** — `ChunkStartDTO`, `ChunkEndDTO`, `ChunkCancelDTO`, reshaped per KD-05; a transfer's Start creates receiver state keyed by transfer id, End or Cancel or the prune timeout destroys it. (`ChunkAckDTO`/`ChunkRequestDTO` exist but are outside the protocol — OI-15.) **ClientInfo** — the server's live per-connection metadata record, created at endpoint construction, updated by registration, destroyed with the endpoint. **ConnectionEntry_v1** — the versioned cache/routing record derived from ClientInfo for the client-mapping service; deliberately a separate type so the cache contract cannot drift through casual library edits. **cEndpoint_Metrics** — per-connection counters/timestamps, reset per connection.

### 6.2 Schema

**MessageEnvelope** (JSON, default Newtonsoft settings — property names exactly as declared):

| Field | Type | Populated on send |
|---|---|---|
| `MsgId` | string | sequential id (integer string) |
| `SentTimeUTC` | DateTime | UTC send time |
| `MessageType` | string | payload CLR type name (compared case-insensitively on receive) |
| `Data` | string | payload JSON (never null; `""` when empty) |
| `Scope` | string | routing scope (`""` = unset; `loopback=rawmsg` triggers echo) |
| `Channel` | string | channel name (`""` = no channel) |
| `ReplyTo` | string | declared, never populated by the library |
| `Props` | string[] | `["corelationid=<id>"]` |

**Binary message body (v3, KD-04):** `[Int32 headerLen][UTF-8 JSON header][payload bytes]`, where the header carries `MsgId`, `SentTimeUTC`, `MessageType`, `Scope`, `Channel`, `Props` — the envelope schema minus `Data`. The exact header DTO is fixed at Release 2 implementation.

**Chunk control DTOs (v3, KD-05):** `ChunkStartDTO { TransferId, InnerFrameType, TotalSize (bytes), ChunkSize, ChunkCount, MessageType, SentTimeUTC }`; `ChunkEndDTO { TransferId }`; `ChunkCancelDTO { TransferId }`. Chunk data messages are binary messages whose routing header identifies the chunk-data type and carries `TransferId` and byte offset; their payload is the raw slice.

**ConnRegisterDTO:** `ConnectionId`, `UserId` (Guid), `DeviceId`, `Props[]` (prop strings, §9.1.3). **ConnRegisterReplyDTO:** `ConnectionId` (server-assigned), `OldConnectionId` (echo for stale-reply rejection), `UserId`, `DeviceId`, `Props[]` (empty today; reserved as the server's capability-announcement carrier — OI-41).

**ClientInfo:** `ConnectionTimeUTC`, `AuthLevel` (`NoAuth`/`ClientAuth`/`UserAuth`), `ClientIP`, `ConnectionId`, `UserId`, `DeviceId`, `Pid`, `AppId`, `AppVersion`, `Language`, `RuntimeId`, `LibVersion`, computed `IsRegistered`/`UnRegisteredAge`. **ConnectionEntry_v1:** the same identity/app fields plus manager-supplied `Hostname`, `Host_Port`, `Region`, minus `AuthLevel`/`ClientIP`.

### 6.3 Identifiers

- **Connection id** — dual-phase: the client generates a GUID per connection attempt; the server assigns the authoritative id as a ULID (`WSId`, NUlid) created at endpoint construction and delivered in the registration reply; the client adopts it. Server-side registries and routing key on the ULID exclusively (an anti-forgery measure — client-proposed ids never key anything server-side).
- **MsgId** — integer-string envelope id. Client-side: a process-global interlocked counter shared by all client instances (ids are unique in-process but non-contiguous per connection). Server-side: per-endpoint counter. MsgIds are diagnostic/correlational; nothing orders or dedupes by them.
- **TransferId (v3)** — the chunking transfer key, explicit and distinct from any frame's MsgId (the KD-05 remedy for the OI-18 ambiguity). Unique per in-flight transfer per connection.
- **InstanceId** — per-object diagnostic id from static counters, used only in logs.
- **DeviceId / UserId** — consumer-supplied identity; the manager's device query returns the newest connection per device and lazily disposes older duplicates.

### 6.4 Versioning and Soft Delete

Not applicable in the persistence sense: the library holds no durable state. Wire-protocol versioning (LibVersion) is a protocol concern, covered in §9.

### 6.5 Data Retention

Not applicable: the library persists nothing. In-memory lifecycle concerns (stale chunk receivers, unregistered-connection reaping) are runtime behaviors, covered in §9.

---

## 7. Solution Structure

The solution's unit of code ownership is the Visual Studio Shared Project (`_SP`): source-inclusion containers with no binary output of their own. Target csprojs import shared-project sources and compile them per framework. This section defines what belongs where and what may reference what; §4.4 gives the filesystem view.

### 7.1 Libraries and Projects Overview

**OGA.TCP.ClientServerShared_SP** — the wire and substrate layer, used by both packages: `MessageEnvelope`, registration DTOs, chunking (DTOs + helpers), `cReceiveLoop`, `cBuffer`, `FrameTypes`, `Constants`, enums, `ExpBackoff_wJitter`, `cEndpoint_Metrics`, `cCustom_Serializer`; post-KD-08, also the channel substrate (`IChannelAdapter` family, common host interface, `ChannelDispatcher`). Depends only on Newtonsoft/NLog-level packages. This project is the seed of the future common-elements package (OI-40).

**OGA.TCP.Lib_SP** — the client session and transport layer: `Client_v1_Abstract`, `TCPClient_v1_Abstract`, `TCPClient_v1_Impl`, `LibVersions`. Compiled with `ClientServerShared_SP` into the client package.

**OGA.TCP.Server.Lib_SP** — the server layer: `Endpoint_Abstract`, `TCPEndpoint`, `cListener`, the connection managers, `ClientInfo`/`ConnectionEntry_v1`. Compiled with `ClientServerShared_SP` into the server package.

**Test shared projects** — `OGA.TCP_Test_SP` (client tests), `OGA.TCP.Server.Lib_Tests_SP` (server tests), `Testing_CommonHelpers_SP` (helpers, including the forked `TESTINGSRVR_*` server copies — OI-06).

**Target csprojs** — one per package per framework (client: net452→net8.0; server: net5.0→net8.0), byte-identical except TFM and define constants; plus per-framework test csprojs. `scratchdev` is out of scope (OI-09).

### 7.2 Library Boundary Rationale

The shared-project mechanism exists for one reason: one body of source, many target frameworks, without per-framework source drift. The `ClientServerShared` / `Lib` / `Server.Lib` split exists so the two packages share their wire layer by construction — an envelope or framing change cannot reach one package and miss the other. The client/server session classes are deliberate mirrors rather than a shared base class; where that mirroring has produced drift-prone duplication (dispatch, chunking), the remedy is extracting shared components into `ClientServerShared_SP` (KD-05, KD-08), not merging the two classes — their genuine differences (registration direction, silence detection, manager integration) should stay visibly different. The separate WebSocket library shares the session-layer *design* today by copy, and the substrate *code* tomorrow by package (OI-40); anything intended for that sharing must live in `ClientServerShared_SP` and must not reference the TCP transport types.

### 7.3 Reference Rules

- `ClientServerShared_SP` references no other shared project. Transport-specific types (`cReceiveLoop`, `FrameTypes`) live here only because the WebSocket library does not consume this project today; at OI-40 extraction they stay behind in the TCP repository.
- `Lib_SP` may use `ClientServerShared_SP`; never `Server.Lib_SP`. `Server.Lib_SP` may use `ClientServerShared_SP`; never `Lib_SP`.
- Client package csprojs import `Lib_SP` + `ClientServerShared_SP`; server package csprojs import `Server.Lib_SP` + `ClientServerShared_SP`. No csproj imports a shared project it doesn't ship.
- Test csprojs reference their side's **built library project** (not its sources) and import only test shared projects — the post-OI-07 arrangement, which tests the shipped assembly rather than a recompilation.
- `Testing_CommonHelpers_SP` is imported by test projects only; nothing shipped may reference it.

---

## 8. Key Interfaces and Contracts

### 8.1 Internal Interfaces

**The transport seam** — the abstract members of `Client_v1_Abstract` a transport implementation supplies. This is the boundary that lets the TCP and WebSocket client libraries share one session layer:

```csharp
abstract string  TransportShortName / TransportLongName / PropName_ClientLibVer;
abstract bool    Cfg_TransportRequiresReceiverLoop;   // false for TCP (callback-driven), true for WS
abstract bool    TransportIsOpen;
abstract Task<int> TransportSpecific_Connect();
abstract int     TransportSpecific_CreateNewConnection();
abstract Task    CloseandDisposeTransport();
abstract void    DereferenceTransport();
abstract Task<int> RawTransportSend(byte frameType, byte[] body);   // v3 signature: frame type added
abstract Task    ReceiveLoop();                        // WS only; TCP is BeginRead-callback-driven
```

Virtual extension points: `IsConnected`, `Send_RegistrationMessage` (v2+ clients must override; the base is v1-only), `Get_ConnectionInfo`, `Do_TransportSpecific_PostConnectionWork_Async`, `IsInternetAvailable`, `DispatchConnected`, `Dispose(bool)`.

**The receive-loop contract** (`cReceiveLoop`, TCP-only): constructor takes a connected `TcpClient`; `Begin_Comms()` starts reading; delegates `OnMessage_Received` (v3: `(loop, frameType, byte[])`), `OnConnection_Went_Bad`, `OnStatus_Change`. The loop parses and validates the preamble and never interprets bodies (§5.1).

**The channel substrate (KD-08):**

```csharp
public interface IMessagingHost      // implemented by Client_v1_Abstract, Endpoint_Abstract, (later) WS client
    { /* send capability, instance identity, logging access — minimal surface adapters consume */ }

public interface IChannelAdapter
{
    string ChannelId { get; }
    eChannelKind Kind { get; }       // Json | Binary, declared at construction
    int AcceptIncomingMessage(IMessagingHost host, string messagetype, string json, string corelationid);
    int AcceptIncomingMessage(IMessagingHost host, string messagetype, byte[] payload, string corelationid);
    void RegisterAdapter(IMessagingHost host);
    void Close();
    // functioning counters: ReceiveCount, SentCount, BadCount
}

public class ChannelDispatcher       // one instance composed by each endpoint class
    { /* thread-safe registry, Add/Remove/CloseAll, DispatchJson, DispatchBinary, no-channel fallbacks */ }
```

**Connection-manager hooks** (`ConnectionMgr_Abstract` virtuals): `DoStartupChecks`, `StartListener`, `Send_NewClientConnection_to_ClientMappingService(entry, oldvals)`, `Send_ConnectionClosed_to_ClientMappingService(connid)` — the integration seam for an external routing/mapping service; no-op stubs in the base.

**Consumer delegate surface** (set-only, single-subscriber, per side with side-typed first parameters): `OnMessageReceived`, `OnBinaryFrameReceived`, `OnConnectionLost`/`OnConnectionClosed`, `OnStatus_Change`, `OnRawMessageReceived` (exclusive dumb-pipe tap, FR-15), plus the connection manager's `OnNew_Client_Connection` on the listener.

### 8.2 External Contracts

**The wire protocol** is the principal external contract: framing, envelope schema, internal message types, registration handshake, keepalive semantics, and chunking protocol, versioned by LibVersion (§9.1.6). Commitments: LibVersions 1 and 2 remain wire-stable; LibVersion 3 is a deliberate framing break deployed only with both ends upgraded (KD-07); within a LibVersion, changes are additive-only; frame-type values and prop key strings are never repurposed.

**The NuGet package surface**: the public API of both packages follows additive evolution; breaking API changes ride major versions. The Debug+PDB packaging posture is contractual for consumers' logging expectations (KD-01).

**Registration prop strings** are a wire contract in their own right (exact key strings, `"key":"value"` fragment format, §9.1.3) — parsers on both sides must accept the established forms indefinitely.

### 8.3 DTOs and Wire Types

Enumerated with schemas in §6.2: `MessageEnvelope`, binary message body layout, `ConnRegisterDTO`/`ConnRegisterReplyDTO`, chunk control DTOs, plus the tracking records `ClientInfo` (in-memory) and `ConnectionEntry_v1` (the versioned mapping-service contract — treat as append-only). `FrameTypes` (§9.1.1) is the registry of frame discriminators. No DTO generation pipeline exists; all types are hand-maintained C# in `ClientServerShared_SP`.

---

## 9. Protocols and Flows

### 9.1 Protocols

#### 9.1.1 TCP Framing

**Legacy framing (LibVersions 1–2, current wire):**

```
[ Int32 length, 4 bytes, little-endian ][ length bytes: UTF-8 JSON MessageEnvelope ]
```

`length` counts payload bytes only. A zero-length frame is a legal implicit keepalive probe: header only, no body, no reply; the receiver refreshes its receive timestamp and counters. A declared length exceeding the frame cap is fatal (connection torn down as `Error`). The frame boundary is the only structure; there are no delimiters, checksums, or type markers — the receiver's sole interpretation is "UTF-8 JSON envelope."

**v3 framing (KD-03):**

```
[ Int32 length, 4 bytes, LE ][ 1 byte frame type ][ length bytes: body ]
```

The frame type comes from the `FrameTypes` registry (`OGA.TCP.FrameTypes`): `0 Invalid` (reserved; zeroed-buffer canary), `1 Json` (body = UTF-8 JSON envelope, byte-identical to legacy bodies), `2 Binary` (body per §9.1.2), `123 Reserved_LegacyJsonFraming` (permanently reserved: a legacy peer's first body byte is `{` = 0x7B, which lands in the type position, so receipt is reported specifically as a legacy-framing peer). Consecutive frames may carry different types. Any unregistered type value is fatal (KD-07). Zero-length keepalive probes remain legal.

**Size limits (KD-06):** `MaxFrameSize` (default 1 MiB) bounds every frame body, enforced byte-true on send and receive; `MaxChunkPayloadSize` (default fit-to-frame) bounds chunk-data payloads; `MaxTransferSize` (default 64 MiB) bounds reassembled transfers. Limits are local policy, not negotiated (OI-41); relationships are library-enforced. All supported runtimes are little-endian; the wire is defined as little-endian.

#### 9.1.2 Message Bodies

**JSON body:** the `MessageEnvelope` (§6.2). `MessageType` carries the payload's CLR type name as sent; receivers compare it lowercased, so type identity is case-insensitive on the wire. `Data` carries the payload's JSON as a string (payload is double-serialized by design — the envelope is parseable without knowing the payload type).

**Binary body (v3, KD-04):** `[Int32 headerLen][UTF-8 JSON routing header][payload bytes]`. The header carries the envelope's routing fields minus `Data`; the payload rides outside the JSON, so it costs exactly its own length. Binary messages carry application payloads and chunk data only — never session machinery (FR-13).

**Internal message types** (exact `MessageType` strings, matched lowercased): `ping` and `pong` (empty `Data`; §9.1.4), `ConnRegisterDTO` and `ConnRegisterReplyDTO` (§9.1.3), and the chunk control types (§9.1.5). The scope value `loopback=rawmsg` on any message causes the server to echo the envelope back (a diagnostics feature; the registration prop `loopback` controls echo-all mode).

#### 9.1.3 Registration Handshake

1. Client connects and sends `ConnRegisterDTO`: its self-generated GUID connection id, `UserId` (Guid.Empty allowed pre-v2-auth), `DeviceId`, and `Props[]` — an array of `"key":"value"` fragments. Prop keys in use: `loopback` (`rawmsg`/`off`), `keepalive` (`on`/`off` — `off` requests exemption from server silence checks), `pid`, `runtimeid`, and for v2+: `appid`, `appver`, `language`, and the transport libver prop (`tcplibver` for TCP; `wslibver` for WebSocket). The base implementation registers v1 (the four base props only) and v3 (adding the libver prop plus the mandatory app identity from the client's `AppId`/`AppVersion` properties, and `language` when populated); v2 clients override `Send_RegistrationMessage`, the historical v2 pattern.
2. Server validates (non-empty connection id and device id; both immutable per connection; libver parses into the supported range; v2+ requires non-empty appid/appver, appid immutable), records everything into `ClientInfo`, and replies with `ConnRegisterReplyDTO`: the server-generated ULID as `ConnectionId`, and `OldConnectionId` echoing the id being replaced (the client's own, on first registration).
3. Client accepts the reply only if `OldConnectionId` matches its current id (defeats stale-receive-loop races), adopts the server's id, and marks registration complete — which (by default) is what releases `AllowSend`.
4. Re-registration over a live connection is legal for user-context changes and repeats the exchange (FR-09).

**LibVersion semantics** (`LibVersions.cs`; this section is the spec's self-sufficient record — the file's wiki links are temporarily dead pending the owner's Confluence-to-Bookstack migration, and will be repointed per OI-43): **v1** — baseline: registration with the four base props; user-auth token required by consuming deployments. **v2** — adds client app identity props (`appid`, `appver`, `language`, libver) and permits connecting without a user auth token. **v3 (KD-03…KD-09)** — the five-byte frame preamble, dual JSON/binary messaging, the rebuilt chunking protocol, and the KD-06 limits; its registration carries the v2 contract forward (app identity mandatory — owner decision, OI-50). The server accepts registrations announcing any supported version [1..3], enforcing the v2+ app-identity mandate uniformly; version negotiation cannot guard across the v2/v3 framing break — the frame-type byte is the mismatch detector (KD-07).

#### 9.1.4 Keepalive

Application-level `ping`/`pong` messages (not transport-level probes), governed by take-credit semantics: any received message defers the next ping, since traffic proves liveness. Client side: after `Cfg_KeepAliveInterval` (default 15 s, floor 3 s) of receive silence, send `ping` and set in-flight state; a received `pong` clears it; with a ping in flight, declare the connection dead when nothing has been received for `Cfg_KeepAlive_ReplyMaxDuration` (default 20 s), floored at two connection-loop ticks after the ping was sent, then recycle. Evaluation cadence is the connection-loop tick (`Cfg_Connected_InnerLoop_Delay`, default 5000 ms, auto-lowered for sub-5-second keepalive intervals). Server side: answers every `ping` with a `pong` unconditionally; when chatter is required and the client has not requested keepalive exemption, declares the client dead after `Cfg_DeadClientTimeout` (default 30 s, floor 5 s) of receive silence. Coordination rules for the three client delays are documented on the properties themselves (OI-02's fix). `Cfg_Disable_KeepAlive` suspends client pinging (a debugging affordance); the registration `keepalive:off` prop requests the mirror exemption server-side (resource-exposure caveat: OI-26).

#### 9.1.5 Chunking (v3 protocol, KD-05)

Trigger: a message's encoded body exceeds the frame budget. The sender splits **the encoded bytes** — the chunker never interprets them — and emits, on the original message's channel/scope:

1. **`ChunkStartDTO`** (JSON): `TransferId`, `InnerFrameType`, `TotalSize` (bytes), chunk sizing, count, original `MessageType`.
2. **N binary chunk-data messages**: routing header identifies chunk-data and carries `TransferId` + byte offset; payload = raw slice of `MaxChunkPayloadSize` bytes (final chunk = remainder).
3. **`ChunkEndDTO`** (JSON): `TransferId`.

Receiver: Start creates a receiver keyed by `TransferId` (rejected if declared size exceeds `MaxTransferSize`); chunks must arrive in order (offset check = corruption guard; TCP/WS guarantee order); End validates byte and chunk counts and **re-injects** `(InnerFrameType, bytes)` into normal receive processing — a reassembled message is indistinguishable from a single-frame arrival, and a reassembled body that itself parses to chunk messages is a protocol error. **`ChunkCancelDTO`** (JSON) from the sender (cancellation or mid-sequence send failure) tears down the receiver; the prune timer (`Cfg_ReceiverTimeout`, default 120 s, floor 5 s) collects abandoned transfers. Concurrent transfers interleave by `TransferId`; other channels' traffic interleaves between chunk frames. No acks, no retransmission (reliable ordered transports; the write semaphore provides backpressure). The pre-v3 string-based chunk protocol (`ChunkDTO` string slices keyed by envelope MsgId) is superseded and documented only by OI-18/OI-19.

#### 9.1.6 Version Compatibility Posture

Within a framing generation, LibVersion gates capabilities via registration. Across the v2→v3 framing break there is no on-wire negotiation by design (no mixed-pair deployments — KD-03); mismatch outcomes: v2 client → v3 server dies immediately with the legacy-framing diagnosis; v3 client → v2 server fails registration parse and cycles on the registration-reply timeout. Both are deliberate early-fail behaviors.

### 9.2 Architectural Data Flows

The principal flows are narrated in §5.2 (send, receive, connection establishment). Two additional flows warrant detail:

**Chunked transfer, end to end (v3).** Consumer sends an 8 MiB object on channel `X` → JSON-serialized, envelope built and encoded; body exceeds budget → chunker takes over: Start (JSON, channel X) → 8 binary chunk-data frames interleaved under the write semaphore with any other channels' traffic → End (JSON). Receiver side: Start creates receiver state; each chunk accumulates bytes on the receive-callback thread; End validates counts, and the reassembled envelope bytes re-enter `Process_ReceivedMessage` as a Json frame — flowing through internal-message checks and dispatch exactly as a small message would, channel X's adapter receiving it with no visible difference. A failure at any chunk aborts with Cancel; the receiver's state disappears; the consumer sees nothing.

**Connection recycle after silent failure.** Server host loses power; client's TCP stack sees nothing. Traffic stops → after the keepalive interval, client pings into the void → in-flight state set → nothing received through the reply window → keepalive check returns dead → connection loop closes the transport, fires connection-lost (once), applies the post-connect-failure backoff, and loops to reconnect — where the connect attempt fails until the server returns, under startup backoff with jitter. When the server returns, connect succeeds, registration runs fresh (new GUID → new ULID), and the connected delegate fires.

### 9.3 User Workflow Narration

Not applicable: the library has no end-user workflows; consumer-facing usage flows belong to the implementation guide (OI-16).

---

## 10. API Surface

Not applicable in the HTTP sense: this is a library distributed as NuGet packages; its surface is the public API of its types, documented in §8 and in the consumer implementation guide (OI-16).

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

**Consequences.** `LibVersions.cs` (TCP) gets the v3 entry; the corresponding `WSLibVersions.cs` in the WebSocket library gets its own v3 entry when that library adopts the shared elements (OI-42). Server registration validation accepts [1..3]. Version semantics are documented in the file itself and in §9.1.3; the file's external wiki links are repointed to the owner's new wiki per OI-43. Server-side capability announcement in the registration reply remains with OI-41.

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

Status: resolved by replacement in Release 2. Chunk control processing is synchronous by design in the rebuild (`ProcessChunkingControl` performs no awaits), so failures are observable by the caller and ordering cannot slip; there is no `async void` anywhere in the chunking path. Closes on release.

### OI-05 — Test coverage gaps

Areas with no direct test coverage: the chunking helpers (`LargeMsgSender`, `LargeMsgReceiver`) in isolation, chunk-cancel/timeout/prune paths, `ExpBackoff_wJitter`, `cBuffer`, `cEndpoint_Metrics`, `ConnectionMgr_Abstract` in isolation, and `cCustom_Serializer` (one test for ~1,580 lines). The binary-frame members added in a9f960d are also untested.

The review pass sharpened the priority: **an end-to-end large-message test over a real TCP connection, in both directions, would have caught OI-18** (client-to-server chunked transfers never complete; chunk frames exceed the frame cap). That test is the first one to write, before the OI-18 fix, so it anchors the regression the same way the keepalive test anchors OI-02. Other high-value additions suggested by the findings: a message containing non-BMP characters large enough to chunk (OI-19 surrogate splitting), a payload that is quote-dense enough to expose the char-vs-byte escaping margin (OI-18b), registration props containing colons and quotes (OI-29), runtime channel-adapter registration while traffic flows (OI-25), and a dispose-during-active-receive race (OI-20). Note the existing client suite binds a hardcoded LAN address, so new tests should use loopback to stay portable (see OI-38).

Status: substantially addressed across the two releases. Now covered: the chunking path end-to-end in both directions with json and binary inner types, non-BMP payloads, transfer-limit rejection, and the framing-mismatch detectors (`V3Framing_Tests`); keepalive regression (`KeepAlive_Tests`); dispatcher concurrency and kind enforcement (`ChannelDispatcher_Tests`); the single-use lifecycle (`ClientLifecycle_Tests`); and the rewritten receive loop's contract (`ReceiveLoop_Test`, reworked for v3). Remaining gaps, narrowed: `ExpBackoff_wJitter`, `cBuffer`, `cEndpoint_Metrics`, `ConnectionMgr_Abstract` in isolation, `cCustom_Serializer` breadth (tied to OI-11's disposition), and registration-prop parsing edge cases (OI-29).

### OI-06 — TESTINGSRVR_* forked server copies have drifted ⚠ NEEDS YOUR REVIEW

`Testing_CommonHelpers_SP` contains forked copies of the server classes (`TESTINGSRVR_Endpoint_Abstract`, plus listener/endpoint/model copies) declared into the real `OGA.TCP.Server` namespaces, because client test targets include frameworks the server library does not support. Client/server compatibility is therefore validated against a snapshot rather than against the shipping server code.

This is a test-fidelity concern only — there is no packaging exposure. `Testing_CommonHelpers_SP` is imported exclusively by test projects (all client and server test csprojs, plus the isolated NET452 test solution); no library target project imports it, so the forked copies never reach either published package.

The drift is no longer hypothetical. The binary-frame receive capability added to the real `Endpoint_Abstract` in commit a9f960d was never mirrored into the fork: `Process_ReceivedBinaryFrame_from_Client`, `Cfg_BinaryFrameHandling_IsFatal`, and `OnBinaryFrameReceived` appear 4, 4, and 6 times respectively in the real class and **zero** times in the fork (fork 2,489 lines vs. real 2,572). Chunking, keepalive/silence detection, chunk-cancel handling, and connection-entry population are still present in both. Practical consequence: client-side binary-frame behavior cannot be exercised against the fork at all, which matters directly for OI-13 and OI-36.

Options to assess: re-sync the fork and add a scripted diff check to catch future divergence; consolidate so the tests build the real server sources for the frameworks that support them; or accept the drift, document the fork as a frozen v1/v2-protocol reference server, and test newer capabilities only in the server test projects.

Status: the fork was re-synced to the v3 protocol as part of Release 2 (typed framing, frame-send choke, binary receive path, rebuilt chunking, v3 registration range) — it now exercises the full v3 wire against the real client, including the new binary and chunked-transfer tests. One deliberate divergence: the fork has no channel-adapter machinery, so received binary messages land on a fork-specific `OnBinaryMessageReceived` delegate. The longer-term structural decision (scripted diff check vs. consolidation) remains open for the owner.

### OI-07 — (Closed)

### OI-08 — (Closed by KD-02)

### OI-09 — (Cancelled)

### OI-10 — (Closed by §9.1.3)

### OI-11 — cCustom_Serializer breadth vs. actual usage

Usage is now established: repository-wide, the only production callers of `cCustom_Serializer` are `Serialize_Integer32` (frame-length writers in `TCPClient_v1_Abstract` and `TCPEndpoint`) and `Deserialize_Integer32` (the frame-length reader in `cReceiveLoop`), plus the `size_of_Int32` constant. Every other type — bool, short, long, float, double, DateTime, Guid, Version, string, string[] — is referenced only from within the class and its own test file. So roughly 95% of the codec is unused by the wire protocol, and several of its unused paths are defective (OI-32). Decide its disposition: keep and repair it as the designated primitive codec for the binary work (OI-13), trim it to the Int32 methods actually in use, or leave as-is with its status documented. Relevant to OI-13 and OI-05.

### OI-12 — (Closed)

### KD-08 — The channel subsystem becomes one shared, kind-aware substrate

**Decision.** The channel subsystem is unified into a shared substrate living in `OGA.TCP.ClientServerShared_SP`:

- The adapter abstractions relocate out of the client-only project into the shared project, under a transport-neutral namespace, and `IChannelAdapter` is evolved directly (it has no downstream implementors yet): it gains a channel kind (`Json` | `Binary`) declared at construction, and an `AcceptIncomingMessage` overload accepting `byte[]` alongside the existing string form. The kind is the enforcement point for the one-kind-per-channel rule: delivery checks frame type against kind and rejects mismatches.
- The adapter contract decouples from `Client_v1_Abstract` via a minimal common host interface (working name `IMessagingHost`: send capability, instance identity, logging access), implemented by `Client_v1_Abstract` and `Endpoint_Abstract` — and adoptable by the WebSocket library's client, whose dispatch logic the owner has verified is close enough to migrate.
- A single **`ChannelDispatcher`** class owns the whole subsystem: the one thread-safe registry, registration/removal/close-all, routing, the kind guard, delivery of both payload shapes with correlation id, functioning per-channel counters, and the no-channel fallback hooks (`OnMessageReceived` for JSON; the existing `OnBinaryFrameReceived` plumbing for binary). Both endpoint classes compose a dispatcher instance; the server migrates off its `Dictionary<string, DelMessageReceived>`. Per-side behavioral differences (such as task-wrapped delivery) become explicit constructor policy rather than accidental divergence.
- Each side's existing delegate-registration signatures are preserved as facades: `Add_ChannelHandler` wraps the side's own delegate type in a closure-capturing `ChannelAdapter_DelegateType`, so dependent projects recompile unchanged. The canonical adapter delivery signature includes the correlation id (the server's richer form).

**Rationale.** Hand-mirrored dispatch is the demonstrated primary defect generator in this codebase — the keepalive inversion cloned into the WebSocket library, the chunking implementations whose only differences were the bugs, and the five accidental divergences between the two current `DispatchReceivedMessage` methods (registry type, correlation id, task wrapping, logging source, exception mapping). A single implementation makes cross-side fixes land by construction, is independently unit-testable without a live connection, and extends the same benefit to the WebSocket library through the common-elements package (OI-40).

**Alternatives considered.** Patching the two existing implementations in place (rejected: preserves the drift mechanism the fix exists to remove). Separate channel stacks for JSON and binary (rejected: doubles registration, storage, and routing, and makes the one-kind-per-channel rule an unenforceable convention across two dictionaries).

**Consequences.** OI-25's thread-safety defect and OI-37's close-loop defect are resolved once, in the dispatcher. The declared-but-dead adapter counters become real; `ExpectedMessageType` is dropped from the contract (unused and consulted by nothing) unless the owner objects. The dispatcher, host interface, and adapter family join the OI-40 export set. The WebSocket library migrates at its own phase, per OI-40/OI-42.

### KD-09 — The dual-frame work ships as two releases: substrate, then wire break

**Decision.** Implementation is scoped as two releases:

**Release 1 — substrate (no wire change).** The KD-08 channel work in full: adapter relocation, common host interface, `ChannelDispatcher`, server migration off its delegate dictionary, kind flag and binary delivery overload (dormant until binary traffic exists). Bundled with the independent defect fixes that touch the same files and need no protocol change: the dispose/shutdown-ordering fixes (OI-24), the metrics fixes (OI-33), and the adapter close-loop and Start-guard items from OI-37. Fully testable against the current wire format; the `ChannelDispatcher` gains unit tests, which no dispatch code currently has.

**Release 2 — the LibVersion 3 wire break.** One coherent release containing everything the v3 entry declares: the `cReceiveLoop` rework (a rebuilt async read pump resolving OI-20 through OI-23), the five-byte preamble consuming `FrameTypes`, the binary message body, live binary delivery through the dispatcher, the KD-05 chunking rebuild, and the KD-06 limits. Not split further: v3's documented capabilities ship together so the version entry never misstates what a v3 peer supports.

**Binary send surface (Release 2).** Binary send is a distinct method, not an overload of the object-accepting send — `byte[]` is an `object`, and an overload would silently JSON-serialize byte arrays today; the distinct name removes the ambiguity. Client: `SendBinaryMessage_to_Endpoint(byte[] payload, string channel = "", string scope = "", string corelationid = "", string messagetype = "")`; server: the `SendBinary_toClient` equivalent; binary-kind channel adapters expose `SendMessage(byte[])`; and an `Add_ChannelHandler(name, DelBinaryMessageReceived)` facade mirrors the JSON delegate registration.

**Sequencing rules.** Tests lead: the end-to-end large-message tests (both directions, loopback-based) are written against the Release 2 design before the chunking rebuild lands, anchoring the regression the way the keepalive test anchored OI-02. The remaining review defects (OI-26, OI-27, OI-29, OI-30, OI-31, OI-34, OI-35, OI-38) stay off this train as independently shippable patches. The WebSocket library adopts the substrate and v3 elements at its own phase; the OI-40 package extraction happens after Release 2 has proven the shapes — extract known-good code, not designs in motion.

**Rationale.** The substrate refactor and the wire break are the two risky changes; landing them separately means neither's failure investigation is confounded by the other. Release 1 delivers standalone value (thread safety, working counters, correct dispose) even if the v3 work paused. Bundling the receive-loop rework into the wire break avoids rebuilding the read state machine twice.

### KD-10 — Client and endpoint instances are single-use

**Decision.** A session instance (`Client_v1_Abstract` derivatives; server `Endpoint_Abstract` derivatives) is single-use: it may be started once. `Start_Async` SHALL validate instance viability and refuse with an error result unless the instance is in its never-started state; starting from stopping, stopped, or disposed states is refused. Viability is tracked by a lifecycle flag or small state enum (the existing transport-driven `State` is not suitable — it is not reset and not authoritative for the session). A consumer wanting a new session constructs a new instance. `Stop_Async` remains terminal: quiesce, clear delegates, close channel adapters — its existing clearing behavior now matches an enforced contract instead of silently breaking a restart. A consumer-initiated Stop SHALL NOT raise the connection-lost delegate.

**Rationale.** The class previously permitted Start-after-Stop while Stop's teardown cleared all consumer wiring — one method serving both "pause" and "shutdown" contracts, with restart silently losing every registration. The owner's convention never restarts an instance, so the cheap and honest fix is enforcing single-use rather than engineering a correct restart (delegate preservation, state reset, CTS lifecycle). The same viability check closes the double-start hole (two connection loops fighting over one client).

**Consequences.** The already-started guard and the Stop-clearing/lost-handlers items in OI-37 are resolved by this contract; the guide documents single-use lifecycle (create per session) rather than restart flows. Implemented in Release 1 alongside the OI-24 shutdown-ordering fixes, on both client and server session classes.

A pause capability — stopping a client while retaining its delegate/adapter graph until Dispose wipes it — was considered and deferred: it would only earn its place if constructing a configured client instance proves impractical for some consumer, and no near-term use case exists. The codebase is deliberately not structured for it now; if the need arises, it becomes a new decision.

### OI-13 — (Closed by KD-09)

### OI-39 — (Closed by KD-09)

### OI-14 — (Closed)

### OI-15 — Unused chunking DTOs

Established during the review pass: `ChunkAckDTO` and `ChunkRequestDTO` are empty classes with no senders or handlers. `ChunkCancelDTO` is different — it **is** sent (by `LargeMsgSender` on cancellation or mid-sequence send failure) but is not intercepted on either receive side, so it reaches consumer dispatch as a bogus application message and its handler branches are dead (see OI-19). KD-05 wires Cancel into the rebuilt protocol and keeps Ack/Request out of it (reliable ordered transports need neither). Remaining decision: whether the two excluded DTOs stay as reserved surface or are deleted.

### OI-16 — Author the consumer implementation guide

Produce the consumer-facing usage documentation from `references/IMPLEMENTATION_GUIDE_TEMPLATE_R1.md`, covering client setup (`TCPClient_v1_Impl`), server setup (`TCPConnMgr_wListener` + `TCPEndpoint`), channel adapters, configuration knobs for failure-mode timing — including guidance on coordinating the delay intervals (keepalive interval, pong reply window, connection-loop tick; see OI-02) and the size limits (frame, chunk payload, transfer; see KD-06, including the local-policy posture that limit mismatches surface as transfer-time cancels) — and chunking/keepalive behavior a consumer should understand. Status: the guide is authored (`docs/OGA.TCP.Lib_IMPLEMENTATION_GUIDE.md`, anchored to spec Revision 2, describing the LibVersion 3 build) and awaits owner review; it doubles as a fidelity signal against the design during implementation. Remaining in this item: the repository README SHALL gain a high-level overview of the library (both nuspecs point their `releaseNotes` at the README, and both packages embed it as their package readme, so it carries the packages' front-page description).

### OI-17 — (Closed by §13)

### OI-18 — Chunking is non-functional over TCP ⚠ NEEDS YOUR REVIEW

Two independent defects mean the large-message chunking feature cannot work over the TCP transport as shipped. Both were verified directly in code.

**(a) The server keys receivers by the wrong identifier.** `Endpoint_Abstract.cs:1968` passes the *envelope* id (`me.MsgId`) into `ProcessChunkingMessage`, and the receiver is stored under it (`:2131`) and looked up by it (`:2163`, `:2220`, `:2288`). But every frame gets a fresh envelope id — `Client_v1_Abstract.cs:2242` assigns `me.MsgId = GetNextMessageId()` inside the per-frame send — so the id under which the receiver was stored never matches any subsequent chunk frame. The client side keys by the logical transfer id `dto.MsgId` (`Client_v1_Abstract.cs:2973`, `:3005`, `:3062`), which is correct. Net effect: **client-to-server chunked transfers never complete**; every chunk logs "Received message chunk without a chunk start message", the message is lost silently, and an orphaned receiver accumulates per attempt until the 120-second prune. Server-to-client transfers work.

**(b) Chunk frames exceed the frame cap.** The chunking threshold and chunk size are character-based (`Client_v1_Abstract.cs:2105`, `:2118` — `MaxMessageSize - 1024` = 1,047,552 chars), but each chunk's `Data` slice is JSON-escaped twice before hitting the wire — once serializing `ChunkDTO`, again as the envelope's `Data` string — and only 1024 bytes of headroom exist. Because the chunked payload is itself JSON, it is quote-dense, and each `"` expands to `\\\"`. A measured worst case produced a 1,591,749-byte frame against the 1,048,576-byte cap. `cReceiveLoop.cs:1097` treats an oversized frame as fatal, so the feature tears down the connection it exists to protect. Note the send-side size guard is deliberately skipped when chunking is enabled (`:2220-2237`), so nothing else catches it. The same char-vs-byte confusion also lets a *non-chunked* message just under the threshold exceed the cap after single-level escaping.

Resolution path decided: these defects are resolved by replacement rather than patching — the KD-05 chunking rebuild removes both failure classes by construction. This item stays open as the defect record until the rebuild ships. Until then, consumers should be told the practical message ceiling is a single frame.

Status: resolved by replacement in Release 2. The KD-05 rebuild keys receivers by the explicit TransferId (defect a cannot recur) and slices encoded bytes with per-segment byte-true measurement (defect b cannot recur). The end-to-end large-message tests OI-05 called for — both directions, json and binary inner types — are in the client suite (`V3Framing_Tests`) and pass over loopback. Closes on release.

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

Status: resolved by replacement in Release 2. Byte-based slicing removes surrogate splitting (verified by a non-BMP payload test); receivers are removed on End/Cancel/rejection under a lock; completeness is validated byte-true before dispatch; ChunkCancel is intercepted and handled on both sides; the sender checks every control and segment send result and issues a best-effort cancel on failure; MaxTransferSize is enforced at start declaration and during accumulation (over-limit test in `V3Framing_Tests`). Closes on release.

### OI-20 — Receive-loop teardown can crash the process ⚠ NEEDS YOUR REVIEW

In `cReceiveLoop.cs`, the try/catch around the read callback ends at line 874; all frame-processing code after it (through `Process_Received_MessageBuffer`) runs unprotected on an IO-completion thread. `CloseDown()` concurrently disposes and nulls the buffer (`:388-396`) with no synchronization against an in-flight callback. A read completing while the owner disposes the loop — or while the frame-read timeout fires and calls `CloseDown` — dereferences the nulled buffer at `:979` or `:1424`, throwing a `NullReferenceException` on a threadpool thread with no handler, which terminates the process. Candidate fix: a close/callback gate (lock or interlocked state) plus wrapping the post-`EndRead` processing.

Status: resolved by replacement in Release 2. The rewritten `cReceiveLoop` is a single async read pump with no shared mutable buffers between a callback and teardown; the entire pump body is exception-guarded, lifecycle transitions are serialized under one lock, and reads abandoned at closedown have their eventual faults observed. Closes on release.

### OI-21 — Receive loop caps throughput at a few messages per second ⚠ NEEDS YOUR REVIEW

`cReceiveLoop.cs` blocks a threadpool thread with `Thread.Sleep(100)` on every read-callback entry (`:687`), before every body-read queue (`:519`), and in `CloseDown` (`:383`). A single message therefore costs roughly 300 ms of blocked thread time (length callback + body queue + body callback), limiting a connection to roughly 3–10 messages per second and holding a blocked thread per active connection. The sleeps appear to exist to let a cancellation token settle before disposal; a proper CTS lifecycle would remove the need. This is a scalability ceiling on a live service and worth measuring before and after.

Status: resolved by replacement in Release 2. The rewritten pump contains no `Thread.Sleep` and blocks no threadpool thread between frames. Informal measurement from the suite's 10 MB chunked-transfer test: ~35 MB/s over loopback (thousands of frames per second), versus the old ceiling of roughly 3–10 messages per second. Closes on release.

### OI-22 — Frame validation gaps

`cReceiveLoop.cs:1097` sanity-checks the frame length only against `MaxMessageSize`; a negative length passes and dies later via an incidental `ArgumentOutOfRangeException` from `BeginRead` rather than the check. Separately, `FrameReadTimeout` bounds only a single `BeginRead` and is re-armed on every partial read, and the length-read phase has no timeout at all — so a peer dripping one byte every few seconds holds a connection and its buffer indefinitely (slowloris). Candidate fixes: add `tempint < 0` to the sanity check, and add a whole-frame deadline distinct from the per-read one.

Status: resolved by replacement in Release 2. The v3 preamble validation rejects negative and over-cap lengths explicitly, and `FrameReadTimeout` is now a whole-frame deadline armed at a frame's first byte — a dripping peer is classified Lost at the deadline regardless of how the bytes arrive. Covered by the receive-loop suite's partial-frame tests. Closes on release.

### OI-23 — Frame-read timeout races

`cReceiveLoop.Arm_FrameReadTimeout` (`:619-666`) awaits `Task.Delay` on a token, then re-reads `this._readcts` — which may by then be a *new* CTS belonging to a later frame — and declares the connection `Lost` on a healthy connection. The same block swallows an `ObjectDisposedException` from a concurrently disposed CTS, silently leaving that frame with no read timeout. Candidate fix: capture the CTS (or a generation counter) in the local scope the timeout task closes over.

Status: resolved by replacement in Release 2. The rewritten pump has no per-frame CTS churn: the deadline is a local value raced against each read, so there is no stale-token window to misclassify a healthy connection. Closes on release.

### OI-24 — Client shutdown ordering and dispose do not wait ⚠ NEEDS YOUR REVIEW

Two related defects in the client lifecycle:

- `Client_v1_Abstract.cs:479` calls `Stop_Async().GetAwaiter()` without `GetResult()`, so `Dispose` returns immediately while teardown continues on another thread — and `Logger` is nulled underneath it. The same fire-and-forget pattern appears at `TCPClient_v1_Abstract.cs:733`, `:798`, `:849` (benign only because the TCP override completes synchronously) and in the server at `Endpoint_Abstract.cs:371`, `:1040`.
- `Stop_Async` closes the transport *first* and cancels `_cts` last, after ~200 ms of delays (`:574-656`). In that window the connection loop can observe `!IsConnected`, treat it as connection loss, and start a full reconnect — including re-registration with the server — after the consumer called Stop. Candidate fix: cancel `_cts` before closing the transport.

Status: implemented in Release 1 (commit 15519fa) — Stop cancels the loop first and disarms the connection-lost delegate; Dispose blocks until teardown completes on both session classes; the fire-and-forget transport closes now wait. Verified by the lifecycle test suite; closes on release.

### OI-25 — Non-thread-safe dictionaries on hot paths ⚠ NEEDS YOUR REVIEW

Three plain `Dictionary` instances are mutated and enumerated from different threads with no synchronization:

- `_ChannelMessageHandlers` (client): written by `Add_ChannelAdapter`/`Remove_ChannelHandler`/`Close_ChannelAdapters` (`Client_v1_Abstract.cs:685`, `:735`, `:745`), read by dispatch on the receive thread (`:3430`). Registering a channel adapter at runtime while traffic flows can throw or corrupt the table — and runtime channel registration is an advertised feature (UR-02).
- `_largemsgreceivers` on both sides: mutated on the receive thread, enumerated by the prune on the connection-loop thread (`Client_v1_Abstract.cs:2973`/`:3142`; `Endpoint_Abstract.cs:2131`/`:2304`). A chunk arriving during a prune throws inside the loop, killing the prune pass (client) or tearing down the connection (server).

Resolution path decided: the channel-handler dictionaries are resolved by the KD-08 `ChannelDispatcher` (one thread-safe registry replacing both sides' collections), and the `_largemsgreceivers` dictionaries by the KD-05 chunking rebuild. Status: the channel-handler half is implemented in Release 1 (commit 15519fa), with a concurrency test in the dispatcher suite; the receiver-dictionary half is implemented in Release 2 — every access to the receiver registry (register, lookup, remove, prune, clear) is serialized under one lock on both sides. Closes on release.

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

Status: implemented in Release 2 per the owner's decision — the tap is a deliberate dumb-pipe testing mode that owns ALL processing. The behavior is now stated in the tap's XML documentation on both sides, and the delegate signature carries the v3 typed frame (`frametype`, `byte[]`) rather than a decoded string. Closes on release.

### OI-32 — cCustom_Serializer correctness defects

Independent of how widely it is used (OI-11), the codec has real defects: the embedded-length string methods store `string.Length` (char count) as the prefix while writing UTF-8 bytes, so any non-ASCII string round-trips truncated and misaligns the rest of the stream (`:144-171`, and the string-array metadata at `:1570`); `Deserialize_Version` passes recovered values positionally into a constructor with different parameter order, swapping Build and Revision, and throws on 2- or 3-part versions (`:559-562` vs `:1239-1245`); `size_of_short` is 4 rather than 2 (`:11`), so short round-trips misreport consumed bytes; the big-endian path reverses bytes **in place** (mutating the caller's buffer) and even reverses UTF-8 text, which has no endianness; and `cMultiString_MetaData.Deserialize` allocates an array from an unvalidated wire-supplied count before its bounds check (`:1466-1499`). Since production traffic only uses the Int32 codec, none of this is live-service urgent — but it bears directly on OI-11's disposition and on OI-13, which may want a working primitive codec.

### OI-33 — Metrics and telemetry defects

`Sent_Message_Count` is permanently zero on both sides: `TCPEndpoint.cs:354` and `TCPClient_v1_Abstract.cs:605` increment through a `Metrics` getter that returns a fresh copy each call. The same getters dereference the receive loop without a null check, so reading `Metrics` before the first connection throws. Owner decision: reading `Metrics` before a first connection SHALL return a baseline (empty) metrics instance rather than throwing. In `cReceiveLoop.cs:1444`, `Last_Received_Message_Time` is set from `DateTime.Now` while every sibling write uses `UtcNow`. And all three OpenTelemetry span names lose their suffix to `??` precedence, so every span is named just `tcpsocket`/`tcp` instead of the three intended distinct names.

Status: all four defects implemented/fixed in Release 1 (commit 15519fa); closes on release.

### OI-34 — Unbounded task spawning on ping and loopback paths

Each received ping spawns a `Task.Run` to send the pong (`Endpoint_Abstract.cs:2376-2384`), and each message spawns one per loopback echo (`:1887`, `:1937`), all queueing on the write semaphore. A client sending faster than the server's synchronous write drains accumulates unbounded queued tasks and buffered payloads, with no cap, coalescing, or drop policy. A pending-pong flag would bound the keepalive case.

### OI-35 — Client connection loop blocks threadpool threads

The connection loop uses `ExpBackoff_wJitter.Delay` (a `Thread.Sleep` loop) at eight call sites despite an available `DelayAsync`, and `WaitforCondition` uses `SpinWait.SpinUntil` for up to the full registration-reply timeout (`Client_v1_Abstract.cs:1646`). One client instance can therefore tie up a threadpool thread for the whole backoff or registration window; many client instances in one process can starve the pool. `WaitforCondition`'s `scaninterval` parameter is computed and then ignored.

### OI-36 — Binary-frame plumbing is inert on the TCP transport

`Process_ReceivedBinaryFrame_from_Client` (`Endpoint_Abstract.cs:2000-2058`) is only ever called from the WebSocket endpoint, and `cReceiveLoop` has no binary-frame concept — its only payload delegate hands out a decoded string. So on TCP, `OnBinaryFrameReceived` never fires and `Cfg_BinaryFrameHandling_IsFatal` has no effect: a consumer wiring a binary handler on a TCP endpoint silently gets nothing. Resolution path decided: the Release 2 wire break (KD-09) connects this plumbing — the surviving delegate becomes the no-channel binary fallback of the KD-08 dispatcher. This item stays open as the record until that ships.

Status: implemented in Release 2. Binary frames are first-class on the TCP transport (KD-03/KD-04), and `OnBinaryFrameReceived` is the no-channel binary fallback wired through the dispatcher on both sides. Binary roundtrips in both directions, including chunked ones, are covered in `V3Framing_Tests`. Closes on release.

### OI-37 — Dead code and minor defects (batch)

Collected low-severity items, each small and independent:

- `cEndpoint_Ping_Tracking` is confirmed dead — zero references repository-wide outside the shared-project include. It is public API, so removal is a minor breaking change; decide keep-or-remove. (If revived it needs fixes: a 20 ms coercion of its delay setters, a copy/paste log message in `Disable()`, an unknown-state branch that returns `None` instead of `CloseConnection`, and local-time timestamps.)
- The `res == -1` fatal-recycle path in `TCPClient_v1_Abstract.cs:829-859` is unreachable: `Process_ReceivedMessage` maps every negative internal-message result to `0` (`Client_v1_Abstract.cs:2752-2763`), so a garbled registration reply is only logged.
- `Close_ChannelAdapters` (`Client_v1_Abstract.cs:745-757`) removes the adapter *after* calling its `Close()`; a consumer adapter that throws from `Close()` leaves the collection unchanged and the loop refetches the same element forever — an infinite loop inside `Dispose`. (Resolved in Release 1: the dispatcher's `CloseAll` empties the registry before closing, and a throwing `Close` is contained — covered by a dispatcher test.)
- `Start_Async` has no already-started guard (`:541-552`); a second call orphans the first `CancellationTokenSource` and runs two connection loops against one client. (Resolved by KD-10's viability check.)
- `Stop_Async` nulls all consumer delegates and closes all channel adapters (`:586-590`, `:647-649`), so a Stop-then-Start cycle silently loses every handler the consumer registered. (Resolved by KD-10: Start-after-Stop is refused, making the clearing consistent with an enforced single-use contract.)
- A failed ping *send* returns success (`:1832-1841`), so only pong timeout recycles the connection.
- `ExpBackoff_wJitter` clamps `JitterHeight` above 1.0 to **0** rather than 1.0 (`:31-37`), silently disabling jitter for a caller asking for maximum; its `maxRetries` constructor argument is stored but never enforced; and `Delay()` returns success even when cancelled.
- `_instance_counter++` in `cReceiveLoop.cs:161` is not atomic despite the field being `volatile`, so concurrent accepts can share an InstanceId in logs.
- Log-string precedence bugs of the same shape as OI-03 exist in `ClientInfo.ToLogString` and the chunking DTOs' `ToLogString`.
- `cListener`'s `SendTimeout` floor is dead code (missing `else`, `:62-67`).
- Explicit JSON nulls for `MessageType`/`Scope` cause an NRE that drops the message (`Endpoint_Abstract.cs:1872`, `:1875`).
- Several `cReceiveLoop` error paths request a `Closed` transition from `Open`, which the state machine forbids, producing a spurious "state change prevented" error log on every such path and losing the intended `Error` classification.

### OI-44 — XML documentation completeness on touched classes

Classes modified by this effort SHALL gain XML documentation on the properties, methods, and fields currently emitting missing-comment warnings, as part of the change that touches them — starting with the Release 1 and Release 2 work (KD-09). Comments follow the OI-02 precedent: expressive, stating what a member protects or coordinates with, not restating its name. A final sweep for untouched classes still emitting warnings is deferred until the release train completes.

### OI-45 — Component-level machinery documentation in the design spec

Extend the design documentation so a future reader can understand the machinery of each library component and class — deeper than §5.1's tier descriptions: per-component narratives of internal mechanics (the receive state machine, the connection loop's phases, dispatcher internals, chunking sender/receiver lifecycles, manager/listener interplay), likely as an expansion of §5 or a dedicated component reference within the spec. Authored against the Release 2 design so it documents what is being built, then verified against code as implementation lands (a congruency-check candidate per the methodology).

### OI-46 — Test helper classes require updates mirroring the v3 changes

The test projects contain helper classes that proxy library functionality — the `TESTINGSRVR_*` forked server classes, `Simple_TCPListener` harnesses, `cClientServer_Test_Helper`/`cClient_Helper`, `CommonChannel`, and the test-only client subclasses — simulating "the other end" of conversations or acting as harnesses. The Release 1 and Release 2 changes (channel substrate, framing, receive-loop contract, chunking) will invalidate some of them. Most will surface through unit-test failures during the release work; this item tracks the ones that need explicit, deliberate update — in particular the `TESTINGSRVR_*` fork (whose re-sync/consolidation decision is OI-06) and any helper that hand-builds legacy frames. Resolved incrementally alongside the KD-09 releases.

Status: completed with Release 2. The `TESTINGSRVR_*` fork is re-synced (see OI-06); the test-side frame-send helpers in `ReceiveLoop_Test` and `TCPEndpoint_Tests` now write the v3 preamble (with a typed overload for non-json frames); hand-built legacy frames inside tests were converted to v3 framing except where a legacy frame is the point of the test (the KD-07 detector test). Closes on release.

### OI-43 — Repoint wiki links to the Bookstack instance after page migration

File headers and version documentation — notably `LibVersions.cs` — reference the owner's former Confluence wiki. Those links are temporarily dead: the Confluence site is taken down and its pages are being migrated to the owner's Bookstack instance. Once the pages are migrated, update the in-code URL references to their new Bookstack locations; the owner prioritizes the migration itself. Independent of the links, §9.1.3 remains this spec's self-sufficient record of LibVersion semantics, and the v3 `LibVersions.cs` entry (KD-07, written at Release 2) should carry the new wiki URL from the start.

Status note: the v3 entry was written at Release 2 while the Bookstack migration was still pending, so no new URL existed to carry; it references this spec (KD-03/04/05/07) instead. When the wiki pages land, add the Bookstack URL to the v3 entry along with repointing the v1/v2 links.

### OI-42 — Propagate LibVersion 3 into the WebSocket library's version registry

The WebSocket library (OGA.WSClient_Base) maintains its own `WSLibVersions.cs` documenting versions and capabilities for the WebSocket transport; both libraries currently stand at version 2. When the WebSocket library adopts the shared dual-frame elements (OI-40), its `WSLibVersions.cs` SHALL gain the corresponding version-3 entry documenting the adopted capabilities (binary message body layout, rebuilt chunking, limits), mirroring the TCP library's `LibVersions.cs` v3 entry from KD-07. Recorded so the propagation is not forgotten; owner executes it in the WebSocket library's own repository at that phase.

Carried from OI-48: when editing that file, also apply the constants-before-default declaration ordering (version constants declared above any default field that references them) — the harmonized copy likely shares the TCP file's latent static-initializer-order null, which becomes live the moment any code consumes the default.

### OI-41 — Negotiated size limits and capability exchange at registration

Under KD-06, the size limits are local policy: each side enforces its own receive limits, and a sender exceeding the receiver's `MaxTransferSize` discovers it via the transfer-time Cancel rather than up front. The owner's suggestion, deferred to a later phase: exchange limits at registration so the two ends operate on the more constrained set — the client announcing its receive limits in its registration props, the server announcing its own in the reply's currently-empty `Props`, and each sender honoring the peer's receive limits so oversize sends fail fast at the call site instead of mid-transfer. This dovetails with the version-guard/capability-announcement question (OI-39): the registration reply's `Props` is the natural single mechanism for the server to declare limits, supported frame types, and other capabilities. Held until the dual-frame work is functioning; when taken up, decide what is announced, what is enforced versus advisory, and how config asymmetry (send vs. receive limits) is expressed.

### OI-40 — Publishable common-elements library for cross-library reuse

The dual-frame design work is producing elements that are deliberately transport- and library-neutral: the common messaging-host interface that decouples the channel-adapter substrate from `Client_v1_Abstract` (so client, server, TCP, and WebSocket endpoints can all host the same adapters), the relocated adapter family, the binary message body layout, and the rebuilt chunking classes. The separate OGA.WSClient_Base library is intended to consume these same elements; today, sharing between the two libraries happens by manually harmonized code copies, which is the drift mechanism already seen in OI-06 and in the keepalive fix backport.

Plan for a published library (nuget package) of the common elements that both this library and the WebSocket library reference, replacing copy-based sharing for the shared substrate. Deliberately deferred: the elements are being designed and proven inside this repository first, and the extraction/packaging boundary gets decided once something is functioning here. When taken up, decisions needed: package naming and namespace (transport-neutral), which of the existing `ClientServerShared_SP` contents belong in it versus staying TCP-specific (e.g., the frame-type registry and `cReceiveLoop` are TCP-only), and the release-coordination model between three packages on one Jenkins/GitVersion pipeline pattern.

### OI-38 — (Closed)

### OI-47 — (Closed)

Resolution: environmental to the review VM only — its 4.5.2 targeting pack was never fully installed (doc files present, no reference DLLs). The owner's Jenkins build VM and dev PC both carry full 4.5.2 support and compile and run these tests there; the sources themselves are proven 4.5.2-compatible (the SDK-style NET452 library target and the shared test sources under the old-style NET48 csproj both build clean). No change needed.

### OI-48 — (Closed)

Resolution: the latent static-initializer-order null in `LibVersions.cs` never affected shipped code (nothing read the default field until Release 2's constructor change) and is fixed by declaration reordering with an explanatory comment. Owner reviewed and closed. Suggested follow-up carried by OI-42: apply the same constants-before-default ordering to the WebSocket library's `WSLibVersions.cs` when its v3 entry is added.

### OI-49 — Discuss in isolation: the receive-loop rewrite's contract regressions ⚠ NEEDS YOUR REVIEW

Scoping first: these were regressions of the **Release 2 rewrite relative to the old `cReceiveLoop`**, introduced and fixed within this session's implementation work — they never existed in any shipped version, and `cReceiveLoop` is TCP-only, so there is nothing that must be backported. Three items, caught by the reworked receive-loop suite during verification:

1. **Status-publish granularity.** The old loop published both the `Shutting_Down` and `Closed` transitions on closedown; the rewrite initially collapsed that to one published change. Restored: consumers observe both, matching the historical contract the endpoint state machines and tests encode.
2. **Socket ownership on Dispose.** The rewrite initially passed a cancellation token into `NetworkStream.ReadAsync`; cancelling a pending socket read **aborts the underlying connection**, so disposing the loop killed a socket the parent endpoint still owned. Restored: reads start token-free and are raced against a cancellable delay; local closedown abandons the pending read (its eventual fault is observed) and leaves the transport to its owner. **This is the one item with possible WebSocket-library relevance**: if any WS receive path cancels a pending `ReceiveAsync` via token expecting the socket to survive, it is worth verifying what that does to the WebSocket state — that is the discussion to have.
3. **Metrics baseline.** The old loop's last-received timestamp read as "now" from construction (connection open counts as liveness, so keepalive credit never sees a default time); the rewrite initially left it at default until the first frame. Restored at construction and re-baselined at start.

### OI-50 — Discuss in isolation: v3 registration semantics in the base classes ⚠ NEEDS YOUR REVIEW

Nothing about v1 or v2 handling changed — both behave exactly as before, which is why the WebSocket library's v1/v2 flow is unaffected and this library's mirror of it keeps working. What Release 2 added is how the **base classes** handle version 3, and two judgment calls inside that are the discussion:

1. **Client side.** The base `Send_RegistrationMessage` was v1-only: it refused to run unless `LibVersion == "1"`, because v2 registration carries mandatory app-identity props (`appid`, `appver`) that only a derived class knows — so v2 clients override it (same pattern as the WebSocket library). KD-07 requires the stock v3 client to announce version 3, so the base method now handles v1 **and** v3: v3 sends the `tcplibver` prop plus the same props v1 sends; v2 still requires the override, unchanged.
2. **Server side.** The accepted range extends to [1..3]. The app-identity presence mandate was written as `libver > 1`; kept as-is, it would reject every stock v3 client, since the base class has no app identity to send. The implemented choice scopes the **presence mandate to v2 only**, treats app-identity props as optional-but-honored for v3, keeps the AppId-immutability rule for any registration that supplies one, and stops a later registration that omits app-identity props from erasing previously recorded `ClientInfo` values.

The alternative worth weighing together: define v3 registration as "v2 plus the version prop" — i.e., app identity stays mandatory at v3. That is arguably cleaner policy, but it means a stock base client cannot register without the consumer supplying app identity — either by overriding registration (as for v2) or by the base class gaining `AppId`/`AppVersion` properties. Owner's call; the server-side mandate is a one-line scope change in each of the two endpoint classes (plus the fork) if the decision goes the other way.

**Decision (owner, 2026-08-03).** v3 registration includes the registration info of v2, as v2 expanded on v1 — app identity stays mandatory at every version past 1.

Status: implemented. `Client_v1_Abstract` gained `AppId`, `AppVersion`, and `Language` properties; the base `Send_RegistrationMessage` registers v1 and v3, and for v3 requires `AppId`/`AppVersion` before sending (failing fast client-side with a clear log) and announces them alongside the libver prop, with `language` sent when populated. The server-side mandate is restored to v2-and-later on both endpoint classes and the fork. v2 clients continue to override registration, unchanged. §9.1.3 and the v3 entry in `LibVersions.cs` record the contract; the client suite registers with app identity throughout. Closes on release.

---

*End of document.*
