# NanaST v0.1 Implementation Plan

## Status

Draft for review. This plan implements the approved scope in [`docs/SPEC-v0.1.md`](../docs/SPEC-v0.1.md); it does not authorize implementation until this plan is approved.

## Objective

Deliver a native Rust CLI, `nanastc`, that compiles the v0.1 Structured Text subset into a core Wasm module. An integration test runs that module under Wasmtime with a fake BNC host and proves the `nana_init` / `nana_scan` lifecycle and BNC I/O behavior.

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
| CLI | Parse `nanastc compile <input> --output <output>`; read/write files; render diagnostics; return correct exit status. | compiler facade |
| Lexer | Convert source text into span-carrying ST tokens. | source/span types |
| Parser | Parse the supported program, declarations, expressions, and statements into an AST. | lexer, AST |
| Semantic analysis | Resolve names; enforce v0.1 types and assignment rules; reject unsupported forms. | AST |
| Wasm emitter | Convert validated program state and statements into the defined `bnc` imports and `nana_init` / `nana_scan` exports. | typed AST |
| Runtime test host | Instantiate emitted modules in Wasmtime; provide input/output imports; assert scan behavior. | emitted module, ABI |

The v0.1 compiler directly emits Wasm from a validated AST. It deliberately has no separate IR: the supported language is small, and an IR should be introduced only when a later feature makes direct emission difficult to maintain.

## Implementation Sequence

### 1. Establish the compiler boundary

- Add the approved Wasm encoder and Wasmtime test dependencies.
- Define source spans, diagnostic types, and the compile facade returning either Wasm bytes or source-aware errors.
- Define CLI argument and filesystem error behavior.

**Checkpoint:** `nanastc compile` recognizes its required arguments and reports a usable error for missing or unreadable input.

### 2. Parse the v0.1 ST subset

- Implement lexical recognition for keywords, identifiers, literals, punctuation, and operators.
- Implement a precedence-aware expression parser and program/declaration/statement parser.
- Preserve spans from source through AST nodes.

**Checkpoint:** parser fixtures cover a valid pass-through program and malformed source reports a line/column location.

### 3. Validate language semantics

- Build declaration scopes for `VAR`, `VAR_INPUT`, and `VAR_OUTPUT`.
- Resolve references; reject duplicates and undeclared names.
- Enforce `BOOL`, `INT`, and `DINT` operator and assignment compatibility.
- Allocate BNC input/output indices from declaration order.

**Checkpoint:** valid typed fixtures pass; duplicate declarations, unknown variables, invalid operators, and unsupported declarations fail without generating Wasm.

### 4. Emit the BNC Wasm ABI

- Emit only the `bnc.read_input` and `bnc.write_output` imports and `nana_init` / `nana_scan` exports from the specification.
- Represent scalar program state as Wasm `i32` globals; initialize `VAR` state in `nana_init`.
- At scan start, read every input into program state; emit assignments and `IF` control flow; at scan end, write every output.
- Validate emitted bytes before writing the output file.

**Checkpoint:** generated modules validate and their imported/exported ABI exactly matches the specification.

### 5. Prove the vertical slice under Wasmtime

- Implement a fake BNC host with inspectable input and output state.
- Compile and instantiate a Boolean pass-through fixture.
- Call `nana_init`, change host input values, call `nana_scan` twice, and assert the expected outputs.
- Add CLI integration coverage that ensures failed compilation does not leave a usable output file.

**Checkpoint:** the documented v0.1 user workflow is automated and passes with `cargo test`.

## Key Decisions and Risks

| Item | Plan | Mitigation / decision gate |
|---|---|---|
| Wasm libraries | Add `wasm-encoder` for binary generation and `wasmtime` for runtime integration tests. | Requires dependency approval before implementation. |
| ABI stability | Treat the spec's `bnc` imports and `nana_*` exports as a tested contract. | ABI changes require a spec and decision update. |
| Input determinism | Snapshot all inputs before program statements, then flush outputs after them. | Runtime tests assert the ordering behavior. |
| IEC integer semantics | `INT` overflow policy is not yet selected. | Select and document wrap, trap, or diagnostic behavior before semantic/code-generation work. |
| Scope growth | PLC features such as functions, timers, direct I/O addresses, and retain state create distinct semantics. | Keep them rejected with diagnostics until separately specified. |
| Clean-room provenance | No MATIEC material may enter the implementation or fixtures. | Write original fixtures from the approved spec and review contributions for provenance. |

## Verification Checkpoints

Each implementation increment must pass:

```sh
cargo fmt --check
cargo clippy -- -D warnings
cargo test
```

The final increment additionally proves that emitted output is accepted by Wasmtime and meets the BNC ABI contract.

## Requires Approval Before Implementation

1. Add `wasm-encoder` and `wasmtime` Cargo dependencies.
2. Choose the v0.1 `INT` overflow policy:
   - **wrap** as a signed 16-bit value;
   - **trap** at runtime on overflow; or
   - **diagnose** potentially overflowing constant expressions only.
3. Approve this plan before producing the implementation task list.
