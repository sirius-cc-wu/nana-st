---
type: "Software Architecture Design"
title: "Architecture: IEEE-754 REAL Support"
description: "Typed NanaST-to-rfopt float flow over a separate Forth float stack."
status: "accepted"
tags: [architecture, nanast, rfopt, floating-point, ieee-754, stc]
---

# Architecture: IEEE-754 REAL Support

## Architecture Question

How can NanaST add binary64 `REAL` execution while preserving the existing
integer Forth data stack, opaque host capabilities, and native STC boundary?

## Selected Structure

```text
NanaST source: REAL
  -> lexer, parser, semantic analysis, and Forth emitter
  -> FVARIABLE / F@ / F! / float words and literals
  -> rfopt restricted loader and two-stack validator
  -> native STC emitter with a data-stack pointer and float-stack pointer
  -> opaque CellHandle or FloatCellHandle
```

- **NanaST owns** source spelling, `REAL` type rules, and the generated Forth
  profile.
- **rfopt loader owns** Forth-token recognition, typed stack validation, and
  the distinction between integer and floating-point addresses.
- **rfopt runtime owns** stable integer cells, stable binary64 cells, executable
  words, and opaque typed capabilities.
- **The STC emitter owns** direct scalar floating-point instructions. It does
  not call a numeric runtime or a formatting library for profile operations.

## Runtime Interaction

A generated lifecycle word starts with an empty data stack and float stack.
`F@` consumes a typed float address from the data stack and pushes one binary64
value on the float stack. Float arithmetic consumes and produces only float
stack values. A float comparison consumes float values and pushes an integer
Forth flag on the data stack, which an existing `IF` can consume. `F!` consumes
a typed address and one float-stack value.

The native invocation wrapper saves the caller's floating-point control state,
installs the profile state, invokes the STC word, verifies both stack pointers,
and restores the caller state. A generated word cannot retain a float pointer
or floating-point value after it returns.

## Platform Realization

- **AMD64 Linux:** The existing integer STC registers remain unchanged. A spare
  callee-saved register carries the float-stack pointer. The emitter uses scalar
  SSE2 binary64 instructions and scratch XMM registers.
- **AArch64 Linux:** The existing integer STC registers remain unchanged. A
  spare callee-saved general-purpose register carries the float-stack pointer.
  The emitter uses scalar `D` registers and AArch64 floating-point instructions.
- **AArch32:** Unsupported. The architecture adds no conditional path,
  assembler encoding, or verification obligation for it.

## Failure Boundaries

- The loader rejects an operation that mixes integer and float-stack values or
  addresses before native code is published.
- Native floating-point exceptions stay masked. IEEE non-finite results are
  stored and returned as values rather than converted into a runtime fault.
- A failed load drops local typed storage and native words without publishing a
  partial runtime.
- A changed data-stack or float-stack pointer is an execution failure.

## Verification

- NanaST unit tests verify source-to-profile generation and rejected type mixes.
- rfopt unit tests verify loader type checks, direct operations, non-finite
  comparison behavior, typed handles, stack restoration, and control-state
  restoration.
- The existing NanaST integration gate verifies one representative `REAL`
  scan on AMD64. [AArch64 verification](../../verification/floating-point/aarch64.md)
  records an AArch64 Linux userspace emulator run; native hardware evidence
  remains required for hardware-specific claims.
