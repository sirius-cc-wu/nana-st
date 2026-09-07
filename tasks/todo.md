# NanaST v0.1 Task List

## Status

**Completed (Historical).** This task list tracked the implementation of the original WebAssembly plan in [`plan.md`](plan.md). 

On 2026-09-06, the approved [Forth-2012 vision](../docs/VISION.md) superseded the Wasm target. This file is retained as a historical record of completed work.

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
  - **Acceptance:** Running `nanastc compile <input> --output <output>` checks required arguments, reads source files, writes only on successful compilation, prints formatted errors to stderr, and returns non-zero on failure. Adds `wasm-encoder` as a main dependency and `wasmtime` as a dev-dependency.
  - **Verification:** CLI integration tests verify missing arguments, unreadable files, and compilation errors. `cargo fmt --check`, `cargo clippy -- -D warnings`, and `cargo test` pass.
  - **Files:** `Cargo.toml`, `Cargo.lock`, `src/lib.rs`, `src/main.rs`, `src/cli.rs`, `tests/cli.rs`.

- [x] **Task: Define syntax structures and lexer** (`syntax-lexer`)
  - **Depends on:** `foundation-cli`
  - **Acceptance:** Tokens with line/column spans recognize v0.1 keywords, identifiers, integer and Boolean literals, punctuation, and operators. Invalid characters and unclosed tokens report precise source locations.
  - **Verification:** Unit tests cover valid token streams and lexical errors. `cargo fmt --check`, `cargo clippy -- -D warnings`, and `cargo test` pass.
  - **Files:** `src/lib.rs`, `src/ast.rs`, `src/lexer.rs`, `tests/lexer.rs`.

- [x] **Task: Parse v0.1 program grammar** (`syntax-parser`)
  - **Depends on:** `syntax-lexer`
  - **Acceptance:** Parses exactly one `PROGRAM`, the three declaration blocks (`VAR`, `VAR_INPUT`, `VAR_OUTPUT`), operator expressions, assignments, and `IF` statements into an AST. Syntax errors report source spans without panicking.
  - **Verification:** Unit tests cover declarations, assignments, `IF`/`ELSE` branching, operator precedence, malformed syntax, and trailing tokens. `cargo fmt --check`, `cargo clippy -- -D warnings`, and `cargo test` pass.
  - **Files:** `src/lib.rs`, `src/ast.rs`, `src/parser.rs`, `tests/parser.rs`.

- [x] **Task: Semantic analysis for names and types** (`semantic-analysis`)
  - **Depends on:** `syntax-parser`
  - **Acceptance:** Resolves variables, rejects duplicate or undeclared names, verifies valid operations and assignments for `BOOL`, `INT`, and `DINT`, restricts initializers to literal values in `VAR` blocks, and assigns declaration-order I/O indices. Runtime `INT` operations wrap; folded constant results outside signed 16-bit bounds produce compiler errors.
  - **Verification:** Unit tests cover valid type-checking and diagnostics for duplicates, unknown names, type mismatches, non-literal initializers, and constant overflows. `cargo fmt --check`, `cargo clippy -- -D warnings`, and `cargo test` pass.
  - **Files:** `src/lib.rs`, `src/sema.rs`, `tests/sema.rs`.

- [x] **Task: Generate valid BNC ABI WebAssembly** (`wasm-emitter`)
  - **Depends on:** `semantic-analysis`
  - **Acceptance:** Emits a validated core Wasm module matching the specified `bnc.read_input`/`bnc.write_output` imports and `nana_init`/`nana_scan` exports. Preserves program state in global variables, snapshots inputs before statements run, and flushes outputs after statements finish.
  - **Verification:** Tests compile valid source files, validate output bytes with Wasmtime, and verify exported and imported signatures. `cargo fmt --check`, `cargo clippy -- -D warnings`, and `cargo test` pass.
  - **Files:** `src/lib.rs`, `src/compiler.rs`, `src/wasm.rs`, `tests/compiler.rs`, `tests/cli.rs`.

- [x] **Task: End-to-end Wasmtime scan cycle test** (`runtime-vertical-slice`)
  - **Depends on:** `wasm-emitter`
  - **Acceptance:** A mock BNC host inside tests instantiates the Boolean pass-through module, calls `nana_init` and `nana_scan` with changed inputs, and verifies expected outputs. Multi-I/O tests prove input snapshots precede output flushing, state persists between scans, and runtime `INT` arithmetic wraps properly. A failed compilation leaves existing files untouched.
  - **Verification:** Runtime tests, CLI file-safety tests, and manual CLI runs pass. `cargo fmt --check`, `cargo clippy -- -D warnings`, and `cargo test` pass.
  - **Files:** `Cargo.toml`, `src/cli.rs`, `src/sema.rs`, `src/wasm.rs`, `tests/fixtures/pass_through.st`, `tests/runtime.rs`, `tests/sema.rs`, `tests/cli.rs`.
