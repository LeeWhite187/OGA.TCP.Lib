# Implementation Guide Template

**Template Revision:** 1
**Status:** Active
**Created:** 2026-06-04
**Related Documents:** METHODOLOGY.md, SPEC_TEMPLATE.md

This document is the structural template for implementation guides — documents intended for downstream consumers of a library. An implementation guide describes how to use a library successfully; it does not describe how the library was designed. Design rationale, alternatives considered, and design history all live in the library's spec.

The template carries its own revision number. The implementation guides authored from it do not — guides are anchored to a specific spec revision via their `Spec` field, and that anchor is the only versioning information a consumer needs.

---

## Template Revision Log

### Revision 1

**Date:** 2026-06-04T00:00:00Z

Initial published version. Established the shape of implementation guides as forward-looking consumer references: minimal title block (Library, Spec anchor, optional Sample) with no revision log, no status field, no creation date, no library version, and no guide revision number. Sixteen sections covering problem framing, mental model, public components, dependencies, configuration, interface implementation, wiring, startup, operational patterns, extension points, error handling, compatibility, migration, client implementation, testing, and troubleshooting. Several sections marked conditional based on library shape.

---

## Conventions

The conventions below apply to all guides authored under this template. They are guidance for the author, not content that guides reference back to (guides do not carry a `Template Revision` field). When in doubt about whether content earns its place in a guide, apply these conventions.

**Forward-looking reference, not a history book.** The guide describes the library as currently designed. It does not describe how the library evolved, what alternatives were considered, or what was previously true and is no longer. Historical context lives in Git (commit messages and diffs) for both the guide and the spec; neither document carries its own in-document revision log.

**Audience is the consumer, not the designer.** Every sentence should serve a consumer's success with the library. If a sentence answers a question the consumer would not ask ("why was it designed this way?"), it does not belong in the guide. It belongs in the spec.

**Reference current design, not work-list items.** When the guide cites the spec, it points at design content — Key Decisions (KD-NN), Functional Requirements (FR-NN), other numbered design items, sections (§X.Y). It does not cite Open Items (OI-NN), ever. The reason is semantic, not defensive: an Open Item is by definition not current-state design. It tracks work that may or may not become design. A guide describes how to implement current design, so it forward-points to the design content that defines that current state. If the work an OI tracked is done, its output lives in an FR, a KD, or a section — cite that. If the work isn't done, there is no current design to describe. Either way, the OI is never the right citation target.

**Code examples are authoritative.** Examples should be runnable as written, or as close to runnable as the medium permits. Pseudo-code is acceptable when it conveys structure better than real code, but should be marked as such. Examples that drift from the real library produce consumer confusion.

**Tables for component listings; prose for explanations.** Use tables when the content has regular shape across rows (interfaces with purpose and required-status, DTOs with direction and description). Use prose for explanations, rationale-when-brief, and operational guidance. Mixing the two in one section is fine; using tables to render prose is not.

**Brief design-decision callouts are acceptable.** When a piece of operational guidance is non-obvious and a single sentence of "why" helps the consumer remember it, include the sentence. The boundary: a sentence is acceptable; a paragraph belongs in the spec.

**No revision log in the guide body.** The guide does not carry its own revision log. The Spec field anchors the guide to a specific design version of the library; git history captures everything else. Under the methodology's forward-looking-reference principle, neither specs nor guides carry in-document revision logs — both rely on Git for history. The guide is held to this rule with the same strictness as the spec.

**Section numbering is stable.** Sections are not renumbered across guide revisions. If a section becomes irrelevant, it is removed and the surrounding sections retain their numbers — leaving a gap in the sequence is acceptable. Consumers may cite section numbers; renumbering breaks those citations.

---

## Using This Template

To author a new implementation guide:

1. Copy the content between the `BEGIN GUIDE TEMPLATE CONTENT` and `END GUIDE TEMPLATE CONTENT` markers below into a new file.
2. Fill in the title block fields. Set `Spec` to the path and revision of the library's spec.
3. Author each section per its prompt. Use the section as written, mark conditional sections "not applicable: [brief reason]" if they do not apply, or remove them entirely if they would only add noise.
4. Refer to the Conventions section above for authoring discipline. The forward-looking principle in particular is load-bearing — content that describes "how it used to be" or "why this was decided" belongs in the spec, not the guide.

---

<!-- BEGIN GUIDE TEMPLATE CONTENT — copy from the H1 below through "End of template" -->

# [Library Name] — Implementation Guide

**Library:** [Package names. For a library distributed as multiple NuGet packages, list them all: `Foo.Abstractions` / `Foo.Core`.]
**Spec:** [Path to the library's spec, with the specific spec revision the guide is anchored to: `FOO-SPEC.md (Revision 4)`.]
**Sample:** [Optional. Path to sample code or sample domain that demonstrates the library in use. Omit the field if no sample exists.]

[One paragraph, two to four sentences. Establish who this guide is for, what it covers, and the division of labor with the spec. Recommended phrasing: "This guide is a standalone reference for anyone building an application on top of [library name]. It explains what each piece is, why it exists at a brief level, and how to wire it together. Where the guide describes a design decision briefly, the spec contains the full rationale."]

---

> **Critical context callout — optional.** If the library has a non-obvious mental model that, if misunderstood, leads to consumers building implementations that work but demonstrate the wrong pattern, place a brief callout here directing the reader to the section that establishes the mental model (typically §2). Omit this callout if the library is straightforward to consume without prior orientation.

---

## Table of Contents

[Single-level list, sections only. Anchor links optional. Sections that are omitted (because they do not apply to this library) are also omitted from the table of contents.]

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
14. [Client implementation](#14-client-implementation)
15. [Testing your implementation](#15-testing-your-implementation)
16. [Troubleshooting](#16-troubleshooting)

---

## 1. What this is and what problem it solves

[Consumer-facing problem framing. Two to four paragraphs. What world does this library live in? What problem does it solve, in plain language? What does a successful consumer get out of using it?

End with a brief "the library does X" / "the library does NOT do Y" delineation so the consumer knows the boundary of the library's responsibilities. Example: "CMS handles the server-side mechanics of optimistic concurrency. CMS does NOT handle HTTP routing, WebSocket connections, authentication, push notification delivery, or client-side state management."

The reader should come away knowing whether the library is what they need.]

---

## 2. How it works — the big picture

[The mental model the consumer must hold to use the library successfully. Enough to make the rest of the guide comprehensible; not so much that it duplicates the spec.

Typical structure: name the major types and concepts, describe how they interact, walk through the primary flow. If the library has any non-obvious invariants the consumer must understand (timestamp authority, version counter semantics, log-as-source-of-truth), surface them here.

Subsection breakdown is encouraged for libraries with multiple distinct concepts. For example:

### 2.1 [Core concept A]
### 2.2 [Core concept B]
### 2.3 [How they interact]

The criterion: a consumer should be able to read this section and then read the rest of the guide without getting lost.]

---

## 3. Public components

[The consumer's API map. Organize by what the consumer implements vs. what the library provides vs. what crosses the wire.

Tables are appropriate here because the content has regular shape:

### 3.1 Interfaces you implement

| Interface | Purpose | Required? |
|---|---|---|
| ... | ... | Yes / No |

### 3.2 Components the library provides

| Component | Purpose |
|---|---|
| ... | ... |

### 3.3 DTOs / wire types

| Type | Direction | Description |
|---|---|---|
| ... | Client → Server | ... |

The consumer should be able to scan these tables and orient to the library's surface area in a minute or two.]

---

## 4. Library dependencies

[What the consumer must install or have available. Package references, runtime requirements, transitive dependencies the consumer needs to be aware of.

Example shape:

**NuGet packages:**

```xml
<PackageReference Include="Foo.Abstractions" Version="*" />
<PackageReference Include="Foo.Core" Version="*" />
```

`Foo.Core` brings in:
- [transitive dependency 1] — [why it's used]
- [transitive dependency 2] — [why it's used]

**Runtime:** .NET 8 or later. Language ceiling: C# 10.]

---

## 5. Configuration and modes

[Conditional. Use this section when the library has runtime configuration choices the consumer must make. Common pattern: modes (e.g., "trusted client" vs. "validated client" in CMS), or options structures with significant choices.

For each mode or significant configuration choice, structure as:

### 5.1 [Mode or option name]

[Brief description of what this mode/option does.]

**Choose this when:**
- [criterion]
- [criterion]

**Risk / cost:** [What the consumer takes on by choosing this.]

When the library has no significant runtime configuration choices, mark this section "not applicable: this library has no runtime configuration choices beyond default options" and proceed.]

---

## 6. Implementing the required interfaces

[Conditional. Use this section when the library requires the consumer to provide implementations of one or more interfaces. Walk through each required interface with code examples.

Structure per interface:

### 6.N `IInterfaceName<T>`

[Brief description of the interface's role.]

[Methods and their contracts.]

**Implementation pattern:**

```csharp
public sealed class YourImpl : IInterfaceName<T>
{
    // example implementation
}
```

[Any contracts, gotchas, or invariants the consumer must respect in their implementation.]

When the library is purely consumed (no interfaces to implement), mark this section "not applicable: this library does not require consumer-provided implementations."]

---

## 7. Wiring and integration

[How the consumer plugs the library into their application. DI registration, controller wiring, host configuration.

For .NET libraries this typically includes `IServiceCollection` extension methods and example controller/host wiring. Show working code, not pseudocode.

Example shape:

```csharp
services.AddFoo<TEvent, TModel>(options =>
{
    options.Setting = value;
});

services.AddSingleton<IFooDependency, YourFooDependency>();
```

This section is required for any library the consumer integrates into a larger application. For standalone tools, adapt to "Installation and invocation."]

---

## 8. Startup sequencing

[Conditional. Use this section when the library has startup ordering requirements — initialization that must happen before normal operation begins. Common examples: read model seeding, cache warmup, schema migration, license verification.

Describe the sequencing requirements concretely, with code examples where helpful.

When the library has no startup ordering requirements, mark this section "not applicable: this library has no startup ordering requirements beyond standard DI registration."]

---

## 9. Operational patterns and recipes

[The cookbook. Common use cases the consumer will encounter, with concrete code. Each recipe is "how do I do X?" answered with a working code example and brief explanation.

Subsection per recipe:

### 9.1 [Recipe name]

[Brief context: when does the consumer need this?]

[Code example.]

[Brief notes on edge cases or related recipes.]

Avoid letting this section sprawl into design discussion. If a recipe needs three paragraphs of rationale to explain why it works, the rationale belongs in the spec; the recipe here just shows the pattern.]

---

## 10. Extension points

[Conditional. Use this section when the library has optional extensibility surfaces — interfaces or hooks the consumer can implement to customize behavior beyond the required interfaces in §6.

Structure per extension point:

### 10.N [Extension point name]

[Brief description of when the consumer would extend here.]

[Interface or hook signature.]

[Implementation example.]

[Constraints or contracts the extension must respect.]

When the library has no extension points beyond the required interfaces, mark this section "not applicable: this library has no optional extension points."]

---

## 11. Error handling

[How errors surface from the library to the consumer, and what the consumer should do with them.

For libraries that return result objects: describe the result type's fields and what each combination means.

For libraries that throw exceptions: enumerate the exceptions the consumer should expect to handle, with their conditions and recommended responses.

For libraries that produce HTTP-shaped responses: map status codes to consumer actions.

The consumer should come away knowing how to recognize each failure mode and how to respond.]

---

## 12. Compatibility and versioning

[Conditional. Use this section when the library has a stated compatibility commitment — semver discipline, breaking-change posture, deprecation policy.

Topics to cover when applicable:
- The library's versioning scheme (semver, calendar, custom).
- What constitutes a breaking change.
- How long deprecations remain before removal.
- Compatibility matrix if the library has known interactions with other versioned components.

When the library has no stated compatibility commitment, mark this section "not applicable: this library does not have a published compatibility policy."]

---

## 13. Migration from earlier library versions

[Conditional. Use this section when consumers may be upgrading from earlier versions of the library and need guidance for the transition.

For each upgrade path:

### 13.N [From version X to version Y]

**What changed:** [Summary of the breaking changes.]
**What to update:** [Concrete consumer-side changes required.]
**Example before/after:** [Code that worked under X, and its replacement under Y.]

When the library has no migration story (first release, or no breaking changes across versions), mark this section "not applicable: no breaking changes have required consumer migration."]

---

## 14. Client implementation

[Conditional. Use this section when the library has both a server-side and a client-side concern — i.e., when consumers may be building either side of an API the library defines.

The section becomes a sub-guide for client-side consumers. Structure can mirror the server-side sections at a smaller scale:

### 14.1 Client overview
### 14.2 Endpoint calls
### 14.3 Local state management
### 14.4 Reconnection and recovery
### 14.5 Optimistic UI (if applicable)

When the library is purely server-side or purely client-side, mark this section "not applicable: this library has only [server-side / client-side] consumers."]

---

## 15. Testing your implementation

[How the consumer validates their integration. Two parts: test infrastructure the library provides, and what the consumer should test in their own integration.

### 15.1 Test infrastructure provided

[Test doubles, harnesses, sample data the library makes available for consumer testing. Code examples for how to wire a test setup.]

### 15.2 What to test in your domain

[A list of behaviors the consumer should validate in their own integration tests. Each item is a concrete test scenario, not a vague guideline.

Example:
- **[Scenario name]** — [what to set up, what to do, what to assert]

This section is essential for any non-trivial library. Consumers need to know what to test, not just that they should test.]

---

## 16. Troubleshooting

[Optional. Known issues, common error messages, gotchas. Each entry is a concrete problem-and-resolution.

Structure:

### 16.N [Symptom or error message]

[Brief description of when this occurs.]

[How to resolve it.]

This section is the consumer's lookup table when something goes wrong. Add entries as patterns emerge from consumer questions; do not invent troubleshooting content speculatively.]

---

*End of template.*

<!-- END GUIDE TEMPLATE CONTENT — copy up to (and including) the line above when authoring a new guide -->
