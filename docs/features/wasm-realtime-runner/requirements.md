---
type: "Supplementary Specification"
title: "Requirements: NanaST Wasm Real-Time Runner Feasibility"
description: "Approved scope, timing, load, thermal, and DTC-reporting requirements for the ARM64 host-neutral runner benchmark."
status: "approved"
revision: "5633aa2"
tags: [requirements, nanast, wasm, real-time]
---

# Requirements: NanaST Wasm Real-Time Runner Feasibility

## Purpose and Scope

NanaST must establish whether a prepared Wasm runner can meet real-time timing
limits before BNC integration begins. The feasibility test is a NanaST
responsibility. It does not integrate with BNC, EtherCAT, DoIP, UDS, DIDs, or a
`vcmd` mapping. It uses fake `INT` host inputs and outputs.

The first target is a LubanCAT 1N with an RK3566 ARM64 CPU. Linux PREEMPT_RT is
the test environment. AMD64 is deferred until ARM64 succeeds.

The harness adapts `rt-tests-rs`. It prepares and warms a Wasmtime module
outside the periodic path. Each periodic scan calls `nana_scan` on one
independent module instance. One module instance does not scan concurrently on
more than one core. Multiple independent instances can run on separate pinned
real-time threads.

The test uses four independent instances, one per core. Each instance targets
Wasm work equal to 50 percent of its period. Normal-priority workers on the
same cores continuously request 80 percent CPU load. Their achieved load is
recorded because real-time execution preempts them.

## Terminology

- **`wasm_work_target`:** The requested share of each periodic interval for
  Wasm scan work. Its baseline value is 50 percent. Scan duration is the
  recorded observed measure; this target is not the observed duration.
- **`background_load_request`:** The requested share of one pinned core for
  its normal-priority background worker. Its baseline value is 80 percent.
- **Achieved background load:** The observed normal-priority background load on
  a core. It can be lower than `background_load_request` because periodic
  real-time work preempts it.

## Quality Requirements

The thresholds below are approved by Sirius Wu. No verification result exists
yet. Each requirement applies after the module is prepared and warmed outside
the periodic path.

- **QR-PLC-RT:** Given the PLC runner runs on the ARM64 target under the stated
  test conditions, it shall complete every scan within 5 ms and remain within
  a 1 ms maximum absolute wake-up deviation at a 10 ms period. It shall not
  miss a deadline.
  - Source and status: Sirius Wu, approved.
  - Affected boundary: NanaST real-time runner feasibility.
  - Verification: One-hour target run after thermal stabilization.
- **QR-MOTION-RT:** Given the motion runner runs on the ARM64 target under the
  stated test conditions, it shall complete every scan within 500 us and remain
  within a 100 us maximum absolute wake-up deviation at a 1 ms period. It shall
  not miss a deadline.
  - Source and status: Sirius Wu, approved.
  - Affected boundary: NanaST real-time runner feasibility.
  - Verification: One-hour target run after thermal stabilization.
- **QR-THERMAL-EVIDENCE:** The verification run shall record CPU temperature,
  frequency or governor, throttling events, ambient temperature, and cooling or
  enclosure configuration. CPU temperature shall remain below 90 C for the
  complete one-hour measurement. A thermal throttle makes the result failed or
  inconclusive unless the timing limits remain satisfied in the throttled
  steady state.
  - Source and status: Sirius Wu, approved.
  - Affected boundary: Target verification evidence.
  - Verification: Retained measurement log and operator review.

## Binding Constraints

- **BC-JITTER-MEASURE:** The harness shall calculate maximum absolute wake-up
  deviation as `max(abs(min_jitter), abs(max_jitter))`. It shall not use only
  the existing signed `Max` value from `rt-tests-rs`.
  - Source and status: Sirius Wu, approved.
  - Verification: Inspect the harness calculation and retained measurement log.
- **BC-THERMAL-LIMIT:** If CPU temperature reaches 90 C or higher, invalidate
  the current run. The caller shall reduce `background_load_request` before it
  reduces `wasm_work_target`, wait for the CPU to cool down, repeat thermal
  stabilization, and rerun the full test. The record shall state the adjusted
  `wasm_work_target`, `background_load_request`, and achieved background load.
  A rerun with either target below its original value does not demonstrate the
  original 50-percent-Wasm and 80-percent-background-load condition.
  - Source and status: Sirius Wu, approved.
  - Verification: Retained temperature and load measurements.
- **BC-THERMAL-STABILIZATION:** Sample CPU thermal-zone temperature once per
  second. Discard warm-up measurements. Start the one-hour measurement only
  after the preceding ten minutes have a temperature range of 2 C or less, no
  thermal-throttling event, and a CPU temperature below 90 C.
  - Source and status: Sirius Wu, approved.
  - Verification: Retained temperature and throttling measurement log.
- **BC-TARGET:** The first feasibility target is a LubanCAT 1N with an RK3566
  ARM64 CPU. AMD64 is deferred until ARM64 succeeds.
  - Source and status: Sirius Wu, approved.
  - Verification: Inspect target hardware and retained test record.
- **BC-ENVIRONMENT:** The feasibility test shall run on Linux PREEMPT_RT.
  - Source and status: Sirius Wu, approved.
  - Verification: Inspect the target kernel and retained test record.
- **BC-PERIODIC-PATH:** Module compilation, loading, preparation, and warming
  shall occur outside the periodic path.
  - Source and status: Approved vision at commit `1501430`.
  - Verification: Inspect the runner and trace the periodic path.
- **BC-LOAD:** The test configuration shall name the periodic Wasm target as
  `wasm_work_target` and set it to 50 percent of each period while remaining
  within the QR-PLC-RT or QR-MOTION-RT execution limit. It shall name the
  normal-priority target as `background_load_request` and set it to 80 percent
  CPU load on the same cores. The test shall record scan duration and achieved
  background load.
  - Source and status: Sirius Wu, approved.
  - Verification: Inspect workload configuration and retained measurement log.
- **BC-DURATION:** Run each baseline and runner configuration for one hour
  after thermal stabilization.
  - Source and status: Sirius Wu, approved.
  - Verification: Retained measurement log and operator review.

## Delegated Benchmark Definition

The runner feasibility work may define a synthetic, bounded benchmark that
represents the 50 percent scan load. This authority is delegated by Sirius Wu.
A trivial pass-through module does not prove the 50 percent condition.

## Failure Reporting Boundary

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

## Traceability

- Candidate-direction history and approval: [`docs/ideas/nanast-wasm-realtime-runner.md`](../../ideas/nanast-wasm-realtime-runner.md), commit `5633aa2`.
- Approved vision: [`docs/VISION.md`](../../VISION.md), commit `1501430`.
- Current compiler ABI: [`docs/SPEC-v0.1.md`](../../SPEC-v0.1.md), commit `a63b5ca`.
- Current feature architecture: [`architecture.md`](architecture.md).
