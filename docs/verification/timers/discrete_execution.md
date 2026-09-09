---
type: "Verification Evidence"
title: "Verification: Deterministic Timer and Edge Detection Profile"
description: "Verification evidence for R_TRIG, F_TRIG, TON, and TOF lowering and execution on the rfopt runtime."
status: "passed"
tags: [verification, nanast, rfopt, timers, r_trig, f_trig, ton, tof]
---

# Verification: Deterministic Timer and Edge Detection Profile

## Scope

This record verifies the deterministic timer and edge detection implementation in NanaST.

The verification covers:
- Lexing and parsing of `R_TRIG`, `F_TRIG`, `TON`, and `TOF` declarations, invocations, and field accesses.
- Semantic validation of instance storage in `VAR`, named parameter bindings, read-only output assertions, and `cycle_dt` resolution.
- Pure Forth lowering to `@`, `!`, arithmetic, and conditional branches without runtime extensions or OS clock dependencies.
- Multi-cycle execution on the `rfopt` virtual machine, validating edge pulses, discrete delay accumulation, PT saturation, and immediate reset.

## Environment

| Property | Value |
| --- | --- |
| Run time (UTC) | 2026-09-09T08:56:00Z |
| Build host | Linux 6.12.95+deb13-rt-amd64, x86_64 |
| Rust | rustc 1.93.1 |
| Cargo | cargo 1.93.1 |

## Commands and Results

| Command | Result | Details |
| --- | --- | --- |
| `cargo test --test parser` | Passed | Verified parsing of timer declarations, named argument invocations, and `.Q` / `.ET` field accesses. |
| `cargo test --test sema` | Passed | Verified instance allocation, argument checks, rejection of mutations, and cycle time resolution. |
| `cargo test --test forth` | Passed | Verified Forth emission for `R_TRIG`, `F_TRIG`, `TON`, and `TOF`. |
| `cargo test --test rfopt_target` | Passed | Verified runtime execution: single-cycle pulses for `R_TRIG` and `F_TRIG`, on-delay and reset for `TON`, hold and timeout for `TOF`. |
| `cargo test` | Passed | All 56 tests in the NanaST suite passed. |
| `cargo clippy --all-targets` | Passed | Clean build with zero warnings. |
| `cargo fmt -- --check` | Passed | Formatted according to repository guidelines. |

## Conclusion

The deterministic timer and edge detection profile satisfies all requirements. The code lowers directly to supported Forth operations and executes deterministically across discrete cycles.
