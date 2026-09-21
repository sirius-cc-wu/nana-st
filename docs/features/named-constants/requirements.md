---
type: "Feature Requirements"
title: "Requirements: Named Constants (VAR CONSTANT Profile)"
description: "Requirements and Example Mapping specification for compile-time immutable named constants in NanaST."
status: "approved"
tags: [requirements, nanast, rfopt, constants, immutability, var-constant]
---

# Requirements: Named Constants (VAR CONSTANT Profile)

## Status

Approved for specification and design.

- NanaST introduces `VAR CONSTANT ... END_VAR` declaration blocks for immutable named values.
- Supported data types for constants include `BOOL`, `INT`, `DINT`, and `REAL`.
- Every constant declaration requires a static compile-time initializer expression.
- Constant identifiers are read-only: assignment statements targeting a constant symbol are rejected during semantic analysis with source-located diagnostics.
- Integer and Boolean constants lower directly to Forth-2012 `<val> CONSTANT nana-const-<name>` (using `TRUE` and `FALSE` for booleans), which `rfopt` natively compiles into Subroutine Threaded Code (STC) immediate literals.
- `REAL` constants lower directly to Forth-2012 `<val> FCONSTANT nana-const-<name>` (e.g. `26.0e FCONSTANT nana-const-high_limit`), supported natively by `rfopt` and compiled into STC immediate float literals.
- Both `CONSTANT` and `FCONSTANT` allocate zero bytes of mutable RAM cells and emit zero initialization instructions in `nana-init`.

This feature extends the approved [NanaST vision](../../VISION.md) and addresses Capability Gap 4 identified in the [legacy Forth migration proposal](../../proposals/nanast-legacy-forth-migration.md).

## Objective

Machine control programs in BNC (including chiller temperature limits, dosing pH thresholds, debounce intervals, and state machine step IDs) require fixed configuration parameters. Currently, NanaST only supports mutable variables (`VAR`), forcing developers to either scatter magic literals across the program or allocate mutable RAM cells. Magic numbers create severe maintenance hazards and risk inconsistent thresholds across control branches. Mutable cells waste target RAM, incur redundant reset instructions in `nana-init`, add memory bus read latency during cyclic scans (`nana-scan`), and provide no protection against accidental mutation. The `VAR CONSTANT` profile provides immutable, self-documenting symbols with zero runtime memory overhead.

## Core Design Principles

- **Standard IEC 61131-3 syntax:** Constants are declared within `VAR CONSTANT ... END_VAR` blocks (with `VAR_CONSTANT ... END_VAR` supported as an alias).
- **Mandatory static initialization:** A constant declaration without an initializer is syntactically invalid and rejected at parse time.
- **Strict immutability:** Constant identifiers cannot appear on the left-hand side of an assignment statement or as output parameters. Reassignment is rejected at compile time.
- **Zero runtime RAM allocation:** Unlike `VAR` and `VAR_OUTPUT`, constants do not allocate Forth `VARIABLE` or `FVARIABLE` storage cells.
- **Zero scan latency overhead:** Constants compile to immediate operand literals in machine instructions, avoiding memory bus reads during `nana-scan`.

## Ubiquitous Language

- **Named Constant:** An immutable symbol bound to a statically evaluated literal value at compile time.
- **Constant Block (`VAR CONSTANT`):** A declaration scope containing exclusively immutable named constants.
- **Constant Initializer:** An expression evaluated at compile time whose result determines the permanent value of a constant.
- **Immediate Literal:** A machine instruction operand encoded directly within the executable instruction stream rather than fetched from a RAM address.
- **Immutability Invariant:** The compiler guarantee that a declared constant's value cannot be altered during program execution.

## System Behavioral Contract (Larman SSD & Operation Contracts)

### Contract: `declare_constant`

- **Preconditions:**
  - Token stream enters a `VAR CONSTANT` block.
- **Inputs:**
  - `name : Identifier`
  - `data_type : DataType` (restricted to `Bool`, `Int`, `Dint`, or `Real`)
  - `initializer : Expression` (mandatory compile-time constant expression)
- **Postconditions:**
  - Parser constructs a `Declaration` node with `storage: StorageClass::Constant`.
  - Semantic analysis evaluates `initializer`, verifies type compatibility, and registers the symbol in the constant symbol table.
  - Subsequent statements referencing `name` evaluate its value without memory fetch words.
  - Any statement attempting `name := <expr>;` produces a diagnostic error.

## Business Rules and Example Mapping

### Rule Matrix

| Rule # | Rule Name | Specification |
|:---|:---|:---|
| **R1** | **Mandatory Initializer Rule** | Every constant declaration in a `VAR CONSTANT` block must include an initializer (`:= <expr>`). A missing initializer is rejected with a parse error. |
| **R2** | **Strict Immutability Rule** | Any assignment targeting a constant identifier is rejected during semantic analysis with diagnostic: `cannot assign to constant '<name>'`. |
| **R3** | **Type Compatibility Rule** | The constant initializer type must strictly match or be implicitly coercible to the declared constant type (e.g., integer literal to `DINT`). Incompatible types produce compile-time type mismatch errors. |
| **R4** | **Supported Type Range** | Supported constant types are `BOOL`, `INT`, `DINT`, and `REAL`. Function block instances (`TON`, `TOF`, `R_TRIG`, `F_TRIG`) cannot be declared in `VAR CONSTANT`. |
| **R5** | **Zero RAM Cell Allocation** | The compiler emits no Forth `VARIABLE` or `FVARIABLE` declarations for constants, consuming zero bytes of target data memory. |
| **R6** | **Zero `nana-init` Overhead** | The compiler generates no store (`!`, `F!`) instructions in `nana-init` for constants. |
| **R7** | **Constant Expression Inlining in Expressions** | Referencing a constant identifier inside an expression evaluates its value directly as an immediate literal without emitting `@` or `F@` fetch words. |
| **R8** | **Forth Target Emission** | For integer and boolean constants, the compiler emits `<val> CONSTANT nana-const-<name>` (using `TRUE`/`FALSE` for booleans). For REAL constants, the compiler emits `<val>e FCONSTANT nana-const-<name>`. Both forms support top-level dictionary inspection and compile to immediate STC literals. |

### Concrete Examples

#### Example 1: Physical Limit Constants in Chiller Control
```pascal
PROGRAM ChillerParameters
VAR CONSTANT
    HIGH_LIMIT : REAL := 26.0;
    LOW_LIMIT : REAL := 24.0;
    SAMPLE_RATE_MS : DINT := 100;
    ENABLE_DEFAULT : BOOL := TRUE;
END_VAR
VAR_INPUT
    water_temp : REAL;
END_VAR
VAR_OUTPUT
    chiller_enable : BOOL;
    valve_open : BOOL;
END_VAR

chiller_enable := ENABLE_DEFAULT;
IF water_temp > HIGH_LIMIT THEN
    valve_open := TRUE;
ELSE
    IF water_temp < LOW_LIMIT THEN
        valve_open := FALSE;
    END_IF;
END_IF;
END_PROGRAM
```
- Valid program. All parameters are self-documenting and immutable.

#### Example 2: Reassignment Rejection (Negative Case)
```pascal
VAR CONSTANT
    TIMEOUT_MS : DINT := 5000;
END_VAR

TIMEOUT_MS := 10000; (* REJECTED: cannot assign to constant 'TIMEOUT_MS' *)
```
- Compile error: `cannot assign to constant 'TIMEOUT_MS'` pointing to line 5.

#### Example 3: Missing Initializer Rejection (Negative Case)
```pascal
VAR CONSTANT
    UNINITIALIZED_LIMIT : REAL; (* REJECTED: constant declaration requires an initializer *)
END_VAR
```
- Parse error: `constant declaration 'UNINITIALIZED_LIMIT' requires an initializer`.

## Acceptance Criteria

1. **Parser & Lexer:**
   - Recognizes `CONSTANT` keyword.
   - Parses `VAR CONSTANT ... END_VAR` and `VAR_CONSTANT ... END_VAR`.
   - Enforces initializer presence on all constant declarations.
2. **Semantic Analysis:**
   - Adds `StorageClass::Constant`.
   - Enforces read-only protection against assignments.
   - Evaluates and type-checks constant initializers.
   - Rejects primitive instance declarations in constant blocks.
3. **Forth Code Generation:**
   - Emits zero `VARIABLE` or `FVARIABLE` allocations for constants.
   - Emits zero initialization instructions in `nana-init`.
   - Inlines constant values as immediate literals without fetch words (`@`, `F@`).
4. **Target Parity (`rfopt`):**
   - Verifies that ST programs with constants compile and execute on `rfopt` with correct values and zero memory drift.
