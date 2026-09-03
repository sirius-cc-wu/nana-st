# NanaST v0.1 Task List

## Status

Completed. This task list implements the approved [`plan.md`](plan.md).

## Dependency Order

```text
foundation-cli
    -> syntax-lexer
    -> syntax-parser
    -> semantic-analysis
    -> wasm-emitter
    -> runtime-vertical-slice
```

- [x] **Task: Establish the compiler facade and CLI** (`foundation-cli`)
  - Acceptance: `nanastc compile <input> --output <output>` validates required arguments, reads source, writes only successful compilation output, prints structured user-facing errors to stderr, and returns non-zero on failure. `wasm-encoder` is a normal dependency and `wasmtime` is a development dependency.
  - Verify: CLI tests cover missing arguments, unreadable source, and stub compilation failure; `cargo fmt --check`, `cargo clippy -- -D warnings`, and `cargo test` pass.
  - Files: `Cargo.toml`, generated `Cargo.lock`, `src/lib.rs`, `src/main.rs`, `src/cli.rs`, `tests/cli.rs`.

- [x] **Task: Define syntax data and lex v0.1 source** (`syntax-lexer`)
  - Depends on: `foundation-cli`
  - Acceptance: Span-carrying tokens recognize v0.1 keywords, identifiers, integer and Boolean literals, punctuation, and all supported operators. Invalid characters and unterminated token forms report their source location.
  - Verify: Unit tests cover representative valid token streams and lexical errors; `cargo fmt --check`, `cargo clippy -- -D warnings`, and `cargo test` pass.
  - Files: `src/lib.rs`, `src/ast.rs`, `src/lexer.rs`, `tests/lexer.rs`.

- [x] **Task: Parse the v0.1 program grammar** (`syntax-parser`)
  - Depends on: `syntax-lexer`
  - Acceptance: Parse exactly one `PROGRAM`, the three permitted declaration blocks, precedence-aware expressions, assignments, and `IF` statements into the AST. Syntax errors report a span and do not panic.
  - Verify: Unit tests cover declarations, assignments, `IF`/`ELSE`, expression precedence, malformed syntax, and source after `END_PROGRAM`; `cargo fmt --check`, `cargo clippy -- -D warnings`, and `cargo test` pass.
  - Files: `src/lib.rs`, `src/ast.rs`, `src/parser.rs`, `tests/parser.rs`.

- [x] **Task: Analyze names, types, and constants** (`semantic-analysis`)
  - Depends on: `syntax-parser`
  - Acceptance: Resolve variables, reject duplicate or unknown declarations, enforce the permitted `BOOL`/`INT`/`DINT` operations and assignments, reject non-literal or non-`VAR` initializers, and assign declaration-order I/O indices. Runtime `INT` operations are marked as wrapping; a folded constant outside the signed 16-bit range is a diagnostic.
  - Verify: Unit tests cover valid analysis plus duplicate, unknown-name, type-mismatch, unsupported-feature, non-literal initializer, and constant-overflow diagnostics; `cargo fmt --check`, `cargo clippy -- -D warnings`, and `cargo test` pass.
  - Files: `src/lib.rs`, `src/sema.rs`, `tests/sema.rs`.

- [x] **Task: Generate valid BNC ABI Wasm** (`wasm-emitter`)
  - Depends on: `semantic-analysis`
  - Acceptance: The compiler pipeline emits a validated core Wasm module with only the specified `bnc.read_input`/`bnc.write_output` imports and `nana_init`/`nana_scan` exports. The emitter uses persistent scalar state, snapshots inputs before statements, and flushes outputs afterward.
  - Verify: Compiler tests compile valid source, validate emitted bytes with Wasmtime, and assert the exact imports/exports; `cargo fmt --check`, `cargo clippy -- -D warnings`, and `cargo test` pass.
  - Files: `src/lib.rs`, `src/compiler.rs`, `src/wasm.rs`, `tests/compiler.rs`, `tests/cli.rs`.

- [x] **Task: Prove the Wasmtime scan-cycle vertical slice** (`runtime-vertical-slice`)
  - Depends on: `wasm-emitter`
  - Acceptance: A fake BNC Wasmtime host compiles and instantiates the Boolean pass-through fixture, calls `nana_init` and `nana_scan` across changed inputs, and observes the expected output. A multi-I/O scan-cycle test proves input snapshots precede output flushing and scalar state persists across scans; an end-to-end test proves runtime `INT` wrapping. A failed compile leaves an existing output unmodified; a successful compile atomically replaces it.
  - Verify: Runtime integration tests, CLI output-safety tests, and a manual `cargo run --bin nanastc -- compile` invocation pass; `cargo fmt --check`, `cargo clippy -- -D warnings`, and `cargo test` pass.
  - Files: `Cargo.toml`, `src/cli.rs`, `src/sema.rs`, `src/wasm.rs`, `tests/fixtures/pass_through.st`, `tests/runtime.rs`, `tests/sema.rs`, `tests/cli.rs`.
