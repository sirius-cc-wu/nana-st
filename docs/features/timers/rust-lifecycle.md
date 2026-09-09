---
type: "Rust Lifecycle Design"
title: "Rust Lifecycle Design: Deterministic Timer and Edge Detection Profile"
description: "Ownership and lifecycle rules for static timer cells and discrete update execution."
status: "accepted"
language: "rust"
tags: [rust, lifecycle, nanast, rfopt, timers, ton, tof, r_trig, f_trig]
---

# Rust Lifecycle Design: Deterministic Timer and Edge Detection Profile

## Ownership

| Resource | Owner | Release Behavior |
|---|---|---|
| AST Primitive Node | `Program` AST | Drops when compilation completes |
| Analyzed Instance Definition | `AnalyzedProgram` | Drops after Forth emission completes |
| Instance State Cells (`VARIABLE`) | Private loaded runtime in `rfopt` | Drops when `Runtime` drops |
| Cycle Delta Cell (`cycle_dt`) | BNC process image or test driver | Managed by caller |
| Invocation Data Stack | STC execution frame | Stack pointer restored before word returns |

Instances are static and allocated once at program load time. The compiler decomposes each high-level timer instance into discrete scalar cells (`-m`, `-q`, `-et`, `-pt`). These cells live within the existing `rfopt::nanast::Runtime` cell pool and do not require separate lifecycle handles or dynamic heap storage.

## Execution Lifecycle

```text
Host triggers scan
  -> BNC writes process image inputs (including cycle_dt if present)
  -> Host invokes nana-scan
  -> Evaluates instance inputs and updates state cells in place
  -> Evaluates remaining cyclic control logic and writes output cells
  -> nana-scan returns with balanced data stack
Host applies process image outputs
```

### Initialization (`nana-init`)
1. Resets all instance state cells to zero:
   - For `R_TRIG` / `F_TRIG`: sets `m := 0`, `q := 0`.
   - For `TON` / `TOF`: sets `et := 0`, `pt := 0`, `q := 0`.
2. Can be called at system startup or upon fault reset without destroying or recreating the runtime.

### Scan Execution (`nana-scan`)
1. Executes discrete state transition logic synchronously.
2. Clamps elapsed durations to preset limits to prevent arithmetic overflow.
3. Leaves stack pointers balanced upon completion.

## Failure and Error Handling

1. **Compilation Phase:**
   - Syntax errors and semantic type errors abort compilation before emitting Forth source.
   - The compiler produces structured, source-located error diagnostics.
2. **Runtime Loading Phase:**
   - `rfopt` validates that all emitted words and cells conform to the restricted Forth profile.
   - Any stack imbalance or unrecognized token rejects the entire program load cleanly.
3. **Execution Phase:**
   - Saturation logic prevents elapsed time arithmetic from overflowing.
   - Timers do not perform fallible runtime operations, ensuring execution cannot trap during cyclic control tasks.

## Verification Obligations

1. Verify that instance state cells drop cleanly when the `rfopt::nanast::Runtime` drops.
2. Verify that repeated calls to `nana-init` return all timer instances to pristine zero states.
3. Verify that multiple timer instances maintain independent state without memory aliasing.
4. Verify stack balance after executing timer and trigger update blocks.
