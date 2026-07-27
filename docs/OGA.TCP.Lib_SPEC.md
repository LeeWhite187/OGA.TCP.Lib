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

OGA.TCP.Lib is an in-service library used for message-based TCP connectivity between processes. It is published as two NuGet packages — OGA.TCP.Lib (client) and OGA.TCP.Server.Lib (server) — built from a common set of Visual Studio Shared Projects and compiled into multiple .NET target frameworks (client: NET Framework 4.5.2 through NET 7; server: NET 5 through NET 7). Packages are versioned and published by a Jenkins pipeline to a private BaGet feed.

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

**UR-08 — Multi-framework availability.** The client library SHALL be consumable from NET Framework 4.5.2, NET Framework 4.8, NET Standard 2.1, and NET 5/6/7 applications; the server library from NET 5/6/7 applications. (A NET 8 target is planned; see OI-12.)

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

C# on .NET, multi-targeted (client: net452, net48, netstandard2.1, net5.0, net6.0, net7.0; server: net5.0, net6.0, net7.0) from Visual Studio Shared Projects. JSON serialization via Newtonsoft.Json. Logging via NLog through OGA logging conventions. Build/publish via Jenkins to a private BaGet feed; versioning via GitVersion with `+semver:` commit-message bumps. Full inventory and rationale to be completed during the reverse-engineering pass (OI-14).

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
    OGA.TCP.Lib_NET452 ... _NET7/       client target csprojs importing the shared projects
    OGA.TCP.Server.Lib_NET5 ... _NET7/  server target csprojs
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
- **No new transports.** Beyond the planned binary-transmission capability (OI-13), no additional transports are in scope.

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

Status: items 1, 2, 3, and 5 are implemented in commit 8b8df28. The regression test fails against the prior logic and passes with the fix. Item 4 is carried by OI-16. Remaining before closure: run the full test suites in the owner's test environment (the client suite binds an environment-specific address and cannot run elsewhere), then push to trigger the Jenkins release.

### OI-03 — MessageEnvelope.ToLogString defects ⚠ NEEDS YOUR REVIEW

`MessageEnvelope.ToLogString()` prints `MsgId` on the `Message_Type` line (copy/paste slip), and every `"label = " + value ?? ""` line null-coalesces the already-non-null concatenation result rather than the value (operator precedence). Log-only impact; recommendation: fix the field reference and parenthesize the coalesces.

### OI-04 — async void chunk processing

`ProcessChunkingMessage` is `async void` on both client and server sides. Exceptions thrown in it are unobservable by callers and can crash the process depending on runtime; completion cannot be awaited. Candidate fix: `async Task` with an explicitly discarded/logged continuation, or restructure the call site. To be assessed during the review pass.

### OI-05 — Test coverage gaps

Areas with no direct test coverage: the chunking helpers (`LargeMsgSender`, `LargeMsgReceiver`) in isolation, chunk-cancel/timeout/prune paths, `ExpBackoff_wJitter`, `cBuffer`, `cEndpoint_Metrics`, `ConnectionMgr_Abstract` in isolation, and `cCustom_Serializer` (one test for ~1,580 lines). The binary-frame members added in a9f960d are also untested. This item tracks the test gap-filling phase; it will be broken into concrete work once the review pass completes.

### OI-06 — TESTINGSRVR_* forked server copies drift hazard

`Testing_CommonHelpers_SP` contains forked copies of the server classes (`TESTINGSRVR_Endpoint_Abstract` at ~2,960 lines, plus listener/endpoint/model copies) declared into the real `OGA.TCP.Server` namespaces, because client test targets include frameworks the server library doesn't support. Client/server compatibility is therefore tested against a snapshot that can silently drift from the real server code. Options to assess: scripted diff check between fork and source, consolidation, or accepting and documenting the drift risk with a periodic re-sync step.

### OI-07 — NETStd21_Test project double-references the library

`OGA.TCP.Lib_NETStd21_Test.csproj` both imports the library shared-project sources and carries a `ProjectReference` to `OGA.TCP.Lib_NETStd21.csproj`, unlike the other test projects (import-only). Potential duplicate-type situation; verify intent and align with the other test projects.

### OI-08 — Build pipeline issues

Known issues in the Jenkinsfile/nuspec flow: (a) no `dotnet test` stage exists, so tests never gate publication; (b) the version-stamping stage skips the NETStd21 client csproj and all three server csprojs; (c) the client nuspec `releaseNotes` is boilerplate. Each sub-item needs an owner decision on whether it is intentional before changes are made. (Debug-configuration packaging was initially listed here and is confirmed deliberate — see KD-01.)

### OI-09 — scratchdev project does not reference the library

`scratchdev/scratchdev.csproj` imports no shared project and has no project reference, while its `Program.cs` calls into library types — it almost certainly does not compile. Decide: fix its references, or remove it from the solution.

### OI-10 — Dead documentation links

`LibVersions.cs` and possibly other file headers link to a wiki (`wiki.galaxydump.com` / Atlassian) for LibVersion semantics. Verify the links, and in any case capture the LibVersion 1 vs 2 semantics in §9 of this spec so the spec is self-sufficient (folds into OI-14).

### OI-11 — cCustom_Serializer breadth vs. actual usage

`cCustom_Serializer` (~1,580 lines) implements a hand-rolled binary codec for many primitive types, but in practice only the Int32 length-prefix serialize/deserialize appears to be exercised on the wire path. Assess how deeply it is actually used (the owner is also unsure), then decide: document it as the standard wire-primitive codec (and cover it with tests, per OI-05), trim it, or leave as-is with its role documented. Relevant to the binary-transmission design (OI-13), which may want exactly such a codec.

### OI-12 — Add NET 8 compile targets

Add `OGA.TCP.Lib_NET8` and `OGA.TCP.Server.Lib_NET8` csprojs mirroring the NET7 ones (TFM `net8.0`, `NET8` define), plus corresponding test projects; add to `OGA.TCP.Lib.sln`; add net8.0 dependency groups and file entries to both nuspecs; add restore/build/version-stamp lines to the Jenkinsfile (coordinate with OI-08(c) so the new project is not skipped by the stamping stage).

### OI-13 — Binary data transmission capability ⚠ NEEDS YOUR INPUT

Today all traffic is JSON text inside the envelope; a recent commit (a9f960d) added receive-only binary-frame delegate plumbing to the base classes. Design a first-class binary transmission capability for the TCP transport: whether binary rides inside the existing envelope (e.g., base64 or a new frame discriminator), or as a distinct frame type alongside the length-prefixed JSON frames; send-side API shape; interaction with chunking; and LibVersion implications for wire compatibility with live peers. Scope decision needed from the owner before design: minimum viable (opaque byte[] send/receive per channel) vs. typed binary payloads. Design outcome will be recorded as KDs and §9 protocol content.

### OI-14 — Complete the reverse-engineered spec sections

Author the pending sections of this spec from the code: §2 Functional Requirements, §3 NFRs, §5 Architecture, §6 Data Model, §7 Solution Structure, §8 Interfaces/Contracts, §9 Protocols and Flows (framing, envelope, registration, keepalive, chunking, LibVersion 1 vs 2). This is the "reverse out a design spec" goal of the improvement effort and is expected to run alongside the code-review pass.

### OI-15 — Unused chunking DTOs

`ChunkAckDTO`, `ChunkRequestDTO`, and `ChunkCancelDTO` are marked as currently-unused chunk message types (Cancel is handled server-side only). Decide during review whether they represent planned protocol surface (document as reserved) or dead code (candidate for removal in a deliberate change).

### OI-16 — Author the consumer implementation guide

Produce the consumer-facing usage documentation from `references/IMPLEMENTATION_GUIDE_TEMPLATE_R1.md`, covering client setup (`TCPClient_v1_Impl`), server setup (`TCPConnMgr_wListener` + `TCPEndpoint`), channel adapters, configuration knobs for failure-mode timing — including guidance on coordinating the delay intervals (keepalive interval, pong reply window, connection-loop tick; see OI-02) — and chunking/keepalive behavior a consumer should understand. Blocked behind OI-14 far enough that the protocol facts it cites are settled.

### OI-17 — Systematic code-review pass

The findings recorded so far (OI-02, OI-03, OI-04, OI-06, OI-07, OI-09) came from an initial structural survey, not a systematic review. Perform the full review pass over the shared projects (excluding the OI-01 WebSocket files), recording each accepted finding as a new OI (or folding trivial ones into a batch item), covering at minimum: the six largest files, thread-safety of send/receive/dispose paths, registration prop parsing, timer/loop lifecycle, and exception handling patterns.

---

*End of document.*
