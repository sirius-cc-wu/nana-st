# Implementation Plan: Generalized Forth Code Generation

## Objective

Generalize `src/forth.rs` to compile any valid Structured Text program analyzed by `src/sema.rs` into valid Forth-2012 source code, preserving the exact token output for the `pass_through.st` fixture while expanding test coverage to arithmetic, comparisons, logic, branching, and local variables.

## Tasks

- [ ] **Task 1: Generalize Forth AST/Sema Emitter** (`forth-codegen-emitter`)
  - **Acceptance:** `src/forth.rs` recursively translates `AnalyzedProgram`, emitting `VARIABLE` declarations, `nana-init`, and `nana-scan` definitions. Emits correct postfix operations for arithmetic, comparisons, logic, assignments, and `IF`/`ELSE` control flow.
  - **Verify:** `cargo check` and `cargo test --test forth` compile cleanly.
  - **Files:** `src/forth.rs`.

- [ ] **Task 2: Expand Unit Test Suite** (`forth-codegen-tests`)
  - **Acceptance:** `tests/forth.rs` verifies:
    - Exact preservation of `pass_through.st` output.
    - Arithmetic expressions (`+`, `-`, `*`, `/`, unary negation).
    - Comparisons (`=`, `<>`, `<`, `<=`, `>`, `>=`).
    - Logical operators (`AND`, `OR`, `NOT`).
    - Local variable declarations and initialization.
    - Branching with `IF` / `ELSE` / `END_IF`.
  - **Verify:** `cargo test` passes.
  - **Files:** `tests/forth.rs`.

- [ ] **Task 3: Integration Verification with rfopt** (`forth-integration-gate`)
  - **Acceptance:** Verify that existing AMD64 integration gate script `scripts/run-rfopt-target.sh` passes with zero regressions.
  - **Verify:** `bash scripts/run-rfopt-target.sh` exits 0.
  - **Files:** `scripts/run-rfopt-target.sh`, `tests/rfopt_target.rs`.
