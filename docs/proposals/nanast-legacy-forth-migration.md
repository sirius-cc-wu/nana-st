---
type: Proposal
title: Incremental Migration of Legacy Forth Control Logic to NanaST
status: proposed
tags: [proposal, nanast, forth, plc, migration, bnc]
---

# Proposal: Incremental Migration of Legacy Forth Control Logic to NanaST

## Status

Proposed. This document records a migration recommendation. It does not approve
new NanaST syntax, change the BNC runtime boundary, or authorize deployment.

## Context

`tests/fixtures/legacy_forth/` captures 101 Forth source files from the
Mapacode repositories. The capture contains repeated product variants, runtime
libraries, device protocols, SFC applications, and benchmarks. It is reference
material, not NanaST input.

NanaST currently supports one cyclic program with:

- `VAR`, `VAR_INPUT`, and `VAR_OUTPUT` storage;
- `BOOL`, `INT`, `DINT`, and binary64 `REAL` values;
- persistent local variables;
- assignments, expressions, and nested `IF` statements.

NanaST and rfopt prove source-to-runtime behavior through in-memory typed cell
handles. They do not yet provide BNC process-image binding, EtherCAT access,
cyclic deployment, or real-time evidence.

The suitable initial class of behavior is a device-neutral cyclic decision,
such as level-state evaluation, fault aggregation, edge detection, or request
priority selection. Each migration must derive this behavior directly from a
selected legacy Forth source and verify it against recorded traces.

## Recommendation

Migrate only device-neutral, cyclic decision logic first. Keep scheduling,
fieldbus communication, device protocols, motion execution, diagnostics, and
safety response in BNC and its runtime services.

BNC must read physical inputs, invoke the compiled NanaST scan once per chosen
control cycle, and apply approved outputs. NanaST must not generate direct
EtherCAT, UART, Modbus, SDO, gateway, or motion words.

## Candidate Migration Groups

| Group | Legacy sources | Recommendation | Additional requirement |
|---|---|---|---|
| Simple cyclic decisions | `Ewater/sfc/chiller.fs`; `Ewater/sfc/dosing.fs`; selected decision sections from the CR6 sources | Migrate first. Rewrite Forth actions as named Boolean, integer, and `REAL` I/O variables. | BNC signal mapping. |
| Simple retained state | `ecm/sfc/simulation.fs`; parts of `run_time.fs` and `motion_state.fs` | Migrate after deciding the input/output names and scan ownership. | BNC signal mapping; a scan-period value when results represent elapsed time. |
| Timed sequence logic | `buzzer.fs`, `trigger.fs`, `system_on_off.fs`, and portions of `power_on_off.fs` | Migrate after a small timer and state-machine profile exists. | Timer semantics and trace-based parity tests. |
| Process and equipment charts | `cr6plc/process.fs`, `aqua_machine.fs`, `power_on_off.fs` | Decompose into smaller charts. Do not translate a complete chart as one unverified port. | Reusable state-machine components, timers, functions, and typed BNC command/acknowledgement signals. |
| Protocol and device services | `communication.fs`, `modbus.fs`, `munk.fs`, TPM scripts | Keep outside NanaST initially. | BNC or Rust service implementation. |
| Runtime and benchmarks | `motion/src/scripts/lib.fs`, Forth runtimes, benchmarks | Do not migrate. | None. |

Some source paths recur in several product repositories. A migration should
select one deployed product configuration and one behavior at a time instead of
porting every duplicate source file.

## Missing Capabilities

### BNC Runtime Capabilities

These capabilities belong in BNC, not in NanaST:

1. **Typed process-image mapping.** BNC must map stable logical signal names to
   NanaST `VAR_INPUT` and `VAR_OUTPUT` cells. It must define ownership,
   initialization, update order, and output application.
2. **Cyclic execution.** BNC must invoke `nana-init` once and `nana-scan` once
   per selected cycle. It must define overrun, restart, and watchdog behavior.
3. **Device commands and acknowledgements.** ST logic should set command
   outputs and consume status inputs. BNC must translate these values to
   EtherCAT, motion, UART, gateway, or other device operations.
4. **HMI request delivery.** BNC must convert queued HMI requests into typed
   input events or command values and expose acknowledgement outputs.
5. **Diagnostics and safety response.** BNC must record diagnostics and apply
   safety policy. NanaST programs should emit status and fault values instead
   of Forth text output such as `error|...`.

### NanaST Language and Runtime Profile Capabilities

The following additions require separate requirements, architecture, lifecycle,
and verification work before implementation.

1. **Timers and edges.** Timed legacy behavior uses `elapsed`, `timer-expired?`,
   `0timer`, `renew`, and scan counters. Define a small deterministic timer and
   edge-detection profile before migrating the full buzzer, trigger, or power
   sequences. The profile must define time source, resolution, reset behavior,
   startup state, overflow behavior, and behavior after an overrun.
2. **Reusable functions and function blocks.** Legacy modules define helpers,
   state machines, and multiple instances. NanaST needs a constrained,
   statically allocated function or function-block model before these can
   migrate without unsafe copy-and-paste expansion.
3. **State-machine conventions.** A first-class SFC syntax is not required for
   the first migration. A retained integer state plus conditionals can express
   one chart. Before translating a legacy chart, define transition order,
   entry-action behavior, concurrent-chart behavior, and output-conflict rules.
4. **Constants, enumerations, and `CASE`.** They are not required for semantic
   power, but they make mode and state logic reviewable. The legacy sources use
   named modes and nested `case` dispatch heavily.
5. **Bounded arrays and loops.** Sampling buffers, calibration tables, axis
   checks, and serial frames require indexed storage and bounded iteration.
   Any future design must use static bounds and define out-of-range behavior.
6. **Numeric and bit-operation profile.** The legacy code uses integer modulo,
   shifts, masks, unsigned protocol fields, `s>f`, `f>s`, `fabs`, `fmax`,
   `floor`, and exponentiation. Define types, overflow, conversion rounding,
   range failures, and non-finite `REAL` behavior before adding these features.
7. **Module composition.** Larger applications need a way to compose reviewed
   logical units without depending on Forth load order or dynamic word lookup.

## Explicit Non-Goals

This migration must not:

- add direct hardware or fieldbus calls to NanaST;
- claim real-time, safety, or device-equivalence evidence from compiler tests;
- port Forth interpreters, task scheduling, or CNC execution to NanaST;
- add broad IEC 61131-3 compatibility without a selected behavior and tests;
- replace an entire legacy product in one change.

## Migration Method

Each selected behavior must use this sequence:

1. **Select one behavior.** Identify its legacy source, product configuration,
   inputs, outputs, retained state, timing rules, and device command effects.
2. **Define the BNC boundary.** Name every input, output, command,
   acknowledgement, and diagnostic. Keep the mapping in BNC.
3. **Record traces.** Capture representative normal, boundary, startup,
   restart, and fault input/output traces from the legacy system or a verified
   legacy test harness.
4. **Write an ST fixture and tests.** Test the behavior through the NanaST to
   rfopt boundary. Add stateful multi-scan tests where required.
5. **Compare behavior.** Run the same trace through the legacy and new paths.
   Compare observable outputs, commands, acknowledgements, and state changes.
6. **Measure the target.** Measure cycle time, allocation behavior, and failure
   handling on the intended BNC hardware before making operational claims.

## First Proposed Work Items

1. Define the BNC logical process-image contract for one small water-system
   controller.
2. Translate `Ewater/sfc/chiller.fs` into an ST fixture with temperature inputs
   and chiller, pump, and valve outputs.
3. Translate `Ewater/sfc/dosing.fs` into an ST fixture with mode, pH,
   conductivity, limits, and dosing outputs.
4. Add trace-based parity tests for both fixtures.
5. Use the resulting evidence to decide whether the next feature is the timer
   profile or a constrained state-machine/function-block profile.

## Decision Gates

- The responsible BNC authority must approve the process-image contract before
  a physical I/O migration starts.
- The requester must approve each new NanaST language or runtime profile before
  implementation begins.
- A behavior may replace its legacy counterpart only after parity tests and
  target-runtime evidence cover its defined operational and fault scenarios.
