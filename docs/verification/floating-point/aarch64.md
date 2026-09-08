---
type: "Verification Evidence"
title: "Verification: IEEE-754 REAL Support on AArch64"
description: "AArch64 Linux user-mode emulation evidence for rfopt float operations and the NanaST REAL integration scenario."
status: "passed"
tags: [verification, nanast, rfopt, floating-point, ieee-754, aarch64, qemu]
---

# Verification: IEEE-754 REAL Support on AArch64

## Scope

This record verifies the float-support change in the uncommitted worktrees
based on NanaST revision `96e6723382adb6be3a4d646c2b321407f894c80e` and rfopt
revision `866e646bf2d37a95ac769634c05e43aa97cc56a0`.

It verifies AArch64 execution of the rfopt float profile and the NanaST `REAL`
integration scenario. It does not measure real-time latency, prove hardware
behavior, or replace a native AArch64 hardware gate.

## Environment

| Property | Value |
| --- | --- |
| Run time (UTC) | 2026-09-08T19:14:44Z |
| Build host | Linux 6.12.95+deb13-rt-amd64, x86_64 |
| Target runtime | QEMU user-mode AArch64 Linux with `/usr/aarch64-linux-gnu` userspace |
| QEMU | 10.0.11 |
| Rust | rustc 1.93.1 (01f6ddf75 2026-02-11) |
| Cargo | cargo 1.93.1 (083ac5135 2025-12-15) |

## Commands and Results

| Command | Result |
| --- | --- |
| `cd rfopt && cargo test --target aarch64-unknown-linux-gnu --no-run --locked` | Passed. |
| `qemu-aarch64 -L /usr/aarch64-linux-gnu target/aarch64-unknown-linux-gnu/debug/deps/rfopt-6f86ef276bda1c52 nanast::tests:: --nocapture` | Passed: 19 tests, including float arithmetic, NaN comparisons, non-finite values, typed handles, and FPCR/FPSR restoration. |
| `CARGO_TARGET_AARCH64_UNKNOWN_LINUX_GNU_LINKER=aarch64-linux-gnu-gcc cargo test --target aarch64-unknown-linux-gnu --no-run --locked` | Passed. |
| `qemu-aarch64 -L /usr/aarch64-linux-gnu target/aarch64-unknown-linux-gnu/debug/deps/rfopt_target-ee15b7d2e294925d --nocapture` | Passed: 4 tests, including the NanaST `REAL` arithmetic and branch scenario. |

## Conclusion

The QEMU AArch64 Linux userspace executed the native AArch64 STC float profile
and the NanaST `REAL` integration scenario successfully. Native AArch64
hardware verification remains required for a hardware-performance or
hardware-specific floating-point claim.
