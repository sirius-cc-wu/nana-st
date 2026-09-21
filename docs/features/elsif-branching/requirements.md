---
type: "Feature Requirements"
title: "Requirements: ELSIF Multi-Branch Conditional Syntax"
description: "Requirements and Example Mapping specification for ELSIF conditional branching in NanaST."
status: "approved"
tags: [requirements, nanast, rfopt, syntax, conditionals, elsif, control-flow]
---

# Requirements: ELSIF Multi-Branch Conditional Syntax

## Status

Approved for specification and design.

- NanaST introduces the `ELSIF` keyword to support ordered, multi-branch conditional ladders within a single `IF ... END_IF` block.
- Each `ELSIF` clause pairs a boolean condition expression with a statement block.
- Branch evaluation is strictly short-circuiting: the first branch whose condition evaluates to `TRUE` executes its statements, and subsequent branch conditions and the optional `ELSE` block are skipped.
- Pure Forth-2012 lowering maps `ELSIF` to nested structured branch words (`IF ... ELSE ... THEN`) without requiring any new words or runtime additions in `rfopt`.

This feature extends the approved [NanaST vision](../../VISION.md) and supports the incremental migration of legacy BNC control logic (e.g., hysteresis in `chiller.fs` and multi-stage threshold control in `dosing.fs`).

## Objective

In industrial automation and BNC cyclic control programs, decision logic frequently involves multi-state hysteresis, sensor threshold classification, and step transition sequences with three or more mutually exclusive states. Currently, NanaST only supports `IF <cond> THEN <stmts> ELSE <stmts> END_IF;`. Expressing multi-way branches requires artificial nesting of `IF` blocks inside `ELSE` clauses, causing deep indentation ("pyramid of doom") and syntax errors. `ELSIF` enables clean, readable, flat multi-branch decision tables while preserving deterministic execution and stack safety.

## Core Design Principles

- **Flat multi-branch syntax:** Structured Text programs can specify zero or more `ELSIF` clauses between the initial `IF ... THEN` block and the optional `ELSE` block, terminated by a single `END_IF`.
- **Deterministic short-circuit evaluation:** Branches are evaluated strictly in source order. Once a branch condition succeeds, its body executes and control immediately transfers past `END_IF`. Subsequent conditions are not evaluated.
- **Type safety:** Every `ELSIF` condition must evaluate to `DataType::Bool`. Non-boolean condition expressions are rejected with source-located diagnostics during semantic analysis.
- **Pure Forth lowering:** Code generation maps `ELSIF` onto standard Forth-2012 structured conditionals (`IF ... ELSE ... THEN`). No runtime additions, temporary variables, or OS calls are introduced.
- **Balanced control stack:** The code generator tracks the number of open branch frames and emits exactly the required number of matching `THEN` words, preventing Forth compile-time stack imbalance.

## Ubiquitous Language

- **Conditional Statement (`IF`):** A compound control-flow construct that executes one of multiple alternative statement blocks based on boolean conditions.
- **Primary Branch (`IF ... THEN`):** The initial condition and corresponding body of the conditional statement.
- **Secondary Branch (`ELSIF ... THEN`):** An intermediate condition and body evaluated only when all preceding branches evaluated to `FALSE`.
- **Fallback Branch (`ELSE`):** An optional block executed only when the primary branch and all secondary branches evaluated to `FALSE`.
- **Termination Marker (`END_IF`):** The delimiter marking the conclusion of the conditional construct.
- **Branch Frame Count:** The number of nested Forth branch structures compiled during code generation that require corresponding `THEN` resolution words.

## System Behavioral Contract (Larman SSD & Operation Contracts)

### Contract: `compile_if_statement`

- **Preconditions:**
  - Token stream is positioned at the `IF` keyword.
  - Enclosing program or statement list is syntactically valid up to this point.
- **Inputs:**
  - `condition : Expression` (following `IF`, before `THEN`)
  - `then_body : Vec<Statement>`
  - `elsif_branches : Vec<ElsifBranch>` where each branch has `(condition : Expression, body : Vec<Statement>)`
  - `else_body : Option<Vec<Statement>>`
- **Postconditions:**
  - Parser constructs an AST node `StatementKind::If` containing `then_body`, `elsif_branches`, and `else_body`.
  - Semantic analysis confirms `condition.type == Bool` and `branch.condition.type == Bool` for all branches.
  - Forth code generator emits valid structured Forth words with balanced data and control stacks.

## Business Rules and Example Mapping

### Rule Matrix

| Rule # | Rule Name | Specification |
|:---|:---|:---|
| **R1** | **Primary Branch Execution** | When the initial `IF` condition evaluates to `TRUE`, `then_body` executes; no `ELSIF` conditions are evaluated, and `else_body` does not execute. |
| **R2** | **First Matching ELSIF Execution** | When the primary condition is `FALSE` and an `ELSIF` condition is `TRUE`, that `ELSIF` body executes; all subsequent `ELSIF` conditions and `else_body` are skipped. |
| **R3** | **Multiple ELSIF Short-Circuiting** | In a sequence of multiple `ELSIF` branches, evaluation halts at the first matching branch. Side-effects or expression evaluations in subsequent branches do not occur. |
| **R4** | **Fallback ELSE Execution** | When the primary condition and all `ELSIF` conditions evaluate to `FALSE`, `else_body` executes if present. |
| **R5** | **No Branch Matches Without ELSE** | When the primary condition and all `ELSIF` conditions evaluate to `FALSE` and `ELSE` is omitted, no statement in the conditional block executes, and program state remains unmodified. |
| **R6** | **Boolean Condition Invariant** | Semantic analysis rejects any `ELSIF` condition whose expression does not evaluate to `DataType::Bool`. |
| **R7** | **Empty Branch Body Tolerance** | Empty statement lists inside an `ELSIF` body or `ELSE` body are valid and produce well-formed, no-op Forth branch paths. |
| **R8** | **Bounded Nesting Support** | `IF ... ELSIF ... END_IF` statements can be nested inside `then_body`, `elsif_branches`, or `else_body`. Semantic analysis tracks cumulative control-flow nesting depth across enclosing blocks and branches, rejecting any path that exceeds the target runtime limit of 64 open frames with a compile-time diagnostic. |

### Concrete Examples

#### Example 1: Three-Band Temperature Hysteresis (Chiller Control)
```pascal
IF water_temp > high_limit THEN
    valve_open := TRUE;
    pump_run := TRUE;
ELSIF water_temp < low_limit THEN
    valve_open := FALSE;
    pump_run := FALSE;
ELSE
    (* Deadband: outputs remain at current latch values *)
END_IF;
```
- When `water_temp = 28.0` (above `high_limit = 26.0`): `valve_open` and `pump_run` become `TRUE`. `ELSIF` condition `water_temp < low_limit` is skipped.
- When `water_temp = 22.0` (below `low_limit = 24.0`): `IF` condition is `FALSE`, `ELSIF` condition is `TRUE`; `valve_open` and `pump_run` become `FALSE`.
- When `water_temp = 25.0` (between limits): both conditions are `FALSE`, `ELSE` block executes (no-op), preserving latched state.

#### Example 2: Multi-Stage Priority Gating
```pascal
IF emergency_stop THEN
    drive_enable := FALSE;
    fault_code := 1;
ELSIF interlock_open THEN
    drive_enable := FALSE;
    fault_code := 2;
ELSIF manual_mode THEN
    drive_enable := manual_switch;
    fault_code := 0;
ELSE
    drive_enable := auto_cmd;
    fault_code := 0;
END_IF;
```
- Verifies chaining of 3 `ELSIF` branches with fallback `ELSE`.

#### Example 3: Invalid Condition Type Rejection (Negative Case)
```pascal
IF enable_flag THEN
    count := count + 1;
ELSIF preset_val + 10 THEN
    count := 0;
END_IF;
```
- Compile-time error: `mismatched types: expected 'BOOL', found 'DINT'` pointing to `preset_val + 10`.

## Acceptance Criteria

1. **Lexer & Parser:**
   - Recognizes case-insensitive `ELSIF` token.
   - Parses zero or more `ELSIF <expr> THEN <stmts>` sections preceding an optional `ELSE`.
   - Parses a single terminal `END_IF` with optional trailing semicolon.
2. **Semantic Analysis:**
   - Validates each `ELSIF` condition as boolean.
   - Reports source span on condition type errors.
   - Scopes variable reads and writes identically across all branch bodies.
3. **Forth Code Generation:**
   - Lowers `N` branches into `N` structured `IF ... ELSE` constructs resolved by `N` matching `THEN` words.
   - Leaves data stack and control stack balanced upon completion.
4. **Target Parity (`rfopt`):**
   - Verified on pinned `rfopt` virtual machine under multi-scan execution.
