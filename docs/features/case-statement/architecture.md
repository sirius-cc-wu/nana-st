---
type: "Software Architecture Design"
title: "Architecture: Discrete Ordinal CASE Statement"
description: "Architecture design for lowering discrete ordinal CASE statements to stack-neutral Forth branch ladders in rfopt."
status: "accepted"
tags: [architecture, nanast, rfopt, control-flow, case-statement, state-machine, stc]
---

# Architecture: Discrete Ordinal CASE Statement

## Architecture Question

How can NanaST introduce discrete ordinal `CASE` statements to model industrial SFC state machines while maintaining single-expression evaluation, zero runtime allocations, strict data-stack neutrality under `rfopt`'s static stack verifier, and respecting `rfopt`'s control-flow nesting ceiling?

## Selected Structure

```text
ST Source: CASE state OF 1: b1; 2, 3: b2; ELSE b_else; END_CASE;
  -> Lexer: Emits TokenKind::Case, TokenKind::Of, TokenKind::EndCase
  -> Parser: Constructs AST StatementKind::Case
  -> Semantic Analysis: Type-checks selector (INT/DINT), validates unique match values,
                        checks branches.len() <= 60
  -> Forth Codegen: Emits selector; for each branch: DUP <val> = IF DROP <body> ELSE ...;
                    fallback: DROP <else_body>; terminal: matching THENs
  -> rfopt Static Verifier: Confirms true_stack == false_stack == 0 at each IF/ELSE boundary
  -> Native STC Execution: Compiles directly into native jump tables / branch ladders
```

### Component Responsibilities

- **Compiler Lexer (`src/lexer.rs`):**
  - Adds keywords: `CASE`, `OF`, `END_CASE`.
  - Distinguishes `CASE` and `OF` tokens; handles optional `END_CASE` with colon/semicolon punctuation.
- **Compiler AST (`src/ast.rs`):**
  - Defines `CaseBranch { match_values: Vec<Expression>, body: Vec<Statement>, span: Span }`.
  - Adds variant `StatementKind::Case { selector: Expression, branches: Vec<CaseBranch>, else_body: Vec<Statement> }`.
- **Compiler Parser (`src/parser.rs`):**
  - `parse_case_statement`: Parses selector expression, expects `OF`.
  - While not `ELSE` and not `END_CASE`: parses one or more comma-delimited match expressions, expects `:`, parses statements until next case arm or terminator.
  - Optionally parses `ELSE <statements>`.
  - Expects `END_CASE` and optional semicolon.
- **Semantic Analyzer (`src/sema.rs`):**
  - Asserts `selector` evaluates to `DataType::Int` or `DataType::Dint`. Rejects `DataType::Real` and `DataType::Bool`.
  - Evaluates match values to constant values; maintains a set of seen values to detect duplicates.
  - Enforces `branches.len() <= 60` to stay strictly within `rfopt`'s `MAX_CONTROL_NESTING = 64`.
  - Recursively checks statements in all branch bodies.
- **Forth Code Generator (`src/forth.rs`):**
  - Emits selector expression once.
  - Lowers single-value branches using `DUP <val> = IF DROP <body> ELSE`.
  - Lowers multi-value branches using `DUP <v1> = OVER <v2> = OR IF DROP <body> ELSE`.
  - Emits fallback: `DROP` followed by `else_body`.
  - Emits matching `THEN` words for all open branch frames.
- **rfopt Runtime (`rfopt`):**
  - Verified against `rfopt::nanast::loader` with zero new words required.

## Detailed Component Design

### 1. AST Representation (`src/ast.rs`)

```rust
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct CaseBranch {
    pub match_values: Vec<Expression>,
    pub body: Vec<Statement>,
    pub span: Span,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum StatementKind {
    Assignment {
        target: Identifier,
        field: Option<Identifier>,
        value: Expression,
    },
    Invocation {
        instance: Identifier,
        arguments: Vec<NamedArgument>,
    },
    If {
        condition: Expression,
        then_body: Vec<Statement>,
        elsif_branches: Vec<ElsifBranch>,
        else_body: Vec<Statement>,
    },
    Case {
        selector: Expression,
        branches: Vec<CaseBranch>,
        else_body: Vec<Statement>,
    },
}
```

### 2. Forth Lowering Algorithm (`src/forth.rs`)

```rust
fn emit_case_statement(
    &mut self,
    selector: &Expression,
    branches: &[CaseBranch],
    else_body: &[Statement],
    tokens: &mut Vec<String>,
) -> Result<(), Diagnostic> {
    // 1. Evaluate selector once onto the stack (S: -- sel)
    self.emit_expression(selector, tokens)?;

    let mut open_frames = 0;
    for branch in branches {
        // 2. Emit comparison for branch match values
        // For single value: DUP <val> =
        // For multiple values: DUP <v1> = OVER <v2> = OR ...
        self.emit_case_match_condition(&branch.match_values, tokens)?;

        // IF consumes flag (S_true: -- sel ; S_false: -- sel)
        tokens.push("IF".to_string());
        // On match: drop selector and execute body (S_true: -- )
        tokens.push("DROP".to_string());
        for stmt in &branch.body {
            self.emit_statement(stmt, tokens)?;
        }

        // On mismatch: branch continues to ELSE (S_false: -- sel)
        tokens.push("ELSE".to_string());
        open_frames += 1;
    }

    // 3. Fallback path (S: -- sel): drop selector and execute optional else_body
    tokens.push("DROP".to_string());
    for stmt in else_body {
        self.emit_statement(stmt, tokens)?;
    }

    // 4. Close all open frames
    for _ in 0..open_frames {
        tokens.push("THEN".to_string());
    }

    Ok(())
}
```

### 3. Data Stack Invariant Proof

Let ambient data stack before `CASE` be $S$:
1. **Entry:** Selector evaluates onto stack: $S \circ [\text{sel}]$ (depth $+1$).
2. **Branch Condition:** Leaves $S \circ [\text{sel}, \text{flag}]$.
3. **`IF` execution:** Consumes $\text{flag}$.
   - **True Path:** Stack enters with $S \circ [\text{sel}]$. `DROP` consumes $\text{sel}$, leaving $S$. Branch body has net stack effect $0$. True path exits with stack $S$.
   - **False Path:** Stack enters with $S \circ [\text{sel}]$. Continues to next branch comparison or fallback.
4. **Fallback Path (`ELSE` of final branch):** Stack enters with $S \circ [\text{sel}]$. `DROP` consumes $\text{sel}$, leaving $S$. Fallback body has net stack effect $0$. Exits with stack $S$.
5. **Rejoining at `THEN`s:** Every branch path exits with identical stack state $S$. Net stack change of the complete construct is precisely $0$.
6. **Stack Shape Verification:** `rfopt::nanast::loader`'s verifier checks `true_stack == false_stack` at each `THEN` and succeeds without errors.

## Architectural Invariants

1. **Zero Runtime Submodule Additions:** Must require no additions or modifications to the words in `rfopt`.
2. **Single Evaluation Invariant:** The selector expression must only be evaluated once per scan execution.
3. **Strict Stack Neutrality:** Every execution path through the `CASE` statement must drop the selector and balance the stack prior to exiting.
4. **Compile-Time Nesting Ceiling:** Must reject `branches.len() > 60` with a compile-time diagnostic.
