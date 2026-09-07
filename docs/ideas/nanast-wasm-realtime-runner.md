---
type: "Candidate Direction"
title: "Candidate Direction: NanaST Wasm Real-Time Runner"
description: "Superseded candidate direction that established the former host-neutral Wasm runner feasibility feature."
status: "superseded"
tags: [idea, nanast, wasm, real-time]
---

# Candidate Direction: NanaST Wasm Real-Time Runner

## Status

**Superseded.** Sirius Wu superseded this direction on 2026-09-06 by approving NanaST as an ST-to-Forth-2012 compiler in [`docs/VISION.md`](../VISION.md). 

This document is preserved for historical context regarding the earlier WebAssembly exploration. It does not authorize Wasm or Wasmtime development.

## Historical Problem Statement

The earlier direction aimed to let BNC developers replace existing `rtforth` programs with Structured Text (ST). The goal was to produce portable compiled programs that could meet real-time timing constraints before integrating with BNC.

## Historical Recommended Direction

1. NanaST would compile Structured Text to a portable WebAssembly module.
2. A standalone runner library would load, prepare, and execute that module.
3. BNC could then adopt this runner library once feasibility tests passed.

Wasmtime was selected as the first trial runner. NanaST planned to evaluate benchmark results before choosing Wasmtime as a permanent production engine.

Existing `rtforth` programs were intended to run alongside new programs during the migration. ST programs were not intended to compile to or run through `rtforth`.

## Historical Scope

This exploration defined a host-neutral benchmark on ARM64 Linux PREEMPT_RT, without direct BNC, EtherCAT, DoIP/UDS, or `vcmd` integration:
- Used mock `INT` input/output channels to isolate runner performance from hardware factors.
- Measured scan execution time, scheduling jitter, CPU load, and thermal stability.
- Converted runtime execution faults into local Diagnostic Trouble Code (DTC) reports.

## Historical Non-Goals

- Full IEC 61131-3 language support.
- Direct BNC hardware integration, EtherCAT mapping, or diagnostic protocols.
- Dynamic module compilation or loading inside the periodic real-time loop.
- Translation from ST to `rtforth`.
- Production safety or real-time motion claims before gathering benchmark evidence.

## Original Open Questions

- Which Wasmtime configuration and module preparation workflow could satisfy ARM64 real-time requirements?
- Which runner API and error reporting structures would integrate cleanly with BNC without introducing tight coupling?

## Historical Feature Artifacts

- Requirements: [`docs/features/wasm-realtime-runner/requirements.md`](../features/wasm-realtime-runner/requirements.md)
- Architecture: [`docs/features/wasm-realtime-runner/architecture.md`](../features/wasm-realtime-runner/architecture.md)
- Rust Lifecycle Design: [`docs/features/wasm-realtime-runner/rust-lifecycle.md`](../features/wasm-realtime-runner/rust-lifecycle.md)

## Authority and Evidence

- **Approval:** Sirius Wu approved this direction at commit `1501430` and superseded it on 2026-09-06. No formal feasibility benchmark results were recorded.
- **Baseline implementation:** The v0.1 compiler at revision `a63b5ca` ([`docs/SPEC-v0.1.md`](../SPEC-v0.1.md)) established the initial Wasm compiler ABI.
- **BNC deployment:** Botnana Control revision `4ef5e97` relies on `rtforth` in `motion/Cargo.toml`. This confirms existing BNC behavior but did not authorize an immediate switch to Wasm.
- **Governing policy:** [`docs/VISION.md`](../VISION.md) defines the active project vision and acceptance criteria.
