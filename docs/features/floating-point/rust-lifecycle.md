---
type: "Rust Lifecycle Design"
title: "Rust Lifecycle Design: IEEE-754 REAL Support"
description: "Ownership and invocation boundaries for typed float cells and native float stacks."
status: "accepted"
language: "rust"
tags: [rust, lifecycle, nanast, rfopt, floating-point, stc]
---

# Rust Lifecycle Design: IEEE-754 REAL Support

## Ownership

| Resource | Owner | Release behavior |
|---|---|---|
| Integer `VARIABLE` storage | Private loaded runtime | Drops with `Runtime` |
| Binary64 `FVARIABLE` storage | Private loaded runtime | Drops with `Runtime` |
| Integer cell capability | Caller-held `CellHandle` with a weak runtime link | Cannot extend runtime lifetime |
| Float cell capability | Caller-held `FloatCellHandle` with a weak runtime link | Cannot extend runtime lifetime |
| Native executable allocation | `NativeWord` | Drops after the last strong owner drops |
| Per-invocation data, float, and return stacks | `NativeWord::invoke` stack frame | Drops when invocation returns |
| Caller floating-point control state | Invocation guard | Restored before `invoke` returns, including an error return |

`Runtime` remains single-threaded through `Rc<RefCell<RuntimeState>>`. A
`FloatCellHandle` follows the same weak-link rule as `CellHandle`. It can access
only an `FVARIABLE`; it cannot reinterpret integer storage.

## Invocation State

```text
Saved caller FP state
  -> profile FP state + empty data/float/return stacks
  -> native word executes
  -> both stack pointers checked
  -> caller FP state restored
```

The profile control state is established before the native entry point receives
control. The restoration guard runs after normal completion and after a
stack-imbalance or integer-division error. It does not turn IEEE non-finite
values into `ExecutionError`.

## Load and Handle Failure Behavior

`Runtime::load` builds typed storage, validates definitions, and emits native
words in local values. It publishes the loaded runtime only after every step
succeeds. A failed load leaves the runtime empty and all local resources drop.

A typed resolution mismatch reports a lookup failure. A used handle after the
parent runtime drops reports `InvalidHandle` without dereferencing storage.

## Verification Obligations

- Verify that cloned float handles share one float cell and fail safely after
  runtime drop.
- Verify that resolving an integer name as float, or a float name as integer,
  fails without exposing storage.
- Verify that stack-improper generated source cannot enter native execution.
- Verify the caller floating-point control and status state after successful
  and failed native invocation.
