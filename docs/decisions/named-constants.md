---
type: "Architecture Decision"
title: "Decision: Named Constants (VAR CONSTANT Profile)"
description: "Introduce immutable named constants in NanaST compiled to immediate Forth literals."
status: "accepted"
date: "2026-09-20"
tags: [decision, nanast, constants, immutability, var-constant, stc]
---

# Decision: Named Constants (VAR CONSTANT Profile)

## Status

Accepted.

## Context

Structured Text programs for Botnana Control (BNC) require fixed parameters, such as physical temperature thresholds in `chiller.fs`, pH limits in `dosing.fs`, filter durations, and state machine step constants.

Previously, NanaST supported only mutable variables (`VAR`), requiring developers to either scatter raw magic literals across logic blocks or allocate mutable cells in `VAR`. Magic numbers create severe maintenance hazards and risk inconsistent safety thresholds. Declaring parameters under mutable `VAR` wastes runtime RAM memory cells, incurs redundant store operations during `nana-init`, adds memory load latency during cyclic scans (`nana-scan`), and provides zero compile-time protection against accidental reassignment.

Furthermore, `rfopt` already natively compiles `<val> CONSTANT <name>` into Subroutine Threaded Code (STC) immediate literals (`Instruction::Literal`).

## Decision

Introduce native named constant declarations via `VAR CONSTANT` into NanaST:

1. **Syntax and AST Representation:**
   - Add `StorageClass::Constant` to the compiler AST.
   - Accept declarations within `VAR CONSTANT ... END_VAR` blocks (and `VAR_CONSTANT ... END_VAR`).
   - Require an explicit static initializer expression for every constant declaration.
2. **Semantic Immutability Invariant:**
   - Enforce that constant identifiers cannot be assigned to. Any assignment targeting a constant identifier is rejected during semantic analysis with diagnostic: `cannot assign to constant '<name>'`.
   - Restrict constant data types to scalar primitives (`BOOL`, `INT`, `DINT`, `REAL`). Function block instances are forbidden in constant blocks.
3. **Forth Code Generation & Zero Runtime Overhead:**
   - Omit constant declarations from `VARIABLE` and `FVARIABLE` RAM allocations.
   - Omit constant symbols entirely from `nana-init` reset routines.
   - Lower references to constants directly as immediate literals in expressions, avoiding memory fetch words (`@`, `F@`).
   - Emit `<val> CONSTANT nana-const-<name>` for integer and boolean constants to support top-level Forth dictionary inspection.

## Consequences

- Programmers can define clear, self-documenting domain parameters with compile-time immutability guarantees.
- Zero bytes of mutable RAM are consumed for constants.
- Zero initialization instructions are emitted in `nana-init`.
- Cyclic scan execution in `nana-scan` experiences lower memory bus pressure, using immediate machine instruction operands.
- Direct alignment with IEC 61131-3 standards and `rfopt`'s native STC backend.

## Links

- Requirements: [`docs/features/named-constants/requirements.md`](../features/named-constants/requirements.md)
- Architecture: [`docs/features/named-constants/architecture.md`](../features/named-constants/architecture.md)
- Rust Lifecycle: [`docs/features/named-constants/rust-lifecycle.md`](../features/named-constants/rust-lifecycle.md)
