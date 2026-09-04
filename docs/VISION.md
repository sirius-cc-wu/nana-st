---
type: "Vision"
title: "Vision: NanaST"
description: "NanaST gives BNC/control developers a portable Structured Text path that can replace rtForth incrementally when target real-time evidence supports it."
status: "approved"
tags: [vision, governance, nanast]
---

# Vision: NanaST

NanaST exists so BNC/control developers can incrementally replace rtForth
programs with portable, testable Structured Text (ST) modules.

It owns a defined ST subset, the compiler and Wasm module contract, and a
host-neutral runner library with real-time feasibility evidence. It does not
own BNC device integration or control policy.

## Principles

- **ST is the program language:** Add a small, explicit ST subset for real BNC
  control work. Reject unsupported forms with source-located diagnostics. Do
  not translate ST to rtForth.
- **Wasm is the portable program contract:** Compile a program to a Wasm module
  so it can move between supported CPU architectures. Keep runner internals
  replaceable until target evidence selects them.
- **Real-time claims require target evidence:** Prepare modules outside a
  periodic path. Accept a runner for a loop only after it meets that loop's
  recorded timing gate on the target hardware.
- **Migration is incremental:** Keep existing rtForth programs working in BNC
  while ST programs gain equivalent, verified use cases. Do not require an
  all-at-once Forth migration.
- **Failures are diagnosable:** Report a Wasm execution failure as a diagnostic
  trouble code (DTC). Make the runner report testable without assigning BNC
  safety behavior to NanaST.
- **BNC owns BNC integration:** Keep EtherCAT mapping, DTC persistence and
  publication, DoIP/UDS, DID design, `vcmd` meaning, and control safety policy
  in BNC. Keep NanaST host interfaces narrow and device-neutral.
- **Scope grows from demonstrated need:** Add language features, runner APIs,
  and execution topology only when a named BNC control case and verification
  evidence justify them.

## Non-Goals

- Full IEC 61131-3 or MATIEC compatibility.
- An IDE, online debugging, or a general PLC engineering environment.
- BNC EtherCAT, DTC persistence or publication, DoIP/UDS, DID, or `vcmd`
  integration.
- A permanent commitment to Wasmtime before the ARM64 feasibility gate passes.
- A Forth backend for ST or forced removal of existing rtForth programs.
- A production-safety or real-time-motion claim without target evidence.

## Acceptance Policy

- A change aligns when it gives BNC/control developers a clearer, tested path
  from a defined ST program to a portable Wasm module or a host-neutral runner.
- A change aligns when it closes a recorded ARM64 real-time feasibility risk
  through reproducible target evidence.
- A change aligns when it adds the smallest ST or runner capability needed by a
  named BNC control case and preserves explicit diagnostics and tests.
- A change aligns when it reports Wasm execution failures as testable DTCs
  without coupling the runner to BNC DTC storage or publication.
- A change should be resisted when it couples NanaST to EtherCAT devices, BNC
  DTC storage, DIDs, DoIP/UDS, or BNC control policy.
- A change should be resisted when it expands toward full IEC support, an IDE,
  or a permanent engine selection without approved use-case and target
  evidence.
- A change should be resisted when it breaks rtForth coexistence or makes ST
  execute through rtForth.

## Boundary Cases

- **Wasmtime meets the PLC gate but not the motion gate:** Accept a PLC runner
  and retain the motion runner as deferred work. Resist a claim that NanaST is
  ready for motion control.
- **A BNC contribution requests an EtherCAT-specific `vcmd` import:** Accept a
  device-neutral scalar host boundary when it serves the runner contract.
  Resist the EtherCAT or DID mapping because BNC owns it.
- **A runner scan fails:** Accept a testable DTC report from NanaST. Resist BNC
  DTC storage, publication, or safety behavior in NanaST.
- **A contributor requests broad IEC syntax without a control case:** Accept a
  narrowly specified feature when a named BNC program and tests need it.
  Resist generic completeness work.
- **A contribution replaces all BNC Forth programs at once:** Accept an
  independently verified migration slice. Resist a cutover that removes the
  coexistence path before equivalent ST behavior is proven.

## Authority and Evidence

- Authority and status: Sirius Wu approved this vision.
- Approved source revision: NanaST revision `a63b5ca`, the completed v0.1
  baseline that this vision extends.
- Candidate direction: [`docs/ideas/nanast-wasm-realtime-runner.md`](ideas/nanast-wasm-realtime-runner.md)
  records the approved Wasm runner feasibility gate.
- Current feature requirements:
  [`docs/features/wasm-realtime-runner/requirements.md`](features/wasm-realtime-runner/requirements.md).
- Evidence: [`docs/SPEC-v0.1.md`](SPEC-v0.1.md) at revision `a63b5ca` defines
  the completed compiler vertical slice, its Wasm ABI, and its current scope
  limits.
- Evidence: Botnana Control revision `4ef5e97` uses `rtforth` in
  `motion/Cargo.toml` and documents a real-time Forth VM. This history supports
  the migration need. It is not approval of this vision.
- Open question: Select a permanent real-time execution engine only after the
  ARM64 feasibility result is available.
