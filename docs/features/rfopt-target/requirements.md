---
type: "Feature Requirements"
title: "Requirements: rfopt Boolean Pass-Through Target"
description: "Draft requirements for the first NanaST Forth-subset artifact executed by rfopt."
status: "proposed"
tags: [requirements, nanast, rfopt, forth-2012, pass-through]
---

# Requirements: rfopt Boolean Pass-Through Target

## Status

Proposed for review. The approved [NanaST vision](../../VISION.md) selects
rfopt as the sole planned runtime and this feature's Boolean pass-through case
as its first compatibility gate. Its in-process host boundary is approved in
[architecture.md](architecture.md). This artifact does not authorize
implementation until the proposed requirements receive approval.

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

## Host Execution Boundary

rfopt provides the in-process host boundary for this feature. It loads the
generated source from memory, resolves and invokes its public words, and
exposes I/O cells only through opaque host handles. The NanaST AMD64 integration
test links this API from the pinned rfopt submodule; it is not a CLI test and
adds no Forth words.

The host boundary must write the nonzero `-1` test value through a cell handle,
not by parsing additional Forth source. rfopt owns runtime-unit evidence for
this boundary; NanaST owns the pinned cross-repository pass-through gate. See
[architecture.md](architecture.md) for the selected structure and failures.

## Target Contract

### Generated artifact

The compiler produces Forth source. For this control case, it must define these
public words in the current Forth word list:

| Word | Stack effect | Contract |
|---|---|---|
| `nana-input-0` | `( -- a-addr )` | Defines the input cell. rfopt maps its Forth address to an opaque host-cell handle for host writes before a scan. |
| `nana-output-0` | `( -- a-addr )` | Defines the output cell. rfopt maps its Forth address to an opaque host-cell handle for host reads after a scan. |
| `nana-init` | `( -- )` | Clears generated outputs; it does not change input cells. This feature has no persistent `VAR` state. |
| `nana-scan` | `( -- )` | Reads the input cell and writes the normalized Boolean result to the output cell. |

After normalizing whitespace, the generated source must have exactly this token
sequence:

```forth
VARIABLE nana-input-0 VARIABLE nana-output-0 : nana-init 0 nana-output-0 ! ; : nana-scan nana-input-0 @ IF 1 ELSE 0 THEN nana-output-0 ! ;
```

It defines exactly the two `VARIABLE` words and two `:` definitions named
above. It contains no Forth comments, private helper definitions, or additional
tokens. Whitespace is the only permitted formatting variation.

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

The first rfopt target test must prove all of the following on the rfopt
revision pinned by NanaST's submodule:

1. Before Cargo compiles the rfopt path dependency, the checked-in
   `scripts/run-rfopt-target.sh` gate runner verifies that the rfopt submodule
   worktree is clean and that its `HEAD` object ID equals the `rfopt` gitlink
   object ID in the tested NanaST commit. A mismatch fails the gate before
   runtime execution; direct `cargo test` is not target-gate evidence.
2. The generated source has exactly the required token sequence above.
3. rfopt loads the source, resolves both `VARIABLE` words to opaque host-cell
   handles, and resolves both lifecycle words to executable handles.
4. Invoking `nana-init` completes with no returned value; reading the output
   through its opaque host-cell handle returns `0`.
5. Writing `0` through the input handle before `nana-scan` produces output `0`
   through the output handle.
6. Writing `1` through the input handle before `nana-scan` produces output `1`
   through the output handle.
7. The host-cell API can write another nonzero value, including `-1`, without
   requiring the generated artifact or test source to parse `-1`; a later
   `nana-scan` produces output `1` through the output handle.
8. In a fresh runtime instance, writing `1` through the input handle before
   `nana-init`, then invoking `nana-init` and `nana-scan`, produces output `1`.
   This proves initialization preserves input cells.
9. A missing required word or an induced Forth load, lookup, or handle error
   fails the test with named word or source context; it must not be accepted as
   target support.

The test must run on the AMD64 Linux development host from a NanaST checkout
with the rfopt submodule initialized and validated as above. It must not
require SwiftForth, rtForth, BNC hardware, or BNC software.

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

- What Rust types and error model implement rfopt's approved opaque host API?
- What exact rfopt invocation does `scripts/run-rfopt-target.sh` use after its
  pre-compilation checks?
- Which rfopt revision first satisfies the inventory and pass-through gate?
- What named BNC control case and measurable target conditions justify the
  later rfopt real-time evidence feature?
