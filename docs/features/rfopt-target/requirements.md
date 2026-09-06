---
type: "Feature Requirements"
title: "Requirements: rfopt Boolean Pass-Through Target"
description: "Draft requirements for the first NanaST Forth-subset artifact executed by rfopt."
status: "draft"
tags: [requirements, nanast, rfopt, forth-2012, pass-through]
---

# Requirements: rfopt Boolean Pass-Through Target

## Status

Draft for review. The approved [NanaST vision](../../VISION.md) selects rfopt
as the sole planned runtime and this feature's Boolean pass-through case as its
first compatibility gate. This artifact does not authorize implementation until
its requirements and open source-loading boundary are approved.

## Objective

Prove the smallest device-neutral NanaST-to-rfopt vertical slice. A NanaST
program with one Boolean input and one Boolean output translates to loadable
Forth source. rfopt loads that source and executes the lifecycle words so the
output equals the normalized input.

The control case is [`tests/fixtures/pass_through.st`](../../../tests/fixtures/pass_through.st):

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

The first target is the AMD64 Linux development host. rfopt must build and run
the generated artifact there before NanaST begins any AArch64 target work.

The current pinned rfopt revision does not build on this host because
`src/rforth.rs` imports AArch64-only `slow` code and uses the AArch64 `x9`
register. Restoring an AMD64 build is rfopt prerequisite work; its failure is
not compatibility evidence.

AArch64 is out of scope for this feature. A separate AArch64 target feature may
start only after every acceptance item below passes on AMD64 Linux and the
requirements are approved.

## Target Contract

### Generated artifact

The compiler produces Forth source. For this control case, it must define these
public words in the current Forth word list:

| Word | Stack effect | Contract |
|---|---|---|
| `nana-input-0` | `( -- a-addr )` | A cell that the host writes before a scan. |
| `nana-output-0` | `( -- a-addr )` | A cell from which the host reads the scan result. |
| `nana-init` | `( -- )` | Clears generated outputs; it does not change input cells. This feature has no persistent `VAR` state. |
| `nana-scan` | `( -- )` | Reads the input cell and writes the normalized Boolean result to the output cell. |

The output must be observationally equivalent to:

```forth
VARIABLE nana-input-0
VARIABLE nana-output-0

: nana-init ( -- )
  0 nana-output-0 ! ;

: nana-scan ( -- )
  nana-input-0 @ IF 1 ELSE 0 THEN nana-output-0 ! ;
```

Formatting and private helper words are not part of the contract. The public
names, stack effects, lifecycle, and observable Boolean behavior are.

### Boolean behavior

- A zero input cell represents `FALSE` and produces output cell value `0`.
- Any nonzero input cell represents `TRUE` and produces output cell value `1`.
- `nana-init` preserves a host-written input value and clears
  `nana-output-0` to `0`. Persistent `VAR` initialization is a later,
  separately inventoried target feature.
- NanaST's public I/O boundary never requires Forth's native true flag value
  (`-1`); rfopt may use Forth flags internally.

## Required Forth-2012 Inventory

rfopt need not implement all Forth-2012. It must implement the following
required words with the semantics needed by the target contract:

| Word | Forth-2012 word set | Required use |
|---|---|---|
| `:` | Core | Begin `nana-init` and `nana-scan` definitions. |
| `;` | Core | End those definitions. |
| `VARIABLE` | Core | Define each public I/O cell. |
| `@` | Core | Fetch the input cell during a scan. |
| `!` | Core | Store initialized and scanned output values. |
| `IF` | Core | Select the true or false output branch. |
| `ELSE` | Core | Select the false output branch. |
| `THEN` | Core | Close the conditional branch. |

The decimal cell literals `0` and `1` are source numbers, not required named
Forth words. rfopt must parse them as single-cell values for this artifact.

A missing inventory item is rfopt work. NanaST must record the gap and must not
supply a compatibility definition in generated source.

## Acceptance Evidence

The first rfopt target test must load the generated artifact and prove all of
the following on the rfopt revision pinned by NanaST's submodule:

1. Executing `nana-input-0` and `nana-output-0` leaves usable cell addresses:
   the host can store through the first and fetch through the second.
2. Executing `nana-init` leaves no value and makes `nana-output-0 @` equal `0`.
3. An input cell written with `0` before `nana-scan` produces output `0`.
4. An input cell written with `1` before `nana-scan` produces output `1`.
5. The selected host-cell API can write another nonzero value, including `-1`,
   without requiring the generated artifact or test source to parse `-1`; a
   later `nana-scan` produces output `1`.
6. An input written before `nana-init` survives initialization and produces the
   expected output after `nana-scan`.
7. A missing required word or a Forth load/execute error fails the test with the
   named word or source diagnostic; it must not be accepted as target support.

The test must run on the AMD64 Linux development host from a NanaST checkout
with the rfopt submodule initialized. It must not require SwiftForth, rtForth,
BNC hardware, or BNC software.

## Boundaries

### Always

- Keep the generated artifact within the inventory above.
- Commit rfopt implementation changes and its runtime-unit evidence in the
  rfopt repository, then advance NanaST's submodule revision in a separate
  NanaST commit.
- Retain NanaST's compiled-source artifact and pinned cross-repository execution
  evidence with the target test.

### Ask First

- Adding a Forth word, public target word, source-language construct, or
  target-specific extension.
- Changing the public names, stack effects, Boolean representation, or
  initialization behavior.
- Claiming real-time, memory, safety, or BNC integration behavior.
- Beginning AArch64 target work before the AMD64 acceptance gate passes.

### Never

- Add a NanaST fallback for an rfopt word gap.
- Require full Forth-2012 conformance from rfopt for this feature.
- Add BNC mapping, device I/O, migration, scheduling, or safety policy.
- Treat this compatibility test as real-time evidence.

## Open Questions

- What rfopt source-loading and host-cell API will load the generated source,
  invoke public words, and inject the nonzero `-1` test value?
- What AMD64 rfopt executor runs the target test, and what stable command
  invokes it from the NanaST checkout?
- Where should the cross-repository target test live?
- Which rfopt revision first satisfies the inventory and pass-through gate?
- What named BNC control case and measurable target conditions justify the
  later rfopt real-time evidence feature?
