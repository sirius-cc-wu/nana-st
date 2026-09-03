---
type: "Candidate Direction"
title: "Candidate Direction: NanaST Wasm Real-Time Runner"
description: "Proves whether prepared NanaST Wasm modules can meet BNC real-time timing limits before BNC integration."
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

## Feasibility Scope

The feasibility test is a NanaST responsibility. It does not integrate with
BNC, EtherCAT, DoIP, UDS, DIDs, or a `vcmd` mapping. It uses fake `INT` host
inputs and outputs.

The first target is a LubanCAT 1N with an RK3566 ARM64 CPU. Linux PREEMPT_RT is
the test environment. AMD64 is deferred until ARM64 succeeds.

The harness adapts `rt-tests-rs`. It prepares and warms a Wasmtime module
outside the periodic path. Each periodic scan calls `nana_scan` on one
independent module instance. One module instance does not scan concurrently on
more than one core. Multiple independent instances can run on separate pinned
real-time threads.

The test uses four independent instances, one per core. Each instance executes
Wasm work for up to 80 percent of its period. Normal-priority workers on the
same cores continuously request 80 percent CPU load. Their achieved load is
recorded because real-time execution preempts them.

## Feasibility Gate

Run each baseline and runner configuration for one hour after CPU temperature
and frequency stabilize. Record CPU temperature, frequency or governor,
throttling events, ambient temperature, and cooling or enclosure configuration.

| Loop | Period | Maximum wake-up jitter | Maximum scan execution |
| --- | ---: | ---: | ---: |
| PLC | 10 ms | 1 ms | 8 ms |
| Motion | 1 ms | 100 us | 800 us |

A configuration passes only when every scan remains within its execution budget
and its wake-up jitter limit. A scan must not overrun the next deadline. A
thermal throttle makes the result failed or inconclusive unless the limits
remain satisfied in the throttled steady state.

The harness converts a Wasm execution failure into a diagnostic trouble code
(DTC) report and stops the affected test scan cleanly. For example, a
non-constant zero divisor can make the current Wasm signed division trap. The
harness verifies the DTC report. BNC owns DTC persistence, publication,
DoIP/UDS handling, and any control safety response.

## Decision Gate

- If the PLC gate passes, continue with a BNC PLC runner.
- If the motion gate also passes, continue with motion-loop integration for the
  EDM orbit-velocity case.
- If motion fails and PLC passes, retain the PLC scope and defer motion-loop
  execution.
- If PLC fails, do not integrate Wasmtime as the BNC runner. Evaluate a native
  execution backend without reverting ST programs to rtForth.

## Not Doing

- Full IEC 61131-3 support.
- BNC host integration, EtherCAT mapping, `vcmd` DID design, or DoIP/UDS
  design.
- Dynamic module preparation in a real-time loop.
- ST-to-rtForth translation.
- A safety or production-motion claim before target evidence passes.

## Open Questions

- Which bounded ST workload represents the intended 80 percent scan load?
- Which Wasmtime configuration and module-preparation method pass the ARM64
  gate?
- Which runner API and DTC report are sufficient for BNC without coupling
  NanaST to BNC host details?

## Authority and Evidence

- Authority and status: Sirius Wu approved this direction. Implementation
  remains gated on the recorded feasibility result.
- Evidence: NanaST v0.1 scope at revision `a63b5ca` ([`docs/SPEC-v0.1.md`](../SPEC-v0.1.md)); it establishes the current compiler ABI and explicitly excludes
  production real-time guarantees.
- Evidence: Botnana Control revision `4ef5e97`; `motion/Cargo.toml` depends on
  `rtforth`, and its architecture documentation describes a real-time Forth VM.
  This is implementation evidence, not approval.
- Related governance: [`docs/VISION.md`](../VISION.md) is the approved
  acceptance policy.
