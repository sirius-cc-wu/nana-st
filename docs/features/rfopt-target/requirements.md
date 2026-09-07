---
type: "Feature Requirements"
title: "Requirements: rfopt Boolean Pass-Through Target"
description: "Requirements for the first NanaST Forth-subset artifact executed by rfopt."
status: "approved"
tags: [requirements, nanast, rfopt, forth-2012, pass-through]
---

# Requirements: rfopt Boolean Pass-Through Target

## Status

Sirius Wu approved these requirements on 2026-09-06.

The approved [NanaST vision](../../VISION.md) selects `rfopt` as the sole planned runtime. It also establishes this Boolean pass-through feature as the first compatibility gate. The in-process host boundary is approved in [architecture.md](architecture.md).

Implementation may begin within these requirements and the approved architecture.

## Objective

Prove the smallest device-neutral vertical slice between NanaST and rfopt. A NanaST program with one Boolean input and one Boolean output translates into loadable Forth source. rfopt loads that source and runs its lifecycle words so the output matches the normalized input.

The test fixture is [`tests/fixtures/pass_through.st`](../../../tests/fixtures/pass_through.st):

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

## Target Platform and Promotion Gate

The initial target platform is the AMD64 Linux development host. rfopt must build and run the generated Forth code on AMD64 before NanaST begins any AArch64 work.

The currently pinned rfopt revision does not build on AMD64 Linux because `src/rforth.rs` imports AArch64-only `slow` code and references the AArch64 `x9` register. Fixing the AMD64 build is prerequisite work within rfopt; a build failure is not valid compatibility evidence.

AArch64 is out of scope for this feature. Work on an AArch64 target may start only after all acceptance criteria below pass on AMD64 Linux and the requirements are approved.

## Host Execution Boundary

rfopt provides the in-process host boundary for this feature:
- Loads generated Forth source directly from memory.
- Resolves and invokes public lifecycle words.
- Exposes I/O cells exclusively through opaque host handles.

The NanaST AMD64 integration test links this API from the pinned rfopt submodule. The test does not use a CLI and introduces no extra Forth words.

The host boundary writes non-zero test values (such as `-1`) through cell handles, without requiring Forth source to parse them. rfopt provides its own unit-test evidence for this boundary, while NanaST provides the cross-repository integration gate. See [architecture.md](architecture.md) for the architecture design and error handling.

## Target Contract

### Generated Artifact

The compiler produces Forth source code. For this control case, it must define the following public words in the current Forth word list:

| Word | Stack Effect | Contract |
|---|---|---|
| `nana-input-0` | `( -- a-addr )` | Defines the input cell. rfopt maps its Forth address to an opaque cell handle for host writes before a scan. |
| `nana-output-0` | `( -- a-addr )` | Defines the output cell. rfopt maps its Forth address to an opaque cell handle for host reads after a scan. |
| `nana-init` | `( -- )` | Clears output cells to `0`. It does not modify input cells. This feature has no persistent `VAR` state. |
| `nana-scan` | `( -- )` | Reads the input cell and writes the normalized Boolean value (`0` or `1`) to the output cell. |

After normalizing whitespace, the generated source code must match this exact token sequence:

```forth
VARIABLE nana-input-0 VARIABLE nana-output-0 : nana-init 0 nana-output-0 ! ; : nana-scan nana-input-0 @ IF 1 ELSE 0 THEN nana-output-0 ! ;
```

The output defines only the two `VARIABLE` declarations and the two `:` definitions listed above. It must not contain comments, helper words, or additional tokens. Whitespace formatting is the only allowed variation.

### Boolean Behavior

- A value of `0` in the input cell represents `FALSE` and produces `0` in the output cell.
- Any non-zero value in the input cell represents `TRUE` and produces `1` in the output cell.
- Calling `nana-init` preserves any value previously written to the input cell and sets `nana-output-0` to `0`. Initializing persistent `VAR` state will be handled in a separate feature.
- NanaST's public I/O boundary never requires Forth's native true flag (`-1`). rfopt may still use `-1` internally.

## Required Forth-2012 Inventory

rfopt does not need to implement the full Forth-2012 standard. It only needs to implement the following words required by the target contract:

| Word | Forth-2012 Word Set | Purpose in Generated Code |
|---|---|---|
| `:` | Core | Begins the `nana-init` and `nana-scan` definitions. |
| `;` | Core | Ends those definitions. |
| `VARIABLE` | Core | Declares each public I/O cell. |
| `@` | Core | Fetches the value from the input cell during a scan. |
| `!` | Core | Stores values into initialized and scanned output cells. |
| `IF` | Core | Branches based on whether the input is non-zero. |
| `ELSE` | Core | Handles the false branch when input is zero. |
| `THEN` | Core | Closes the conditional branch. |

The numbers `0` and `1` are numeric literals, not named Forth words. rfopt must parse them as single-cell integers.

If any required word is missing, it must be implemented in rfopt. NanaST will record the missing word and will not generate compatibility workarounds.

## Acceptance Evidence

The first rfopt target test must verify all of the following points using the pinned rfopt submodule:

1. **Submodule verification:** Before Cargo compiles the rfopt path dependency, `scripts/run-rfopt-target.sh` verifies that:
   - The rfopt submodule worktree is clean.
   - The submodule `HEAD` commit matches the gitlink recorded in NanaST.
   Any mismatch fails the gate immediately. Running `cargo test` directly does not count as gate evidence.
2. **Exact token sequence:** The generated source matches the exact token sequence defined above.
3. **Handle resolution:** rfopt loads the source, resolves both `VARIABLE` definitions to opaque cell handles, and resolves both lifecycle words to executable function handles.
4. **Initialization:** Calling `nana-init` returns no value, and reading the output cell handle returns `0`.
5. **False pass-through:** Writing `0` to the input handle before running `nana-scan` produces `0` at the output handle.
6. **True pass-through:** Writing `1` to the input handle before running `nana-scan` produces `1` at the output handle.
7. **Non-zero handling:** The host-cell API can write `-1` (or another non-zero number) directly to the input handle without Forth parsing; running `nana-scan` afterward produces `1` at the output handle.
8. **Input preservation:** In a fresh runtime instance, writing `1` to the input handle before calling `nana-init` leaves the input intact; running `nana-scan` afterward produces `1`.
9. **Error reporting:** A missing word, invalid handle, or Forth load error fails the test with clear diagnostics (such as the word name or source location). Errors must never be reported as successful execution.

The test must run on an AMD64 Linux development host with the rfopt submodule initialized. It must not require SwiftForth, rtForth, BNC hardware, or BNC software.

## Boundaries

### Always
- Keep generated code strictly within the required Forth word inventory above.
- Commit runtime changes and unit tests in the `rfopt` repository, then update the submodule commit in NanaST as a separate commit.
- Keep the generated Forth fixture and test evidence alongside the integration test.

### Ask First
- Adding a new Forth word, public lifecycle word, or language feature.
- Changing word names, stack effects, Boolean encoding, or initialization rules.
- Making claims about real-time timing, memory bounds, safety, or BNC integration.
- Starting AArch64 work before the AMD64 gate passes.

### Never
- Add compatibility fallback definitions in NanaST for words missing in rfopt.
- Require complete Forth-2012 conformance from rfopt for this feature.
- Add BNC device mappings, hardware I/O, scheduling, or control policies into NanaST.
- Treat passing this compatibility test as proof of real-time performance.

## Open Questions

- What Rust types and error structures will implement rfopt's opaque host API?
- What exact command does `scripts/run-rfopt-target.sh` run after completing its pre-compilation checks?
- Which rfopt commit will be the first to satisfy the word inventory and pass the gate?
- Which real-world BNC control program will be used to define the subsequent real-time verification feature?
