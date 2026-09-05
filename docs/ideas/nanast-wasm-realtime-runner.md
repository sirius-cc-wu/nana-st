---
type: "Candidate Direction"
title: "Candidate Direction: NanaST Wasm Real-Time Runner"
description: "Superseded candidate direction that established the former host-neutral Wasm runner feasibility feature."
status: "superseded"
tags: [idea, nanast, wasm, real-time]
---

# Candidate Direction: NanaST Wasm Real-Time Runner

## Superseded

Sirius Wu superseded this direction on 2026-09-06 by approving NanaST as an
ST-to-Forth-2012 compiler for BNC in [`docs/VISION.md`](../VISION.md). Retain this
artifact as history of the former Wasm direction; it does not authorize Wasm or
Wasmtime implementation work. The remaining present-tense wording records the
former direction. Its replacement premise is superseded by the current vision's
incremental coexistence policy.

## Historical Problem Statement

The former direction sought to let BNC/control developers replace rtForth
programs with Structured Text (ST). It intended the compiled program to be
portable across CPU architectures and to meet real-time timing limits before
BNC integration began.

## Historical Recommended Direction

NanaST would compile ST to a portable Wasm module. A reusable runner library
would load, prepare, and run that module. BNC could use the library after the
feasibility gate passed.

Wasmtime was the first runner candidate. NanaST would not have selected
Wasmtime as the permanent production engine until target evidence passed.

Existing rtForth programs would have continued to run during BNC migration. ST
programs would not have compiled to or run through rtForth.

## Historical Scope

This historical direction established a host-neutral ARM64 Linux PREEMPT_RT
feasibility test, with no BNC, EtherCAT, DoIP/UDS, DID, or `vcmd` integration.
It selected fake `INT` imports and outputs so the runner could be measured
before BNC integration. The archived requirements record the former target,
timing, load, thermal, DTC-reporting, and decision-gate details.

## Historical Non-Goals

- Full IEC 61131-3 support.
- BNC host integration, EtherCAT mapping, `vcmd` DID design, or DoIP/UDS
  design.
- Dynamic module preparation in a real-time loop.
- ST-to-rtForth translation.
- A safety or production-motion claim before target evidence passes.

## Original Open Questions

- Which Wasmtime configuration and module-preparation method pass the ARM64
  gate?
- Which runner API and DTC report are sufficient for BNC without coupling
  NanaST to BNC host details?

## Historical Feature Artifacts

- Superseded requirements: [`docs/features/wasm-realtime-runner/requirements.md`](../features/wasm-realtime-runner/requirements.md).
- Superseded architecture: [`docs/features/wasm-realtime-runner/architecture.md`](../features/wasm-realtime-runner/architecture.md).
- Superseded Rust lifecycle design: [`docs/features/wasm-realtime-runner/rust-lifecycle.md`](../features/wasm-realtime-runner/rust-lifecycle.md).

## Authority and Evidence

- Authority and status: Sirius Wu approved this direction at commit `1501430`
  and superseded it on 2026-09-06. No feasibility result was recorded.
- Evidence: NanaST v0.1 scope at revision `a63b5ca`
  ([`docs/SPEC-v0.1.md`](../SPEC-v0.1.md)); it establishes the former compiler
  ABI and explicitly excludes production real-time guarantees.
- Evidence: Botnana Control revision `4ef5e97`; `motion/Cargo.toml` depends on
  `rtforth`, and its architecture documentation describes a real-time Forth VM.
  This is implementation evidence, not approval.
- Related governance: [`docs/VISION.md`](../VISION.md) is the approved
  acceptance policy.
