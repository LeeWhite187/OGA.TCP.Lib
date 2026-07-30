# OGA.TCP.Lib — Implementation Guide

**Library:** `OGA.TCP.Lib` (client) / `OGA.TCP.Server.Lib` (server)
**Spec:** OGA.TCP.Lib_SPEC.md (Revision 2)

This guide is a standalone reference for anyone building an application on top of OGA.TCP.Lib. It explains what each piece is, why it exists at a brief level, and how to wire it together — for both the client and server sides. Where the guide describes a design decision briefly, the spec contains the full rationale. The guide describes the library at **LibVersion 3** (the dual-frame version defined by spec KD-03 through KD-09).

---

> **Critical context callout.** Two things trip up new consumers. First: a connected socket is not a ready session — gate your sends on `AllowSend` (or the connected delegate), not `IsConnected`; see §2.1. Second: every channel is declared JSON-kind or binary-kind when registered, and carries only that kind of message; see §2.3.

---

## Table of Contents

1. [What this is and what problem it solves](#1-what-this-is-and-what-problem-it-solves)
2. [How it works — the big picture](#2-how-it-works--the-big-picture)
3. [Public components](#3-public-components)
4. [Library dependencies](#4-library-dependencies)
5. [Configuration and modes](#5-configuration-and-modes)
6. [Implementing the required interfaces](#6-implementing-the-required-interfaces)
7. [Wiring and integration](#7-wiring-and-integration)
8. [Startup sequencing](#8-startup-sequencing)
9. [Operational patterns and recipes](#9-operational-patterns-and-recipes)
10. [Extension points](#10-extension-points)
11. [Error handling](#11-error-handling)
12. [Compatibility and versioning](#12-compatibility-and-versioning)
13. [Migration from earlier library versions](#13-migration-from-earlier-library-versions)
15. [Testing your implementation](#15-testing-your-implementation)
16. [Troubleshooting](#16-troubleshooting)

---

## 1. What this is and what problem it solves

Raw TCP gives you a byte stream between two processes — no message boundaries, no typing, no routing, no liveness detection, no reconnect. Every application that opens a socket ends up re-implementing the same scaffolding around it. OGA.TCP.Lib is that scaffolding, done once: a message-based connectivity layer where a client sends an object (or a byte array) and the other end receives it with enough metadata — message type, channel, scope, correlation id — to route it to the right handler.

Connections are self-maintaining. The client owns a connection loop that establishes the transport, registers itself with the server, exchanges keepalives, detects dead peers, and reconnects with exponential backoff — your code just sends, receives, and reacts to connection-state delegates. The server tracks every connected client with identity metadata (user id, device id, app id/version) and lets your service code look connections up and push messages to them.

Two message kinds travel over one connection: **JSON messages** (any serializable object, wrapped in an envelope) and **binary messages** (raw `byte[]` payloads with the same routing metadata, no encoding overhead). Messages larger than the frame limit are transparently split into chunks and reassembled on the far side, interleaved so a large transfer doesn't monopolize the connection.

**The library does:** message framing and typing, channel multiplexing, registration and client tracking, keepalive and dead-peer detection, automatic reconnect, large-message chunking, and binary transport.
**The library does NOT do:** TLS or any transport security, authentication (it carries identity, you verify it), message brokering or queuing, delivery guarantees beyond the live TCP session, or persistence. If the connection drops, unacknowledged in-flight messages are gone — design your application protocol accordingly.

---

## 2. How it works — the big picture

### 2.1 Connections and readiness

The client (`TCPClient_v1_Impl`) runs a connection loop: connect → register → monitor → reconnect on loss. Registration is a handshake — the client announces its identity and capabilities, the server assigns the connection an authoritative id and replies. Only after the reply is validated (by default) does the session become **ready**: `AllowSend` turns true and the connected delegate fires. Sends attempted before readiness return `0` rather than throwing.

This is the invariant to internalize: **`IsConnected` describes the socket; `AllowSend` describes the session.** A socket can be connected while registration is still in flight, and `TcpClient.Connected` can read true long after a peer has silently died. React to `OnConnectionLost`/your connected handling, and gate sends on readiness.

On the server, each accepted connection becomes a `TCPEndpoint` — a per-connection object owning that conversation — registered in a connection manager that your service queries.

### 2.2 Messages and frames

Every message travels as a frame: a 4-byte length, a 1-byte frame type, then the body (spec §9.1.1). You never touch frames directly, but the frame type is why one connection carries both message kinds:

- **JSON messages** — your object is serialized, wrapped in a `MessageEnvelope` (message id, type name, channel, scope, correlation props), and sent as a JSON frame. Receivers get the payload JSON plus the type name for dispatch.
- **Binary messages** — your `byte[]` travels as a binary frame: a small JSON routing header (same fields, minus the payload) followed by your bytes, raw. No base64, no escaping — a 1 MB payload costs 1 MB plus ~100 bytes of header.

All of the library's own machinery — registration, keepalive pings, chunk control — rides JSON frames. The binary path carries only your payloads (and chunk data).

### 2.3 Channels

A channel is a name on a message that routes it to a registered handler, letting independent features share one connection. Register a handler as a simple delegate or as a channel adapter (§6.1); either way, **the channel's kind is declared at registration** — JSON-kind channels deliver `(messagetype, json)` to your handler, binary-kind channels deliver `(messagetype, byte[])`. A message arriving on a channel of the wrong kind is rejected and logged, not delivered. Messages with no channel fall back to the endpoint-wide `OnMessageReceived` (JSON) or `OnBinaryFrameReceived` (binary) delegates; channeled messages with no registered handler are dropped with an error log.

### 2.4 Keepalive

Liveness uses application-level ping/pong with *take-credit* semantics: any received traffic proves the peer is alive, so pings only flow when the connection is quiet. If a ping goes unanswered past the reply window, the client declares the connection dead and recycles it — which is how half-open "zombie" sockets get cleaned up. The server independently drops clients that stay silent past its own timeout, unless the client requested keepalive exemption at registration. The three client-side timing knobs must be coordinated (§5.1).

### 2.5 Large messages

When a message's encoded size exceeds the frame limit, the library chunks it: a start notice, N binary chunk frames, an end notice — reassembled and delivered on the far side exactly as if it had arrived whole. Other channels' traffic interleaves between chunks, so a bulk transfer shares the pipe. Three size limits govern this (§5.2); the receiver's limits are local policy, so a transfer the receiver won't accept is rejected at transfer time, not negotiated up front.

---

## 3. Public components

### 3.1 Interfaces you implement

| Interface | Purpose | Required? |
|---|---|---|
| `IChannelAdapter` | A channel handler object with kind, counters, and lifecycle — for consumers who want more than a delegate | No — `Add_ChannelHandler` with a delegate covers most uses |
| Client subclass (`TCPClient_v1_Abstract`) | Custom connection-info resolution, auth-token supply, network-availability checks | No — `TCPClient_v1_Impl` is ready-made |
| Manager subclass (`ConnectionMgr_Abstract` virtuals) | Integration with an external client-mapping/routing service | No — base works standalone |

### 3.2 Components the library provides

| Component | Package | Purpose |
|---|---|---|
| `TCPClient_v1_Impl` | client | The ready-to-use TCP client: host/port + logger, start it, use it |
| `TCPClient_v1_Abstract` / `Client_v1_Abstract` | client | The transport and session base classes, for derivation (§10) |
| `ChannelAdapter_DelegateType` | both | Wraps your delegate as a channel adapter; created for you by `Add_ChannelHandler` |
| `TCPConnMgr_wListener` | server | Connection manager with integrated listener — the standard server entry point |
| `TCPConnectionMgr_Base` | server | Listener-less manager for externally accepted connections |
| `TCPEndpoint` / `Endpoint_Abstract` | server | The per-connection server endpoint (send to a client through this) |
| `cListener` | server | The TCP accept loop (owned by the manager; rarely used directly) |
| `FrameTypes` | both | The frame-type registry (diagnostic reference; you don't construct frames) |
| `ExpBackoff_wJitter` | both | The backoff helper the client uses; available for your own retry logic |

### 3.3 DTOs / wire types

| Type | Direction | Description |
|---|---|---|
| `MessageEnvelope` | both | The JSON message wrapper; visible to you mainly in logs |
| `ConnRegisterDTO` | client → server | Registration announcement (ids, props, LibVersion) |
| `ConnRegisterReplyDTO` | server → client | Server-assigned connection id + echo |
| `ChunkStartDTO` / `ChunkEndDTO` / `ChunkCancelDTO` | both | Chunk-transfer control (internal; never dispatched to your handlers) |
| `ClientInfo` | server, in-memory | Per-connection identity/metadata; read it from endpoint queries |
| `ConnectionEntry_v1` | server → mapping service | The versioned connection record for external routing integration |

---

## 4. Library dependencies

**Client:**

```xml
<PackageReference Include="OGA.TCP.Lib" Version="*" />
```

Brings in: `Newtonsoft.Json` (all serialization — the wire format is Newtonsoft-shaped), `NLog` (logging), `OGA.SharedKernel` (logging conventions; absent on the netstandard2.1 target).

**Server:**

```xml
<PackageReference Include="OGA.TCP.Server.Lib" Version="*" />
```

Adds: `NUlid` (server-side connection ids), `OGA.Common.Lib.NetCore` (async primitives), `OpenTelemetry` (trace activities).

**Runtime:** client — .NET Framework 4.5.2/4.8, .NET Standard 2.1, or .NET 5/6/7/8; server — .NET 5/6/7/8. Packages ship Debug-configuration binaries with PDBs by design (spec KD-01), so your logs get file/line metadata.

---

## 5. Configuration and modes

### 5.1 Keepalive timing (client)

Three knobs cooperate; their XML docs carry the coordination rules:

| Knob | Default | Meaning |
|---|---|---|
| `Cfg_KeepAliveInterval` | 15 s | Quiet time before a ping is sent (floor 3 s; values < 5 s also tighten the loop tick) |
| `Cfg_KeepAlive_ReplyMaxDuration` | 20 s | Total silence, with a ping in flight, before the connection is declared dead |
| `Cfg_Connected_InnerLoop_Delay` | 5000 ms | The connection loop's evaluation tick |

**Coordination rule:** keep `ReplyMaxDuration > KeepAliveInterval + one tick`, so a ping has a real reply window. The library floors the deadline at two ticks after the ping regardless, so misconfiguration degrades rather than breaks. Server side, `Cfg_DeadClientTimeout` (default 30 s) should exceed the client's ping interval, or the server will drop quiet-but-healthy clients.

**Choose shorter intervals when:** you need fast dead-peer detection (seconds matter). **Risk/cost:** more keepalive traffic; less tolerance of stalls (including debugger breakpoints — see `Cfg_Disable_KeepAlive` below).

### 5.2 Size limits

| Knob | Default | Meaning |
|---|---|---|
| `MaxFrameSize` | 1 MiB | Largest single frame; also the interleave granularity of the pipe |
| `MaxChunkPayloadSize` | fit-to-frame | Payload bytes per chunk; tune *down* to give latency-sensitive channels more slots during bulk transfers |
| `MaxTransferSize` | 64 MiB | Largest reassembled chunked message the receiver accepts |

Limits are **local policy on each end, not negotiated** (spec KD-06). A sender exceeding the receiver's `MaxTransferSize` finds out via a transfer-time cancel, not at the call site — configure both ends of a conversation consistently.

### 5.3 Behavioral modes

- **`Cfg_ConnectionWaitsforRegistrationReply`** (default true) — readiness waits for a validated registration reply. Turn off only for servers that don't implement registration replies; you lose the server-assigned connection id.
- **`Cfg_Disable_KeepAlive`** (default false) — suspends client pinging and, via the registration prop, requests exemption from the server's silence check. Intended for debugging sessions where breakpoints would trip timeouts. Leave off in production.
- **`Cfg_EnableChannelLayerChunking`** (default true) — with it off, oversized sends fail with `-10` instead of chunking.
- **`Register_with_Loopback_AllMessages` / scope `loopback=rawmsg`** — server echoes traffic back; diagnostics only.
- **TCP tuning** — `Cfg_NoDelay` (default true), `Cfg_SendTimeout` (5 s), `Cfg_ConnectTimeout` (5 s).

---

## 6. Implementing the required interfaces

No interface implementation is required for the standard path — construct, configure, start. One optional interface matters:

### 6.1 `IChannelAdapter`

Implement this instead of registering a delegate when a channel deserves its own object: state, metrics, lifecycle.

```csharp
public sealed class TelemetryChannelAdapter : ChannelAdapter_abstract
{
    public TelemetryChannelAdapter() : base("telemetry", eChannelKind.Json) { }

    public override int AcceptIncomingMessage(IMessagingHost host, string messagetype, string json, string corelationid)
    {
        if (messagetype == nameof(TelemetrySample).ToLower())
        {
            var sample = JsonConvert.DeserializeObject<TelemetrySample>(json);
            // ... handle ...
            return 1;   // handled
        }
        return 0;       // not mine; logged by the dispatcher
    }

    public override int AcceptIncomingMessage(IMessagingHost host, string messagetype, byte[] payload, string corelationid)
        => -1;          // json-kind channel; binary should never arrive (the dispatcher enforces this)

    public override void Close() { /* release resources; called on Stop/Remove */ }
}
```

Contracts to respect: declare the channel's kind at construction; return `1` handled / `0` not handled / negative for errors; keep `AcceptIncomingMessage` fast — it runs on the receive path, so queue heavy work; `Close()` must not throw.

---

## 7. Wiring and integration

### 7.1 Client

```csharp
var client = new TCPClient_v1_Impl("srv.example.local", 5030, logger);

// Identity, carried in registration:
client.DeviceId  = deviceId;
client.UserId    = userId;          // Guid.Empty if no user context yet
client.RuntimeId = runtimeId;
client.Pid       = Environment.ProcessId;

// Session events:
client.OnConnectionLost = (c) => appState.MarkOffline();
client.OnStatus_Change  = (c, status) => logger.Info($"conn status: {status}");

// Channels (register before Start, or any time after):
client.Add_ChannelHandler("commands", (host, messagetype, json) =>
{
    // json-kind channel
    return 1;
});
client.Add_ChannelHandler("filesync", (host, messagetype, payload) =>
{
    // binary-kind channel: payload is byte[]
    return 1;
});

var res = await client.Start_Async();   // 1 = loop started; connection proceeds in background
```

Sending:

```csharp
// JSON: any serializable object; MessageType is the CLR type name.
await client.SendMessage_to_Endpoint(new StatusUpdate { State = "idle" }, channel: "commands");

// Binary: raw bytes, no encoding cost.
await client.SendBinaryMessage_to_Endpoint(fileBytes, channel: "filesync", corelationid: transferCid);
```

### 7.2 Server

```csharp
var mgr = new TCPConnMgr_wListener();
mgr.ListeningAddress = "0.0.0.0";
mgr.ListeningPort    = 5030;

var res = mgr.Startup();               // 1 = listening
if (res != 1) { /* port in use / bad address — do not proceed */ }
```

Reaching clients:

```csharp
// Find a connection and push to it:
var entry = mgr.QRYGetConnection_ByClientDeviceId(deviceId);
var ep    = mgr.QRYGetConnection_ByConnId(entry.ConnectionId);
await ep.Send_Object_toClient(new PolicyUpdate { ... }, channel: "commands");
await ep.SendBinary_toClient(payloadBytes, channel: "filesync");
```

Server-side channel handlers register on each endpoint (typically in your `AddConnection`/registration hook) with the same `Add_ChannelHandler` shapes as the client.

---

## 8. Startup sequencing

**Server before clients.** Start the manager before clients attempt connection; clients tolerate an absent server (they retry with backoff), but there's no reason to burn the retries.

**Client: start, then wait for readiness.** `Start_Async` returns when the loop is running, not when the session is ready. Either send opportunistically (sends return `0` until ready) or wait:

```csharp
await client.Start_Async();
var ready = await Client_v1_Abstract.WaitforCondition(() => client.AllowSend, timeout: 10000);
```

**Instances are single-use.** A client (or server endpoint) is started once; `Stop_Async` is terminal, and `Start_Async` refuses with an error after a stop or dispose. To open a new session, construct a new instance and register its handlers fresh. (Reconnection is different — the client reconnects *itself* internally for the life of the instance; Stop is the consumer saying "done.")

**Register channel handlers early.** Handlers registered before traffic arrives can't miss messages.

---

## 9. Operational patterns and recipes

### 9.1 Typed request/response over a channel

There is no built-in request/response — correlate with `corelationid`:

```csharp
var cid = Guid.NewGuid().ToString();
pending[cid] = new TaskCompletionSource<QueryResult>();
await client.SendMessage_to_Endpoint(new QueryRequest { ... }, channel: "query", corelationid: cid);
// In the "query" handler for QueryResult: pending[cid].SetResult(result);
```

### 9.2 Sending a large payload

Nothing special — chunking is transparent:

```csharp
await client.SendBinaryMessage_to_Endpoint(bigBytes, channel: "filesync");   // 40 MB: chunked automatically
```

Ensure the *receiver's* `MaxTransferSize` accommodates your largest payload (§5.2), and expect the send to take multiple frames' time; other channels interleave meanwhile.

### 9.3 Reacting to reconnects

The client reconnects by itself; your job is application-level state. On `OnConnectionLost`, mark state stale; when readiness returns, re-sync. Registration (including the server-assigned connection id) is fresh on every reconnect — the server sees a *new* connection, so any server-side association with the old connection id must be rebuilt (the manager's mapping-service hooks exist for exactly this).

### 9.4 User login on a live connection

Re-registration updates the connection's user context without reconnecting:

```csharp
client.UserId = loggedInUser;
await client.Send_RegistrationMessage();
```

### 9.5 Tuning for chatty vs. quiet deployments

A connection carrying steady traffic never pings (take-credit). For mostly-idle connections, the defaults mean: silent failure detected within ~35 s worst case. Tighten `Cfg_KeepAliveInterval`/`Cfg_KeepAlive_ReplyMaxDuration` for faster detection, respecting the §5.1 coordination rule on both ends.

---

## 10. Extension points

### 10.1 Deriving a custom client

Subclass `TCPClient_v1_Abstract` when connection info is dynamic (service discovery, failover lists) or connectivity is conditional:

- `Get_ConnectionInfo()` — resolve host/port per connection attempt.
- `IsInternetAvailable()` — gate attempts on platform network state.
- `Dispose(bool)` — extend teardown (call base).

### 10.2 Connection-manager service hooks

Override on your manager subclass to integrate an external routing/mapping service:

- `Send_NewClientConnection_to_ClientMappingService(entry, oldvals)` — a connection registered (or re-registered).
- `Send_ConnectionClosed_to_ClientMappingService(connid)` — a registered connection closed.

### 10.3 The raw-message tap

`OnRawMessageReceived` hands you every frame (`frameType`, `byte[]`) **exclusively** — assigning it disables all internal processing: no keepalive replies, no registration handling, no dispatch. It is a dumb-pipe mode for tests and protocol tooling where your code owns the entire session semantics. Do not assign it in a normal application.

---

## 11. Error handling

The library reports through **return codes and delegates, not exceptions** — consumer-facing calls catch internally and return:

- **`1`** success · **`0`** benign refusal (most commonly: session not ready — `AllowSend` false) · **negative** error (logged with specifics; the connection may be recycling).

Failure surfaces to know:

- **Connection loss** → `OnConnectionLost`, exactly once per established connection; the loop is already reconnecting. In-flight messages are not redelivered.
- **Send failures** → negative return; treat the session as suspect and wait for readiness again.
- **Oversized send with chunking off** → `-10`.
- **Chunked transfer failures** → sender-side abort emits a cancel; the receiver discards the partial transfer silently. A transfer exceeding the receiver's `MaxTransferSize` is rejected the same way. The *application* sees nothing — if delivery matters, acknowledge at the application level (§9.1's correlation pattern).
- **Framing violations** (oversized/invalid/unknown frame type) are fatal to the connection by design — they indicate corruption or a mismatched peer (§16.2) — and surface as a connection loss.
- **Malformed application messages** are logged and dropped without affecting the connection.

Everything is logged with class/instance/method context; the packages ship PDBs so log call-sites resolve to file/line.

---

## 12. Compatibility and versioning

**LibVersion** is the wire-protocol version, announced by the client at registration (`tcplibver` prop): **1** — baseline; **2** — adds app-identity props and tokenless connection; **3** — the dual-frame protocol this guide describes (typed frames, binary messaging, rebuilt chunking, size limits). A server accepts registrations from any version it supports ([1..3]).

**The v3 boundary is a framing break:** v3 and pre-v3 libraries cannot share a connection at all (§16.2 shows the symptoms). Deploy v3 to **both ends of each conversation together**. There is no wire negotiation or fallback by design (spec KD-07).

**Package versioning** is semver, automated by the build pipeline; within a LibVersion, wire changes are additive-only, and frame-type values and registration prop keys are never repurposed.

---

## 13. Migration from earlier library versions

### 13.1 From LibVersion 2 to LibVersion 3

**What changed:** the wire format (typed frames), the chunking protocol (byte-based; the old string-based chunk messages no longer exist), binary messaging added, channels became kind-declared, and the keepalive dead-connection check now actually enforces the reply window.

**What to update:**

1. **Upgrade both ends of every conversation in one deployment step.** Mixed pairs cannot communicate (§12).
2. **Recompile against the new packages.** Existing delegate-based channel registrations and send calls compile unchanged; delegate-registered channels default to JSON-kind.
3. **If you assigned `OnRawMessageReceived`:** its signature changed from a decoded string to `(frameType, byte[])` — update your handler and decode where needed.
4. **If you relied on never-dropping quiet connections:** the keepalive fix means a genuinely unanswered ping now recycles the connection (it previously never did). Deployments with servers stalled beyond the reply window will see reconnect cycles that were silently tolerated before — that is the feature working; tune §5.1 if your topology needs more patience.
5. **If you consumed `Metrics` before first connect** it previously threw; it now returns an empty snapshot.
6. **If you restarted clients (`Stop_Async` then `Start_Async`):** that was never reliable (handlers were silently cleared on stop) and is now refused — `Start_Async` returns an error on a stopped instance. Construct a new client per session (§8, §16.3).

---

## 15. Testing your implementation

### 15.1 Test infrastructure provided

The packages ship no test doubles. The practical harness is the library itself on loopback — a real `TCPConnMgr_wListener` on `127.0.0.1` with an ephemeral port, plus a real client, runs anywhere with no environment assumptions:

```csharp
var mgr = new TCPConnMgr_wListener { ListeningAddress = "127.0.0.1", ListeningPort = port };
mgr.Startup();
var client = new TCPClient_v1_Impl("127.0.0.1", port, logger);
// ... exercise, assert, dispose both.
```

For failure-mode tests (unanswered keepalives, absent registration replies), a raw `TcpListener` that accepts and stays silent is the standard "dead peer" stand-in.

### 15.2 What to test in your domain

- **Reconnect resilience** — kill the server mid-conversation; assert your `OnConnectionLost` handling marks state stale, and your re-sync runs when readiness returns.
- **Handler re-registration** — Stop then Start a client; assert your channels function afterward (this catches the cleared-delegates trap).
- **Largest real payload, both directions** — at your production size limits; assert delivery and measure transfer time under concurrent small-channel traffic.
- **Kind mismatch** — send binary at a JSON channel in a test; assert your monitoring surfaces the rejection log.
- **Readiness gating** — call your send paths before the session is ready; assert your code handles the `0` returns without losing data.
- **Silence tolerance** — hold your longest legitimate processing stall (GC, batch job) against your keepalive settings; assert no spurious recycle.

---

## 16. Troubleshooting

### 16.1 Client connects, then recycles every few seconds

The registration reply isn't arriving or isn't being processed. Causes in order of likelihood: the server doesn't send registration replies (set `Cfg_ConnectionWaitsforRegistrationReply = false` if that's by design); `OnRawMessageReceived` is assigned (it swallows *everything*, including the reply — §10.3); version mismatch (§16.2).

### 16.2 Log says "legacy JSON framing detected" — or the other end cycles on registration timeouts

A v3 and a pre-v3 library are talking. The v3 side kills the connection immediately with that specific log line; the pre-v3 side fails to parse and cycles. Upgrade the lagging end (§12); there is no interop mode.

### 16.3 `Start_Async` returns an error on a client that worked before

The instance was stopped (or disposed). Instances are single-use (§8): `Stop_Async` is terminal, and a stopped instance refuses to start. Construct a new client, register its handlers and delegates, and start that — treat client configuration as per-session.

### 16.4 Sends return 0

The session isn't ready: still connecting, registration in flight, or mid-recycle. Gate on `AllowSend` / readiness (§8) rather than retrying blindly.

### 16.5 Large message never arrives, no error at the sender

The receiver rejected or discarded the transfer: its `MaxTransferSize` is smaller than the payload, or the transfer idled past `Cfg_ReceiverTimeout`. Check the receiver's logs for the rejection/prune entry; align limits on both ends (§5.2).

### 16.6 Connection drops while debugging

Breakpoints stall the keepalive exchange past its windows. Set `Cfg_Disable_KeepAlive = true` on the client for debug sessions (it also requests server-side exemption at registration).

---

*End of guide.*
