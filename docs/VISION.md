---
type: "Vision"
title: "Vision: NanaST"
description: "NanaST gives BNC/control developers a Structured Text path intended to target an explicit Forth-2012 subset in rfopt."
status: "approved"
tags: [vision, governance, nanast, forth-2012, rfopt]
---

# Vision: NanaST

NanaST exists so BNC/control developers can incrementally introduce testable
Structured Text (ST) programs through a translation that an approved rfopt
target feature specification defines. The emitted artifact must use an
explicit, verified subset of Forth-2012 semantics and words; it does not require
rfopt to claim complete Forth-2012 conformance.

It owns defining its ST subset, source-aware diagnostics, the ST-to-Forth
subset contract, the exact required-word inventory for each target feature, and
compiler-correctness evidence. It pins the rfopt revision used for integration
verification. rfopt retains ownership of its runtime implementation and BNC
retains ownership of BNC integration and control policy.

## Principles

- **ST is the program language:** Specify a small, explicit ST subset for real
  BNC control work. Reject unsupported forms with source-located diagnostics.
- **The Forth subset is explicit:** Each target feature specification must
  enumerate every Forth-2012 word and semantic behavior its artifact requires.
  The artifact and host contract must be explicit and testable. A
  target-specific extension requires an explicit, separately verified boundary;
  it must not become an implicit compiler dependency.
- **NanaST declares; rfopt implements:** If the required-word inventory includes
  a word absent from rfopt, its implementation belongs in rfopt, committed in
  the rfopt repository. NanaST must not add a compatibility definition or
  fallback for that gap.
- **rfopt is the sole planned runtime:** NanaST develops and verifies its
  artifacts against the rfopt submodule's pinned revision. The first gate is
  loading and executing the generated Boolean pass-through artifact. rfopt
  becomes the sole target only if later evidence establishes that it satisfies
  BNC's needs.
- **Migration is incremental:** Keep existing BNC Forth programs working while
  ST-generated rfopt artifacts gain equivalent, verified control cases. Do not
  require an all-at-once migration.
- **Real-time claims require rfopt target evidence:** Do not infer a timing,
  memory, safety, or motion claim merely because one BNC deployment uses a
  particular Forth implementation. A target feature specification and recorded
  rfopt test evidence must establish any such claim for a generated control
  case.
- **Translation failures are diagnosable:** Preserve source-aware translation
  diagnostics. BNC and the selected Forth execution host own target-execution
  failure reporting, including DTC persistence, publication, and safety
  behavior.
- **BNC owns BNC integration:** Keep EtherCAT mapping, DTC persistence and
  publication, DoIP/UDS, DID design, `vcmd` meaning, scheduling integration,
  control safety policy, and rfopt migration in BNC. Keep NanaST's Forth
  subset boundary narrow and device-neutral.
- **Scope grows from demonstrated need:** Add language features or required
  Forth words only when a named BNC control case and verification evidence
  justify them.

## Non-Goals

- Full IEC 61131-3 or MATIEC compatibility.
- An IDE, online debugging, or a general PLC engineering environment.
- Full Forth-2012 conformance by rfopt; it need implement only NanaST's
  explicitly required subset.
- A NanaST compatibility definition for a Forth word that rfopt lacks.
- BNC EtherCAT, DTC persistence or publication, DoIP/UDS, DID, `vcmd`,
  scheduling, control-safety integration, or rfopt migration.
- A Wasm output contract, a Wasmtime runner, or a direct native-code target.
- A second compiler backend or an unverified Forth runtime target.
- Forced removal or wholesale replacement of existing BNC Forth programs.
- A production-safety or real-time-motion claim without rfopt target evidence.

## Acceptance Policy

- A change aligns when it gives BNC/control developers a clearer, tested path
  from an ST subset specified for a named BNC control case to the approved
  Forth subset contract.
- A target change aligns when it enumerates its required Forth-2012 words,
  preserves their required semantics, and verifies the resulting artifact on
  the pinned rfopt revision.
- A change aligns when it adds the smallest ST or target capability needed by a
  named BNC control case and includes verification evidence.
- A real-time claim aligns only when target tests run the generated artifact on
  rfopt and retain the required evidence.
- A change should be resisted when it couples NanaST to EtherCAT devices, BNC
  DTC persistence, DIDs, DoIP/UDS, scheduling, control policy, or the later
  BNC migration to rfopt.
- A change should be resisted when it adds Wasm, Wasmtime, a direct native-code
  backend, broad IEC syntax, a second Forth runtime target, or an IDE without a
  later approved vision revision.

## Boundary Cases

- **A contribution proposes a Wasm or native-code backend:** Resist it.
  NanaST's approved target compatibility boundary is the explicit Forth subset.
- **rfopt lacks a needed Forth-2012 word:** NanaST records the word in its
  target feature inventory and the implementation is developed and committed
  in rfopt. Resist adding a NanaST definition or fallback for that word.
- **rfopt is incomplete:** Accept incremental implementation only when the
  required-word inventory and generated artifact tests define the supported
  subset. Resist making full Forth-2012 conformance a prerequisite.
- **A generated artifact passes on rfopt but needs a real-time claim:** Treat
  the result as compatibility evidence only. Run and retain the specified
  rfopt real-time target evidence before accepting the claim.
- **A BNC contribution requests an EtherCAT-specific target operation:** Accept
  a narrow, device-neutral target operation only when BNC already owns its
  mapping. Resist the EtherCAT or DID mapping itself.
- **A contribution proposes BNC adoption of rfopt:** Route it to BNC. NanaST
  accepts only the device-neutral compiler target contract and its evidence.
- **A target execution fails:** BNC and the selected Forth execution host own
  its reporting, DTC persistence, publication, and safety behavior. Resist
  adding a NanaST runtime report; accept only source-aware NanaST translation
  diagnostics.
- **A contributor requests broad IEC syntax without a control case:** Accept a
  narrowly specified feature when a named BNC program and tests need it.
  Resist generic completeness work.
- **A contribution replaces all BNC Forth programs at once:** Accept an
  independently verified migration slice. Resist a cutover that removes the
  coexistence path before equivalent ST behavior is proven.

## Authority and Evidence

- Authority and status: Sirius Wu approved this revision on 2026-09-06. It
  selects rfopt as NanaST's sole planned runtime, subject to evidence that it
  satisfies BNC's needs; it requires rfopt for real-time target evidence; and
  it selects an explicit Forth-2012 subset rather than full runtime
  conformance. It supersedes the earlier SwiftForth-first and rtForth-specific
  target selections, while continuing to supersede the Wasm compiler target
  established by the v0.1 baseline at `a63b5ca` and the Wasmtime runner
  direction approved at commit `1501430`.
- Transition boundary: `src/wasm.rs`, the current CLI behavior, and the
  Wasmtime tests are retained historical v0.1 implementation evidence while the
  rfopt target is specified. They must not be extended or treated as rfopt
  compatibility evidence. An approved rfopt target specification must define
  their replacement or removal before implementation changes that behavior.
- Current runtime evidence: `rfopt` is a pinned submodule. Its vision and TODO
  record an intended native engine and an initial eForth implementation; they
  do not establish the required NanaST target contract or any real-time claim.
- Current BNC evidence: Botnana Control revision `4ef5e97` uses `rtforth` in
  `motion/Cargo.toml` and documents a real-time Forth VM. This is evidence of
  the existing BNC deployment, not approval to migrate BNC to rfopt.
- Historical implementation evidence: [`docs/SPEC-v0.1.md`](SPEC-v0.1.md),
  [`tasks/plan.md`](../tasks/plan.md), and [`tasks/todo.md`](../tasks/todo.md)
  record the completed Wasm vertical slice at revision `a63b5ca`; they are not
  the current NanaST target specification.
- Historical runner evidence: [`docs/ideas/nanast-wasm-realtime-runner.md`](ideas/nanast-wasm-realtime-runner.md)
  and its feature artifacts are superseded and retained for history.
- Open questions: The rfopt source-loading and host API, exact generated
  artifact format, approval of the Boolean pass-through inventory and
  lifecycle, and real-time timing and memory acceptance evidence require an
  approved rfopt target feature specification.
