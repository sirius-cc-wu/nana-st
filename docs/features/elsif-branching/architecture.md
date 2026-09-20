---
type: "Software Architecture Design"
title: "Architecture: ELSIF Multi-Branch Conditional Syntax"
description: "Architecture design for lowering ELSIF multi-branch conditionals to pure Forth branch structures in rfopt."
status: "accepted"
tags: [architecture, nanast, rfopt, conditionals, elsif, forth-2012, control-flow]
---

# Architecture: ELSIF Multi-Branch Conditional Syntax

## Architecture Question

How can NanaST introduce `ELSIF` multi-branch conditional ladders into Structured Text without adding new runtime words to `rfopt`, while preserving exact source spans for diagnostics, maintaining compile-time control stack balance, and ensuring strict short-circuit evaluation semantics?

## Selected Structure

```text
ST Source: IF c1 THEN b1 ELSIF c2 THEN b2 ELSE b3 END_IF;
  -> Lexer: Emits TokenKind::Elsif
  -> Parser: Constructs AST StatementKind::If with elsif_branches: Vec<ElsifBranch>
  -> Semantic Analysis: Checks condition types (DataType::Bool) and recursively validates bodies
  -> Forth Codegen: Emits c1 IF b1 ELSE c2 IF b2 ELSE b3 THEN THEN
  -> rfopt Native Loader: Ingests pure Forth structured conditional words
  -> Native STC Execution: Compiles to native conditional branch instructions on AMD64 and AArch64
```

### Component Responsibilities

- **Compiler Lexer (`src/lexer.rs`):**
  - Adds `TokenKind::Elsif` mapped to `"ELSIF"`.
  - Case-insensitive matching preserves standard IEC 61131-3 tokenization rules.
- **Compiler AST (`src/ast.rs`):**
  - Defines dedicated `ElsifBranch` struct containing `condition: Expression`, `body: Vec<Statement>`, and `span: Span`.
  - Updates `StatementKind::If` to include `elsif_branches: Vec<ElsifBranch>`.
  - Preserving distinct AST representation avoids lossy parser-level desugaring and maintains precise diagnostic spans.
- **Compiler Parser (`src/parser.rs`):**
  - `parse_if_statement` parses the initial `IF <expr> THEN <stmts>`.
  - Loops while `peek_is(&TokenKind::Elsif)` to parse zero or more `ElsifBranch` structures.
  - Optionally parses an `ELSE <stmts>` fallback block.
  - Enforces closure with `END_IF` and optional semicolon.
- **Semantic Analyzer (`src/sema.rs`):**
  - Analyzes each branch condition expression with expected type `DataType::Bool`.
  - Reports `mismatched types` if any condition does not evaluate to boolean.
  - Recursively validates each branch's statement list against symbol tables.
- **Forth Code Generator (`src/forth.rs`):**
  - Emits the initial condition followed by `IF` and the `then_body`.
  - For each `ElsifBranch`, emits `ELSE`, the branch condition, `IF`, and the branch body, incrementing the open frame counter.
  - If an `else_body` is present and non-empty, emits `ELSE` followed by the fallback statements.
  - Emits exactly as many `THEN` words as open `IF` branch frames were created, guaranteeing strict control-flow stack balance.
- **rfopt Runtime Submodule (`rfopt`):**
  - Executes existing core words `IF`, `ELSE`, `THEN` via native STC emission.
  - Zero runtime modifications or new Forth word requirements.

## Component Design

### 1. AST Definition (`src/ast.rs`)

```rust
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ElsifBranch {
    pub condition: Expression,
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
}
```

### 2. Parser Logic (`src/parser.rs`)

```rust
fn parse_if_statement(&mut self) -> Result<Statement, Diagnostic> {
    let start = self.consume_exact(&TokenKind::If)?.span.start;
    let condition = self.parse_expression(0)?;
    self.consume_exact(&TokenKind::Then)?;
    
    let then_body = self.parse_statement_list_until(|kind| {
        matches!(kind, TokenKind::Elsif | TokenKind::Else | TokenKind::EndIf)
    })?;

    let mut elsif_branches = Vec::new();
    while self.match_token(&TokenKind::Elsif) {
        let branch_start = self.previous().span.start;
        let branch_condition = self.parse_expression(0)?;
        self.consume_exact(&TokenKind::Then)?;
        let branch_body = self.parse_statement_list_until(|kind| {
            matches!(kind, TokenKind::Elsif | TokenKind::Else | TokenKind::EndIf)
        })?;
        let branch_span = Span {
            start: branch_start,
            end: self.previous().span.end,
        };
        elsif_branches.push(ElsifBranch {
            condition: branch_condition,
            body: branch_body,
            span: branch_span,
        });
    }

    let else_body = if self.match_token(&TokenKind::Else) {
        self.parse_statement_list_until(|kind| matches!(kind, TokenKind::EndIf))?
    } else {
        Vec::new()
    };

    let end = self.consume_exact(&TokenKind::EndIf)?.span.end;
    let _ = self.match_token(&TokenKind::Semicolon);

    Ok(Statement {
        kind: StatementKind::If {
            condition,
            then_body,
            elsif_branches,
            else_body,
        },
        span: Span { start, end },
    })
}
```

### 3. Forth Lowering Algorithm (`src/forth.rs`)

To ensure standard Forth-2012 structured jump resolution without control-flow leaks:

```rust
fn emit_if_statement(
    &mut self,
    condition: &Expression,
    then_body: &[Statement],
    elsif_branches: &[ElsifBranch],
    else_body: &[Statement],
    tokens: &mut Vec<String>,
) -> Result<(), Diagnostic> {
    // 1. Emit primary condition and initial IF
    self.emit_expression(condition, tokens)?;
    tokens.push("IF".to_string());
    for stmt in then_body {
        self.emit_statement(stmt, tokens)?;
    }

    // 2. Emit each ELSIF branch as ELSE <cond> IF <body>
    let mut frame_count = 1;
    for branch in elsif_branches {
        tokens.push("ELSE".to_string());
        self.emit_expression(&branch.condition, tokens)?;
        tokens.push("IF".to_string());
        for stmt in &branch.body {
            self.emit_statement(stmt, tokens)?;
        }
        frame_count += 1;
    }

    // 3. Emit optional ELSE fallback
    if !else_body.is_empty() {
        tokens.push("ELSE".to_string());
        for stmt in else_body {
            self.emit_statement(stmt, tokens)?;
        }
    }

    // 4. Emit matching THEN words for all open frames
    for _ in 0..frame_count {
        tokens.push("THEN".to_string());
    }

    Ok(())
}
```

### 4. Stack Invariant Proof

- **Data Stack Balance:**
  - Before `IF`: The evaluated condition expression leaves exactly 1 boolean cell on top of the data stack (`TOS`).
  - `IF` consumes this boolean cell.
  - Inside each branch body (`then_body`, `branch.body`, `else_body`), statements only load and store to named cells (`@`, `!`, `F@`, `F!`). At statement boundaries, net data stack effect is zero.
  - Across all execution paths (whether branch 1, branch $k$, or fallback is taken), the net data stack change upon reaching `THEN` is exactly 0.
- **Control-Flow Stack Balance:**
  - Each `IF` places an unresolved forward branch reference on the Forth control stack.
  - Each `ELSE` resolves the preceding `IF` to point to the instruction after `ELSE`, and pushes a new forward branch to the end of the entire construct.
  - Emitting exactly `frame_count` `THEN` words resolves every outstanding forward jump in LIFO order.

## Invariants & Guardrails

1. **No Runtime Additions:** NanaST must not require or emit synthetic runtime helpers for `ELSIF`.
2. **Short-Circuit Guarantee:** Once a condition evaluates to `TRUE`, no subsequent condition expressions shall be evaluated. In STC native execution, this translates to an unconditional direct branch past all subsequent checks.
3. **Purity of Expressions:** Conditions in `ELSIF` clauses are pure expressions that evaluate to boolean cells without side-effects on process-image variables.
4. **Compile-Time Nesting Bound:** Cumulative control-flow nesting depth (enclosing `IF` blocks plus sequential `ELSIF` branch frames and branch-body controls) must not exceed `rfopt`'s `MAX_CONTROL_NESTING = 64`. Semantic analysis must reject excessive nesting at compile time with a source-located diagnostic.
