---
type: "Vision"
title: "Vision: NanaST"
description: "NanaST provides an incremental Structured Text compiler for BNC control developers, targeting a verified Forth-2012 subset in rfopt."
status: "approved"
tags: [vision, governance, nanast, forth-2012, rfopt]
---

# Vision: NanaST

NanaST allows Botnana Control (BNC) developers to introduce testable Structured Text (ST) programs incrementally. It translates ST code into a specific, verified subset of Forth-2012 words and semantics that are executed by the `rfopt` runtime. The project does not require `rfopt` to support the full Forth-2012 standard.

### Project Boundaries
- **NanaST** defines the supported ST language subset, source-located diagnostics, the ST-to-Forth compilation contract, and the required Forth word inventory. It pins the specific `rfopt` commit used for integration testing.
- **rfopt** implements the runtime engine, source loading, and execution.
- **BNC** manages hardware integration, fieldbus communications, and control policies.

## Principles

- **ST is the programming language:** NanaST targets real BNC control tasks using a small, explicit ST language subset. The compiler rejects unsupported syntax with clear, source-located diagnostics.
- **The Forth subset is explicit:** Every target feature specification must list every required Forth-2012 word and behavior. Target extensions must be explicitly documented and tested rather than assumed.
- **NanaST declares; rfopt implements:** If a required Forth word is missing from rfopt, it must be implemented directly in the `rfopt` repository. NanaST must never add fallback definitions or workarounds.
- **rfopt is the primary planned runtime:** NanaST builds and tests against a pinned `rfopt` submodule. The first milestone is loading and running a Boolean pass-through program. `rfopt` becomes the permanent runtime only if test evidence proves it meets BNC's needs.
- **AMD64 before AArch64:** The initial development target is AMD64 Linux. AArch64 work begins only after rfopt passes the approved AMD64 integration gate.
- **Incremental migration:** Existing BNC Forth programs continue to run alongside new ST-generated code. System migration is step-by-step, not all-at-once.
- **Real-time claims require target evidence:** Using a specific Forth engine does not automatically prove timing, memory, or safety claims. Every claim must be backed by recorded benchmark evidence from target tests.
- **Diagnostics remain clear:** The compiler must provide helpful source-located error messages. BNC and the runtime host handle runtime errors, Diagnostic Trouble Codes (DTCs), and safety responses.
- **BNC handles hardware and communication:** EtherCAT configuration, DTC storage, DoIP/UDS diagnostics, scheduling, and safety policies belong in BNC. NanaST remains device-neutral.
- **Scope grows only from real need:** Add language syntax or Forth words only when a verified BNC control task requires them.

## Non-Goals

- Full compatibility with IEC 61131-3 or MATIEC.
- Graphical programming, online debugging, or an IDE.
- Full Forth-2012 compliance in `rfopt`.
- Compatibility workarounds in NanaST for words missing in `rfopt`.
- BNC hardware integration (such as EtherCAT, DTC storage, DoIP/UDS, or scheduling).
- WebAssembly (Wasm) backends, Wasmtime runners, or direct native-code compilation.
- Multiple unverified Forth runtime targets.
- Replacing all existing BNC Forth programs at once.
- Making real-time motion claims without experimental test evidence.

## Acceptance Policy

### What We Accept
- **Targeted improvements:** Changes that give developers a clearer, tested path from an ST control program to the approved Forth contract.
- **Documented Forth words:** Target additions that list their required Forth-2012 words, preserve expected semantics, and include tests against pinned `rfopt`.
- **Minimal additions:** Changes that add only the minimal ST syntax or Forth words required by a specific BNC use case.
- **Evidence-backed performance:** Real-time claims supported by recorded test runs of generated code on `rfopt`.

### What We Resist
- **Tight BNC coupling:** Changes that tie NanaST directly to EtherCAT devices, BNC DTC systems, scheduling, or control policies.
- **Out-of-scope targets:** Pull requests adding Wasm, native code backends, broad IEC syntax, or alternate Forth engines without an approved vision revision.

## Boundary Cases and Guidance

- **A pull request proposes a Wasm or native code backend:**
  *Reject it.* NanaST compiles exclusively to the approved Forth subset.
- **`rfopt` lacks a needed Forth-2012 word:**
  *Document the gap in NanaST and implement the word in `rfopt`.* Do not add fallback code inside NanaST.
- **`rfopt` is missing features from the Forth-2012 standard:**
  *Accept incremental progress.* `rfopt` only needs to implement the specific words required by active NanaST features.
- **An artifact passes on `rfopt` but claims real-time performance:**
  *Treat it as basic compatibility only.* Require documented timing measurements before accepting any real-time claim.
- **A contribution adds EtherCAT-specific logic:**
  *Keep NanaST device-neutral.* Device and bus mapping belongs in BNC.
- **A contribution proposes migrating BNC to `rfopt` immediately:**
  *Direct it to the BNC repository.* NanaST only defines and verifies the compiler target contract.
- **Runtime execution fails:**
  *Handle diagnostics properly.* BNC and the runtime handle runtime errors and DTCs. NanaST handles only compile-time errors.
- **A request asks for broad IEC language syntax:**
  *Accept only what is needed.* New syntax requires a concrete BNC control use case and automated tests.
- **A proposal replaces all BNC Forth programs at once:**
  *Reject sudden replacement.* Migration must be incremental so existing systems continue to operate reliably.

## Authority and Evidence

- **Approval:** Sirius Wu approved this vision revision on 2026-09-06. It establishes `rfopt` as the sole planned runtime, prioritizes AMD64 Linux before AArch64, and chooses an explicit Forth-2012 subset. This supersedes earlier Wasm and SwiftForth directions.
- **Transition status:** Historical files (such as `src/wasm.rs` and Wasmtime tests) represent completed v0.1 baseline work. They must not be extended. They will be replaced or removed as the `rfopt` target is implemented.
- **Runtime evidence:** `rfopt` is tracked as a pinned Git submodule. Its documentation describes an intended native engine and an eForth base, but does not yet establish full NanaST compatibility or real-time performance.
- **BNC deployment:** Botnana Control revision `4ef5e97` uses `rtforth` in `motion/Cargo.toml`. This confirms existing BNC behavior but does not authorize an immediate migration to `rfopt`.
- **Historical documentation:** [`docs/SPEC-v0.1.md`](SPEC-v0.1.md), [`tasks/plan.md`](../tasks/plan.md), and [`tasks/todo.md`](../tasks/todo.md) preserve the record of the completed v0.1 Wasm implementation at revision `a63b5ca`.
- **Open questions:** Concrete Rust API types for `rfopt`, exact formatting of generated Forth files, and real-time benchmark criteria will be defined in subsequent feature specifications.
