---
type: "Supplementary Specification"
title: "Requirements: NanaST Wasm Real-Time Runner Feasibility"
description: "Superseded scope, timing, load, thermal, and DTC-reporting requirements for the former ARM64 Wasm runner benchmark."
status: "superseded"
revision: "5633aa2"
tags: [requirements, nanast, wasm, real-time]
---

# Requirements: NanaST Wasm Real-Time Runner Feasibility

## Status

**Superseded.** The approved [Forth-2012 vision](../../VISION.md) replaced this Wasm feasibility feature on 2026-09-06. 

This document records the requirements and test thresholds defined for that earlier experiment. It does not authorize current implementation or verification work. Requirements for Forth-2012 targets are specified separately.

## Historical Purpose and Scope

This feature evaluated whether a prepared WebAssembly runner could meet real-time timing limits before connecting to BNC hardware. The benchmark was designed as an isolated NanaST test:
- Did not integrate with BNC, EtherCAT, DoIP, UDS, DIDs, or `vcmd` commands.
- Used mock `INT` inputs and outputs to test the runner in isolation.
- Targeted a LubanCAT 1N board equipped with an RK3566 ARM64 CPU running Linux PREEMPT_RT. AMD64 testing was deferred until ARM64 succeeded.

The test harness was adapted from `rt-tests-rs`. It compiled, instantiated, and warmed a Wasmtime module before periodic execution began:
- Each periodic thread ran `nana_scan` on its own dedicated module instance.
- Module instances were not shared across CPU cores.
- The test ran four independent instances simultaneously across four CPU cores.
- Each instance targeted Wasm execution work taking 50% of its period.
- Normal-priority background threads requested 80% CPU load on the same cores to simulate background contention.

## Terminology

- **`wasm_work_target`:** The intended portion of each period dedicated to Wasm execution (baseline: 50%). Actual measured scan duration is tracked separately.
- **`background_load_request`:** The requested CPU load for the normal-priority background worker on each core (baseline: 80%).
- **Achieved background load:** The actual measured background CPU utilization. This is typically lower than requested because real-time threads preempt background work.

## Historical Quality Requirements

Sirius Wu approved the following requirements for the benchmark. No experimental run was completed before this direction was superseded.

- **QR-PLC-RT (PLC Timing):** Under standard test conditions on the ARM64 target, every PLC scan must complete within 5 ms at a 10 ms period. The maximum absolute wake-up jitter must remain below 1 ms. No deadlines may be missed.
  - *Verification:* One-hour test run after thermal stabilization.
- **QR-MOTION-RT (Motion Timing):** Under standard test conditions on the ARM64 target, every motion scan must complete within 500 µs at a 1 ms period. The maximum absolute wake-up jitter must remain below 100 µs. No deadlines may be missed.
  - *Verification:* One-hour test run after thermal stabilization.
- **QR-THERMAL-EVIDENCE (Thermal Monitoring):** The test harness must record CPU temperature, CPU governor/frequency, throttling events, ambient temperature, and cooling configuration. CPU temperature must remain below 90°C throughout the full one-hour run. Any thermal throttling invalidates the test unless all timing requirements are still met.
  - *Verification:* Review of retained measurement logs.

## Historical Binding Constraints

- **BC-JITTER-MEASURE:** The harness must calculate maximum absolute wake-up deviation as `max(abs(min_jitter), abs(max_jitter))`, rather than using only the signed `Max` value from `rt-tests-rs`.
- **BC-THERMAL-LIMIT:** If the CPU temperature reaches 90°C or higher, the test run is invalidated. To rerun:
  1. Reduce `background_load_request` before reducing `wasm_work_target`.
  2. Allow the hardware to cool down.
  3. Repeat thermal stabilization, then restart the one-hour test.
  4. Record the adjusted targets and achieved loads in the final report.
- **BC-THERMAL-STABILIZATION:** Sample CPU temperatures once per second. The one-hour measurement begins only after the CPU maintains a temperature variation of 2°C or less over a 10-minute window, with no throttling and temperatures below 90°C.
- **BC-TARGET:** The test target is a LubanCAT 1N with an RK3566 ARM64 CPU. Testing on AMD64 is deferred until ARM64 passes.
- **BC-ENVIRONMENT:** Tests must execute on a Linux kernel with PREEMPT_RT patches.
- **BC-PERIODIC-PATH:** All module compilation, loading, linking, and warm-up must happen outside the periodic real-time scan path.
- **BC-LOAD:** The benchmark sets `wasm_work_target` to 50% of the period and `background_load_request` to 80% background CPU load on each core. Both scan duration and achieved background load must be recorded.
- **BC-DURATION:** Run each test configuration for one continuous hour after thermal stabilization.

## Historical Failure Handling

The harness converts any Wasm execution trap into a Diagnostic Trouble Code (DTC) and stops the affected scan safely. For example, a dynamic divide-by-zero error in Wasm produces a DTC. BNC is responsible for storing, reporting, and acting on DTCs in production.

## Historical Decision Gate

If this feasibility test had been executed:
- Passing the PLC gate would have led to a prototype BNC PLC runner.
- Passing both PLC and motion gates would have led to testing motion-loop control.
- Passing PLC but failing motion would have kept PLC scope and dropped motion work.
- Failing PLC would have eliminated Wasmtime as a candidate engine in favor of a native code generator.

## Traceability

- Candidate direction: [`docs/ideas/nanast-wasm-realtime-runner.md`](../../ideas/nanast-wasm-realtime-runner.md)
- Former vision approval: [`docs/VISION.md`](../../VISION.md) (commit `1501430`)
- Historical compiler specification: [`docs/SPEC-v0.1.md`](../../SPEC-v0.1.md)
- Feature architecture: [`architecture.md`](architecture.md)
