---
type: "Feature Requirements"
title: "Requirements: Discrete Ordinal CASE Statement"
description: "Requirements and Example Mapping specification for discrete ordinal CASE selection statements in NanaST."
status: "approved"
tags: [requirements, nanast, rfopt, control-flow, case-statement, state-machine, sfc]
---

# Requirements: Discrete Ordinal CASE Statement

## Status

Approved for specification and design.

- NanaST introduces the `CASE ... OF ... ELSE ... END_CASE` multi-way selection statement for discrete ordinal types (`INT` and `DINT`).
- Enables 1:1 migration of legacy Forth SFC integer state machines (such as `cr6plc/process.fs`, `aqua_machine.fs`, and `power_on_off.fs`) without requiring a complex graphical SFC compiler or deep conditional nesting.
- Branches match one or more comma-delimited discrete ordinal values (`val1, val2: <stmts>`).
- Selector expression is evaluated once; matching branches execute their statements and exit immediately without evaluating subsequent branches.
- Lowers into standard, pinned Forth-2012 words (`DUP`, `=`, `IF`, `DROP`, `ELSE`, `THEN`, `OVER`, `OR`), requiring zero new words in `rfopt`.
- Guarantees strict data stack balance ($0$ net stack delta) and control stack safety.

This feature extends the approved [NanaST vision](../../VISION.md) and fulfills Capability Gap 3 & 4 of the [legacy Forth migration proposal](../../proposals/nanast-legacy-forth-migration.md).

## Objective

Industrial machine control logic across the 101 captured legacy Forth programs is predominantly organized into integer-state sequence charts (e.g. 0: init, 1: standby, 2: run, 3: pause, 4: alarm). Currently, NanaST lacks multi-way selection syntax, forcing developers to translate 9-step state charts into 9-level deeply nested `IF ... ELSE IF ... END_IF` ladders. These nested ladders are difficult to audit, prone to scope errors, and re-fetch state variables from memory on every branch. The `CASE` statement provides standard, clean, flat state machine dispatch with single-evaluation efficiency and complete stack determinism.

## Core Design Principles

- **Discrete ordinal selection:** Selectors are strictly restricted to integer types (`INT` and `DINT`). Selectors of type `REAL` or `BOOL` are rejected at compile time.
- **Single evaluation:** The selector expression is evaluated exactly once onto the data stack upon entry.
- **Discrete literal match lists:** Each case branch specifies one or more comma-separated ordinal literal values (`val1, val2: <stmts>`). Duplicate match values within a `CASE` statement are rejected at compile time.
- **Exhaustive fallback:** An optional `ELSE` branch executes when no case match occurs. If `ELSE` is omitted, the selector is cleanly dropped and state remains unchanged.
- **Stack-neutral Forth lowering:** Lowering uses `DUP` and `DROP` to ensure all execution paths leave identical data stack shapes, satisfying `rfopt`'s static stack verifier.
- **Nesting limit guardrail:** Semantic analysis tracks cumulative control-flow nesting depth across enclosing blocks, case branches, and nested bodies to prevent exceeding `rfopt`'s `MAX_CONTROL_NESTING = 64`.

## Ubiquitous Language

- **Selector Expression:** The expression whose integer value determines which case branch executes.
- **Case Branch:** An arm of the `CASE` statement composed of one or more match values, a colon delimiter, and an associated statement list.
- **Match Value:** A compile-time constant integer literal against which the selector is compared.
- **Fallback Branch (`ELSE`):** The optional branch executed when the selector matches none of the declared case values.
- **Control Nesting Depth:** The depth of nested structured conditional frames compiled into Forth control structures.

## System Behavioral Contract (Larman SSD & Operation Contracts)

### Contract: `compile_case_statement`

- **Preconditions:**
  - Token stream is positioned at the `CASE` keyword.
- **Inputs:**
  - `selector : Expression` (must evaluate to `INT` or `DINT`)
  - `branches : Vec<CaseBranch>` where each branch contains:
    - `match_values : Vec<Expression>` (evaluating to compile-time integer literals)
    - `body : Vec<Statement>`
  - `else_body : Option<Vec<Statement>>`
- **Postconditions:**
  - Parser constructs an AST node `StatementKind::Case`.
  - Semantic analysis confirms selector type is ordinal, verifies all match values are unique and type-compatible, and enforces `branches.len() <= 60`.
  - Forth code generator emits stack-balanced Forth words (`DUP`, `=`, `IF`, `DROP`, `ELSE`, `THEN`).

## Business Rules and Example Mapping

### Rule Matrix

| Rule # | Rule Name | Specification |
|:---|:---|:---|
| **R1** | **Exact Branch Matching** | When the selector value matches a declared branch value, that branch's body executes; subsequent branches and `ELSE` are skipped. |
| **R2** | **Multi-Value Branch Matching** | In a branch with multiple comma-separated values (`val1, val2:`), the branch executes if the selector matches any value in the list. |
| **R3** | **Fallback ELSE Execution** | When the selector matches none of the declared branch values, `else_body` executes if present. |
| **R4** | **Omitted ELSE Safety** | When the selector matches none of the declared branch values and `ELSE` is omitted, the selector is discarded and no statement executes. |
| **R5** | **Ordinal Selector Restriction** | The selector must evaluate to `DataType::Int` or `DataType::Dint`. Selectors of type `REAL` or `BOOL` are rejected with compile-time type errors. |
| **R6** | **Duplicate Value Prohibition** | Specifying the same match value across multiple branches or within the same branch list is rejected with diagnostic: `duplicate case match value '<val>'`. |
| **R7** | **Cumulative Nesting Limit Enforcement** | Semantic analysis tracks cumulative control-flow nesting depth through enclosing blocks, case branches, and nested branch bodies. The compiler rejects any program where peak cumulative nesting depth exceeds the target limit of 64 open frames (with diagnostic: `control-flow nesting exceeds limit of 64`). |
| **R8** | **Single Evaluation Invariant** | The selector expression is evaluated once upon entering the `CASE` block. It is not re-evaluated during subsequent branch comparisons. |

### Concrete Examples

#### Example 1: 3-State Machine Dispatch (Aqua Machine Sequence)
```pascal
PROGRAM AquaStepControl
VAR
    step_state : INT;
    pump_cmd : BOOL;
    valve_cmd : BOOL;
    alarm_flag : BOOL;
END_VAR

CASE step_state OF
    0:
        pump_cmd := FALSE;
        valve_cmd := FALSE;
    1, 2:
        pump_cmd := TRUE;
        valve_cmd := FALSE;
    3:
        pump_cmd := TRUE;
        valve_cmd := TRUE;
    ELSE
        pump_cmd := FALSE;
        valve_cmd := FALSE;
        alarm_flag := TRUE;
END_CASE;
END_PROGRAM
```
- Valid program. Dispatches sequence states cleanly.

#### Example 2: Non-Ordinal Selector Rejection (Negative Case)
```pascal
VAR_INPUT
    pressure : REAL;
END_VAR

CASE pressure OF (* REJECTED: CASE selector must be an INT or DINT, found REAL *)
    1.0: valve_cmd := TRUE;
END_CASE;
```
- Compile error: `expected INT or DINT for CASE selector, found REAL`.

#### Example 3: Duplicate Match Value Rejection (Negative Case)
```pascal
CASE step_state OF
    1: cmd := 10;
    2, 1: cmd := 20; (* REJECTED: duplicate case match value '1' *)
END_CASE;
```
- Compile error: `duplicate case match value '1'`.

## Acceptance Criteria

1. **Parser & Lexer:**
   - Recognizes `CASE`, `OF`, and `END_CASE` keywords.
   - Parses `CASE <expr> OF <val [, val...]>: <stmts> ... [ELSE <stmts>] END_CASE;`.
2. **Semantic Analysis:**
   - Validates ordinal selector type (`INT` or `DINT`).
   - Detects and reports duplicate branch match values.
   - Enforces the cumulative $\le 64$ control frame ceiling.
3. **Forth Code Generation:**
   - Emits single selector evaluation.
   - Lowers branches with `DUP` and `DROP` ensuring zero net stack delta on all paths.
   - Emits matching `THEN` words for all open branch frames.
4. **Target Parity (`rfopt`):**
   - Verifies execution against pinned `rfopt` across all branch paths and fallback cases with complete data and float stack balance.
