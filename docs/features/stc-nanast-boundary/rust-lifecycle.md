---
type: "Rust Lifecycle Design"
title: "Rust Lifecycle Design: STC NanaST Execution Boundary"
description: "Ownership and failure design for opaque NanaST handles over native STC words."
status: "accepted"
language: "rust"
tags: [rust, lifecycle, nanast, rfopt, stc]
---

# Rust Lifecycle Design: STC NanaST Execution Boundary

## Ownership

| Resource | Owner | Release behavior |
|---|---|---|
| Loaded definitions and stable cells | Private `RuntimeState` | Drops with `Runtime` |
| Native STC allocation | `NativeWord` | Drops after its last strong `Rc` owner |
| Word and cell capability | Caller-held handle with `Weak<RuntimeState>` | Cannot extend runtime lifetime |
| Per-invocation STC data and return stacks | `NativeWord::invoke` stack frame | Drops when invocation returns |

`Runtime` uses `Rc<RefCell<RuntimeState>>`, so it is single-threaded and is
neither `Send` nor `Sync`. `WordHandle::invoke` upgrades its weak link and
clones the selected `Rc<NativeWord>` before it releases the state borrow. This
keeps the executable allocation and embedded cell addresses valid throughout
the native call.

## State Transitions

```text
Empty --load succeeds--> Loaded --Runtime drops--> Dropped
  ^          |
  |          +--load fails--+
  +-------------------------+
```

`load` constructs a complete private program before changing `Empty` to
`Loaded`. It returns `AlreadyLoaded` after a successful load. A failed load
returns an error and leaves the runtime empty, so the caller may retry.

## Native Invocation

A lifecycle word has the validated stack effect `( -- )`. `NativeWord::invoke`
creates a fixed data stack, runs the architecture-specific `ExecutableWord`,
and verifies that the data-stack pointer returns to its entry position. It does
not expose the generated entry point to the caller.

## Failure and Cleanup

| Failure | Result | Cleanup |
|---|---|---|
| Parse, validation, or STC emission error | `RuntimeError::Load` | Local cells and emitted words drop; runtime stays empty |
| Missing name | `WordNotFound` or `CellNotFound` | No state change |
| Runtime already dropped | `InvalidHandle { RuntimeDropped }` | No dereference |
| Dynamic divide by zero or overflow | Named `Execution` error | The generated word completes; prior cell writes remain visible |
| Changed STC stack pointer | `Execution { StackImbalance }` | Invocation-local stacks drop |

No task, callback, blocking cleanup, or explicit shutdown operation exists in
this boundary.
