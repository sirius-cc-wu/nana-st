---
type: "Architecture Decision"
title: "Decision: ELSIF Multi-Branch Conditional Syntax"
description: "Introduce native ELSIF multi-branch conditionals lowered to structured Forth IF-ELSE-THEN ladders."
status: "accepted"
date: "2026-09-20"
tags: [decision, nanast, conditionals, elsif, forth-2012, control-flow]
---

# Decision: ELSIF Multi-Branch Conditional Syntax

## Status

Accepted.

## Context

NanaST targets cyclic Structured Text programs for BNC machine control. Real-world automation logic—such as multi-band temperature hysteresis in `chiller.fs`, multi-stage dosing threshold control in `dosing.fs`, and step transition evaluation in SFCs—requires evaluating three or more mutually exclusive conditions.

Previously, NanaST supported only binary `IF <cond> THEN <stmts> ELSE <stmts> END_IF;`. Expressing multi-way decisions required nesting `IF` statements inside `ELSE` clauses, creating severe rightward drift, syntax errors, and awkward control logic.

Furthermore, discrete `CASE` statements operate only on ordinal integers and cannot evaluate floating-point (`REAL`) relational comparisons or compound boolean expressions.

## Decision

Introduce native `ELSIF` multi-branch conditional syntax into NanaST:

1. **Syntax & AST Structure:**
   - Extend the parser to accept zero or more `ELSIF <expr> THEN <stmts>` clauses between the primary `IF` branch and the optional `ELSE` fallback, terminated by a single `END_IF`.
   - Represent branches explicitly in `StatementKind::If` using an `elsif_branches: Vec<ElsifBranch>` structure, preserving precise source spans for diagnostics.
2. **Type Safety:**
   - Enforce that every `ELSIF` condition expression evaluates to `DataType::Bool`. Non-boolean conditions are rejected with source-located diagnostics during semantic analysis.
3. **Pure Forth Lowering:**
   - Lower $N$ `ELSIF` branches into nested structured Forth conditionals:
     `c1 IF b1 ELSE c2 IF b2 ... ELSE fallback THEN THEN ...`
   - Emits exactly as many `THEN` words as open `IF` frames were created ($1 + N$), guaranteeing complete Forth control-flow and data-stack balance without runtime helpers.
4. **Execution Semantics:**
   - Enforce strict short-circuit evaluation in source order. As soon as one condition evaluates to `TRUE`, subsequent conditions and the fallback `ELSE` are skipped.

## Consequences

- Structured Text authors can express flat, readable multi-way decision ladders without artificial nesting.
- Zero changes or additions are required in the pinned `rfopt` Forth runtime submodule.
- Forth code generation maintains strict compile-time and runtime stack balance invariants.
- Legacy BNC SFC and decision logic can be translated cleanly into standard Structured Text.

## Links

- Requirements: [`docs/features/elsif-branching/requirements.md`](../features/elsif-branching/requirements.md)
- Architecture: [`docs/features/elsif-branching/architecture.md`](../features/elsif-branching/architecture.md)
