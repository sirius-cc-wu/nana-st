---
type: "Software Architecture Design"
title: "Architecture: rfopt AMD64 Pass-Through Gate"
description: "In-process host API between NanaST and rfopt for the first generated Forth test on AMD64."
status: "accepted"
tags: [architecture, design, nanast, rfopt, amd64, forth]
---

# Architecture: rfopt AMD64 Pass-Through Gate

## Architecture Question

How can the first AMD64 NanaST target test:
1. Load generated Forth source,
2. Invoke its public lifecycle words, and
3. Inspect input/output (I/O) cells,

without adding extra Forth words, requiring a CLI, or coupling either project to BNC?

On 2026-09-06, Sirius Wu approved an in-process host API owned by rfopt. The [requirements](requirements.md) document defines the target behavior and the list of supported Forth words.

## Significant Drivers

- **Approved behavior:** rfopt loads generated Boolean pass-through source, runs `nana-init` and `nana-scan`, and lets the host verify canonical `0` and `1` outputs.
- **Target platform:** The initial execution target is the AMD64 Linux development host. AArch64 support will follow after AMD64 verification succeeds.
- **Forth language boundary:** Generated source requires only `:`, `;`, `VARIABLE`, `@`, `!`, `IF`, `ELSE`, and `THEN`. Host operations must not add new Forth language dependencies.
- **Failure handling:** If loading, word lookup, cell access, or execution fails, the system must return a clear, named runtime error. The test suite must never mistake an error for supported behavior.
- **Non-goals:** This gate excludes CLI tools, terminal or file I/O, BNC hardware, shared-memory I/O, real-time performance guarantees, and AArch64 execution.

## Context and Boundaries

```text
NanaST gate runner (AMD64 Linux)
  | verifies clean rfopt gitlink, then starts integration test
  v
NanaST integration test
  | compiles pass_through.st to Forth source
  v
rfopt in-process host API
  | loads source; resolves and invokes public words;
  | manages dictionary, execution, and opaque cell handles
  v
rfopt runtime
```

### Component Responsibilities
- **NanaST:** Responsible for the compiler, generated Forth source, and the integration test runner.
- **rfopt:** Responsible for source loading, word execution, cell storage, and its internal unit tests.
- **Integration scope:** In this initial feature, the NanaST integration test is the only caller of the rfopt host API. BNC hardware is completely outside this scope.

## Selected Architecture

rfopt provides an in-process API with the following core operations:

| Operation | Owner | Contract |
|---|---|---|
| **Load source** | rfopt | Accepts Forth source code from memory. Creates definitions or returns a load error. |
| **Resolve public word** | rfopt | Finds a named generated word. Returns an opaque executable handle or a lookup error. |
| **Invoke word** | rfopt | Executes an opaque word handle. Completes with the expected stack effect or returns an execution error. |
| **Obtain cell handle** | rfopt | Resolves a `VARIABLE` definition internally and returns an opaque cell handle. The host never sees the internal Forth memory address. |
| **Read or write cell** | rfopt | Reads or writes a cell value through its handle. This lets tests inject `-1` directly without needing Forth source to parse it. |

Exact Rust types, ownership models, and error enums will be defined during detailed design. 

The API must keep dictionary pointers and raw runtime memory private from NanaST. Cell and word handles become invalid when their parent runtime instance is dropped. rfopt enforces this lifecycle.

### Gate Runner and Test Flow

NanaST will add a gate runner script at `scripts/run-rfopt-target.sh`. Running `cargo test` directly does not count as gate verification. The runner script must verify two prerequisites before compiling:
1. The `rfopt` git submodule has no uncommitted changes (working directory is clean).
2. The submodule `HEAD` matches the exact commit pinned in the NanaST repository.

Once verified, the runner invokes Cargo to build the path dependency and run the NanaST AMD64 integration test. The test follows this sequence:

1. Compile the pass-through test fixture into Forth source in memory.
2. Create an rfopt runtime instance and load that source.
3. Resolve the public words: `nana-input-0`, `nana-output-0`, `nana-init`, and `nana-scan`.
4. Obtain the two cell handles, call initialization, write input values, trigger scans, and read the output handle.
5. Verify all acceptance cases defined in `requirements.md`. If a step fails, report the compiler or runtime error context.

## Rejected Candidates

| Candidate | Why Rejected |
|---|---|
| **Interactive or batch CLI** | Requires process management, text I/O, and exit-code handling before the runtime can even execute minimal code. |
| **Generated Forth host-access words** | Expands NanaST's Forth vocabulary and turns a test harness into an implicit target dependency. |
| **BNC shared-memory or device host** | Unnecessarily couples this first compatibility gate to future BNC hardware and real-time work. |
| **Raw-address access from NanaST** | Exposes rfopt internal memory layout and cannot enforce runtime lifetimes safely. |

## Verification

To verify this gate, the project requires the following test evidence:

- **rfopt unit tests:** Prove source loading, word lookup, execution, and cell-handle lifetimes on AMD64 Linux.
- **Gate runner script (`scripts/run-rfopt-target.sh`):** Verify that the rfopt submodule is clean and matches the pinned commit before running Cargo.
- **NanaST integration test:** Execute the full pass-through sequence against the pinned rfopt revision and pass all acceptance criteria.
- **Error-handling tests:** Prove that missing words, invalid handles, and syntax/lookup errors fail with clear diagnostics.

> [!NOTE]
> The pinned rfopt revision now builds on AMD64, but it does not yet provide the source-loading and opaque host-cell API required for this gate. Once tests pass, they prove basic compatibility only. They do not prove real-time timing, memory limits, safety properties, BNC integration, or AArch64 support.

## Detailed-Design Handoffs

- **rfopt Rust API:** The accepted [rust-lifecycle.md](rust-lifecycle.md) defines opaque handle ownership, error enums, runtime creation, source parsing, native-code lifetime, and invalid-handle behavior.
- **NanaST test integration:** Add a path development dependency on the pinned rfopt submodule, implement `scripts/run-rfopt-target.sh`, and write the AMD64 integration test once the API is exposed.
- **rfopt AMD64 implementation:** Implement the source loader, native emitter, opaque host API, and rfopt unit tests from the accepted lifecycle design.
