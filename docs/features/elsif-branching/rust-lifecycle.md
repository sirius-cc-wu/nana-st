---
type: "Rust Lifecycle Design"
title: "Rust Lifecycle Design: ELSIF Multi-Branch Conditional Syntax"
description: "Ownership, AST lifecycle, and stack memory discipline for ELSIF multi-branch statements."
status: "accepted"
language: "rust"
tags: [rust, lifecycle, nanast, rfopt, conditionals, elsif]
---

# Rust Lifecycle Design: ELSIF Multi-Branch Conditional Syntax

## Ownership and Resource Allocation

| Resource | Owner | Release Behavior |
|---|---|---|
| `TokenKind::Elsif` | Lexer token stream | Consumed during parser step |
| `ElsifBranch` AST Nodes | `StatementKind::If` struct | Dropped when AST completes semantic analysis |
| Condition Expressions | `ElsifBranch` | Dropped when compiler pass finishes |
| Emitted Forth Tokens | Codegen string buffer | Dropped after compilation emits Forth source text |
| Forth Control-Flow Stack | rfopt STC Compiler Frame | Unwound and resolved before word compilation terminates |
| Forth Data Stack | Virtual machine execution frame | Balanced (net delta 0) upon completing the construct |

`ELSIF` constructs are purely syntactic and control-flow transformations. They introduce zero dynamic heap allocations at runtime, zero static variable cells, and zero runtime handles.

## Execution Lifecycle

```text
Scan Cycle:
  -> Host invokes nana-scan
  -> Condition 1 evaluated (TOS has boolean)
  -> IF branch taken if TRUE:
       Executes Body 1
       Jumps directly to outer exit
  -> If FALSE, execution jumps to ELSE (Condition 2 evaluation)
  -> IF branch taken if TRUE:
       Executes Body 2
       Jumps directly to outer exit
  -> If all conditions FALSE, executes optional fallback ELSE body
  -> All paths exit via matching THEN instructions
  -> nana-scan continues with next statement or returns
```

## Failure Modes & Error Handling

1. **Syntactic Failures (Parse Time):**
   - Missing `THEN` following `ELSIF <expr>` produces immediate diagnostic `expected 'THEN', found '<token>'`.
   - Trailing `ELSIF` without expression or body produces `unexpected end of input`.
   - Missing `END_IF` reports unclosed conditional block.
2. **Semantic Failures (Type Check Time):**
   - Non-boolean expression in `ELSIF` condition produces `mismatched types: expected 'BOOL', found '<type>'` with accurate span pointing to the invalid expression.
   - Undeclared identifier inside an `ELSIF` body reports unknown identifier diagnostic without corrupting outer symbol scopes.
3. **Control-Flow Imbalance (Compiler Invariant):**
   - Number of emitted `THEN` words must strictly equal $1 + (\text{number of ELSIF branches})$. Emitting mismatched counts triggers an internal compiler diagnostic and aborts code generation before feeding text to `rfopt`.

## Verification Obligations

1. Verify that single and multiple chained `ELSIF` clauses compile cleanly.
2. Verify short-circuiting: confirm that mutating a variable in body 1 prevents condition 2 from evaluating if condition 1 was true.
3. Verify that omitting `ELSE` with all conditions false preserves existing variable states untouched.
4. Verify complete stack balance on both AMD64 and AArch64 targets in `rfopt`.
