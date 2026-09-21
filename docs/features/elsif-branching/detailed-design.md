---
type: "Detailed Software Design"
title: "Detailed Design: ELSIF Multi-Branch Conditional Syntax"
description: "Type-anchored compiler representations and zero-cost control-flow lifecycle invariants for ELSIF multi-branch conditionals."
status: "accepted"
tags: [detailed-design, nanast, rust, type-anchoring, lifecycle, elsif, control-flow]
---

# Detailed Design: ELSIF Multi-Branch Conditional Syntax

This companion document specifies the concrete Rust type definitions, compile-time type-anchored rules, and control-flow lifecycle invariants governing `ELSIF` multi-branch conditionals.

---

## 1. Type-Anchored Invariants (`type-anchored-spec`)

To prevent AST representation loss, ensure source-located error reporting, and guarantee type safety at compile time:

### 1.1 Lexer & AST Definitions (`src/lexer.rs`, `src/ast.rs`)

```rust
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum TokenKind {
    // ... existing tokens ...
    Elsif, // "ELSIF" (case-insensitive)
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ElsifBranch {
    pub condition: Expression,
    pub body: Vec<Statement>,
    pub span: Span,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum StatementKind {
    // ... other statement variants ...
    If {
        condition: Expression,
        then_body: Vec<Statement>,
        elsif_branches: Vec<ElsifBranch>,
        else_body: Vec<Statement>,
    },
}
```

### 1.2 Type-Anchored Semantic Invariants (`src/sema.rs`)

1. **Boolean Condition Invariant**:
   - Every condition in `If.condition` and each `ElsifBranch.condition` must evaluate strictly to `DataType::Bool`.
   - Any non-boolean expression triggers compile-time diagnostic:
     `mismatched types: expected BOOL, found <type>`.
2. **Span Preservation**:
   - Each `ElsifBranch` retains its distinct source `Span` covering `ELSIF <expr> THEN <stmts>`.
   - Parse errors and type errors report exact line/column locations of the offending branch.
3. **Exhaustive Traversal**:
   - Statement validation recursively descends into `then_body`, every `branch.body` in `elsif_branches`, and `else_body`.

---

## 2. Resource & Memory Lifecycle (`design-rust-lifecycles`)

### 2.1 Zero-Cost Runtime Invariant

- **RAM Footprint**: Zero bytes. No `VARIABLE` or `FVARIABLE` cells are allocated in the Forth dictionary for `ELSIF` constructs.
- **Dynamic Allocation**: Zero heap allocations at runtime.
- **Initialization Stores**: Zero `nana-init` reset instructions emitted.

### 2.2 Stack & Control-Flow Invariants (`src/forth.rs`)

| Lifecycle Dimension | Invariant Specification |
| :--- | :--- |
| **Control Stack Balance** | For $N$ `ELSIF` branches, the emitter opens $1$ initial `IF` frame and $N$ branch frames (`ELSE ... IF`). Exactly $1 + N$ matching `THEN` tokens are emitted at block termination, balancing the Forth compiler control stack. |
| **Data Stack Neutrality** | Each condition evaluation leaves $1$ boolean cell on TOS, immediately consumed by `IF`. Statement bodies operate on storage cells via `@`/`!`. Net data stack effect across any branch path is strictly $\Delta S = 0$. |
| **Nesting Bound** | Cumulative control-flow nesting depth (enclosing blocks + sequential branch frames) is bounded by `MAX_CONTROL_NESTING = 64`. Exceeding this triggers a compile-time error. |
| **Short-Circuit Execution** | Lowers to STC forward conditional jumps. The first branch whose condition evaluates to `TRUE` executes its body and executes an unconditional jump to the final `THEN`, bypassing all subsequent evaluations. |

---

## 3. Component Responsibilities

1. **Lexer (`src/lexer.rs`)**: Tokenizes `ELSIF` case-insensitively into `TokenKind::Elsif`.
2. **Parser (`src/parser.rs`)**: Parses `IF <expr> THEN <stmts> (ELSIF <expr> THEN <stmts>)* [ELSE <stmts>] END_IF;` into `StatementKind::If`.
3. **Semantic Analyzer (`src/sema.rs`)**: Validates condition types and nesting depth ($\le 64$).
4. **Forth Lowering (`src/forth.rs`)**: Emits `c1 IF b1 ELSE c2 IF b2 ... ELSE bn THEN ... THEN` ladder.
