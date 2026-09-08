---
type: "Feature Requirements"
title: "Requirements: IEEE-754 REAL Support"
description: "A small floating-point profile for NanaST-generated Forth and the rfopt NanaST runtime."
status: "approved"
tags: [requirements, nanast, rfopt, floating-point, ieee-754, amd64, aarch64]
---

# Requirements: IEEE-754 REAL Support

## Status

The requester approved this feature direction in the current working session:

- NanaST provides one floating-point type: `REAL`.
- `REAL` uses IEEE-754 binary64 (`f64`).
- The supported native targets are AMD64 Linux and AArch64 Linux. AArch32 is not a target.

This feature extends the approved NanaST vision and the draft
[Forth code-generation requirements](../forth-codegen/requirements.md).
It does not change the existing integer or Boolean contracts.

## Objective

A NanaST control program can declare `REAL` variables, evaluate binary64
floating-point expressions, compare their values, and access `REAL` I/O through
the safe `rfopt::nanast` boundary. NanaST generates a documented Forth float
profile. rfopt validates, compiles, and executes that profile as native STC on
AMD64 and AArch64 Linux.

## Platform and Numeric Contract

- `REAL` has the IEEE-754 binary64 format and occupies eight bytes.
- Arithmetic uses round to nearest with ties to even.
- The runtime must mask floating-point traps, use the stated rounding mode while
  executing a generated word, and restore the caller's floating-point control
  state before it returns.
- The runtime does not inspect or expose floating-point status flags.
- Decimal source literals must be finite. NanaST accepts a decimal point or an
  `E` exponent, such as `1.0`, `-0.5`, and `6.25E-3`. It rejects `NaN`,
  `Infinity`, and hexadecimal literals.
- Arithmetic may produce signed zero, subnormal values, infinities, or NaN.
  These values remain valid `REAL` values. Floating-point division by zero is
  not an `ExecutionError`.
- `NaN` compares unequal to every value, including itself. Ordered comparisons
  with `NaN` return `FALSE`.
- The first implementation does not fold non-literal `REAL` expressions at
  compile time. Native execution defines their result.

## NanaST Language Scope

NanaST adds `REAL` declarations and `REAL` literals.

A `REAL` expression supports:

- unary negation;
- `+`, `-`, `*`, and `/` when both operands are `REAL`;
- `=`, `<>`, `<`, `<=`, `>`, and `>=` when both operands are `REAL`.

A comparison produces `BOOL`. A `REAL` value cannot be used as an `IF`
condition. NanaST does not perform implicit conversion between `REAL`, `INT`,
and `DINT`.

Explicit integer-to-`REAL` and `REAL`-to-integer conversions are outside this
feature. A later feature must define their syntax, rounding, range, and
non-finite-value behavior before NanaST accepts them.

## Generated Forth Float Profile

rfopt supports a separate floating-point stack for this profile. It must hold
at least six values, as required by the Forth-2012 Floating-Point word set. The
loader statically validates the data-stack and float-stack effects of every
accepted definition. Each public lifecycle word must restore both stacks.

NanaST emits the following Forth words for `REAL` behavior:

| Word | Stack effect | Purpose |
|---|---|---|
| `FVARIABLE` | `( -- f-addr )` | Declares aligned binary64 storage. |
| `F@` | `( f-addr -- ) ( F: -- r )` | Fetches a `REAL`. |
| `F!` | `( f-addr -- ) ( F: r -- )` | Stores a `REAL`. |
| `F+` | `( F: r1 r2 -- r3 )` | Adds two `REAL` values. |
| `F-` | `( F: r1 r2 -- r3 )` | Subtracts two `REAL` values. |
| `F*` | `( F: r1 r2 -- r3 )` | Multiplies two `REAL` values. |
| `F/` | `( F: r1 r2 -- r3 )` | Divides two `REAL` values. |
| `FNEGATE` | `( F: r1 -- r2 )` | Negates a `REAL`. |
| `F0=` | `( F: r -- ) ( -- flag )` | Tests equality with zero. |
| `F0<` | `( F: r -- ) ( -- flag )` | Tests whether a value is less than zero. |
| `F<` | `( F: r1 r2 -- ) ( -- flag )` | Ordered less-than comparison. |
| `F=` | `( F: r1 r2 -- ) ( -- flag )` | Exact IEEE equality. rfopt target extension. |
| `F<>` | `( F: r1 r2 -- ) ( -- flag )` | IEEE inequality. rfopt target extension. |
| `F>` | `( F: r1 r2 -- ) ( -- flag )` | Ordered greater-than comparison. rfopt target extension. |
| `F<=` | `( F: r1 r2 -- ) ( -- flag )` | Ordered less-than-or-equal comparison. rfopt target extension. |
| `F>=` | `( F: r1 r2 -- ) ( -- flag )` | Ordered greater-than-or-equal comparison. rfopt target extension. |

`FVARIABLE`, `F@`, `F!`, arithmetic, and the three basic comparison words are
from the Forth-2012 Floating-Point word set. rfopt adds the named comparison
extensions because expressing Structured Text equality through subtraction
would give the wrong result for infinities and NaN.

The restricted rfopt loader accepts decimal float literals in colon definitions
and lowers them as float literals. NanaST does not need to emit a general Forth
outer-interpreter sequence or float formatting words.

## Safe Host Boundary

`rfopt::nanast` continues to keep raw Forth addresses private.

- `Runtime::resolve_float_cell` resolves an `FVARIABLE` to an opaque
  `FloatCellHandle`.
- `FloatCellHandle::read` and `FloatCellHandle::write` exchange `f64` values.
- `Runtime::resolve_cell` and `CellHandle` remain integer-cell APIs. They must
  reject an `FVARIABLE` name.
- `resolve_float_cell` must reject an integer `VARIABLE` name.
- A float handle becomes invalid under the same parent-runtime drop and unknown
  slot rules as the existing handles.

## Boundaries

### Always

- Keep `REAL` binary64 on both supported architectures.
- Keep floating-point storage, stack entries, and host handles typed.
- Preserve the existing `BOOL`, `INT`, and `DINT` source, Forth, and host API
  behavior.
- Run the NanaST unit tests, rfopt unit tests, and the pinned cross-repository
  gate for every completed implementation slice.

### Ask First

- Adding `LREAL`, binary32 storage, implicit conversions, transcendental words,
  float formatting, or a floating-point ABI for BNC devices.
- Changing the documented treatment of non-finite values, rounding, or the
  floating-point control state.
- Adding a non-Linux or non-AMD64/AArch64 target.
- Making real-time latency or numerical-performance claims.

### Never

- Store a `REAL` in an integer `CellHandle` or expose a Forth address.
- Use the data stack as the generated-code floating-point stack.
- Add AArch32 support.
- Treat successful AMD64 tests as AArch64 execution evidence.

## Acceptance Evidence

1. NanaST parses and type-checks valid `REAL` declarations and decimal literals.
   It rejects invalid literals, implicit mixed numeric expressions, and a
   `REAL` condition.
2. NanaST emits only the documented float profile for representative `REAL`
   declarations, arithmetic, comparisons, and conditionals.
3. rfopt rejects float-profile source with an unknown word, unbalanced float
   stack, a float operation on an integer address, or an integer operation on a
   float address.
4. rfopt unit tests verify binary64 arithmetic, signed zero, finite literal
   parsing, infinities, NaN comparison behavior, and restoration of the
   caller's floating-point control state.
5. The host boundary reads and writes `f64` through `FloatCellHandle` and
   rejects cross-kind handle resolution.
6. An end-to-end NanaST fixture passes a `REAL` input through arithmetic and a
   comparison branch on AMD64 Linux.
7. The same rfopt unit tests and NanaST gate run on AArch64 Linux before the
   feature claims AArch64 support. The recorded emulator evidence is in
   [AArch64 verification](../../verification/floating-point/aarch64.md).
8. The existing integer and Boolean test suites remain green.

## Capability Map

| Module | Responsibility | Depends on |
|---|---|---|
| `nanast-real` | Source syntax, type checking, and Forth emission for `REAL`. | Float-profile contract. |
| `rfopt-float-profile` | Typed loader, float storage, stack validation, and native STC operations. | Float-profile contract. |
| `rfopt-float-boundary` | Opaque binary64 host-cell capability and end-to-end verification. | `rfopt-float-profile`, `nanast-real` |

Build order: `rfopt-float-profile` and `nanast-real` → `rfopt-float-boundary`.
