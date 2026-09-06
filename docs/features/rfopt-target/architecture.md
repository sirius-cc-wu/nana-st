---
type: "Software Architecture Design"
title: "Architecture: rfopt AMD64 Pass-Through Gate"
description: "In-process rfopt host boundary for the first NanaST generated-artifact test."
status: "accepted"
tags: [architecture, design, nanast, rfopt, amd64, forth]
---

# Architecture: rfopt AMD64 Pass-Through Gate

## Architecture Question

How does the first AMD64 NanaST target test load generated Forth source, invoke
its public lifecycle words, and inspect I/O cells without adding Forth words,
requiring a CLI, or coupling either project to BNC?

Sirius Wu approved an rfopt-owned in-process host API on 2026-09-06. The
[requirements](requirements.md) own the target behavior and word inventory.

## Significant Drivers

- **Approved behavior:** rfopt loads the generated Boolean pass-through source,
  executes `nana-init` and `nana-scan`, and lets the host prove canonical
  `0`/`1` output behavior.
- **Platform:** The first execution target is the AMD64 Linux development host.
  AArch64 work waits for the AMD64 gate.
- **Forth boundary:** Generated source requires only `:`, `;`, `VARIABLE`,
  `@`, `!`, `IF`, `ELSE`, and `THEN`; host operations must not become implicit
  Forth dependencies.
- **Failure behavior:** A load, lookup, cell-access, or invocation failure must
  reach the test as a named runtime failure. It must not be mistaken for target
  support.
- **Non-goals:** No CLI, terminal or file I/O, BNC hardware, shared-memory I/O,
  real-time claim, or AArch64 execution is part of this gate.

## Context and Boundaries

```text
NanaST gate runner (AMD64 Linux)
  | verifies clean rfopt gitlink, then starts integration test
  v
NanaST integration test
  | compiles pass_through.st to Forth source
  v
rfopt in-process host API
  | loads source; resolves and invokes public words;
  | owns dictionary, execution, and opaque cell handles
  v
rfopt runtime
```

NanaST owns the compiler, generated source, and pinned cross-repository gate.
rfopt owns source loading, word execution, cell storage, and its runtime-unit
evidence. The integration test is the only consumer of this host API in the
first feature. BNC is outside this interaction.

## Selected Architecture

rfopt provides an in-process API with these conceptual operations:

| Operation | Owner | Contract |
|---|---|---|
| Load source | rfopt | Accept generated Forth source from memory and either create its definitions or return a load error. |
| Resolve public word | rfopt | Find a named generated word and return an opaque executable handle or a named lookup error. |
| Invoke word | rfopt | Execute an opaque word handle and either complete with the specified stack effect or return an execution error. |
| Obtain cell handle | rfopt | Resolve a `VARIABLE` definition internally and return an opaque host-cell handle. The host never receives its Forth address. |
| Read or write cell | rfopt | Read or write a cell through its handle; this lets the test inject `-1` without requiring Forth source to parse it. |

Exact Rust type names, ownership types, and error enums are detailed design.
The API must keep dictionary pointers and raw runtime memory private from
NanaST. Handles become invalid when their owning runtime instance is dropped;
rfopt enforces that lifecycle.

The target implementation must add a NanaST-owned
`scripts/run-rfopt-target.sh` gate runner. It verifies that the rfopt submodule
is clean and that its `HEAD` equals the superproject gitlink before Cargo
compiles the path development dependency; direct `cargo test` is not
target-gate evidence. Only then does the runner start the NanaST AMD64
integration test, which links the pinned `rfopt` API. Its sequence is:

1. Compile the pass-through fixture to Forth source in memory.
2. Create one rfopt runtime instance and load that source.
3. Resolve `nana-input-0`, `nana-output-0`, `nana-init`, and `nana-scan`.
4. Obtain the two opaque cell handles, invoke initialization, write test inputs,
   invoke scans, and read the output handle.
5. Assert the acceptance cases in `requirements.md`; fail with the returned
   compiler or runtime error context.

## Rejected Candidates

| Candidate | Rejection |
|---|---|
| Interactive or batch CLI | Adds process, text I/O, and exit-code behavior before the runtime can execute the minimal artifact. |
| Generated Forth host-access words | Expands NanaST's Forth inventory and makes a test harness an implicit target dependency. |
| BNC shared-memory or device host | Couples the first compatibility gate to later BNC integration and real-time work. |
| Raw-address access from NanaST | Leaks rfopt memory layout and cannot enforce runtime-instance lifetime. |

## Verification

Required future evidence is:

- rfopt runtime-unit tests that prove loading, lookup, invocation, and opaque
  cell-handle lifetime on AMD64 Linux.
- `scripts/run-rfopt-target.sh`, which proves the rfopt submodule is clean and
  matches the superproject gitlink before it starts Cargo.
- A NanaST integration test that executes the complete pass-through sequence on
  that pinned rfopt revision and proves every requirement acceptance item.
- Error-path tests that prove missing words, invalid handles, and load or
  lookup failures fail the gate with context.

No such evidence exists yet because the pinned rfopt revision does not build on
AMD64. When it exists, it establishes compatibility only; it does not establish
timing, memory, safety, BNC integration, or AArch64 behavior.

## Detailed-Design Handoffs

- **rfopt Rust API:** Specify opaque handle ownership, error types, runtime
  creation, source parsing, and invalid-handle behavior.
- **NanaST test integration:** Add a path development dependency on the pinned
  rfopt submodule, `scripts/run-rfopt-target.sh` to validate its git state
  before Cargo, and the AMD64 end-to-end test after rfopt exposes the API.
- **rfopt AMD64 implementation:** Repair the existing architecture-specific
  build boundary before either test can run.
