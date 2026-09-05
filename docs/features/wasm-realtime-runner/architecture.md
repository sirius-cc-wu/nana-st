---
type: "Software Architecture Design"
title: "Architecture: NanaST Wasm Real-Time Runner Feasibility"
description: "Superseded architecture for the former host-neutral ARM64 Wasm runner feasibility benchmark."
status: "superseded"
tags: [architecture, nanast, wasm, real-time]
---

# Architecture: NanaST Wasm Real-Time Runner Feasibility

## Superseded

The approved Forth-2012 vision superseded this Wasm/Wasmtime architecture on
2026-09-06. Retain it as historical design context; it does not authorize
implementation or verification work. All remaining present-tense and imperative
wording records the former design and is not current instruction.

## Historical Architecture Question

How can NanaST measure a prepared Wasmtime module on the ARM64 Linux PREEMPT_RT
target without BNC integration, while preserving the periodic-path, timing,
thermal, and DTC-reporting constraints in the approved feasibility direction?

At acceptance, the authority was Sirius Wu. This design is historical.

## Historical Significant Drivers

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

The complete historical quality requirements and constraints are in
[`requirements.md`](requirements.md).

## Historical Context and Boundaries

NanaST provides the compiler, the test module, the Wasmtime runner, and the
benchmark process. The test process runs on a LubanCAT 1N with Linux PREEMPT_RT.
It reads local temperature and execution information. It does not connect to
BNC or EtherCAT hardware.

The former Wasm contract imports `bnc.read_input` and `bnc.write_output`, and
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

At acceptance, Sirius Wu selected Candidate A. The Forth-2012 vision superseded
that selection on 2026-09-06 before a feasibility result was recorded.

Wasmtime is no longer an evaluated NanaST engine. Candidate B is historical and
not a later option under the current vision.

## Historical Verification

- It would have inspected the periodic path to show that it started with a
  prepared runner and performed no module preparation.
- It would have exercised a dynamic divide-by-zero scan and verified one local
  DTC report and a stopped affected worker.
- It would have run the approved ARM64 timing, load, and thermal measurement.
  No result was recorded by this design.
- It would have reviewed retained timing, scan duration, missed-deadline,
  temperature, frequency, throttling, ambient, cooling, and adjusted-load
  evidence against the approved gates.

## Historical Detailed-Design Handoffs

- **Rust lifecycle design:** Would have defined ownership and startup,
  warm-reset, cancellation, invalidation, join, and cleanup of the engine,
  runner, periodic workers, load workers, thermal controller, and result
  collector.
- **Rust implementation design:** Would have defined the smallest testable
  runner, benchmark, DTC outcome, timing record, and thermal-sample interfaces.
- **Implementation and verification:** Would have built the synthetic benchmark
  and prepared-runner benchmark after this architecture was approved.

## Evidence

- Former vision approval: [`docs/VISION.md`](../../VISION.md), commit `1501430`.
- Historical requirements: [`requirements.md`](requirements.md), first approved
  at `5633aa2` and organized as this feature at `612c003`.
- Historical candidate direction:
  [`docs/ideas/nanast-wasm-realtime-runner.md`](../../ideas/nanast-wasm-realtime-runner.md),
  commit `1501430`; its constraints were revised at `5633aa2`.
- Former compiler ABI: [`docs/SPEC-v0.1.md`](../../SPEC-v0.1.md), commit `a63b5ca`.
- Former Wasm emitter: [`src/wasm.rs`](../../../src/wasm.rs).
- Wasmtime 45.0.1 documents `Engine::precompile_module` as preparation-time
  AOT support. This is capability evidence, not a target timing result.
