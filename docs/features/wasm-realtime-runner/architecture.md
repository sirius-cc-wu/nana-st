---
type: "Software Architecture Design"
title: "Architecture: NanaST Wasm Real-Time Runner Feasibility"
description: "Proposes the smallest host-neutral runner and benchmark structure for ARM64 real-time feasibility evidence."
status: "accepted"
tags: [architecture, nanast, wasm, real-time]
---

# Architecture: NanaST Wasm Real-Time Runner Feasibility

## Architecture Question

How can NanaST measure a prepared Wasmtime module on the ARM64 Linux PREEMPT_RT
target without BNC integration, while preserving the periodic-path, timing,
thermal, and DTC-reporting constraints in the approved feasibility direction?

Authority: Sirius Wu. This design is accepted.

## Significant Drivers

- A prepared PLC scan must complete within 5 ms at a 10 ms period. A prepared
  motion scan must complete within 500 us at a 1 ms period.
- The maximum absolute wake-up deviation is 1 ms for PLC and 100 us for
  motion. No periodic worker may miss a deadline.
- The benchmark runs four independent module instances, one per core. Each
  targets 50 percent Wasm work per period while normal-priority workers request
  80 percent CPU load on the same cores.
- Compilation, loading, preparation, and warming occur outside the periodic
  path.
- A test run has a one-hour thermal-stable measurement window. The CPU remains
  below 90 C. A scan failure produces a local DTC report.
- BNC, EtherCAT, DoIP/UDS, DID, `vcmd`, DTC storage, publication, and safety
  behavior are out of scope.

The complete approved quality requirements and constraints are in
[`requirements.md`](requirements.md).

## Context and Boundaries

NanaST provides the compiler, the test module, the Wasmtime runner, and the
benchmark process. The test process runs on a LubanCAT 1N with Linux PREEMPT_RT.
It reads local temperature and execution information. It does not connect to
BNC or EtherCAT hardware.

The current Wasm contract imports `bnc.read_input` and `bnc.write_output`, and
exports `nana_init` and `nana_scan`. The feasibility host provides fake `INT`
inputs and outputs through these imports. Each Wasm module keeps program state
in mutable globals. Therefore, each periodic worker owns one separate module
instance and host state.

## Selected Views

### Decomposition

```text
Synthetic ST benchmark source
          |
          v
Compiler -> Wasm module -> Preparation controller -> Prepared runner per core
                                                   |         |
                                                   |         v
                                                   |    periodic scan worker
                                                   |         |
                                                   |         +--> local timing and DTC outcome
                                                   v
                                      thermal controller and result collector

Normal-priority load workers ----------------------------------> CPU contention
```

| Component | Responsibility | Owns | Depends on |
| --- | --- | --- | --- |
| Benchmark definition | Defines a synthetic, bounded ST program that targets 50 percent scan work. | Benchmark source and profile. | Compiler contract. |
| Preparation controller | Creates the Wasmtime engine, compiles and links the module, creates workers, initializes and warms each instance, then resets it before measurement. | Prepared module and startup result. | Wasmtime, benchmark Wasm. |
| Prepared runner | Calls `nana_scan` with fake input and output imports. | One instance, its mutable state, and its fake host state. | Prepared module. |
| Periodic scan worker | Sleeps to absolute deadlines, calls its prepared runner, measures wake-up deviation and scan duration, and detects missed deadlines. | Per-worker measurement state. | Prepared runner, local clock. |
| Load worker | Requests normal-priority CPU load on the same core as a periodic worker. | Local load configuration and achieved-load measurement. | Local clock and CPU affinity. |
| Thermal controller | Samples thermal data, determines stabilization, and invalidates a run at 90 C. | Thermal samples and run validity. | Target thermal interface. |
| Result collector | Collects completed worker outcomes and retains timing, thermal, load, and DTC evidence after the run. | Retained test result. | Workers and thermal controller. |

The preparation controller may share immutable compiled module data. A periodic
worker must not share its Store, instance, fake host state, or mutable metrics
with another periodic worker. The periodic path must not depend on the thermal
controller, result collector, or a load worker completing an operation.

### Runtime Interaction

1. The benchmark definition produces bounded ST source. The compiler produces
   Wasm before the test run.
2. The preparation controller configures Wasmtime, compiles and links the
   module, creates one isolated prepared runner for each periodic worker, and
   calls `nana_init`.
3. The controller warms every runner outside the measured path, then calls
   `nana_init` again so measurement begins from the defined initial state.
4. The thermal controller samples once per second. After the required stable
   window, it permits the one-hour measurement.
5. Every periodic worker wakes at its absolute deadline, records the wake-up
   deviation, calls its own `nana_scan`, records scan duration, and detects a
   missed deadline.
6. A scan error produces a local DTC report and stops the affected worker. The
   controller invalidates that run. It does not perform BNC safety behavior.
7. At 90 C or higher, the thermal controller invalidates the run. After
   cooling and restabilization, the caller supplies an adjusted profile that
   reduces normal-priority background load before reducing Wasm work.
8. After all workers stop, the result collector evaluates the approved gates.

## Candidates and Trade-Offs

### A. Prepared in-process Wasmtime runner — selected

The preparation controller uses Wasmtime to compile, link, instantiate, and
warm the module before periodic execution. Periodic workers call only their
prepared runner instance.

This is the smallest architecture that tests the actual runner path. It keeps
BNC out of scope and preserves host-neutral fake imports. It does not prove
that Wasmtime performs no periodic-path allocation, locking, paging, or other
unbounded work; the target benchmark is the required evidence.

### B. Precompiled Wasmtime artifact

Wasmtime supports ahead-of-time precompilation and compatible artifact
deserialization. This can remove translation and code generation when a module
is prepared.

It adds artifact compatibility, distribution, and validation concerns. Because
preparation is outside the periodic path, it does not answer the first
feasibility risk better than Candidate A. Defer it until Candidate A reveals a
preparation or deployment need.

### C. BNC-integrated benchmark

A BNC runner could use real EtherCAT inputs and BNC DTC infrastructure.

It would provide an end-to-end BNC result, but it violates the approved
feasibility boundary and confounds the runner timing result with BNC behavior.
Reject it for this experiment.

## Decision Status

Sirius Wu selected Candidate A. It separates module preparation from periodic
execution, keeps each stateful instance confined to one worker, and makes the
feasibility result independent of BNC integration.

Wasmtime remains the evaluated engine, not a permanent production-engine
selection. Candidate B remains a later option.

## Verification

- Inspect the periodic path to show that it starts with a prepared runner and
  performs no module preparation.
- Exercise a dynamic divide-by-zero scan and verify one local DTC report and a
  stopped affected worker.
- Run the approved ARM64 timing, load, and thermal measurement. No result is
  recorded by this design.
- Review retained timing, scan duration, missed-deadline, temperature,
  frequency, throttling, ambient, cooling, and adjusted-load evidence against
  the approved gates.

## Detailed-Design Handoffs

- **Rust lifecycle design:** Define ownership and startup, warm-reset,
  cancellation, invalidation, join, and cleanup of the engine, runner,
  periodic workers, load workers, thermal controller, and result collector.
- **Rust implementation design:** Define the smallest testable runner,
  benchmark, DTC outcome, timing record, and thermal-sample interfaces.
- **Implementation and verification:** Build the synthetic benchmark and the
  prepared-runner benchmark only after this architecture is approved.

## Evidence

- Approved vision: [`docs/VISION.md`](../../VISION.md), commit `1501430`.
- Approved requirements: [`requirements.md`](requirements.md), commit `5633aa2`.
- Approved candidate direction:
  [`docs/ideas/nanast-wasm-realtime-runner.md`](../../ideas/nanast-wasm-realtime-runner.md),
  commit `5633aa2`.
- Current compiler ABI: [`docs/SPEC-v0.1.md`](../../SPEC-v0.1.md), commit `a63b5ca`.
- Current Wasm emitter: [`src/wasm.rs`](../../../src/wasm.rs).
- Wasmtime 45.0.1 documents `Engine::precompile_module` as preparation-time
  AOT support. This is capability evidence, not a target timing result.
