# NanaST v0.1 Specification

## Status

**Superseded.** The approved [Forth-2012 vision](VISION.md) replaced this Wasm target on 2026-09-06. 

This document records the completed v0.1 WebAssembly baseline for historical and verification reference. The task checklist is recorded in [`tasks/todo.md`](../tasks/todo.md). This specification does not authorize future Wasm work. All normative language describes the completed v0.1 release, not current development directions.

## Objective

NanaST is a Rust command-line compiler for Botnana Control (BNC). Version 0.1 verified the end-to-end toolchain by compiling a small Structured Text (ST) program into a WebAssembly (Wasm) module. BNC could then execute this module cyclically on Linux using Wasmtime.

The goal was to provide a command-line tool for BNC developers to validate control logic. Success was defined as a reliable, automated compile-and-run workflow, rather than full IEC 61131-3 compliance.

### User Workflow

```text
$ nanastc compile examples/pass_through.st --output pass_through.wasm
$ bnc-wasm-host pass_through.wasm --scans 2
```

In this workflow:
1. The host loads the output Wasm module using Wasmtime.
2. It initializes the module once.
3. It calls the scan function twice and checks the resulting BNC output values.

## v0.1 Scope

### Supported Source Model

Each source file contains exactly one `PROGRAM` declaration. The v0.1 compiler supports:

- Scalar types: `BOOL`, `INT`, and `DINT`.
- Declaration blocks: `VAR`, `VAR_INPUT`, and `VAR_OUTPUT`.
- Literal initial values for `VAR` variables.
- Expressions: variable references, parentheses, unary `NOT`, unary negation, arithmetic, comparisons, and Boolean operations.
- Assignment statements.
- Conditional statements: `IF` / `THEN` / `ELSE` / `END_IF`.

Variables in `VAR_INPUT` and `VAR_OUTPUT` represent BNC I/O channels. Their zero-based index matches their declaration order in each block. For example:
- The first `VAR_INPUT` maps to input index `0`.
- The first `VAR_OUTPUT` maps to output index `0`.

### Explicitly Out of Scope

- Other IEC languages: Instruction List (IL), Sequential Function Chart (SFC), and graphical languages.
- Advanced program organization: functions, function blocks, timers, external libraries, configurations, resources, tasks, and direct-memory syntax (`%I*`, `%Q*`).
- Complex data types: arrays, structs, strings, floating-point numbers (`REAL`), dates, times, and user-defined types.
- Advanced runtime features: retained variables, online debugging, IDE integration, and production real-time guarantees.
- Full source or behavioral compatibility with MATIEC.

Unsupported language features must produce a compiler error with source line and column numbers, followed by a non-zero exit code. The compiler must never produce an incomplete or silent partial Wasm binary.

## BNC Wasm ABI

Every generated Wasm module follows this v0.1 ABI.

### Imports

The module imports two functions from the `bnc` namespace:

```text
read_input(index: i32) -> i32
write_output(index: i32, value: i32)
```

At this boundary, all values are represented as 32-bit integers (`i32`):
- `BOOL` values are normalized to `0` (`FALSE`) or `1` (`TRUE`).
- `INT` values represent signed 16-bit integers.
- `DINT` values represent signed 32-bit integers.
- At runtime, `INT` arithmetic wraps as signed 16-bit integers. The compiler folds constant expressions and reports an error if a constant result falls outside the `INT` range, rather than wrapping it.

### Exports

The module exports two lifecycle functions:

```text
nana_init() -> ()
nana_scan() -> ()
```

- **`nana_init`:** The BNC host must call this function once after instantiating the module, before running the first scan cycle. It sets initial default or declared values for `VAR` variables.
- **`nana_scan`:** The BNC host calls this function once per scan cycle.

During each scan cycle:
1. The module reads all `VAR_INPUT` values into memory before executing any statements.
2. It executes the program logic.
3. It writes all `VAR_OUTPUT` values out to the host.

This snapshot approach ensures inputs remain stable throughout a single cycle and prevents statement order from affecting output timing.

Internal `VAR` values persist across multiple calls to `nana_scan` for the lifetime of the Wasm instance. However, v0.1 does not preserve state after the Wasm instance is discarded or the host restarts.

## Tech Stack

- **Language:** Rust (2024 edition).
- **Executable:** Native Linux command-line tool (`nanastc`).
- **Target Output:** Core WebAssembly MVP (`wasm32` integer and control flow features only).
- **Runtime Environment:** Wasmtime 45.0.1 on Linux (pinned to match the Rust 1.93.1 toolchain).
- **Dependencies:** `wasm-encoder` (0.258.0) for Wasm binary generation; `wasmtime` (45.0.1) for integration tests.

The compiler runs as a native Rust program; only the compiled PLC program runs as Wasm.

## Commands

Standard commands used for the v0.1 build:

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
  cli.rs        # Argument parsing and exit code handling
  lexer.rs      # Source code tokenization
  parser.rs     # Parser that builds the AST from tokens
  ast.rs        # Abstract Syntax Tree definitions and source spans
  sema.rs       # Semantic analysis: types, names, and error reporting
  wasm.rs       # Emits core Wasm bytecode from a validated AST
tests/
  fixtures/     # Sample Structured Text input files
  compiler.rs   # Tests for parsing, type-checking, and bytecode validation
  runtime.rs    # End-to-end scan tests using Wasmtime
docs/
  SPEC-v0.1.md  # This document
```

The codebase remains a single Cargo package until components need to be reused elsewhere.

## Code Style

Use explicit domain types and return clear errors instead of panicking on user input.

```rust
fn compile(source: &str) -> Result<Vec<u8>, CompileError> {
    let tokens = lex(source)?;
    let program = parse(tokens)?;
    let program = analyze(program)?;
    emit_wasm(&program)
}
```

- Format code using `rustfmt`; ensure `cargo clippy` emits zero warnings.
- Use `snake_case` for functions and modules; use `UpperCamelCase` for types.
- Keep lexing, parsing, semantic analysis, and code generation cleanly separated.
- Include source spans (line and column numbers) in compiler diagnostics.
- Avoid adding dependencies for hypothetical future needs.

## Testing Strategy

Automated tests verify all supported language features:

- **Lexer/Parser tests:** Verify that valid syntax produces the expected AST, and invalid syntax reports correct source locations.
- **Semantic analysis tests:** Verify that undeclared variables, duplicate names, type mismatches, and unsupported features produce clear errors.
- **Code generation tests:** Verify that emitted bytecode is valid core WebAssembly.
- **Runtime integration tests:** Compile sample ST files, load them into Wasmtime with a mock BNC host, invoke `nana_init` and `nana_scan`, and verify output values.

The primary test case is a Boolean pass-through program: one `VAR_INPUT` connects directly to one `VAR_OUTPUT`. Changing the input value on the host must produce the corresponding output value after a scan cycle.

## Boundaries

### Always
- Follow the defined BNC Wasm ABI and test it thoroughly.
- Include automated tests for every behavior change.
- Run formatting, Clippy, and tests before committing.
- Maintain clean-room code: do not copy source code, tests, or text from MATIEC.

### Ask First
- Adding new Cargo dependencies.
- Modifying the Wasm ABI (imported or exported functions).
- Adding new Structured Text syntax.
- Splitting the project into a multi-crate workspace, altering CI, or adding real-time requirements.

### Never
- Commit generated build artifacts (`target/`), binaries, credentials, or environment-specific configuration.
- Silently ignore unsupported IEC syntax.
- Claim complete IEC 61131-3 or MATIEC compatibility for this minimal subset.

## Success Criteria

1. Running `nanastc compile <input.st> --output <output.wasm>` compiles valid v0.1 ST source into a valid Wasm module.
2. The generated module exports `nana_init` and `nana_scan`, and imports only the two specified `bnc` functions.
3. A Wasmtime test can initialize the module, run at least two scan cycles, and verify expected output changes.
4. Syntax errors, type errors, and unsupported features report precise source locations and leave the target file untouched. Successful compilation updates the output file atomically.
5. All verification commands (`cargo fmt --check`, `cargo clippy -- -D warnings`, `cargo test`) pass.

## Open Questions

- How will physical BNC I/O channels be mapped to zero-based declaration indices in production? (v0.1 relies on a mock test host; production mapping was deferred).
- Should future versions use the WebAssembly Component Model and WIT? (Deferred until a production host runtime is chosen).
