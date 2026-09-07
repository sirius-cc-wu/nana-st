---
type: "Rust Lifecycle Design"
title: "Rust Lifecycle Design: rfopt AMD64 Host API"
description: "Accepted ownership, native-code lifetime, opaque handles, and error behavior for the first rfopt host API."
id: "rfopt-amd64-host-api-lifecycle"
status: "accepted"
language: "rust"
revision: "rfopt 849cc2a"
tags: [design, rust, lifecycle, rfopt, amd64, forth]
---

# Rust Lifecycle Design: rfopt AMD64 Host API

## At a Glance

rfopt will provide one single-threaded `Runtime` that loads one Forth source
string, compiles the approved subset into AMD64 machine code, and owns both the
cells and executable allocations. `WordHandle` and `CellHandle` values expose
safe host operations without exposing a dictionary pointer, a cell address, or
a code pointer.

Sirius Wu approved this lifecycle design on 2026-09-06. Each handle stores a
weak reference to its runtime state. A handle fails with a named invalid-handle
error after its runtime drops. Source loading builds a complete private program
before rfopt publishes any definitions or executable code. A failed load leaves
the runtime empty.

The first implementation supports only the inventory in
[requirements.md](requirements.md). It does not provide a general interpreter,
a CLI, shared memory, I/O, BNC integration, AArch64 execution, or a claim about
real-time behavior.

## Design Context and Responsibility Inputs

- **System boundary:** NanaST passes generated source from memory to rfopt. It
  resolves lifecycle words and I/O cells, invokes the words, and reads or writes
  cell values through opaque handles.
- **Representative vertical scenario:** The gate loads the approved Boolean
  pass-through source, calls `nana-init`, writes an input cell, calls
  `nana-scan`, and reads a normalized output cell.
- **Verification oracle:** rfopt unit tests prove loading, lookup, native word
  execution, handle invalidation, and diagnostics. The later NanaST integration
  test proves the complete compiler-to-rfopt pass-through flow.
- **Compatibility obligations:** The runtime accepts only `:`, `;`, `VARIABLE`,
  `@`, `!`, `IF`, `ELSE`, `THEN`, and literals `0` and `1`. It preserves the
  required word names, stack effects, initialization rule, and Boolean result.

| Native responsibility | Selected owner | Lifecycle consequence |
|---|---|---|
| Own source-loading state, definitions, cells, and executable allocations | `host::Runtime` | The runtime is the only strong owner of the private program. |
| Parse and validate source before publication | Private `loader` module | It returns a complete load product or a source-located error. |
| Emit and own one AMD64 function for each colon definition | Private `native` module and `NativeWord` | Executable allocations stay alive until their runtime drops. |
| Resolve executable definitions and cells | `Runtime` | It creates opaque handles with a weak link to this runtime only. |
| Invoke a lifecycle word | `WordHandle` | It upgrades the weak link, validates its word slot, then calls private native code. |
| Read or write an I/O cell | `CellHandle` | It upgrades the weak link and validates its cell slot before access. |
| Report load, lookup, handle, and execution failures | `RuntimeError` | Every host-visible failure has a named variant and relevant name or source location. |

## Design Forces

- The approved host boundary requires in-memory source loading and opaque word
  and cell handles.
- rfopt's approved vision requires native subroutine threading. The AMD64 gate
  must execute generated Forth definitions as native machine code, not through
  a general-purpose interpreter.
- The first source has two `VARIABLE` declarations and two colon definitions.
  The definitions use only literals, cell fetch/store, and one conditional.
- The host needs to write non-zero values such as `-1` without Forth parsing.
- NanaST must not observe raw Forth addresses, dictionary pointers, or native
  function pointers.
- This gate has no concurrent callers, I/O callbacks, tasks, explicit shutdown,
  or fallible cleanup requirement.

## Ownership and Capability Model

| Resource or capability | Created by | Owner while prepared | Transfer | Owner while running | Explicit release | `Drop` fallback |
|---|---|---|---|---|---|---|
| Source tokens and compiled definitions | `loader::parse` | Local load product | Successful `Runtime::load` commit | Private runtime state | None | Local values drop on failure; runtime state drops with `Runtime` |
| I/O cells | `loader::parse` | Local boxed `UnsafeCell<Cell>` array | Successful load commit | Private runtime state | None | Boxed cells drop with runtime state |
| AMD64 executable allocation | `native::emit` | Local `NativeWord` | Successful load commit | Private runtime state | None | Allocation unmaps when `NativeWord` drops |
| `WordHandle` capability | `Runtime::resolve_word` | Caller | None | Caller, with a weak runtime link | None | Weak link cannot keep runtime memory alive |
| `CellHandle` capability | `Runtime::resolve_cell` | Caller | None | Caller, with a weak runtime link | None | Weak link cannot keep runtime memory alive |

`Runtime` does not implement `Clone`. Handles may implement `Clone`, but they
hold only `Weak<RefCell<RuntimeState>>`. Therefore, a handle cannot keep cells
or executable code alive after the parent runtime drops. `Rc` and `RefCell`
make `Runtime`, `WordHandle`, and `CellHandle` neither `Send` nor `Sync`. The
first gate runs all host operations on one thread.

The runtime stores its cells in a `Box<[UnsafeCell<Cell>]>`. The boxed
allocation does not move after a successful load. Both native code and handle
operations access a cell only through its `UnsafeCell::get()` raw pointer. They
never create `&Cell` or `&mut Cell` references to the cell storage. This
interior-mutability boundary permits the native emitter to retain a private
cell pointer while safe host calls read and write the same cell. The native
emitter may embed that pointer in a private machine instruction, but it never
crosses the host API.

## Lifecycle States

```mermaid
stateDiagram-v2
    [*] --> Empty: Runtime::new
    Empty --> Loading: Runtime::load
    Loading --> Empty: load error; discard local product
    Loading --> Loaded: atomically publish source, cells, and code
    Loaded --> Dropped: Runtime drops
    Empty --> Dropped: Runtime drops
    Dropped --> [*]
```

`Runtime::new` creates an empty runtime. `Runtime::load` takes `&mut self` and
may run once. A successful load moves the runtime to `Loaded`. A second load
returns `RuntimeError::AlreadyLoaded` and does not replace cells or invalidate
existing handles. A failed first load returns the runtime to `Empty`, so the
caller may load a corrected source.

Handles are capabilities, not lifecycle states. Each handle operation upgrades
its weak link. If that fails, the operation returns
`RuntimeError::InvalidHandle { kind, reason: RuntimeDropped }`.

## Loading, Validation, and Native Emission

1. `Runtime::load` tokenizes the supplied `&str` and records line and column
   for every token.
2. The loader parses the approved top-level forms: `VARIABLE` and colon
   definitions. A user-defined name cannot equal a supported control or
   primitive token. The loader rejects an unknown token, duplicate or reserved
   name, malformed definition, unsupported literal, or unbalanced conditional
   with a source-located `LoadError`.
3. The loader creates a private dictionary and a boxed cell array in local
   temporary values. `VARIABLE` reserves one zero-initialized cell and records
   its private index.
4. The compiler checks each colon definition with an abstract stack of private
   integer and address values. `@` requires an address and yields an integer.
   `!` requires an integer beneath an address. `IF` requires an integer flag
   and both branches must have the same abstract stack shape. Every supported
   lifecycle definition must finish with an empty stack.
5. The AMD64 emitter writes the validated definition to a temporary allocation
   with read-write protection. It finalizes that allocation as read-execute
   before publication. It uses the fixed ABI in [AMD64 Native Entry ABI](#amd64-native-entry-abi).
   It does not call host code.
6. If all parsing, validation, allocation, and finalization steps succeed,
   `Runtime::load` publishes the complete product in one replacement of the
   empty private state. If any step fails, local cells and allocations drop and
   the runtime remains empty.

For this subset, an emitted colon definition uses the machine stack only while
that definition runs. For a variable reference, the emitter loads its private
64-bit cell address with `movabs` into `%rax`, then pushes `%rax`; AMD64 cannot
push a 64-bit immediate address directly. It emits load/store instructions for
`@` and `!`, and branches for `IF`, `ELSE`, and `THEN`. A colon definition must
leave that stack balanced before `ret`. The runtime does not expose this
internal stack or support general Forth stack persistence between host
invocations.

The emitter implements `IF` by testing its fetched integer flag against zero
and selecting the source's true or false instruction sequence. It does not
insert Boolean values itself. In the exact approved `nana-scan` source, the
true branch contains literal `1` and the false branch contains literal `0`.
Therefore, a host-written `-1` selects the true branch without requiring the
source loader to parse `-1`.

## AMD64 Native Entry ABI

Each emitted colon definition has the private entry type
`unsafe extern "C" fn()`. `WordHandle::invoke` is safe because rfopt creates
and validates every entry; callers cannot supply a function pointer.

The emitter and invocation wrapper must meet these rules on AMD64 Linux:

- The entry follows the System V AMD64 ABI. The Rust wrapper calls it normally,
  so `%rsp` equals 8 modulo 16 at entry.
- The emitted code restores `%rsp` exactly to its entry value before `ret`.
  It uses balanced `push` and `pop` pairs and makes no calls. A future emitted
  call must align `%rsp` to 16 bytes before that call and restore it afterward.
- The emitted code may clobber only `%rax` and `%rcx` in this subset. It
  preserves `%rbx`, `%rbp`, and `%r12` through `%r15`. It does not modify the
  direction flag.
- The code cannot panic, unwind, long-jump, invoke a host callback, or access a
  host-provided code pointer. It returns only with `ret`.
- The emitter starts with a read-write allocation, copies complete code, then
  changes it to read-execute. It never publishes or invokes a writable
  executable allocation.
- The new emitter does not call or reuse the historical
  `unsafe fn(isize, isize)` x64 primitive ABI. That ABI has different stack and
  register assumptions and is outside this host API.

`WordHandle::invoke` upgrades its weak link into a local strong `Rc`, borrows
state only long enough to validate the word slot and copy its private entry
pointer, then releases the `RefCell` borrow before the native call. The local
strong `Rc` keeps the cell and executable allocations alive for the entire
call. No host callback or cross-thread access exists, so no public operation
can re-enter the runtime while generated code runs.

## Failure, Rollback, and Cleanup

| Failure point | Resources acquired or started | Required compensation | Primary result | Cleanup evidence |
|---|---|---|---|---|
| Tokenization or parse failure | Local tokens and definitions | Drop local values | `RuntimeError::Load` with source location | Runtime remains empty |
| Abstract-stack validation failure | Local dictionary and cells | Drop local values | `RuntimeError::Load` with word and source location | Runtime remains empty |
| Native allocation or finalization failure | Zero or more temporary executable allocations | Drop all temporary allocations | `RuntimeError::Load` with native allocation context | Runtime remains empty |
| Lookup on an empty runtime | Empty runtime | None | `RuntimeError::NotLoaded` | No state change |
| Missing word or cell | Loaded runtime | None | `RuntimeError::WordNotFound` or `CellNotFound` with name | No state change |
| Handle operation after runtime drop | Weak handle only | None | `RuntimeError::InvalidHandle` with handle kind and `RuntimeDropped` | No dereference occurs |
| Internal slot validation failure | Upgraded runtime state | None | `RuntimeError::InvalidHandle` with handle kind and slot reason | No raw pointer access occurs |

No task, process, file, socket, or external reservation exists in this design.
No explicit terminal method is needed. Dropping `Runtime` synchronously drops
its private state and releases its cells and executable allocations. `Drop`
does not invoke generated code, block, or report a cleanup result.

## Rust Type and API Sketch

```rust
// API summary for rfopt 849cc2a; src/host/mod.rs is the canonical declaration.

pub mod host {
    pub type Cell = isize;

    pub struct Runtime {
        // Private Rc<RefCell<RuntimeState>>. RuntimeState owns cells,
        // dictionary entries, and NativeWord executable allocations.
    }

    #[derive(Clone)]
    pub struct WordHandle {
        // Private Weak<RefCell<RuntimeState>> and private word slot.
    }

    #[derive(Clone)]
    pub struct CellHandle {
        // Private Weak<RefCell<RuntimeState>> and private cell slot.
    }

    #[derive(Debug, Clone, Copy, PartialEq, Eq)]
    pub struct SourceLocation {
        // Both fields use one-based source coordinates.
        pub line: usize,
        pub column: usize,
    }

    #[derive(Debug)]
    pub struct LoadError {
        // None only when host allocation or protection fails without a source token.
        pub location: Option<SourceLocation>,
        pub kind: LoadErrorKind,
    }

    #[derive(Debug)]
    pub enum LoadErrorKind {
        UnexpectedToken { token: String },
        UnsupportedLiteral { literal: String },
        UnknownWord { name: String },
        DuplicateName { name: String },
        MalformedVariable,
        UnbalancedControl,
        ControlNestingLimit { limit: usize },
        DataStackLimit { limit: usize },
        StackEffect { word: String, detail: String },
        NativeEmission { word: String, operation: String, detail: String },
    }

    #[derive(Debug, Clone, Copy, PartialEq, Eq)]
    pub enum HandleKind {
        Word,
        Cell,
    }

    #[derive(Debug, Clone, Copy, PartialEq, Eq)]
    pub enum HandleInvalidReason {
        RuntimeDropped,
        UnknownSlot,
    }

    #[derive(Debug)]
    pub enum ExecutionError {
        EntryUnavailable,
    }

    #[derive(Debug)]
    pub enum RuntimeError {
        Load(LoadError),
        AlreadyLoaded,
        NotLoaded,
        WordNotFound { name: String },
        CellNotFound { name: String },
        InvalidHandle { kind: HandleKind, reason: HandleInvalidReason },
        Execution { word: String, reason: ExecutionError },
    }

    impl Runtime {
        pub fn new() -> Self;
        pub fn load(&mut self, source: &str) -> Result<(), RuntimeError>;
        pub fn resolve_word(&self, name: &str) -> Result<WordHandle, RuntimeError>;
        pub fn resolve_cell(&self, name: &str) -> Result<CellHandle, RuntimeError>;
    }

    impl WordHandle {
        pub fn invoke(&self) -> Result<(), RuntimeError>;
    }

    impl CellHandle {
        pub fn read(&self) -> Result<Cell, RuntimeError>;
        pub fn write(&self, value: Cell) -> Result<(), RuntimeError>;
    }
}
```

`resolve_word` returns a handle only for a colon definition. `resolve_cell`
returns a handle only for a `VARIABLE` definition. Both methods return
`RuntimeError::NotLoaded` while the runtime is empty. After a successful load,
a lookup for any other name returns the relevant named lookup error.

`LoadError` includes one-based source line and column coordinates for a source
failure and a specific parse or validation reason. A native allocation or
protection error may have no source location, but its `NativeEmission` variant
includes the word, operation, and native failure detail. `ExecutionError` is
reserved for an internal native entry failure that rfopt can detect without
exposing unsafe details. The first emitter has no host callback or recoverable
machine-code failure after successful validation, so normal `invoke` calls
return `Ok(())`.

`RuntimeError`, `LoadError`, `LoadErrorKind`, `HandleKind`,
`HandleInvalidReason`, and `ExecutionError` implement `Display` and
`std::error::Error` where applicable. A missing-name message includes the
requested name. A load message includes `line:column` and the offending token,
name, or literal when present. An invalid-handle message includes its kind and
reason. The API does not collapse these failures into a Boolean or a generic
success result.

`HandleKind` distinguishes word and cell diagnostics. `HandleInvalidReason`
includes `RuntimeDropped` and an internal slot-validation failure. The fields
that contain a weak runtime link, dictionary slot, cell slot, raw address, or
native entry point remain private.

The public API stays in a new AMD64-only `host` module. It uses only safe Rust
at its boundary. The private native emitter contains the small, audited unsafe
boundary for executable memory and function-pointer invocation.

## Concurrency Model

This feature has one in-process caller and no spawned work. `Rc<RefCell<_>>`
keeps the API local to one thread and prevents accidental cross-thread sharing.
The first design adds no channels, locks, atomics, traits, or asynchronous
cleanup.

A later runtime may need a different ownership model only when an approved
feature introduces concurrent calls, host callbacks, or shared-memory I/O.
That feature must define its synchronization and execution guarantees before
replacing this model.

## Invariants

- The host API never returns a raw Forth cell address, dictionary pointer, or
  native function pointer.
- Each public handle refers to exactly one runtime state while that state lives.
- A handle cannot keep its runtime alive and cannot dereference state after the
  runtime drops.
- A failed load publishes no cell, definition, or executable allocation.
- A loaded runtime cannot load a second source or replace a handle's target.
- All generated lifecycle definitions use only the approved source inventory.
- Native code uses only private cell addresses whose allocations outlive the
  executable word.
- Each generated lifecycle definition balances its machine stack and returns
  with no Forth value.
- `nana-init` writes only output cells. `nana-scan` writes `0` or `1` based on
  whether its input cell equals zero.

## Verification Obligations

- **Focused lifecycle tests:** Verify an empty runtime rejects lookup; a failed
  load leaves it empty; a second successful load is rejected; and all load
  errors include a source location.
- **Source-boundary tests:** Reject unsupported literals such as `-1` and `2`,
  unknown words, tokens outside a supported form, malformed `VARIABLE` forms,
  duplicate or reserved names, unsafe control nesting or data-stack depth, and
  misplaced, missing, or unbalanced `IF`, `ELSE`, and `THEN` forms.
- **Focused host API tests:** Verify missing names report the requested name;
  the exact source resolves both cell handles and both word handles; and cloned
  handles access the same private cell.
- **Focused execution tests:** Dirty the output cell, write `-1` to the input
  cell, then invoke `nana-init`. Assert directly that output is `0` and input
  remains exactly `-1`. In fresh runtimes, verify scan output for input `0`,
  `1`, and host-written `-1`.
- **Focused ownership tests:** Keep a word handle after its runtime drops and
  verify `invoke` returns `InvalidHandle` with `RuntimeDropped`. Keep a cell
  handle after its runtime drops and verify both `read` and `write` return the
  same named error without panicking.
- **Native-code evidence:** A private AMD64 harness sets sentinel values in all
  callee-saved registers, records `%rsp`, calls a finalized entry, and verifies
  register preservation, unchanged `%rsp`, and a clear direction flag. Private
  emitter tests verify finalized allocations are read-execute and execute a
  validated instruction sequence without a Rust or interpreter fallback.
- **Integration:** `tests/rfopt_target.rs` compiles `pass_through.st`, verifies
  the exact source tokens, and repeats the host API sequence through the
  pinned submodule. `scripts/run-rfopt-target.sh` performs the required
  submodule checks before it runs that test.
- **Human-owned validation:** No hardware, real-time, memory-bound, BNC, or
  AArch64 claim is validated by these tests.

## Completion Boundary

- **Result:** rfopt implements the accepted host API in commit `849cc2a`.
  NanaST emits the source and runs the AMD64 integration gate through the
  pinned submodule.
- **Evidence:** The runtime unit tests cover ownership, diagnostics, load
  atomicity, native-code lifetime, and the entry ABI. The NanaST gate verifies
  the generated source and end-to-end Boolean behavior.
- **Limit:** This evidence does not establish real-time timing, memory bounds,
  BNC integration, safety certification, or AArch64 support.

## Deferred Abstractions

| Candidate | Why deferred | Trigger to reconsider |
|---|---|---|
| General Forth interpreter | The approved source subset must compile to native AMD64 code. | An approved non-native tool or test feature needs interpretation. |
| General Forth data or return stack | The approved lifecycle definitions have validated empty entry and exit stacks. | An approved source needs persistent or cross-word stack effects. |
| Parser or code-generator trait | The crate has one AMD64 loader and emitter. | A second supported backend or independently varying parser appears. |
| AArch64 backend | AArch64 is outside the first gate. | The AMD64 gate passes and approved AArch64 requirements exist. |
| Thread-safe runtime or shared memory | The gate has one local test caller and no concurrent I/O. | An approved feature requires cross-thread access or shared memory. |
| Explicit shutdown API | Runtime cleanup is synchronous and infallible. | A later resource needs ordered, asynchronous, or fallible cleanup. |

## Traceability

- [Requirements: rfopt Boolean Pass-Through Target](requirements.md)
- [Architecture: rfopt AMD64 Pass-Through Gate](architecture.md)
- [NanaST vision](../../VISION.md)
- [rfopt vision](../../../rfopt/VISION.md)
- rfopt commit `849cc2a` (`feat: add AMD64 opaque host runtime`)
