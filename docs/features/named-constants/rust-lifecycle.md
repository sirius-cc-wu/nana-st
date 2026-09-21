---
type: "Rust Lifecycle Design"
title: "Rust Lifecycle Design: Named Constants (VAR CONSTANT Profile)"
description: "Ownership, compile-time evaluation lifecycle, and memory guarantees for named constants."
status: "accepted"
language: "rust"
tags: [rust, lifecycle, nanast, rfopt, constants, immutability, var-constant]
---

# Rust Lifecycle Design: Named Constants (VAR CONSTANT Profile)

## Ownership and Resource Allocation

| Resource | Owner | Release Behavior |
|---|---|---|
| `TokenKind::Constant` / `TokenKind::VarConstant` | Lexer token stream | Consumed during parser step |
| `Declaration` with `StorageClass::Constant` | `Program` AST | Dropped after semantic analysis completes |
| `ConstantSymbol` & `ConstantValue` | `AnalyzedProgram` symbol table | Dropped when Forth emission completes |
| Runtime Memory Cells | None (zero allocation) | No runtime allocations occur |
| Executable Code Bytes | `rfopt::nanast::Runtime` STC text | Allocated once at program load; dropped with `Runtime` |

Constants exist entirely within the compiler's symbol resolution and code generation pipelines. At runtime, they become immediate operand values embedded in the executable machine code. They do not occupy cells in the runtime's data or float memory segments.

## Execution Lifecycle

```text
Compilation Phase:
  -> Lexer identifies VAR CONSTANT or VAR_CONSTANT block
  -> Parser parses identifier, type, and requires initializer
  -> Semantic analyzer evaluates initializer to ConstantValue
  -> Constant symbol registered in immutable symbol table
  -> Analyzes program statements; rejects any assignment to constant symbols
  -> Forth codegen emits immediate literal values directly into expressions
  -> rfopt STC compiles literals directly into machine instructions

Scan Phase (Runtime):
  -> Host invokes nana-scan
  -> CPU loads immediate values directly into registers (zero memory bus traffic)
  -> Zero chance of memory corruption or race conditions altering constant values
```

## Failure Modes & Error Handling

1. **Missing Initializer (Parse Time):**
   - Syntax: `VAR CONSTANT MAX_VAL : INT; END_VAR`
   - Aborts at parse time with `constant declaration 'MAX_VAL' requires an initializer`.
2. **Assignment Attempt (Semantic Analysis Time):**
   - Statement: `MAX_VAL := 10;`
   - Diagnostic: `cannot assign to constant 'MAX_VAL'` with span pointing precisely to the assignment target.
3. **Non-Constant Initializer Expression:**
   - Declaration: `VAR CONSTANT DYNAMIC_VAL : INT := input_cell + 1; END_VAR`
   - Diagnostic: `constant initializer must be a compile-time constant expression`.
4. **Invalid Type for Constant Block:**
   - Declaration: `VAR CONSTANT my_timer : TON; END_VAR`
   - Diagnostic: `type 'TON' cannot be declared in a constant block`.

## Verification Obligations

1. Verify that `VAR CONSTANT` declarations of `BOOL`, `INT`, `DINT`, and `REAL` compile without errors.
2. Verify that constant symbols evaluate to their exact specified values in expressions.
3. Verify that assignments to constant identifiers fail semantic validation with exact error messages.
4. Verify that generated Forth code contains zero `VARIABLE` or `FVARIABLE` allocations for constant symbols.
5. Verify that `nana-init` contains zero store instructions for constant symbols.
6. Verify multi-scan execution in `rfopt` produces identical, immutable results across consecutive scan cycles.
