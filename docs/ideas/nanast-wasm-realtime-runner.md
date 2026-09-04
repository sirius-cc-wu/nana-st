---
type: "Candidate Direction"
title: "Candidate Direction: NanaST Wasm Real-Time Runner"
description: "Approved candidate direction that established the host-neutral Wasm runner feasibility feature."
status: "approved"
tags: [idea, nanast, wasm, real-time]
---

# Candidate Direction: NanaST Wasm Real-Time Runner

## Problem Statement

NanaST must let BNC/control developers replace rtForth programs with Structured
Text (ST). The compiled program must be portable across CPU architectures. It
must meet real-time timing limits before BNC integration begins.

## Recommended Direction

NanaST compiles ST to a portable Wasm module. A reusable runner library loads,
prepares, and runs that module. BNC can use the library after the feasibility
gate passes.

Wasmtime is the first runner candidate. NanaST does not select Wasmtime as the
permanent production engine until target evidence passes.

Existing rtForth programs continue to run during BNC migration. ST programs do
not compile to or run through rtForth.

## Historical Scope

This approved direction established a host-neutral ARM64 Linux PREEMPT_RT
feasibility test, with no BNC, EtherCAT, DoIP/UDS, DID, or `vcmd` integration.
It selected fake `INT` imports and outputs so the runner can be measured before
BNC integration. The approved requirements now own the exact target, timing,
load, thermal, DTC-reporting, and decision-gate details.

## Not Doing

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

## Current Feature Artifacts

- Approved requirements: [`docs/features/wasm-realtime-runner/requirements.md`](../features/wasm-realtime-runner/requirements.md).
- Accepted architecture: [`docs/features/wasm-realtime-runner/architecture.md`](../features/wasm-realtime-runner/architecture.md).
- Proposed Rust lifecycle design: [`docs/features/wasm-realtime-runner/rust-lifecycle.md`](../features/wasm-realtime-runner/rust-lifecycle.md).

## Authority and Evidence

- Authority and status: Sirius Wu approved this direction. Implementation
  remains gated on the recorded feasibility result.
- Evidence: NanaST v0.1 scope at revision `a63b5ca`
  ([`docs/SPEC-v0.1.md`](../SPEC-v0.1.md)); it establishes the current compiler
  ABI and explicitly excludes production real-time guarantees.
- Evidence: Botnana Control revision `4ef5e97`; `motion/Cargo.toml` depends on
  `rtforth`, and its architecture documentation describes a real-time Forth VM.
  This is implementation evidence, not approval.
- Related governance: [`docs/VISION.md`](../VISION.md) is the approved
  acceptance policy.
