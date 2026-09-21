---
type: "Architecture Decision"
title: "Decision: Discrete Ordinal CASE Statement"
description: "Introduce discrete ordinal CASE selection statements lowered to stack-neutral Forth branch ladders."
status: "accepted"
date: "2026-09-20"
tags: [decision, nanast, control-flow, case-statement, state-machine, sfc]
---

# Decision: Discrete Ordinal CASE Statement

## Status

Accepted.

## Context

Sequence logic and SFC state machines across the 101 captured legacy Forth control files (e.g. in `cr6plc/process.fs`, `aqua_machine.fs`, and `power_on_off.fs`) are structured as integer-state transition charts (`0: init`, `1: standby`, `2: run`, etc.).

Currently, NanaST lacks multi-way selection syntax, forcing developers to translate state charts into deeply nested `IF ... ELSE IF ... END_IF` ladders. These pyramids of conditionals obscure state chart logic, risk scoping mistakes, and re-fetch the state variable from memory on every branch evaluation.

## Decision

Introduce native discrete ordinal `CASE` selection statements into NanaST:

1. **Syntax & AST Structure:**
   - Add `CASE <selector> OF <branch...> [ELSE <stmts>] END_CASE;` syntax to the grammar.
   - Support matching single values or comma-separated lists of values (`val1, val2:`).
   - Represent in AST via `StatementKind::Case { selector, branches, else_body }`.
2. **Type Safety & Constraints:**
   - Restrict selector expressions strictly to discrete ordinal types (`INT` and `DINT`). Selectors of type `REAL` or `BOOL` are rejected at compile time.
   - Require match values to be integer literals.
   - Enforce uniqueness: duplicate match values across any branch are rejected at compile time.
   - Enforce a cumulative compile-time control depth ceiling ($\le 64$ open frames across enclosing blocks, case branches, and nested bodies) to comply with `rfopt`'s `MAX_CONTROL_NESTING = 64`.
3. **Forth Code Generation & Stack Invariants:**
   - Evaluate selector once onto the data stack.
   - Lower each branch to: `DUP <val> = IF DROP <branch_body> ELSE ...`
   - Lower multi-value branches to: `DUP <v1> = OVER <v2> = OR ... IF DROP <branch_body> ELSE ...`
   - Lower fallback to: `DROP <else_body>` (ensuring `DROP` is always executed even if `else_body` is empty).
   - Emit matching `THEN` words for all open frames.
   - Zero new Forth words or runtime modifications required in `rfopt`.

## Consequences

- SFC sequence logic can be migrated directly into Structured Text with a clean 1:1 structural representation.
- Developers avoid deep conditional indentation ("pyramid of doom").
- Single evaluation of the selector minimizes memory reads and register pressure during cyclic execution (`nana-scan`).
- Data stack balance ($0$ net stack delta) and control-flow nesting limits are strictly preserved.

## Links

- Requirements: [`docs/features/case-statement/requirements.md`](../features/case-statement/requirements.md)
- Architecture: [`docs/features/case-statement/architecture.md`](../features/case-statement/architecture.md)
