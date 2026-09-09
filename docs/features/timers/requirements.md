---
type: "Feature Requirements"
title: "Requirements: Deterministic Timer and Edge Detection Profile"
description: "Requirements for deterministic timers and edge detectors in NanaST."
status: "approved"
tags: [requirements, nanast, rfopt, timers, ton, tof, r_trig, f_trig]
---

# Requirements: Deterministic Timer and Edge Detection Profile

## Status

Sirius Wu approved these requirements on 2026-09-09:

- NanaST introduces built-in deterministic timing and edge detection primitives.
- Timers use an explicit cycle delta (`dt`) provided by the host process image.
- Instances use static variable allocation inside `VAR` blocks.
- Execution remains deterministic across AMD64 and AArch64 without operating system clock calls during scan execution.

This feature extends the approved [NanaST vision](../../VISION.md) and addresses Item 1 of the missing capabilities in the [legacy Forth migration proposal](../../proposals/nanast-legacy-forth-migration.md).

## Objective

Cyclic control sequences in the legacy Mapacode codebase (including `buzzer.fs`, `trigger.fs`, and `system_on_off.fs`) depend on time delays, pulse holds, and button edge triggers. NanaST provides standard IEC-compatible timer and edge detection primitives so developers can translate these sequences into testable Structured Text without introducing runtime nondeterminism.

## Core Design Principles

- **Explicit discrete time:** The host environment supplies the cycle period `dt` (in milliseconds) through the process image on each scan. The runtime does not invoke system clock calls inside `nana-scan`.
- **Static instance allocation:** Each timer or edge detector instance occupies a fixed set of scalar cells allocated at compile time. NanaST does not perform dynamic memory allocation.
- **Deterministic reset:** Invoking `nana-init` resets all internal timer counters, elapsed values, and edge detection memories to zero or `FALSE`.
- **Saturation arithmetic:** Elapsed time values saturate at preset limits and do not overflow or wrap around.

## Supported Primitives

NanaST provides four built-in primitive types:

### 1. `R_TRIG` (Rising Edge Detector)

Detects a low-to-high transition on a Boolean signal.

- **Inputs:**
  - `CLK : BOOL` (input signal to monitor)
- **Outputs:**
  - `Q : BOOL` (edge detection pulse)
- **Behavior:**
  - `Q` evaluates to `TRUE` for exactly one scan cycle when `CLK` transitions from `FALSE` to `TRUE`.
  - In all other cycles, `Q` evaluates to `FALSE`.
- **Internal State:**
  - `prev_clk : BOOL` (stores the previous value of `CLK`, initialized to `FALSE`).

### 2. `F_TRIG` (Falling Edge Detector)

Detects a high-to-low transition on a Boolean signal.

- **Inputs:**
  - `CLK : BOOL` (input signal to monitor)
- **Outputs:**
  - `Q : BOOL` (edge detection pulse)
- **Behavior:**
  - `Q` evaluates to `TRUE` for exactly one scan cycle when `CLK` transitions from `TRUE` to `FALSE`.
  - In all other cycles, `Q` evaluates to `FALSE`.
- **Internal State:**
  - `prev_clk : BOOL` (stores the previous value of `CLK`, initialized to `FALSE`).

### 3. `TON` (On-Delay Timer)

Delays setting output `Q` to `TRUE` after input `IN` becomes `TRUE`.

- **Inputs:**
  - `IN : BOOL` (timer enable signal)
  - `PT : DINT` (preset time duration in milliseconds, must be non-negative)
- **Outputs:**
  - `Q : BOOL` (timer output state)
  - `ET : DINT` (elapsed time in milliseconds)
- **Behavior:**
  - When `IN` is `FALSE`:
    - `ET` resets to `0`.
    - `Q` becomes `FALSE`.
  - When `IN` is `TRUE`:
    - If `ET < PT`, `ET` advances by `dt`, saturating at `PT`.
    - If `ET >= PT`, `Q` becomes `TRUE`.
- **Internal State:**
  - `et : DINT` (retained elapsed time, initialized to `0`).

### 4. `TOF` (Off-Delay Timer)

Maintains output `Q` at `TRUE` for a specified duration after input `IN` falls to `FALSE`.

- **Inputs:**
  - `IN : BOOL` (input signal)
  - `PT : DINT` (hold time duration in milliseconds, must be non-negative)
- **Outputs:**
  - `Q : BOOL` (timer output state)
  - `ET : DINT` (elapsed time in milliseconds)
- **Behavior:**
  - When `IN` is `TRUE`:
    - `Q` becomes `TRUE`.
    - `ET` resets to `0`.
  - When `IN` is `FALSE`:
    - If `ET < PT`, `ET` advances by `dt`, saturating at `PT`.
    - If `ET < PT`, `Q` remains `TRUE`.
    - When `ET >= PT`, `Q` becomes `FALSE`.
- **Internal State:**
  - `et : DINT` (retained elapsed time, initialized to `0`).

## Language Syntax

### Instance Declaration

Instances are declared exclusively inside `VAR` blocks:

```pascal
VAR
    timer1 : TON;
    trigger1 : R_TRIG;
END_VAR
```

### Invocation Statement

Programs invoke an instance using named parameter assignments:

```pascal
trigger1(CLK := raw_button);
timer1(IN := trigger1.Q, PT := 500);
```

### Output Field Access

Programs read output values using member access syntax:

```pascal
motor_run := timer1.Q;
remaining_display := timer1.ET;
```

Assignments to output fields (such as `timer1.Q := TRUE;`) are invalid and rejected by semantic analysis.

## Process Image Contract for Cycle Time

BNC or the execution host provides the cycle delta `dt` for each scan.

NanaST recognizes a standard input variable named `cycle_dt` of type `DINT`:

```pascal
VAR_INPUT
    cycle_dt : DINT;
END_VAR
```

When `cycle_dt` is declared, the compiled scan logic reads its cell value as `dt`. If the program does not declare `cycle_dt`, NanaST defaults `dt` to 1 millisecond.

## Forth Target Representation

NanaST generates scalar Forth variables for each instance's internal state:

- For `R_TRIG` named `trig`:
  - `VARIABLE nana-var-trig-prev`
  - `VARIABLE nana-var-trig-q`
- For `TON` named `tmr`:
  - `VARIABLE nana-var-tmr-et`
  - `VARIABLE nana-var-tmr-q`

The generated Forth code for `nana-init` stores `0` into all instance state variables. The generated `nana-scan` code executes the discrete state update using native Forth comparison and arithmetic operations.

## Acceptance Criteria

1. **Parser and Lexer:**
   - Recognizes type identifiers `TON`, `TOF`, `R_TRIG`, and `F_TRIG`.
   - Parses instance calls with named argument lists.
   - Parses member field accesses (`instance.Q`, `instance.ET`).
2. **Semantic Analysis:**
   - Rejects instance declarations outside `VAR`.
   - Rejects assignments to read-only outputs `Q` and `ET`.
   - Rejects negative preset durations.
3. **Forth Code Generation:**
   - Generates predictable scalar Forth variable declarations.
   - Generates clean initialization in `nana-init`.
   - Inlines or emits standard helper words for state updates.
4. **Integration Verification (`rfopt`):**
   - Verifies `R_TRIG` and `F_TRIG` generate single-cycle pulses on signal edges.
   - Verifies `TON` delays output assertion until `ET >= PT` and resets when `IN` drops.
   - Verifies `TOF` holds output assertion for `PT` duration after `IN` drops.
   - Verifies output values reset on `nana-init`.
