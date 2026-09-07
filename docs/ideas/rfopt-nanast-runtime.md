---
type: "Candidate Direction"
title: "Candidate Direction: rfopt as NanaST Runtime"
description: "Accepted direction to co-develop NanaST against the independently versioned rfopt runtime."
status: "accepted"
tags: [idea, nanast, rfopt, forth, bnc]
---

# Candidate Direction: rfopt as NanaST Runtime

## Status

Sirius Wu accepted this candidate direction on 2026-09-06. The overarching policy is defined in the approved [NanaST vision](../VISION.md). Active requirements and architecture are owned by the [rfopt Boolean Pass-Through Target](../features/rfopt-target/requirements.md) and its [architecture](../features/rfopt-target/architecture.md). This document preserves the original direction and context.

## Problem Statement

How can NanaST gain a runtime target suitable for BNC without:
- Requiring a full Forth-2012 implementation,
- Embedding Forth runtime internals directly inside NanaST, or
- Starting BNC migration before verifying a minimal generated program?

## Recommended Direction

Track `rfopt` as a pinned Git submodule in NanaST. `rfopt` remains an independently versioned project:
1. Runtime changes are committed in the `rfopt` repository.
2. NanaST updates its pinned submodule commit and runs cross-repository integration tests.

`rfopt` serves as NanaST's primary planned runtime as long as test results show it satisfies BNC's requirements. NanaST emits only the explicitly listed Forth-2012 words that `rfopt` implements for this target. The first milestone is loading and executing the generated Boolean pass-through program. Real-time claims will require separate benchmark evidence in `rfopt`.

## Key Assumptions to Validate

- [ ] `rfopt` can implement the initial word inventory and provide an in-memory API to load and execute the pass-through program.
- [ ] The pass-through test can run on an AMD64 Linux development host using NanaST and the pinned `rfopt` submodule.
- [ ] Subsequent test evidence in `rfopt` can prove required runtime properties without coupling BNC hardware code into NanaST.
- [ ] BNC can evaluate and manage a gradual migration from `rtforth` to `rfopt` after these milestones pass.

## MVP Scope

- Add `rfopt` as a Git submodule.
- Specify and generate the Forth source for the Boolean pass-through program.
- Implement the required Forth words in `rfopt` on AMD64 Linux.
- Run the pass-through artifact on `rfopt` and verify compatibility on AMD64.

Work on the AArch64 target begins only after the AMD64 gate passes.

## Non-Goals (What We Are Not Doing)

- **Git subtree:** Using subtrees would blur project ownership and make independent BNC adoption harder.
- **Full Forth-2012 standard:** `rfopt` only needs the specific Forth words that NanaST generates.
- **SwiftForth or rtForth compiler targets:** `rfopt` is the only planned runtime target.
- **Immediate AArch64 work:** AArch64 development is deferred until AMD64 verification succeeds.
- **Unverified real-time claims:** Passing the initial compatibility gate does not prove timing, memory, or safety limits.
- **Direct BNC hardware migration:** BNC manages its own migration and hardware integration.
