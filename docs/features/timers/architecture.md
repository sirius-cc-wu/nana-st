---
type: "Software Architecture Design"
title: "Architecture: Deterministic Timer and Edge Detection Profile"
description: "Architecture design for compiling built-in timers and edge detectors to static Forth cells in rfopt."
status: "accepted"
tags: [architecture, nanast, rfopt, timers, ton, tof, r_trig, f_trig, stc]
---

# Architecture: Deterministic Timer and Edge Detection Profile

## Architecture Question

How can NanaST introduce deterministic IEC timers (`TON`, `TOF`) and edge detectors (`R_TRIG`, `F_TRIG`) while preserving the existing restricted Forth-2012 word subset, static allocation model, and native STC boundary in `rfopt`?

## Selected Structure

```text
ST Source: TON, TOF, R_TRIG, F_TRIG declarations, calls, and field accesses
  -> Lexer and Parser (syntax for instance calls and member access)
  -> Semantic Analysis (validation, argument checking, and scalar cell layout)
  -> Forth Code Generation (lowers updates to pure Forth conditionals and arithmetic)
  -> rfopt Restricted Loader (loads static VARIABLE cells and lifecycle words)
  -> Native STC Execution (runs verified machine code on AMD64 / AArch64)
```

- **NanaST Compiler owns:**
  - Tokenization and syntax validation for instance declarations, invocation statements, and field reads.
  - Parameter matching and read-only enforcement for output fields.
  - Mapping each logical instance to dedicated scalar Forth state cells.
  - Inlining deterministic update logic using existing Forth arithmetic and branching words.
- **rfopt Runtime owns:**
  - Storage allocation for integer cells.
  - Execution of compiled `nana-init` and `nana-scan` native STC words.
  - Opaque cell handles for process image inspection.
  - No changes required to the restricted Forth word dictionary.
- **BNC Host owns:**
  - Supplying the discrete cycle duration `dt` via the `cycle_dt` input cell on each scan.

## Component Design

### 1. Abstract Syntax Tree (AST)

The compiler AST expands with minimal additions:
- **`DataType`:** Adds variants `RTrig`, `FTrig`, `Ton`, and `Tof`.
- **`StatementKind`:** Adds `Invocation { instance: Identifier, arguments: Vec<NamedArgument> }`, where `NamedArgument` pairs a parameter name with an `Expression`.
- **`ExpressionKind`:** Adds `FieldAccess { instance: Identifier, field: Identifier }`.

### 2. Semantic Analysis (`sema`)

Semantic analysis validates and transforms timer operations:
- **Storage validation:** Instances must be declared in `VAR` (local storage). Declarations in `VAR_INPUT` or `VAR_OUTPUT` return compile-time diagnostics.
- **Argument validation:**
  - `R_TRIG` / `F_TRIG`: Requires exactly `CLK : BOOL`.
  - `TON` / `TOF`: Requires `IN : BOOL` and `PT : DINT` (or integer literal).
- **Field access validation:**
  - `.Q` resolves to type `BOOL`.
  - `.ET` resolves to type `DINT`.
  - Assignments to `.Q` or `.ET` are rejected as invalid assignment targets.
- **State Cell Allocation:**
  - Each `R_TRIG` / `F_TRIG` instance allocates two integer cells: `prev_clk` and `q`.
  - Each `TON` / `TOF` instance allocates three integer cells: `et`, `pt`, and `q`.
- **Cycle Time Detection:**
  - The analyzer detects whether a `cycle_dt` input variable is declared.
  - If present, `cycle_dt` supplies the cycle delta `dt`. If absent, `dt` defaults to literal `1`.

### 3. Forth Code Generation (`forth`)

Update logic is lowered into pure Forth-2012 words already supported by `rfopt` (`@`, `!`, `+`, `<`, `>`, `0=`, `IF`, `ELSE`, `THEN`).

#### Variable Declarations
Each instance expands to scalar Forth `VARIABLE` declarations:
- `VARIABLE nana-var-<inst>-m` (previous clock state for triggers)
- `VARIABLE nana-var-<inst>-q` (output state)
- `VARIABLE nana-var-<inst>-et` (retained elapsed time for timers)
- `VARIABLE nana-var-<inst>-pt` (preset duration for timers)

#### Initialization (`nana-init`)
The initialization word writes `0` to every instance cell, ensuring clean startup without residual state from previous runs.

#### Invocation Lowering (`nana-scan`)
- **`R_TRIG` Lowering:**
  ```forth
  <clk_expr> IF
      nana-var-<inst>-m @ 0= IF 1 ELSE 0 THEN nana-var-<inst>-q !
      1 nana-var-<inst>-m !
  ELSE
      0 nana-var-<inst>-q !
      0 nana-var-<inst>-m !
  THEN
  ```
- **`F_TRIG` Lowering:**
  ```forth
  <clk_expr> IF
      0 nana-var-<inst>-q !
      1 nana-var-<inst>-m !
  ELSE
      nana-var-<inst>-m @ 1 = IF 1 ELSE 0 THEN nana-var-<inst>-q !
      0 nana-var-<inst>-m !
  THEN
  ```
- **`TON` Lowering:**
  ```forth
  <pt_expr> nana-var-<inst>-pt !
  <in_expr> IF
      nana-var-<inst>-et @ nana-var-<inst>-pt @ < IF
          nana-var-<inst>-et @ <dt> + nana-var-<inst>-et !
          nana-var-<inst>-et @ nana-var-<inst>-pt @ > IF
              nana-var-<inst>-pt @ nana-var-<inst>-et !
          THEN
      THEN
      nana-var-<inst>-et @ nana-var-<inst>-pt @ < 0= IF 1 ELSE 0 THEN nana-var-<inst>-q !
  ELSE
      0 nana-var-<inst>-et !
      0 nana-var-<inst>-q !
  THEN
  ```
- **`TOF` Lowering:**
  ```forth
  <pt_expr> nana-var-<inst>-pt !
  <in_expr> IF
      1 nana-var-<inst>-q !
      0 nana-var-<inst>-et !
  ELSE
      nana-var-<inst>-q @ IF
          nana-var-<inst>-et @ nana-var-<inst>-pt @ < IF
              nana-var-<inst>-et @ <dt> + nana-var-<inst>-et !
              nana-var-<inst>-et @ nana-var-<inst>-pt @ > IF
                  nana-var-<inst>-pt @ nana-var-<inst>-et !
              THEN
          THEN
          nana-var-<inst>-et @ nana-var-<inst>-pt @ < 0= IF
              0 nana-var-<inst>-q !
          THEN
      THEN
  THEN
  ```

#### Field Access Lowering
Reading `inst.Q` emits `nana-var-<inst>-q @`. Reading `inst.ET` emits `nana-var-<inst>-et @`.

## Failure and Boundary Guarantees

1. **Zero Dynamic Allocation:**
   All state exists in static Forth cells allocated when the program loads. The runtime cannot run out of memory during a control cycle.
2. **Saturation Arithmetic:**
   Elapsed time comparisons clamp `et` at `pt`. Counter accumulation cannot overflow 64-bit integer limits during ordinary operation.
3. **No Hidden System Clock Calls:**
   Timers depend strictly on `dt`. Scans run with deterministic, cycle-consistent timing across all execution platforms.
4. **No Dictionary Changes in `rfopt`:**
   The implementation reuses verified integer Forth words. No runtime submodule changes or compiler extensions are necessary.

## Verification Strategy

1. **Parser Tests:** Verify parsing of instance declarations, call syntax with named arguments, and member access expressions.
2. **Semantic Analysis Tests:** Verify diagnostic reporting for illegal instance locations, invalid argument names, type mismatches, and assignments to output fields.
3. **Forth Codegen Tests:** Verify generated Forth definitions and update tokens for each primitive.
4. **Target Integration Tests (`rfopt`):** Multi-scan tests verifying edge detection pulses, on-delay timings, off-delay holds, and re-initialization resets.
