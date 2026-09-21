---
type: "Detailed Software Design"
title: "Detailed Design: Discrete Ordinal CASE Statement"
description: "Type-anchored compiler representations and data-stack lifecycle invariants for discrete ordinal CASE statements."
status: "accepted"
tags: [detailed-design, nanast, rust, type-anchoring, lifecycle, case-statement, control-flow]
---

# Detailed Design: Discrete Ordinal CASE Statement

This companion document specifies the concrete Rust type definitions, compile-time type-anchored rules, and data-stack lifecycle invariants governing discrete ordinal `CASE` statements.

---

## 1. Type-Anchored Invariants (`type-anchored-spec`)

To enforce strict ordinal typing, reject floating-point selectors, detect duplicate match values at compile time, and preserve diagnostic spans:

### 1.1 Lexer & AST Definitions (`src/lexer.rs`, `src/ast.rs`)

```rust
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum TokenKind {
    // ... existing tokens ...
    Case,    // "CASE"
    Of,      // "OF"
    EndCase, // "END_CASE"
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct CaseBranch {
    pub match_values: Vec<Expression>,
    pub body: Vec<Statement>,
    pub span: Span,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum StatementKind {
    // ... other statement variants ...
    Case {
        selector: Expression,
        branches: Vec<CaseBranch>,
        else_body: Vec<Statement>,
    },
}
```

### 1.2 Type-Anchored Semantic Invariants (`src/sema.rs`)

1. **Ordinal Selector Invariant**:
   - `selector` must evaluate strictly to discrete ordinal integer types: `DataType::Int` or `DataType::Dint`.
   - Floating-point (`REAL`), boolean (`BOOL`), or aggregate types are rejected at compile time:
     `mismatched types: CASE selector requires INT or DINT, found REAL`.
2. **Compile-Time Constant Match Values & Duplicate Rejection**:
   - Every expression in `CaseBranch.match_values` must be a compile-time evaluatable constant matching the selector's signed integer width.
   - The semantic analyzer tracks a set of encountered values across all branches; duplicate match values trigger an error:
     `duplicate CASE match value: <val>`.
3. **Span Preservation**:
   - Each `CaseBranch` retains its source `Span` covering `match_values : body`.

---

## 2. Resource & Memory Lifecycle (`design-rust-lifecycles`)

### 2.1 Zero-Cost Runtime Invariant

- **RAM Footprint**: Zero bytes. No `VARIABLE` or `FVARIABLE` dictionary cells allocated.
- **Dynamic Allocation**: Zero heap allocations at runtime.
- **Initialization Stores**: Zero `nana-init` reset instructions emitted.

### 2.2 Stack Lifecycle & Neutrality Proof (`src/forth.rs`)

| Execution Stage | Forth Sequence | Stack Transition | Effect |
| :--- | :--- | :--- | :--- |
| **Selector Eval** | `emit_expression(selector)` | $S \to S \circ [\text{sel}]$ | Evaluated exactly once. |
| **Branch Match** | `DUP <val> =` (or `OVER ... OR`) | $S \circ [\text{sel}] \to S \circ [\text{sel}, \text{flag}]$ | Match boolean produced on TOS. |
| **Branch Taken** | `IF DROP <body_stmts>` | $S \circ [\text{sel}, \text{flag}] \to S \circ [\text{sel}] \to S$ | Selector consumed; body executed; stack restored to ambient $S$. |
| **Branch Missed** | `ELSE` | $S \circ [\text{sel}, \text{flag}] \to S \circ [\text{sel}]$ | Selector preserved on stack for subsequent branches. |
| **Fallback Path** | `DROP <else_stmts>` | $S \circ [\text{sel}] \to S$ | Selector consumed; fallback executed; stack restored to $S$. |
| **Construct Exit** | $N \times$ `THEN` | $S \to S$ | All branch exits rejoin with identical stack shape $S$ ($\Delta S = 0$). |

- **Stack Verifier Validation**: `rfopt::nanast::loader`'s verifier statically checks `true_stack == false_stack == 0` at every `THEN` join point, succeeding with zero stack drift.
- **Cumulative Nesting Bound**: Cumulative control-flow nesting depth (enclosing blocks + open branch frames + inner statement blocks) is bounded by `MAX_CONTROL_NESTING = 64`.

---

## 3. Component Responsibilities

1. **Lexer (`src/lexer.rs`)**: Tokenizes `CASE`, `OF`, `END_CASE`.
2. **Parser (`src/parser.rs`)**: Parses `CASE <expr> OF (<vals>: <stmts>)* [ELSE <stmts>] END_CASE;` into `StatementKind::Case`.
3. **Semantic Analyzer (`src/sema.rs`)**: Validates ordinal selector type (`INT`/`DINT`), rejects duplicate match values, and enforces nesting limit ($\le 64$).
4. **Forth Lowering (`src/forth.rs`)**: Emits single-selector evaluation, comparison ladders with immediate `DROP` on match, and fallback `DROP` before `THEN` ladder.
