---
type: "Software Architecture Design"
title: "Architecture: STC NanaST Execution Boundary"
description: "A restricted-source adapter that owns cells and STC words while exposing opaque NanaST lifecycle handles."
status: "accepted"
tags: [architecture, nanast, rfopt, stc, amd64, aarch64]
---

# Architecture: STC NanaST Execution Boundary

## Architecture Question

How can NanaST retain its safe in-process lifecycle and cell boundary while
using rfopt's native STC backends on AMD64 and AArch64?

## Selected Structure

```text
NanaST integration test
  -> rfopt::nanast::Runtime
       -> restricted loader and stack validator
       -> STC lowering through NativeEmitter
       -> NativeWord executable allocation
       -> opaque WordHandle and CellHandle
```

`Runtime` owns the loaded definitions, stable cell storage, and executable
words. The private loader parses and validates the restricted generated source
before `Runtime` publishes it. The private STC lowerer emits direct primitive
instructions through the architecture-selected `NativeEmitter`.

`WordHandle` upgrades a weak link to the runtime, clones the selected word's
strong executable owner, releases the runtime borrow, then runs that word with
private data and return stacks. `CellHandle` upgrades the same weak link and
accesses only its named stable cell. Neither handle exposes an address or an
entry pointer.

## Lifecycle and Failure Boundaries

- Loading creates cells and executable words in local values. Publication occurs
  only after every definition validates and emits successfully.
- A native word owns its executable allocation. A cloned strong owner keeps the
  allocation live until invocation returns.
- A runtime drop invalidates all weak word and cell handles without dereference.
- A definition that returns with a changed data-stack pointer fails as an
  execution error. Validated NanaST lifecycle definitions restore the pointer.

## Platform Selection

`NativeEmitter` selects `X64Emitter` on AMD64 Linux and `A64Emitter` on
AArch64 Linux. The restricted loader and public handle lifecycle contain no
architecture-specific code. AArch64 instruction-cache maintenance remains the
responsibility of `A64Emitter::build_executable`.

## Verification

- rfopt unit tests validate the boundary's source, handle, ownership, and W^X
  behavior.
- `tests/rfopt_target.rs` retains the compiler-to-runtime vertical scenarios.
- The integration script runs on both supported Linux architectures.
- AArch64 execution evidence verifies emitted instructions under an AArch64
  runtime; cross-compilation alone does not prove this boundary.

## Non-Goals

This adapter is not the real-time runner, a general Forth API, a CLI, or an
inter-process shared-memory protocol.
