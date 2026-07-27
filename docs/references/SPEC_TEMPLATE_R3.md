# Spec Template

**Template Revision:** 3
**Status:** Active
**Created:** 2026-05-06
**Related Documents:** METHODOLOGY.md, SPEC_RETROFIT_INSTRUCTION.md, IMPLEMENTATION_GUIDE_TEMPLATE.md

This document is the structural template for project specifications produced under the design-first methodology. It serves two purposes:

1. **Source of structural conventions.** The Conventions section below is the authoritative source for the rules a spec follows — identifier formats, number stability, requirement language, table usage, and so on. Specs authored under this template revision do not embed these conventions verbatim; they reference this document by revision number via the `Template Revision` field in the spec's title block.

2. **Boilerplate for new specs.** The Spec Template Content section below is the starting point for authoring a new spec. When beginning a new spec, copy the content between the `BEGIN SPEC TEMPLATE CONTENT` and `END SPEC TEMPLATE CONTENT` markers into a new file and fill in fields.

The template carries its own revision number. When the template's structure or conventions change, the revision increments. Existing specs continue to reference the revision they were authored under and are not affected by template changes until they are deliberately migrated to a newer revision.

---

## Template Revision Log

### Revision 1

**Date:** 2026-05-06T00:00:00Z

Initial published version. Established the structural shape of project specifications: title block with Project, Short description, Author, Status, Created, Last Updated, and Related Documents fields; 14 sections covering Project Overview, Functional Requirements, Non-Functional Requirements, Constraints and Technology Decisions, Architectural Overview, Data Model, Solution Structure, Key Interfaces and Contracts, Protocols and Flows, API Surface, Infrastructure, Key Decisions, Open Items, and Revision Log. Established item identifier conventions (UR-NN, FR-NN, NFR-NN, KD-NN, OI-NN) with stability rules, document-superset principle, RFC 2119 requirement language, and reverse-chronological revision log with ISO-8601 timestamp headings. Conventions were embedded in each spec's §1.9 as a self-describing block.

### Revision 2

**Date:** 2026-06-04T00:00:00Z

Restructured the template into a reference model. Conventions previously embedded in each spec's §1.9 now live in this template document and are referenced by specs via a `Template Revision` field. Specs no longer carry an embedded conventions block. This eliminates the stale-content problem in which template convention changes would silently invalidate the embedded copies in existing specs.

Template metadata added: this document now carries its own title block (Template Revision, Status, Created, Related Documents) and its own revision log (this section).

Changes to spec title block fields: removed `Author` field; removed `Last Updated` field; added `Revision` field (integer, starting at 1, increments per published change); added `Template Revision` field (identifies the template revision the spec adheres to).

Changes to spec revision log (§14): switched from reverse-chronological to chronological / append-order — oldest entry first, newest entry last. Entry headings changed from ISO-8601 timestamps to `### Revision NN` with the timestamp moved inside the entry as a `**Date:**` field.

New convention added: intra-log cross-references between revision log entries use the stable `Revision NN` identifier, not positional words ("above", "below") or bare timestamps. Positional and temporal references are fragile under reordering; the integer revision number is the durable address.

### Revision 3

**Date:** 2026-06-04T00:00:00Z

Two substantive changes folded into a single R3 publication.

**Universal terminal-item tombstoning, in service of the forward-looking-reference principle.** Adopted the principle (see `METHODOLOGY.md` §9) as the dominant design discipline for spec content: specs describe the system as currently designed; content that does not describe current intent does not belong in the spec body. The principle drives the universal tombstoning rule that follows.

Any item — UR, FR, NFR, KD, or OI — that has reached a terminal state (Withdrawn, Superseded, Closed, Cancelled) is tombstoned in place: the identifier remains as a stable address, the title carries a single-word disposition tag optionally followed by `by <identifier-or-section>`, and the body is empty. The previous convention preserved item bodies after withdrawal/closure with a "brief note" or allowed a "one-sentence pointer"; both loopholes permitted keyword sediment to accumulate in the spec, polluting search by downstream consumers. The new rule (bare body, identifier-only qualifiers) closes those loopholes while still permitting tombstones to point at superseding artifacts via their stable identifiers — which are doc machinery rather than design content and therefore do not mislead keyword search.

Supersession relationships are captured by the superseded item's tombstone (e.g., `### KD-03 — (Superseded by KD-09)`) and by Git history. The superseding item's body describes the current decision on its own terms; it does not recount the predecessor or explain the supersession. When a KD supersedes another KD, this is explicit: the new KD's body stays free of historical context.

Prose qualifiers in tombstones are disallowed — forms like `(Cancelled; scope removed from project)` or `(Closed; resolved by §6.4 final schema)` introduce design-content phrases that the spec body should not carry. Rationale, supersession reasoning, and any other historical context live in the Git commit message that effected the change.

The Open Item disposition convention was split into "Open Item active dispositions" (the urgency flags for items still being worked) and "Terminal item disposition" (the universal tombstoning rule that applies across all item types). §13 (Open Items) in the spec template content updated accordingly.

**Removal of the per-spec revision log.** The §14 Revision Log section was removed from the spec template content. Specs no longer carry an in-document log. Rationale: the keyword sediment accumulating in revision log entries undermines the forward-looking-reference property; Git is the appropriate durable history for "what changed and why" (commit messages and diffs); the in-document log has no readers in the workflow that justify the noise. The Revision number in the title block persists — it identifies a stable state of the document for cross-document references.

The Revision-bump trigger was redefined: revision bumps signal publication moments (handoff to CLI, share with another Claude session, shelving for future resumption), not continuous design edits. Revision consistency and intra-log cross-reference conventions removed (both governed a log that no longer exists). The `Revision` title block field description updated.

Exception: this template document retains its own Template Revision Log because templates are meta-documents read by designers and migration authors, not by library consumers; the consumer-pollution concern does not apply.

**Other R3 changes.** `IMPLEMENTATION_GUIDE_TEMPLATE.md` published as a sibling document for downstream library consumers. The `Related Documents` field above now includes it.

---

## Conventions

The conventions below apply to all specs authored under Template Revision 3. They are the authoritative source for the rules a spec follows. Specs do not embed these conventions; the `Template Revision` field in each spec's title block names which revision of this document applies.

**Item identifiers.** Items in a spec are identified by a type prefix and a two-digit zero-padded sequence number:

- **UR-NN** — User Requirement
- **FR-NN** — Functional Requirement
- **NFR-NN** — Non-Functional Requirement
- **KD-NN** — Key Decision
- **OI-NN** — Open Item

Sequence numbers are assigned in the order items are *created*, not the order they appear in the document. Items may be moved within the document as the spec evolves; their sequence number does not change once assigned.

**Number stability.** Item numbers are stable across all revisions of a spec. A removed item is not deleted from its section — it remains in place with title `### XX-NN — (withdrawn)` and a brief note explaining the removal. New items always take `max(existing_number) + 1` for their type, where the maximum includes all withdrawn entries. Numbers are addresses; addresses are not recycled.

If a project's item count for a given type approaches 99, that is treated as a spec event warranting a major revision and a deliberately widened identifier scheme.

**Cross-references.** Sections within a spec are referenced by number (e.g., `§6.5`). Items are referenced by their full identifier (e.g., `FR-14`, `KD-03`).

**Document order is independent of item numbering.** A section may contain `FR-03, FR-15, FR-04, FR-22` in that order because requirements are grouped topically, not by creation order. The identifier is the address; the position is the organization.

**Open Item active dispositions.** Open Items that are still being worked carry an active disposition flag in the title indicating their current state:

- `⚠ NEEDS YOUR INPUT` — requires the project owner to make a decision. The author has no recommendation, or the decision is not theirs to make.
- `⚠ NEEDS YOUR REVIEW` — the author has a recommendation; the project owner reviews before commit.
- (no flag) — known gap, intentionally not addressed at this stage of the spec.

For Open Items that have reached the end of their useful life, see "Terminal item disposition" below.

**Terminal item disposition (tombstoning).** Any item — UR, FR, NFR, KD, or OI — that has reached a terminal state is tombstoned in place: it keeps its identifier (which is a stable address) and carries a disposition tag in its title. The tombstone has no body.

The disposition tag is a single word from the closed set below, optionally followed by `by <identifier-or-section>`:

- **Withdrawn** — the item was created but the design decision was made not to include it; never implemented.
- **Superseded** — replaced by another item; the new item carries the current design intent.
- **Closed** — work is done; the resolution lives in the current state of the spec (used primarily for OIs whose questions have been answered).
- **Cancelled** — abandoned without resolution (used for OIs that became irrelevant before being resolved).

Tombstone format examples:

```
### FR-07 — (Withdrawn)

### FR-12 — (Superseded by FR-15)

### KD-03 — (Superseded by KD-09)

### OI-12 — (Closed)

### OI-04 — (Closed by KD-09)

### OI-22 — (Closed by §6.4)

### OI-18 — (Cancelled)
```

The body is empty. The `by <identifier-or-section>` qualifier, when present, must be an item identifier (`FR-NN`, `KD-NN`, `OI-NN`, `UR-NN`, `NFR-NN`) or a section reference (`§X.Y`) — never prose. Identifier and section references are doc machinery, not design content; they already exist legitimately elsewhere in the spec, so their presence in a tombstone heading adds no new searchable content that could mislead a consumer.

Prose qualifiers are disallowed. Forms like `(Closed; resolved by §6.4 final schema)`, `(Cancelled; scope removed from project)`, or `(Superseded by FR-15; old approach was inadequate)` introduce design-content phrases that pollute keyword search. Rationale and historical context for any disposition live in the Git commit message that effected the change.

The rule applies universally across item types. When a KD supersedes another KD, the new KD's body does not recount the superseded predecessor or explain the supersession. The new KD describes the current decision on its own terms; the supersession is captured by the old KD's tombstone and by Git history.

**The template is a superset.** This template lists all sections a spec may have. Any individual spec is expected to use a subset of these sections. Sections that do not apply to a given project are not deleted — they remain in place with a brief design statement explaining *why* the section was considered and found inapplicable. A bare "not applicable" is insufficient; the explanation is itself a small piece of design reasoning that confirms the consideration happened.

Examples of well-formed empty-section statements:
- "Not applicable: this system uses standard REST over HTTPS with no bespoke protocols."
- "Not applicable: this is an internal tool with no PII, no regulated data, and no external trust boundary."
- "Not applicable: this is a library distributed as a NuGet package; deployment shape is the consumer's concern, not this system's."

The goal is that a reader (human or CLI) can confirm, section by section, that each topic was thought about — not just skipped.

**Tables are for tabular data.** Tables are reserved for information whose presentation genuinely benefits from a tabular rendering — DDL columns, enum values, header/value pairs, dimensional matrices. Requirements, decisions, and open items are prose. Numbered subsections that stand up like a document outline scale better as items grow rationale, get reframed, and accumulate cross-references.

**Requirement language.** Requirements use RFC 2119 vocabulary:
- **SHALL** / **MUST** — mandatory
- **SHOULD** — strong recommendation; deviations require justification
- **MAY** — optional

SHALL is the default and the strong preference for all requirement statements. A well-formed requirement does not hedge — if a behavior is conditional, the condition belongs inside the SHALL: "When X occurs, the system shall Y" rather than "The system should Y." SHOULD and MAY are available if a requirement genuinely cannot be expressed in conditional SHALL form, but the author should attempt the rewrite first.

**Revision bumps signal publication.** The spec's `Revision` field bumps when the spec crosses a publishing boundary — handed to the implementing CLI, shared with another Claude session, or shelved at a stable state for future resumption. Continuous edits during ongoing design work do not bump the revision. The Revision exists to let other documents reference a specific stable state of this spec; it has no audience during continuous design.

**Forward-looking reference, not a history book.** The spec body describes the system as currently designed. Content that does not describe current intent — old item bodies, stale prose, obsolete terminology, evolution narratives — does not belong in the spec body. Historical detail is captured by version control (Git commit messages, diffs) and is recovered from there when needed. See `METHODOLOGY.md` §9 for the principle in full. The tombstoning rule serves this principle directly; the spec carries no revision log because the keyword sediment a log accumulates undermines the forward-looking property.

---

## Using This Template

To author a new spec under Template Revision 3:

1. Copy the content between the `BEGIN SPEC TEMPLATE CONTENT` and `END SPEC TEMPLATE CONTENT` markers below into a new file.
2. Fill in the title block fields. Set `Template Revision: 3` to indicate this spec adheres to Revision 3 of this template.
3. Author the spec per its sections. Refer to the Conventions section above for rules.
4. Maintain the spec's `Revision` field per conventions: bump it at publication moments (handoff to CLI, share with another Claude session, shelving for future resumption). Continuous design edits do not bump the revision.

To migrate an existing spec from an earlier template revision to Revision 3, use the spec retrofit instruction (`Spec_Migration_Instruction.md`).

---

<!-- BEGIN SPEC TEMPLATE CONTENT — copy from the H1 below through "End of template" -->

# [Working Title] — Specification

**Project:** [Project name]
**Short description:** [One sentence. What this system is, in plain language.]
**Status:** [Draft | In Design | Pre-Implementation | Implementing | Stable | Superseded]
**Revision:** [Integer. The initial published version is `1`. Each subsequent published change increments by one. A "publication" is any moment the spec crosses out of the design conversation — handed to the implementing CLI, shared with another Claude session, or shelved at a stable state for future resumption. Continuous edits during ongoing design work do not bump the revision.]
**Template Revision:** [Integer. Identifies which revision of `SPEC_TEMPLATE.md` this spec adheres to. See the Conventions section of the referenced template for the rules this spec follows.]
**Created:** [ISO-8601 datetime, e.g., 2026-05-06T14:30:00Z]
**Related Documents:** [Other specs this document references or is referenced by, with relative paths. Omit the field if none.]

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

[Narrative context. What world does this system live in? What existed before it? Who are the people involved and what are they doing today? Two to four paragraphs typically. The reader should understand the situation that motivates the project before reading what the project is.]

### 1.2 Purpose

[Why this system exists. The problem it solves, the value it delivers. One paragraph. This is the answer to "why are we building this?"]

### 1.3 Scope

[What this system covers. The boundary of the work. Concrete: "this system handles X, Y, and Z." Reference Non-Goals (§1.6) for things explicitly excluded.]

### 1.4 Problem Statement

[The specific problem(s) this system addresses, stated precisely. Often a short list of current-state pain points, each one or two sentences. Optional if §1.1 and §1.2 already cover this — leave the heading in place with a note that the problem is sufficiently described above, to show the section was considered.]

### 1.5 Goals

[Prose description of what the system is trying to achieve, from the user's perspective. The executive summary of the project. Two or three paragraphs typically. A reader of this section should come away knowing what success looks like, in narrative form.

Goals are platform- and implementation-agnostic. They describe *what we want to be true about the system*, not *how the system is built*.]

### 1.6 User Requirements

[The Goals (§1.5), enumerated. Each UR is a single sentence in user-facing language, identified as `UR-NN`, written in SHALL form. URs and Goals cover the same ground at different resolutions: Goals are the narrative; URs are the enumerable list a reader can check off.

URs are platform- and implementation-agnostic — they describe what the user needs the system to do, not how the system does it.

Format:

**UR-NN — Short title.** A [user role] shall be able to [capability]. [Optional one sentence of clarification.]

URs do not require formal traceability to FRs. FRs derive from URs informally; the relationship is one of design intent, not bookkeeping. If a project is simple enough that its URs are obvious from the Goals, this section is still populated — the discipline of enumerating user needs in one place is worth the small overhead.]

### 1.7 Non-Goals

[Things the project will explicitly NOT cover. Each entry is a short statement of an excluded capability with a brief reason. This section is non-negotiable — every spec has it, even if the list is short. If something is genuinely out of scope, it earns a line here so the boundary is visible. "We considered this and decided no" is more honest than silent omission.]

### 1.8 Glossary

[Terms specific to this project or its domain, used in a non-obvious or technical sense within this spec. Generic terms (REST, SQL, repository pattern) do not earn entries.

Format:
- **Term** — Definition. Cross-reference to relevant section if the term is elaborated elsewhere (e.g., "see §6.2").

If no project-specific vocabulary needs definition, leave the heading in place with the note "No project-specific terminology required."]

### 1.9 Spec Conventions

This spec adheres to the conventions defined in `SPEC_TEMPLATE.md` at the revision identified by the `Template Revision` field in this spec's title block. See the Conventions section of that document for identifier rules, number stability, terminal item disposition (tombstoning), requirement language, table usage, revision-bump triggers, the forward-looking-reference principle, and other conventions governing this spec.

The conventions are not duplicated here. The template is the single source of truth for the rules a spec follows; this spec references the template revision it was authored under, and migrations to newer template revisions are deliberate (see `SPEC_RETROFIT_INSTRUCTION.md`).

---

## 2. Functional Requirements

[What the system shall do, expressed in platform- and implementation-agnostic terms. Each FR is a single statement of system behavior, written in SHALL form, identified as `FR-NN`.

Group FRs under topical subheadings (e.g., `### 2.1 Cataloging`, `### 2.2 Search and Navigation`). Subheadings exist for reader navigation; they do not affect the sequence numbering of the FRs they contain.

Each FR entry follows the form:

**FR-NN — Short title.** The system shall [behavior]. [Optional one or two sentences of clarification, edge cases, or cross-references to related items.]

A reader of this section should come away with a complete picture of what the system does, with no implementation detail bleeding in. If a requirement cannot be expressed without referring to a specific platform, library, or technology, it likely belongs in §4 (Constraints) or §5 (Architecture), not here.]

---

## 3. Non-Functional Requirements

[Quality attributes the system shall exhibit, beyond functional behavior. Each NFR is identified as `NFR-NN` and follows the same form as FRs.

Common categories — populate the ones that apply, leave headings in place with "not applicable: [reason]" for those that don't:

### 3.1 Performance
[Throughput, latency, response time targets. Concrete numbers where possible.]

### 3.2 Reliability and Availability
[Uptime targets, fault tolerance, recovery behavior, data durability.]

### 3.3 Security
[Authentication, authorization, data protection, threat model boundaries.]

### 3.4 Observability
[Logging, metrics, tracing, alerting requirements.]

### 3.5 Accessibility
[WCAG conformance level, assistive technology support, internationalization.]

### 3.6 Regulatory and Compliance
[Industry-specific regulatory requirements (FDA, GMP, HIPAA, SOC 2, etc.), audit trail requirements, data residency. In regulated industries, this subsection is rarely empty; in unregulated contexts, leave the heading with a brief design statement explaining why no regulatory constraints apply (e.g., "no regulatory constraints apply: this is an internal tool with no PII or regulated data").]]

---

## 4. Constraints and Technology Decisions

[Beyond this section, the spec becomes opinionated about platform, language, and library choices. Sections 1–3 are platform-agnostic; §4 onward is not.]

### 4.1 Tech Stack

[Runtime, language, primary frameworks, database, build tooling. The high-level platform shape. Each choice gets a short rationale or a cross-reference to the KD that justifies it.]

### 4.2 Library Dependencies

[Specific libraries the design depends on, framed as design constraints on implementation. These are not preferences — they are choices that shape the design and should not be substituted without revisiting the design.

Each entry:

**[Library name] [version constraint]** — Role: [what this library does in the system]. Rationale: [why this library was chosen, what alternatives were considered, what would change about the design if it were swapped].

If no specific libraries are constraints on the design, leave the heading with "no specific library dependencies are constraints on this design." That is itself a meaningful statement.]

### 4.3 Environment Constraints

[Where the system is deployed and what assumptions it makes about its environment. Operating system, network topology, reverse proxy presence, container runtime, cloud provider, on-premises constraints. Anything an implementer needs to know about the environment that is not negotiable.]

### 4.4 Repository Structure

[Anticipated folder layout for the project. This feeds forward to the implementation phase as concrete guidance. The implementer may deviate from specific paths provided the separation of concerns is preserved.

Use a code block to render the tree. Annotate purpose for each top-level directory:

```
project-root/
  src/
    Project.Core/        [purpose]
    Project.Api/         [purpose]
    ...
  tests/
    Project.Core.Tests/
    ...
  docs/
    SPEC.md              this document
```

This section is one of the most important feed-forwards to the implementation agent. Be specific about library boundaries, since those map to project files.]

### 4.5 Non-Requirements (Explicit Exclusions)

[Capabilities explicitly out of scope for this implementation. Distinct from §1.6 Non-Goals: §1.6 lists user-facing capabilities the project will not provide; §4.5 lists technical or scope decisions the implementation will not pursue.

Examples: "no multi-tenant database support," "no offline-only mode," "no horizontal scaling at v1," "no end-to-end encryption."

Each entry is a short statement with a brief rationale. These exclusions may be revisited in future versions; the spec does not commit to them being permanent.]

---

## 5. Architectural Overview

[High-level architecture of the system. The shape and tiering, the major components and how they relate. This section is the bridge between the platform-agnostic requirements (§§1–3) and the implementation-specific structure (§§6–11). A reader of this section should understand *the shape* of the system before reading any of the detail sections that follow.]

### 5.1 Tiers and Components

[The major components of the system and their roles. For a multi-tier system, this is "client, server, database, external services." For a console application, this might be "main process, plugin host, configured providers." For a library, this is "public surface, internal core, optional extensions." Describe each major component in a paragraph or two.]

### 5.2 Data Flow

[How data and control move through the system at the architectural level. Often best rendered as a numbered narrative ("a client write originates in [X], passes through [Y], is persisted in [Z], and is propagated to other clients via [W]"). Diagrams may be referenced if maintained alongside the spec, but the prose narrative is authoritative.]

---

## 6. Data Model

[Entities the system manages, their fields, their relationships, and their lifecycle. This section is implementation-aware — it commits to specific schemas, identifier formats, retention policies — but should still be readable by someone who hasn't yet looked at the codebase.]

### 6.1 Entity Overview

[Each major entity gets a paragraph: what it represents, what it relates to, what its lifecycle looks like (created when, modified by what, deleted how).]

### 6.2 Schema

[Concrete schema definitions. For a relational database, this is DDL. For a document store, this is the document shape. For an in-memory model, this is the type structure. Tables, columns, types, constraints, indexes, foreign keys.

DDL, JSON schemas, and similar regular-shape definitions are appropriate places for tables or code blocks.]

### 6.3 Identifiers

[How entities are identified. UUIDs vs sequential IDs vs natural keys. Identifier formats (e.g., "26-character base32 of 16 random bytes"). Uniqueness scope (per-tenant, global, per-collection). Whether identifiers are client-supplied or server-assigned.]

### 6.4 Versioning and Soft Delete

[How the system handles entity versions, optimistic concurrency, soft deletes, and history. If the system uses an event journal or change log as source of truth, that goes here or in §9 depending on whether it's primarily a data concern or a protocol concern.]

### 6.5 Data Retention

[The system's lifecycle posture for every kind of data it accumulates. This section commits to retention windows, purge mechanisms, and the boundary between recoverable and unrecoverable deletion.

Topics to consider, populated where relevant:

- **User data** — how long records are retained after soft delete, what triggers hard purge, what users can request, regulatory retention floors and ceilings.
- **Operational and diagnostic data** — log retention windows, metric retention, trace storage. Often has a different lifecycle than user data and deserves explicit treatment.
- **Event journals and change logs** — if the system uses an append-only journal, the retention policy for the journal is a major design decision. Indefinite retention has cost; truncation has consequences for replay and audit.
- **Snapshots and derived state** — if snapshots are materialized, how many are kept and when older ones are pruned.
- **Soft-deleted records** — how long they remain visible to admin queries before becoming eligible for hard purge.
- **Export and right-to-deletion** — what the system commits to for user-initiated data export and deletion requests, where applicable.
- **Purge mechanism** — how retention is enforced at runtime. Scheduled job? On-demand admin operation? This often spawns a corresponding FR (e.g., "the system shall provide a purge function that sunsets data after the configured retention window").

A retention policy that is deferred to "we'll figure it out later" tends to become expensive to retrofit. Force the conversation at design time, even when the answer is "indefinite retention with no purge mechanism, by deliberate choice."]

---

## 7. Solution Structure

[The project graph: what library or project boundaries we have consciously chosen to draw, and what kinds of types belong in each. This is architectural, not filesystem — it answers "what code may reference what other code" rather than "where does code live on disk." Repository structure (§4.4) handles the filesystem question.

This section is one of the highest-value feed-forwards to the implementation agent. Library splits are choices that shape the entire shape of the codebase, and getting them right early prevents reorganization churn later.]

### 7.1 Libraries and Projects Overview

[List each library or project in the solution with a one-paragraph description of its role and its dependency relationships. A reader should be able to draw the dependency graph from this section.

Example shape:

**Project.Domain** — Domain entities, interfaces (repositories, services), value types, domain enums, domain exceptions. No external dependencies beyond standard library. Testable in isolation. Referenced by every other project in the solution.

**Project.Dtos** — Wire-format types. Pure data. No behavior. No references to other projects in the solution. Used by API service and by C# client(s). Source of truth for any generated client types.

**Project.DataAccess** — Repository implementations. References Domain. Does not reference Dtos.

[etc.]]

### 7.2 Library Boundary Rationale

[Why these splits exist. The DTO library is separate so the wire format is a leaf with no incidental dependencies. The Domain library is separate so the domain model can be tested without spinning up infrastructure. The DAL is separate so the persistence technology can be substituted without rewriting domain logic. State the reasoning explicitly — these are decisions that future readers will be tempted to "simplify" without understanding the cost.

If a particular split is non-obvious (e.g., a shared TypeScript package consumed by both web and mobile clients), call it out and justify it here.]

### 7.3 Reference Rules

[The allowed and disallowed reference relationships, stated as rules. Example:

- Domain references nothing in the solution.
- Dtos references nothing in the solution.
- DataAccess references Domain only.
- Api references Domain, Dtos, DataAccess.
- No project may reference Api.

Rules of this form are mechanically checkable and can be enforced by build-time guards if the implementer chooses.]

---

## 8. Key Interfaces and Contracts

[The principal interfaces the system exposes internally and the contracts it commits to externally. This section makes the seams of the system visible — the points where one component talks to another, expressed as interfaces — and the data contracts the system honors at its boundaries.]

### 8.1 Internal Interfaces

[Key interfaces between components within the system. Each interface gets a brief description of its role and a method signature listing (in the spec's source language or pseudocode). Not every interface in the codebase belongs here — only the ones that represent meaningful architectural seams.

Examples: a service interface that encapsulates all event-application logic, a lock provider interface that abstracts per-tenant serialization, a repository interface that defines the data access surface for an entity. The criterion is: would substituting this interface's implementation be a meaningful design decision? If yes, document it here.]

### 8.2 External Contracts

[Contracts the system honors at its external boundaries. This includes: API request/response shapes (often delegated to §10), wire formats for any bespoke protocols (often delegated to §9), file formats consumed or produced, contracts with external services the system depends on (e.g., a media storage service, an authentication provider).

For each external contract, state what is committed: the format, the versioning policy, the breaking-change posture. External contracts are the interfaces the system cannot change unilaterally; treat them with corresponding care.]

### 8.3 DTOs and Wire Types

[The data transfer objects that cross trust boundaries. If §7 describes a separate DTO library, this is where its contents are enumerated.

DTOs are typically organized by feature or endpoint. Each DTO gets a name and a structural description (fields, types, optionality). Where DTOs are generated to other languages (e.g., C# DTOs generated to TypeScript), state the generation pipeline here or cross-reference to a KD.]

---

## 9. Protocols and Flows

[Bespoke protocols the system implements, and narrated flows of how data and control move through the architecture. This section is conditional — many projects have neither bespoke protocols nor flow narration that adds value beyond §5.2.

When the section is not needed, leave the heading in place with a brief design statement explaining the absence — for example, "this system uses standard REST over HTTPS with no bespoke protocols, and the data flow described in §5.2 is sufficient for an implementer's understanding."]

### 9.1 Protocols

[Each bespoke protocol gets its own subsection (`### 9.1.1 Sync Protocol`, `### 9.1.2 Tag Scan Flow`, etc.). For each protocol:

- **Purpose.** What this protocol exists to do.
- **Endpoints / message types.** The messages or endpoints involved.
- **Sequencing.** The order in which messages are exchanged, and the state machine (if any) governing the exchange.
- **Error and edge cases.** What happens when a message arrives out of order, when a participant disconnects, when a payload is malformed.
- **Versioning.** How the protocol handles compatibility across versions.

A bespoke protocol is anything where the system invents or tightly constrains how it talks — sync protocols, journal-and-replay schemes, custom message handling over WebSockets or RabbitMQ, hand-rolled handshakes. Standard HTTP/REST is not a bespoke protocol and is documented in §10 instead.]

### 9.2 Architectural Data Flows

[Narrated descriptions of how data and control ripple through the architecture for selected operations. Useful when the request path is non-trivial and an implementer needs to understand how layers cooperate.

Example shape:

**Submitting a change set.** A client POSTs the change set to `/events`. The request enters the API layer, where it is authenticated and the actor identity is stamped. The request is routed to the event application service, which acquires the per-household async lock. Inside the lock, validation runs; if validation passes, the events are written to the journal, materialized state is updated, and the lock is released. The push dispatcher is notified asynchronously and fans out a notification to other connected clients of the same household. The client receives the response synchronously while other clients receive the change via push.

Each flow narrative covers the same cross-layer journey from a different angle: read paths, write paths, auth flows, error-recovery flows. Pick the ones that are non-obvious; skip the ones that are routine.]

### 9.3 User Workflow Narration

[Narrated descriptions of selected user workflows where the UX is non-obvious or the state transitions are subtle. Useful for cases where requirements (§§1.6, 2) state *what* the user can do but the design needs to convey *how it feels* and what state the system is in at each step.

Example shape:

**Recovering from a tag scan that points to another household's container.** When a user scans a QR sticker that belongs to a different household, the scan resolution endpoint returns a generic "this tag belongs to another household" response without disclosing the holding household. The client displays a page explaining the situation and offers two actions: navigate home, or report the tag as misapplied. If the user reports it, [continue the narrative].

These narrations should be reserved for workflows that warrant explanation. Routine workflows (scan a tag you own, see its contents) need no narration here.]

---

## 10. API Surface

[The HTTP API the system exposes, enumerated. This section is implementation-specific and assumes REST-over-HTTP unless §9 establishes otherwise.

This section is conditional. When the system has no HTTP API surface (a console application, a library, an embedded system, a CLI tool), leave the heading in place with a brief design statement explaining the absence — for example, "this system is a console application invoked from the command line; its surface is its argument grammar, documented in §[X]" or "this is a library distributed as a NuGet package; its surface is the public API of its types, documented in §8."

For each endpoint group, list the endpoints with method, path, brief description, and a cross-reference to the relevant DTO(s) in §8.3. Do not duplicate full DTO definitions here — link to them.

Example shape:

### 10.1 Conventions

[Authentication, base path, error response shape, content type, versioning posture for the API as a whole.]

### 10.2 [Resource group]

`GET /api/v1/[resource]` — [description]. Request: [QueryParamsDto]. Response: `[ResponseDto]`.

`POST /api/v1/[resource]` — [description]. Request body: `[CreateRequestDto]`. Response: `[ResponseDto]`.

[etc.]]

---

## 11. Infrastructure

[Deployment-time concerns: how the system is hosted, what processes run where, how they are configured, what operational surfaces they expose.

This section is conditional. When the system has no deployment surface in the conventional sense (a library distributed as a package, a CLI tool installed locally), leave the heading in place with a brief design statement explaining the absence — for example, "this is a NuGet-distributed library; deployment shape is the consumer's concern, and infrastructure-adjacent constraints on the consumer environment are documented in §4.3."]

### 11.1 Process Topology

[What processes run, on what hosts, how many instances, and how they are connected. For a single-process service, this is short. For a multi-process system, draw the topology in prose: "the API service runs as a single process behind NGINX; the digest worker runs as a sidecar process on the same host; the collection agents run as separate processes, one per configured target."]

### 11.2 Configuration

[How the system is configured at deploy time. Environment variables, configuration files, secrets management. State the precedence (e.g., environment variables override config file values) and what configuration is required vs. optional.]

### 11.3 Observability

[What the system emits at runtime: logs, metrics, traces, health endpoints. State the format conventions and what consumer is expected. If §3.4 specifies observability NFRs, this is where they are realized.]

### 11.4 Reverse Proxy / Network

[If the system sits behind a reverse proxy or has specific network topology requirements, describe them. TLS termination, header forwarding, routing rules, internal vs. public exposure.]

---

## 12. Key Decisions

[Significant design decisions made during the development of this spec, with rationale and what alternatives were considered. Each decision is identified as `KD-NN` and recorded as a numbered subsection.

A decision earns a KD entry when:
- It shaped the design of multiple sections of the spec.
- It is non-obvious and a future reader (or the author, six months later) might be tempted to revisit it without understanding the original reasoning.
- It involved a deliberate tradeoff that should be visible.

A decision does *not* earn a KD entry when it is a routine choice with obvious justification (e.g., "use HTTPS").

Format (full form):

### KD-NN — Short title

**Decision.** [What was decided, in one or two sentences.]

**Rationale.** [Why this was decided. The forces in tension, the constraints in play.]

**Alternatives considered.** [What else was on the table, and why it was rejected.]

**Consequences.** [What this decision implies for the rest of the system. Cross-references to the FRs, NFRs, or sections this decision shapes.]

The four-part form is the full structure and is preferred when alternatives and consequences are substantive. For decisions where alternatives and consequences are routine or self-evident, the entry may collapse to a shorter form (Decision + Rationale prose, with alternatives and consequences woven in or omitted). Choose the form that conveys the decision most faithfully to the reader — including the implementing CLI, which will treat this section as authoritative justification for design choices made elsewhere in the spec.

KD entries are written when the decision is made, not retroactively. They are revised when the decision is revisited; the Git commit captures what shifted.

**When a KD supersedes another KD.** The new KD describes the current decision on its own terms — it does not recount the superseded predecessor, explain why the supersession was needed, or carry historical context about what changed. The "Alternatives considered" subsection is for alternatives weighed *in parallel with* the current decision, not for documenting prior decisions that were replaced. The supersession relationship is captured entirely by the superseded KD's tombstone (e.g., `### KD-03 — (Superseded by KD-09)`) and by Git history. The new KD's body stays free of historical reference.]

---

## 13. Open Items

[Items that are known to be unresolved at this revision of the spec. Each item is identified as `OI-NN` and recorded as a numbered subsection.

Active Open Items carry a disposition flag in the title indicating their state:

- `⚠ NEEDS YOUR INPUT` — the project owner needs to decide. The author has no recommendation, or the decision is not theirs to make.
- `⚠ NEEDS YOUR REVIEW` — the author has a recommendation; the project owner reviews before commit.
- (no flag) — known gap, intentionally not addressed at this stage of the spec.

Active Open Item format:

### OI-NN — Short title [⚠ flag if applicable]

[One or two paragraphs describing the item: what is unresolved, why, what options exist, and what would resolve it. If the author has a recommendation, state it. If the item is deferred, state what would trigger revisiting.]

**Congruency-check Open Items.** A subset of Open Items is written specifically to direct the implementation agent to verify that built code matches a spec revision. These items are flagged in their narrative ("Confirm that [component X] reflects [decision Y] from §[Z]") and resolved when the implementation has been audited against the spec. They are not bugs — they are deliberate audit prompts planted in the spec to make spec-vs-code drift detectable. See the methodology document for the full pattern.

**Terminal Open Items (tombstones).** When an Open Item reaches a terminal state — Closed or Cancelled — it tombstones in place per the universal terminal disposition rule (see Conventions in the template). The number remains as a stable address; the tombstone has no body. The disposition tag in the heading is the single word `(Closed)` or `(Cancelled)`, optionally followed by `by <identifier-or-section>` when a specific superseding artifact is worth naming.

Terminal format examples:

### OI-12 — (Closed)

### OI-04 — (Closed by KD-09)

### OI-22 — (Closed by §6.4)

### OI-18 — (Cancelled)

The qualifier, when present, must be an identifier or section reference — never prose. Identifier references are doc machinery, not design content, and add no misleading sediment to keyword search. Prose explanations (rationale, historical context, descriptions of resolution) live in Git commit messages.

The forward-looking-reference principle (see Conventions and `METHODOLOGY.md` §9) applies most visibly here. An Open Item with its original body preserved after closure pollutes the spec for downstream consumers doing keyword search. The bare-body tombstone eliminates that pollution while preserving the number as a durable address and optionally pointing — via identifier alone — to where the resolution lives.]

---

*End of template.*

<!-- END SPEC TEMPLATE CONTENT — copy up to (and including) the line above when authoring a new spec -->
