# OGA.TCP.Lib
Message-Based TCP Library for NET Framework and Core

A message-based TCP connectivity library for .NET: framed message exchange between clients and a server over plain TCP sockets, with channels, large-message chunking, keepalive, and connection registration. Published as two nuget packages:
* **OGA.TCP.Lib** - the client library (`TCPClient_v1_Impl` and the session layer).
* **OGA.TCP.Server.Lib** - the server library (connection manager, listener, and per-connection endpoints).


## Capabilities (LibVersion 3)
* **Typed wire frames** - every TCP frame carries a five-byte preamble (length + frame type), so JSON and binary messages share one connection. Binary payloads ride the wire raw, with no base64 cost.
* **JSON messaging** - typed messages in a routing envelope (message type, channel, scope, correlation id), dispatched to registered handlers.
* **Binary messaging** - raw byte payloads with the same routing metadata, via distinct send methods (`SendBinaryMessage_to_Endpoint` / `SendBinary_toClient`).
* **Transparent chunking** - messages larger than one frame are split, transferred as binary segments interleaved with other traffic, and reassembled on the far end. Governed by three local limits: frame size (1 MiB default), chunk payload size, and total transfer size (64 MiB default).
* **Channels** - named, kind-declared (JSON or binary) message routes with per-channel handlers or adapter classes, shared by client and server.
* **Keepalive** - application-level ping/pong with take-credit semantics (any received traffic proves liveness). Clients may request a keepalive exemption; servers may refuse it by policy.
* **Connection registration** - a handshake that assigns the server-side connection id and records client identity (device, user, app id/version, library version).
* **Resilient connection loop** - the client reconnects automatically with jittered exponential backoff; client and endpoint instances are single-use (construct a new instance per session).


## Version 3 is a wire break
LibVersion 3 changed the wire format. v3 and pre-v3 libraries cannot share a connection at all - deploy v3 to **both ends of each conversation together**. There is no wire negotiation or fallback by design. See the migration section of the implementation guide for the upgrade steps.


## Documentation
* [Implementation guide](docs/OGA.TCP.Lib_IMPLEMENTATION_GUIDE.md) - consumer-facing: setup for client and server, channels, sizing limits, failure-mode tuning, migration from earlier versions, troubleshooting.
* [Design specification](docs/OGA.TCP.Lib_SPEC.md) - the authoritative record of the wire protocol, design decisions, and open items.


## Package targets
The client library nuget package covers the following:<br>
* NET Framework 4.5.2
* NET Framework 4.8
* NET Standard 2.1
* NET 5
* NET 6
* NET 7
* NET 8

The server library nuget package covers the following:<br>
* NET 5
* NET 6
* NET 7
* NET 8


## Repository layout
NOTE:
This repository contains two VS solutions.
* OGA.TCP.Lib - This is the main library solution, and where all development should be done.
* OGA.TCP.Lib_forNET452Test - This solution was created as a workaround, so the NET4.5.2 library target can be tested.
  This has to be done, since NET4.5.2 is only compatible with earlier MSTest framework versions.
  And, the MSTest framework myopically ONLY uses the latest MSTest framework dll versions it finds across the entire solution, instead of the specific version for a test project.
  So, we had to isolate testing of the NET4.5.2 library to its own solution, so the tests will execute.
See this article for more details: https://wiki.galaxydump.com/link/239
