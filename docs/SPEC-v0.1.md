# NanaST v0.1 Specification

## Status

Superseded as NanaST's current target specification by the approved
[rtForth vision](VISION.md) on 2026-09-06. This document records the completed
Wasm v0.1 implementation and remains historical build and behavior evidence;
the completed task list is [`tasks/todo.md`](../tasks/todo.md). It does not
authorize future Wasm-target work. All remaining present-tense and normative
wording records the completed v0.1 baseline; it is not current direction or
instruction.

## Objective

NanaST is a Rust command-line compiler for Botnana Control (BNC). Version 0.1 proved the end-to-end toolchain: it compiled a deliberately small Structured Text (ST) program into a WebAssembly module that BNC could execute cyclically on Linux through Wasmtime.

The intended user was a BNC developer validating controller logic from the command line. Success was not IEC 61131-3 completeness or production controller readiness; it was a reliable, tested compile-and-run vertical slice.

### User workflow

```text
$ nanastc compile examples/pass_through.st --output pass_through.wasm
$ bnc-wasm-host pass_through.wasm --scans 2
```

The host loads the output module with Wasmtime, initializes it once, invokes its scan function twice, and can observe the expected BNC output values.

## v0.1 Scope

### Supported source model

A source file contains exactly one `PROGRAM` declaration. The compiler supports:

- `BOOL`, `INT`, and `DINT` scalar values.
- `VAR`, `VAR_INPUT`, and `VAR_OUTPUT` declaration blocks.
- Literal initializers for `VAR` declarations.
- Variable references, parentheses, unary `NOT` and negation, arithmetic, comparisons, and Boolean operations.
- Assignment statements.
- `IF` / `THEN` / `ELSE` / `END_IF` statements.

`VAR_INPUT` and `VAR_OUTPUT` variables are BNC I/O mappings. Their zero-based index is their declaration order within their respective block. For example, the first `VAR_INPUT` is read from input index `0`; the first `VAR_OUTPUT` is written to output index `0`.

### Explicitly out of scope

- Instruction List (IL), Sequential Function Chart (SFC), and graphical languages.
- Functions, function blocks, timers, external libraries, configurations, resources, tasks, and direct-address syntax.
- Arrays, structures, strings, real numbers, time/date types, and user-defined types.
- Retained/persistent variables, online debugging, an IDE, and production real-time guarantees.
- Full source or behavioral compatibility with MATIEC.

Unsupported syntax or semantics must produce a source-located diagnostic and a non-zero compiler exit status; they must not silently produce a partial Wasm module.

## BNC Wasm ABI

Each generated module has this v0.1 core Wasm ABI.

### Imports

The module imports these functions from the `bnc` import module:

```text
read_input(index: i32) -> i32
write_output(index: i32, value: i32)
```

Values use Wasm `i32` at this boundary. `BOOL` values are normalized to `0` or `1`; `INT` uses signed 16-bit semantics; and `DINT` uses signed 32-bit semantics. Runtime `INT` arithmetic wraps as signed 16-bit arithmetic. The compiler folds constant expressions and diagnoses a constant result that is outside the `INT` range instead of wrapping it.

### Exports

```text
nana_init() -> ()
nana_scan() -> ()
```

The BNC host must call `nana_init` exactly once after instantiation and before the first scan. It calls `nana_scan` once per controller scan cycle.

For every scan, the generated module reads all `VAR_INPUT` values before executing program statements, then writes all `VAR_OUTPUT` values after execution. This gives one program execution a stable input snapshot and avoids output timing depending on statement order.

`VAR` state survives across calls to `nana_scan` during one Wasm instance lifetime. `nana_init` assigns default or declared initial values. v0.1 does not preserve state after an instance is discarded or the controller restarts.

## Tech Stack

- Rust, edition 2024.
- A native Linux command-line compiler named `nanastc`.
- WebAssembly binary output targeting the core Wasm MVP (`wasm32` integer/control-flow features only).
- Wasmtime 45.0.1 as the Linux development and integration-test runtime; this is the newest supported release compatible with the project Rust toolchain (1.93.1).
- `wasm-encoder` 0.258.0 for binary generation and `wasmtime` 45.0.1 for runtime integration tests.

The compiler itself is a native Rust executable in v0.1; only the generated controller program is Wasm.

## Commands

Commands expected when implementation begins:

```sh
cargo fmt --check
cargo clippy -- -D warnings
cargo test
cargo run -- compile examples/pass_through.st --output target/pass_through.wasm
```

## Intended Project Structure

```text
src/
  main.rs       # CLI entry point
  cli.rs        # command parsing and process exit behavior
  lexer.rs      # source text to tokens
  parser.rs     # tokens to AST
  ast.rs        # syntax tree and source spans
  sema.rs       # names, types, and unsupported-feature diagnostics
  wasm.rs       # typed AST to a core Wasm module
tests/
  fixtures/     # ST input programs
  compiler.rs   # parsing, diagnostics, and output validation
  runtime.rs    # Wasmtime end-to-end scan-cycle tests
docs/
  SPEC-v0.1.md  # this document
```

The project remains a single Cargo package until independently reusable components justify a workspace split.

## Code Style

Use explicit domain types and propagate source-aware failures rather than panicking on user input.

```rust
fn compile(source: &str) -> Result<Vec<u8>, CompileError> {
    let tokens = lex(source)?;
    let program = parse(tokens)?;
    let program = analyze(program)?;
    emit_wasm(&program)
}
```

- Format with `rustfmt`; keep `clippy` warnings at zero.
- Use `snake_case` for functions/modules and `UpperCamelCase` for types.
- Keep parsing, semantic analysis, and code generation separate.
- Include source spans in parse and semantic diagnostics.
- Do not add a dependency solely for anticipated future needs.

## Testing Strategy

Tests define the supported language behavior.

- **Lexer/parser unit tests:** valid constructs produce the intended AST; invalid syntax identifies its source location.
- **Semantic unit tests:** undeclared variables, incompatible types, duplicate declarations, and unsupported features fail with diagnostics.
- **Code-generation tests:** emitted modules validate as core Wasm.
- **Runtime integration tests:** compile fixture ST source, instantiate it in Wasmtime with a fake BNC host, call `nana_init` and `nana_scan`, and assert recorded output values.

The first required runtime fixture is a Boolean pass-through program: one `VAR_INPUT` is assigned to one `VAR_OUTPUT`; changing the host input changes the observed output after a scan.

## Boundaries

### Always

- Preserve the defined BNC Wasm ABI and verify it end-to-end.
- Add a test before or with every behavior change.
- Run formatting, Clippy, and tests before committing.
- Keep the implementation clean-room: do not copy MATIEC source, tests, or documentation.

### Ask first

- Adding Cargo dependencies.
- Changing the Wasm import/export ABI.
- Expanding the supported ST subset.
- Creating a workspace, changing CI, or adding persistent state or real-time behavior.

### Never

- Commit generated `target/` output, binaries, secrets, or machine-specific configuration.
- Silently accept unsupported IEC features.
- Claim full IEC 61131-3 or MATIEC compatibility from this v0.1 subset.

## Success Criteria

1. `nanastc compile <input.st> --output <output.wasm>` accepts a valid v0.1 ST program and writes a valid Wasm module.
2. The module exports `nana_init` and `nana_scan` and imports only the specified `bnc` functions.
3. A Wasmtime integration test can initialize the module, run at least two scans, and observe the expected BNC output changes.
4. Invalid syntax, semantic errors, and unsupported features identify the relevant source location and do not create or modify the requested output module. Successful output replaces an existing module atomically.
5. `cargo fmt --check`, `cargo clippy -- -D warnings`, and `cargo test` pass.

## Open Questions

- What controller configuration will bind named physical BNC I/O to the declaration-order indices? v0.1 tests use a fake host; production configuration is out of scope.
- Should a future BNC ABI use the WebAssembly Component Model and WIT? It is deferred until a production host interface is needed.
