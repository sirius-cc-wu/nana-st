---
type: "Rust Lifecycle Design"
title: "Rust Lifecycle Design: Discrete Ordinal CASE Statement"
description: "Ownership, AST lifecycle, stack verification discipline, and nesting limits for CASE statements."
status: "accepted"
language: "rust"
tags: [rust, lifecycle, nanast, rfopt, control-flow, case-statement]
---

# Rust Lifecycle Design: Discrete Ordinal CASE Statement

## Ownership and Resource Allocation

| Resource | Owner | Release Behavior |
|---|---|---|
| `TokenKind::Case`, `Of`, `EndCase` | Lexer token stream | Consumed during parser step |
| `CaseBranch` AST Nodes | `StatementKind::Case` struct | Dropped when AST completes semantic analysis |
| Selector Expression | `StatementKind::Case` | Dropped when compilation completes |
| Forth Control-Flow Stack | rfopt STC Compiler Frame | Unwound and resolved before word compilation completes |
| Forth Data Stack (Selector Value) | STC execution frame | Dropped via `DROP` on all true and fallback branches |

`CASE` statements are pure control-flow structures. They do not introduce runtime memory allocations, static state cells, or background handles.

## Execution Lifecycle

```text
Scan Execution:
  -> Host invokes nana-scan
  -> Selector expression evaluated once onto data stack (S: -- sel)
  -> Branch 1 condition tested (DUP <v1> =)
       If matched: executes DROP (S: -- ), executes body, exits directly to outer exit
  -> Branch 2 condition tested (DUP <v2> =)
       If matched: executes DROP (S: -- ), executes body, exits directly to outer exit
  -> ...
  -> If no branch matched: executes DROP (S: -- ), executes optional fallback body
  -> All paths exit through matching THEN instructions with balanced data stack (S: -- )
  -> nana-scan continues or returns
```

## Failure Modes & Error Handling

1. **Syntactic Failures (Parse Time):**
   - Missing `OF` following `CASE <expr>` reports `expected 'OF', found '<token>'`.
   - Missing `:` after match value reports `expected ':', found '<token>'`.
   - Missing `END_CASE` reports unclosed `CASE` block.
2. **Semantic Failures (Type & Range Check Time):**
   - Selector evaluating to `REAL` or `BOOL` reports `expected INT or DINT for CASE selector, found <type>`.
   - Non-integer match value reports `expected integer literal for CASE branch`.
   - Duplicate match value reports `duplicate case match value '<val>'` with span pointing to the duplicate.
   - More than 60 branches reports `CASE statement exceeds maximum supported branch limit of 60`.
3. **Control Stack Imbalance Prevention:**
   - Compiler emits exactly as many `THEN` words as open `IF` frames ($N$).
   - Fallback path unconditionally emits `DROP` to ensure stack balance even when `else_body` is empty or omitted.

## Verification Obligations

1. Verify execution when selector matches the first branch.
2. Verify execution when selector matches intermediate branches.
3. Verify multi-value branch execution (`val1, val2:`).
4. Verify fallback `ELSE` execution when no branches match.
5. Verify omitted `ELSE` execution when no branches match (confirms stack remains balanced and state untouched).
6. Verify rejection of duplicate match values and non-ordinal selector types.
7. Verify multi-scan execution against pinned `rfopt` runtime on AMD64 and AArch64 targets.
