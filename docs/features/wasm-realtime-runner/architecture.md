---
type: "Software Architecture Design"
title: "Architecture: NanaST Wasm Real-Time Runner Feasibility"
description: "Superseded architecture for the former host-neutral ARM64 Wasm runner feasibility benchmark."
status: "superseded"
tags: [architecture, nanast, wasm, real-time]
---

# Architecture: NanaST Wasm Real-Time Runner Feasibility

## Status

**Superseded.** The approved Forth-2012 vision replaced this Wasm/Wasmtime architecture on 2026-09-06. 

This document is preserved for historical context and design reference. It does not authorize implementation or testing. All descriptions represent the earlier design.

## Historical Architecture Question

How can NanaST measure a prepared Wasmtime module on an ARM64 Linux PREEMPT_RT board without direct BNC integration, while satisfying the timing, thermal, and DTC-reporting constraints in the approved feasibility plan?

## Historical Significant Drivers

- **Scan execution time:** PLC scans must complete within 5 ms (10 ms period); motion scans must complete within 500 µs (1 ms period).
- **Jitter bounds:** Maximum absolute wake-up deviation must stay below 1 ms for PLC and 100 µs for motion. No deadlines may be missed.
- **System load:** The benchmark runs four module instances across four CPU cores. Each targets 50% period utilization while normal-priority background threads request 80% CPU load on the same cores.
- **Clean periodic loop:** Compilation, module instantiation, preparation, and warm-up must happen completely outside the periodic scan path.
- **Thermal stability:** Runs must remain below 90°C during a one-hour thermal window. Execution faults must produce local DTC reports.
- **Out of scope:** BNC integration, EtherCAT fieldbus, diagnostic protocols (DoIP/UDS), and production safety actions.

Complete quality constraints are documented in [`requirements.md`](requirements.md).

## Historical Context and Boundaries

NanaST provided the compiler, test module, Wasmtime runner, and benchmark process. Tests ran on a LubanCAT 1N board with Linux PREEMPT_RT, reading local CPU temperature and performance counters.

The v0.1 Wasm module imported `bnc.read_input` and `bnc.write_output`, and exported `nana_init` and `nana_scan`. The benchmark harness provided mock integer I/O through these imports. Because Wasm modules hold mutable state in global variables, each periodic worker thread owned its own independent module instance and host state.

## System Architecture

### Component Breakdown

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

| Component | Responsibility | Depends On |
|---|---|---|
| **Benchmark definition** | Generates synthetic Structured Text targeting approximately 50% scan utilization. | Compiler |
| **Preparation controller** | Sets up the Wasmtime engine, compiles and links modules, warms instances, and resets them before testing. | Wasmtime, compiled Wasm |
| **Prepared runner** | Executes `nana_scan` against mock host inputs and outputs. | Compiled module |
| **Periodic scan worker** | Sleeps until scheduled deadlines, triggers scans, measures jitter and scan time, and tracks missed deadlines. | Prepared runner, system clock |
| **Load worker** | Runs a background loop requesting normal-priority CPU load to simulate contention. | System clock, CPU affinity |
| **Thermal controller** | Monitors temperatures, verifies thermal stability, and halts runs that exceed 90°C. | Hardware thermal sensors |
| **Result collector** | Aggregates results from workers and produces final timing, thermal, and DTC reports. | Workers, thermal controller |

The preparation controller could share immutable compiled module definitions. However, periodic workers did not share mutable stores, instances, or metrics. The periodic scan path never waited on the thermal controller, result collector, or background load workers.

### Runtime Interaction Sequence

1. **Pre-test setup:** The compiler generates Wasm bytecode from synthetic Structured Text before the benchmark starts.
2. **Instance preparation:** The controller initializes Wasmtime, creates one isolated runner per periodic worker, and runs `nana_init`.
3. **Warm-up:** The controller executes warm-up scans outside the measurement window, then resets state with `nana_init`.
4. **Thermal stabilization:** The thermal controller samples temperature once per second. Once stable for 10 minutes, the one-hour test begins.
5. **Periodic execution:** Each worker wakes at its scheduled deadline, logs jitter, invokes `nana_scan`, records execution time, and checks for deadlines.
6. **Error handling:** If a scan traps, it records a local DTC and stops that worker, invalidating the test run.
7. **Overheating handling:** If temperature reaches 90°C, the run stops. After cooling, the test is repeated with reduced background load.
8. **Summary:** When all workers complete, the collector checks results against the acceptance gates.

## Candidates Considered

### A. Prepared In-Process Wasmtime Runner (Selected)
Pre-compiles, links, and warms the module before entering the periodic loop. Workers invoke only their prepared instance.
- *Pros:* Minimal architecture that accurately evaluates runner performance while keeping BNC hardware out of scope.
- *Trade-off:* Requires benchmark verification to prove Wasmtime avoids memory allocations or locks during execution.

### B. Precompiled AOT Artifact (Rejected)
Uses Wasmtime ahead-of-time (AOT) compilation to pre-serialize artifacts to disk.
- *Reason for rejection:* Preparation already happens outside the periodic loop. This adds artifact distribution complexity without solving the primary real-time risk.

### C. Full BNC-Integrated Benchmark (Rejected)
Runs the test inside BNC using real EtherCAT hardware and BNC DTC services.
- *Reason for rejection:* Violates the isolated testing boundary and conflates runner performance with external fieldbus and hardware behavior.

## Verification Approach

Historical verification was planned around four checks:
- Confirm that the periodic loop calls only pre-warmed runners and does no runtime compilation or allocation.
- Trigger a dynamic divide-by-zero error to verify local DTC generation and clean worker shutdown.
- Execute the full one-hour ARM64 timing, load, and thermal benchmark.
- Review logs for missed deadlines, jitter bounds, throttling, and temperature limits.

## Traceability

- Former vision approval: [`docs/VISION.md`](../../VISION.md) (commit `1501430`)
- Requirements: [`requirements.md`](requirements.md)
- Candidate idea: [`docs/ideas/nanast-wasm-realtime-runner.md`](../../ideas/nanast-wasm-realtime-runner.md)
- Former compiler ABI: [`docs/SPEC-v0.1.md`](../../SPEC-v0.1.md)
