---
type: "Vision"
title: "Vision: NanaST"
description: "NanaST gives BNC/control developers a Structured Text path targeting BNC's existing rtForth environment."
status: "approved"
tags: [vision, governance, nanast, rtforth]
---

# Vision: NanaST

NanaST exists so BNC/control developers can incrementally introduce testable
Structured Text (ST) programs through a translation that a future approved
target feature specification must explicitly define for BNC's existing rtForth
environment.

It owns defining its ST subset, source-aware diagnostics, the future
ST-to-rtForth target contract, and compiler-correctness evidence. It does not
own the rtForth runtime implementation, BNC device integration, or BNC control
policy.

## Principles

- **ST is the program language:** Specify a small, explicit ST subset for real
  BNC control work. Reject unsupported forms with source-located diagnostics.
- **rtForth is the execution target:** A target feature specification must
  define how a specified ST program translates to an artifact that BNC's
  existing rtForth environment accepts. Keep that target contract explicit and
  testable.
- **Migration is incremental:** Keep existing rtForth programs working while
  ST-generated target artifacts gain equivalent, verified control cases. Do not
  require an all-at-once migration.
- **Real-time claims require target evidence:** Do not infer a timing, memory,
  safety, or motion claim merely because BNC already uses rtForth. A target
  feature specification and recorded evidence must establish any such claim for
  a generated control case.
- **Translation failures are diagnosable:** Preserve source-aware translation
  diagnostics. BNC and rtForth own target-execution failure reporting, including
  DTC persistence, publication, and safety behavior.
- **BNC owns BNC integration:** Keep EtherCAT mapping, DTC persistence and
  publication, DoIP/UDS, DID design, `vcmd` meaning, scheduling integration,
  and control safety policy in BNC. Keep NanaST's rtForth target boundary narrow
  and device-neutral.
- **Scope grows from demonstrated need:** Add language features or rtForth
  target capabilities only when a named BNC control case and verification
  evidence justify them.

## Non-Goals

- Full IEC 61131-3 or MATIEC compatibility.
- An IDE, online debugging, or a general PLC engineering environment.
- BNC EtherCAT, DTC persistence or publication, DoIP/UDS, DID, `vcmd`,
  scheduling, or control-safety integration.
- A Wasm output contract, a Wasmtime runner, Mecrisp, or a direct native-code
  target.
- Forced removal or wholesale replacement of existing rtForth programs.
- A production-safety or real-time-motion claim without target evidence.

## Acceptance Policy

- A change aligns when it gives BNC/control developers a clearer, tested path
  from an ST subset specified for a named BNC control case to an approved
  rtForth target contract.
- A change aligns when it preserves the semantics and diagnostics specified for
  that target feature and supports incremental coexistence with existing
  rtForth programs.
- A change aligns when it adds the smallest ST or target capability needed by a
  named BNC control case and includes verification evidence.
- A change should be resisted when it couples NanaST to EtherCAT devices, BNC
  DTC persistence, DIDs, DoIP/UDS, scheduling, or BNC control policy.
- A change should be resisted when it adds Wasm, Wasmtime, Mecrisp, a direct
  native-code backend, broad IEC syntax, or an IDE without a later approved
  vision revision.

## Boundary Cases

- **A contribution proposes a Wasm, Mecrisp, or native-code backend:** Resist
  it. NanaST's approved target is BNC's existing rtForth environment.
- **A BNC contribution requests an EtherCAT-specific target operation:** Accept
  a narrow, device-neutral target operation only when BNC already owns its
  mapping. Resist the EtherCAT or DID mapping itself.
- **A target execution fails:** BNC and rtForth own its reporting, DTC
  persistence, publication, and safety behavior. Resist adding a NanaST runtime
  report; accept only source-aware NanaST translation diagnostics.
- **A contributor requests broad IEC syntax without a control case:** Accept a
  narrowly specified feature when a named BNC program and tests need it.
  Resist generic completeness work.
- **A contribution replaces all BNC Forth programs at once:** Accept an
  independently verified migration slice. Resist a cutover that removes the
  coexistence path before equivalent ST behavior is proven.

## Authority and Evidence

- Authority and status: Sirius Wu approved this revision on 2026-09-06. It
  selects rtForth over Wasm and Mecrisp, superseding the Wasm compiler target
  established by the v0.1 baseline at `a63b5ca` and the Wasmtime runner
  direction approved at commit `1501430`.
- Historical implementation evidence: [`docs/SPEC-v0.1.md`](SPEC-v0.1.md),
  [`tasks/plan.md`](../tasks/plan.md), and [`tasks/todo.md`](../tasks/todo.md)
  record the completed Wasm vertical slice at revision `a63b5ca`; they are not
  the current NanaST target specification.
- Historical runner evidence: [`docs/ideas/nanast-wasm-realtime-runner.md`](ideas/nanast-wasm-realtime-runner.md)
  and its feature artifacts are superseded and retained for history.
- Evidence: Botnana Control revision `4ef5e97` uses `rtforth` in
  `motion/Cargo.toml` and documents a real-time Forth VM. This history supports
  the selected target; it does not establish a target contract or real-time
  claim for generated ST programs.
- Open questions: The emitted rtForth artifact format, supported rtForth
  version and extension boundary, BNC host contract, lifecycle, and target
  timing and memory acceptance evidence require a new approved feature
  specification.
