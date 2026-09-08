---
type: "Feature Requirements"
title: "Requirements: Generalized Forth Code Generation"
description: "Requirements for compiling the supported Structured Text subset into Forth-2012 source in NanaST."
status: "draft"
tags: [requirements, nanast, forth-2012, codegen, rfopt]
---

# Requirements: Generalized Forth Code Generation

## Status

Draft. Prepared for review following the approved [NanaST vision](../../VISION.md) and the completed [Boolean Pass-Through Gate](../rfopt-target/requirements.md).

## Objective

Generalize the NanaST Forth code generator in [`src/forth.rs`](../../../src/forth.rs). The compiler currently translates only a single hardcoded Boolean pass-through statement (`output := input;`). This feature expands the generator to compile any valid program in the supported Structured Text (ST) subset into Forth-2012 source code.

## Target Platform and Runtime

The compilation target is Forth-2012 source code intended for execution by `rfopt::nanast` on AMD64 Linux and AArch64 Linux. The generated code must use only explicitly documented Forth words.

## Supported Language Scope

The code generator must handle all constructs produced by semantic analysis in [`src/sema.rs`](../../../src/sema.rs):

### 1. Variable Declarations
- **Inputs (`VAR_INPUT`):** Emitted as `VARIABLE nana-input-<N>`, where `<N>` is the zero-based input index in declaration order.
- **Outputs (`VAR_OUTPUT`):** Emitted as `VARIABLE nana-output-<N>`, where `<N>` is the zero-based output index in declaration order.
- **Local variables (`VAR`):** Emitted as `VARIABLE nana-var-<name>`, where `<name>` is the lowercased variable identifier.
- **Data types:** `BOOL`, `INT` (16-bit signed), and `DINT` (32-bit signed).

### 2. Initialization Routine (`nana-init`)
The generated Forth word `: nana-init ... ;` must:
- Initialize every output cell (`nana-output-<N>`) to `0`.
- Initialize every local variable (`nana-var-<name>`) to its declared literal initializer, or `0` if no initializer was specified.
- Leave input cells (`nana-input-<N>`) unchanged.

### 3. Scan Routine (`nana-scan`)
The generated Forth word `: nana-scan ... ;` must:
- Execute all statements in sequential order.
- Store evaluation results into the designated variable cell using `!`.
- Normalize Boolean assignments to output cells to canonical `0` or `1` using `IF 1 ELSE 0 THEN`.

### 4. Expressions
- **Literals:**
  - Boolean: `TRUE` emits `-1` in expression evaluation; `FALSE` emits `0`.
  - Integer: Decimal integer literal (e.g., `0`, `1`, `42`, `-10`).
- **Variables:** `<variable-cell> @` fetches the value of the variable onto the Forth stack.
- **Unary operations:**
  - `NOT`: Emits `0=` (logical inversion, converting zero to `-1` and non-zero to `0`).
  - `-` (negate): Emits `NEGATE`.
- **Binary operations:**
  - Arithmetic: `+`, `-`, `*`, `/`.
  - Logic: `AND`, `OR`.
  - Equality: `=` and `<>`.
  - Comparisons: `<` and `>`.
  - Compound comparisons: `> 0=` for `<=`, and `< 0=` for `>=`.
- **Constant folding:** Expressions folded by semantic analysis emit their folded literal directly.

### 5. Control Flow Statements
- **Assignments:** `<expression> <target-cell> !`
- **Conditionals:**
  - `IF ... THEN ... END_IF` emits `<condition> IF <then-statements> THEN`.
  - `IF ... THEN ... ELSE ... END_IF` emits `<condition> IF <then-statements> ELSE <else-statements> THEN`.

## Required Forth-2012 Word Inventory

Generated code must restrict itself to this explicit inventory:

| Word | Forth-2012 Word Set | Purpose in Generated Code |
|---|---|---|
| `:` | Core | Begins `nana-init` and `nana-scan` definitions. |
| `;` | Core | Ends definitions. |
| `VARIABLE` | Core | Declares variable storage cells. |
| `@` | Core | Reads a value from a variable cell. |
| `!` | Core | Writes a value into a variable cell. |
| `+` | Core | Integer addition. |
| `-` | Core | Integer subtraction. |
| `*` | Core | Integer multiplication. |
| `/` | Core | Signed integer division. |
| `NEGATE` | Core | Arithmetic negation. |
| `AND` | Core | Bitwise / logical AND. |
| `OR` | Core | Bitwise / logical OR. |
| `0=` | Core | Tests if top of stack equals zero; inverts boolean flag. |
| `=` | Core | Tests equality of two stack values. |
| `<>` | Core Extensions | Tests inequality of two stack values. |
| `<` | Core | Tests if first value is less than second value. |
| `>` | Core | Tests if first value is greater than second value. |
| `IF` | Core | Conditional branch on non-zero flag. |
| `ELSE` | Core | Alternative branch on zero flag. |
| `THEN` | Core | Joins conditional branches. |

Numeric literals are signed integer literals parsed directly by Forth.

## Backward Compatibility Contract

For the single Boolean pass-through fixture ([`tests/fixtures/pass_through.st`](../../../tests/fixtures/pass_through.st)):

```st
PROGRAM PassThrough
VAR_INPUT
    input : BOOL;
END_VAR
VAR_OUTPUT
    output : BOOL;
END_VAR

output := input;
END_PROGRAM
```

The generated Forth source code must remain token-identical to:

```forth
VARIABLE nana-input-0 VARIABLE nana-output-0 : nana-init 0 nana-output-0 ! ; : nana-scan nana-input-0 @ IF 1 ELSE 0 THEN nana-output-0 ! ;
```

This guarantees that the integration tests in [`tests/rfopt_target.rs`](../../../tests/rfopt_target.rs) and the gate runner script ([`scripts/run-rfopt-target.sh`](../../../scripts/run-rfopt-target.sh)) continue to pass without regression on both supported architectures.

## Acceptance Criteria

1. **Pass-through preservation:** Compiling `pass_through.st` continues to produce the exact token sequence above.
2. **Multi-variable programs:** Compiling programs with multiple inputs, outputs, and local variables produces corresponding `VARIABLE` declarations and initialization logic.
3. **Arithmetic and logic:** Expressions with `+`, `-`, `*`, `/`, `AND`, `OR`, `NOT`, and comparisons compile into correct postfix Forth instructions.
4. **Nested control flow:** Nested `IF ... ELSE ... THEN` blocks compile with balanced control flow words.
5. **Deterministic formatting:** Emitted code separates tokens with single spaces and definitions with semicolons without extra whitespace.
6. **Error handling:** Any unsupported semantic construct returns a descriptive `ForthError`.
7. **Automated unit tests:** Comprehensive unit tests in `tests/forth.rs` verify all supported statements and expressions.
