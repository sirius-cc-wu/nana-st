---
type: "Verification Evidence"
title: "Verification: STC NanaST Boundary on AArch64"
description: "Native AArch64 Linux execution evidence for the rfopt suite and NanaST integration gate."
status: "passed"
tags: [verification, nanast, rfopt, stc, aarch64]
---

# Verification: STC NanaST Boundary on AArch64

## Scope

This record closes the AArch64 execution evidence required by the [STC NanaST
Execution Boundary requirements](../../features/stc-nanast-boundary/requirements.md).
It proves native execution of the pinned `rfopt` revision and the NanaST
integration gate. It does not make a real-time performance or safety claim.

## Environment

| Property | Value |
| --- | --- |
| Run time (UTC) | 2026-09-08T17:45:57Z |
| Host | Linux 4.19.232-rt104, `aarch64` |
| Rust | rustc 1.98.1 (48a229cea 2026-09-01) |
| Cargo | cargo 1.98.1 (797e8a9b 2026-08-05) |
| NanaST revision | `060764edaeaca9b21bfd999ef355150f4b6526cd` |
| Pinned rfopt revision | `866e646bf2d37a95ac769634c05e43aa97cc56a0` |

Both worktrees were clean before and after the run.

## Commands and Results

| Command | Result |
| --- | --- |
| `cargo test --locked --manifest-path rfopt/Cargo.toml` | Passed: 67 unit tests and 2 integration tests passed. |
| `scripts/run-rfopt-target.sh` | Passed: 3 NanaST compiler-to-rfopt integration tests passed. |

The host linker emitted `unsupported GNU_PROPERTY_TYPE` diagnostics while
linking. Cargo completed successfully and every test passed. These linker
messages do not invalidate the native AArch64 execution result.

## Conclusion

The pinned `rfopt` suite and the NanaST integration gate execute successfully
on native AArch64 Linux. This satisfies the AArch64 execution-gate acceptance
criterion for the STC NanaST execution boundary.
