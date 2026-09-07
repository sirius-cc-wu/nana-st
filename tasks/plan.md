# NanaST v0.1 Implementation Plan

## Status

**Completed (Historical).** This plan implemented the original WebAssembly scope described in [`docs/SPEC-v0.1.md`](../docs/SPEC-v0.1.md). Verification of its completion is recorded in [`todo.md`](todo.md). 

On 2026-09-06, the approved [Forth-2012 vision](../docs/VISION.md) superseded the Wasm target. This document is kept for historical reference and does not represent active tasks.

## Objective

Build a native Rust command-line tool, `nanastc`, that compiles the v0.1 Structured Text subset into a core WebAssembly module. An integration test runs that module inside Wasmtime with a mock BNC host, verifying the `nana_init` and `nana_scan` lifecycle and input/output handling.

## Proposed Architecture

```text
CLI source path
     |
     v
lexer -> parser -> AST -> semantic analysis -> Wasm emitter -> .wasm
                                                        |
                                                        v
                                     Wasmtime test host with `bnc` imports
```

| Component | Responsibility | Depends on |
|---|---|---|
| **CLI** | Parses `nanastc compile <input> --output <output>`, reads/writes files, displays error diagnostics, and returns exit codes. | Compiler facade |
| **Lexer** | Converts source code text into tokens tagged with line/column spans. | Source span types |
| **Parser** | Parses programs, declarations, expressions, and statements into an Abstract Syntax Tree (AST). | Lexer, AST types |
| **Semantic analysis** | Resolves variable names, checks v0.1 types and assignments, and rejects unsupported features. | AST |
| **Wasm emitter** | Generates core Wasm bytecode matching the `bnc` imports and `nana_init`/`nana_scan` exports. | Typed AST |
| **Runtime test host** | Loads emitted modules in Wasmtime, supplies mock inputs/outputs, and verifies scan behavior. | Emitted module, ABI |

The v0.1 compiler generates Wasm bytecode directly from the validated AST. It omits an intermediate representation (IR) to keep the minimal compiler simple. An IR can be added later if language complexity requires it.

## Implementation Sequence

### 1. Establish the Compiler Boundary

- Add dependencies: `wasm-encoder` (0.258.0) and `wasmtime` (45.0.1). Wasmtime is pinned to 45.0.1 for compatibility with Rust 1.93.1.
- Define source spans, diagnostic error types, and the top-level `compile` function returning either Wasm bytes or structured errors.
- Implement basic CLI argument handling and file error messages.

**Checkpoint:** `nanastc compile` recognizes arguments and reports clear errors for missing or unreadable files.

### 2. Parse the v0.1 ST Subset

- Implement lexing for keywords, identifiers, literals, punctuation, and operators.
- Implement an operator-precedence parser for expressions, declarations, and statements.
- Attach source spans to tokens and AST nodes for accurate error reporting.

**Checkpoint:** Test fixtures parse a valid pass-through program, and invalid source files report accurate line and column numbers.

### 3. Validate Language Semantics

- Build symbol tables for `VAR`, `VAR_INPUT`, and `VAR_OUTPUT` blocks.
- Resolve variable names and reject duplicate declarations or undeclared names.
- Enforce type compatibility for `BOOL`, `INT`, and `DINT` expressions and assignments.
- Assign zero-based I/O channel indices based on declaration order.

**Checkpoint:** Valid typed programs pass analysis. Duplicate variables, unknown names, invalid operators, and unsupported features fail before code generation.

### 4. Emit the BNC Wasm ABI

- Emit only the specified `bnc.read_input` and `bnc.write_output` imports, and `nana_init` and `nana_scan` exports.
- Store scalar variables as global Wasm `i32` variables; initialize them during `nana_init`.
- In `nana_scan`, read all inputs into memory, execute statement logic, and write all outputs at the end of the scan.
- Validate emitted bytecode before writing the output file.

**Checkpoint:** Generated Wasm files pass validation and match the specified ABI signature exactly.

### 5. Verify Under Wasmtime

- Implement a mock BNC host in tests with inspectable input and output states.
- Compile and instantiate the Boolean pass-through test fixture.
- Call `nana_init`, modify mock inputs, call `nana_scan` twice, and assert expected outputs.
- Verify that a failed compilation leaves existing output files untouched.

**Checkpoint:** The complete v0.1 workflow is automated and passes under `cargo test`.

## Key Decisions and Risks

| Item | Plan | Mitigation / Decision Gate |
|---|---|---|
| **Wasm libraries** | Use `wasm-encoder` 0.258.0 for code generation and `wasmtime` 45.0.1 for runtime testing. | Wasmtime 45.0.1 is the latest version compatible with Rust 1.93.1. Re-evaluate when upgrading the toolchain. |
| **ABI stability** | Treat `bnc` imports and `nana_*` exports as a fixed interface contract. | Any ABI change requires a spec update and formal decision. |
| **Deterministic inputs** | Snapshot all inputs before statements run; flush all outputs after statements finish. | Runtime tests verify this execution order. |
| **Integer behavior** | Runtime `INT` math wraps as signed 16-bit integers. Out-of-range constant folding triggers compiler errors. | Unit tests cover both behaviors before code generation relies on them. |
| **Scope growth** | PLC features like functions, timers, direct `%I*` addressing, and retain variables introduce subtle semantics. | Reject them with clear diagnostics until explicitly specified. |
| **Clean-room development** | No code or fixtures from MATIEC may be copied. | Write original fixtures and review all contributions for provenance. |

## Verification Checkpoints

Every implementation step must pass:

```sh
cargo fmt --check
cargo clippy -- -D warnings
cargo test
```

The final milestone also verifies that emitted bytecode runs successfully in Wasmtime and satisfies the BNC ABI.

## Decisions Recorded

- Add `wasm-encoder` 0.258.0 and `wasmtime` 45.0.1 once this task list is approved.
- Runtime `INT` arithmetic wraps as signed 16-bit integers; out-of-range constants produce compile-time errors.
- This implementation plan is approved.
