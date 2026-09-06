---
type: "Candidate Direction"
title: "Candidate Direction: rfopt as NanaST Runtime"
description: "Accepted direction to co-develop NanaST against the independently versioned rfopt runtime."
status: "accepted"
tags: [idea, nanast, rfopt, forth, bnc]
---

# Candidate Direction: rfopt as NanaST Runtime

## Status

Sirius Wu accepted this direction on 2026-09-06. The governing policy is the
approved [NanaST vision](../VISION.md); active requirements are owned by the
[rfopt Boolean Pass-Through Target](../features/rfopt-target/requirements.md).
This artifact retains the considered direction and does not own evolving
requirements.

## Problem Statement

How can NanaST gain a runtime target suited to BNC without requiring a complete
Forth-2012 implementation, embedding Forth runtime implementation in NanaST,
or beginning BNC migration before a minimal generated artifact works?

## Recommended Direction

Track rfopt as a Git submodule pinned by NanaST. rfopt remains an independently
versioned project: runtime changes are committed in rfopt, then NanaST advances
its pinned revision and runs cross-repository target evidence.

rfopt is NanaST's sole planned runtime only while it continues to earn that
selection through evidence that it can satisfy BNC's needs. NanaST will emit
only the explicitly inventoried Forth-2012 subset that rfopt implements and
verifies for the target; it does not claim full Forth-2012 conformance. The
first gate is that rfopt loads and executes the generated Boolean pass-through
artifact. rfopt real-time evidence is required for any later real-time claim.

## Key Assumptions to Validate

- [ ] rfopt can implement the first required-word inventory and a source-loading
  boundary sufficient to execute the pass-through artifact.
- [ ] The pass-through result can be tested on the AMD64 Linux development host
  from a NanaST checkout with the pinned rfopt submodule.
- [ ] Later rfopt evidence can establish the BNC-relevant runtime properties
  without placing BNC integration in NanaST.
- [ ] BNC can assess and own a later incremental migration from rtForth to
  rfopt after these gates pass.

## MVP Scope

- Add the rfopt submodule.
- Specify and generate the Boolean pass-through Forth artifact.
- Implement the required subset in rfopt on AMD64 Linux.
- Run the artifact on rfopt and retain AMD64 compatibility evidence.

AArch64 target work begins only after the AMD64 NanaST target gate passes.

## Not Doing

- **Git subtree:** It would blur rfopt ownership and make later independent BNC
  adoption harder.
- **Full Forth-2012:** rfopt only needs the inventory that NanaST requires.
- **SwiftForth or rtForth target support:** rfopt is the sole planned runtime.
- **AArch64 target work:** It waits until the AMD64 NanaST target gate passes.
- **Real-time claim:** The compatibility gate alone is not timing, memory, or
  safety evidence.
- **BNC integration or migration:** BNC owns that later work.
