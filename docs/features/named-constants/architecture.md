---
type: "Software Architecture Design"
title: "Architecture: Named Constants (VAR CONSTANT Profile)"
description: "Architecture design for compiling immutable named constants to immediate Forth literals in rfopt."
status: "accepted"
tags: [architecture, nanast, rfopt, constants, immutability, var-constant, stc]
---

# Architecture: Named Constants (VAR CONSTANT Profile)

## Architecture Question

How can NanaST incorporate immutable named constants (`VAR CONSTANT`) into its Structured Text compiler pipeline while guaranteeing compile-time immutability, eliminating runtime RAM cell consumption, avoiding memory bus reads during cyclic scans, and leveraging `rfopt`'s native Subroutine Threaded Code (STC) immediate literal compiler?

## Selected Structure

```text
ST Source: VAR CONSTANT LIMIT : REAL := 26.0; END_VAR (or VAR_CONSTANT ... END_VAR)
  -> Lexer: Emits TokenKind::Constant or TokenKind::VarConstant
  -> Parser: Constructs Declaration with StorageClass::Constant and mandatory initializer
  -> Semantic Analysis: Evaluates initializer, stores evaluated ConstantValue in symbol table,
                        and rejects assignments targeting constant symbols
  -> Forth Codegen: Omits VARIABLE / FVARIABLE allocation; omits nana-init writes;
                    emits constant value as immediate literal in expressions (omits @ / F@)
  -> rfopt STC Backend: Compiles immediate literals directly into machine instructions (e.g. mov / fmov)
```

### Component Responsibilities

- **Compiler Lexer (`src/lexer.rs`):**
  - Adds `TokenKind::Constant` matching case-insensitive `"CONSTANT"`.
  - Adds `TokenKind::VarConstant` matching case-insensitive `"VAR_CONSTANT"`.
- **Compiler AST (`src/ast.rs`):**
  - Expands `StorageClass` with `StorageClass::Constant`.
  - Reuses existing `Declaration` struct with `initializer: Option<Expression>`.
- **Compiler Parser (`src/parser.rs`):**
  - In `parse_program`: Extends the declaration block loop condition to accept `TokenKind::VarConstant`.
  - In `parse_declaration_block`: Accepts both `TokenKind::VarConstant` and `TokenKind::Var` followed by `TokenKind::Constant` to set `StorageClass::Constant`.
  - In `parse_declaration`: When `storage == StorageClass::Constant`, asserts that `initializer.is_some()`. If absent, raises diagnostic: `constant declaration '<name>' requires an initializer`.
- **Semantic Analyzer (`src/sema.rs`):**
  - Extends `ConstantValue` enum to include `Real(f64)` alongside `Bool`, `Int`, and `Dint`.
  - Evaluates constant initializers at compile time.
  - In `analyze_statement`: If an assignment target resolves to a variable with `StorageClass::Constant`, immediately rejects with diagnostic: `cannot assign to constant '<name>'`.
  - Rejects declarations of function block primitives (`TON`, `TOF`, `R_TRIG`, `F_TRIG`) under `StorageClass::Constant`.
- **Forth Code Generator (`src/forth.rs`):**
  - Variable Declaration Phase: Filters out `StorageClass::Constant` from `VARIABLE` and `FVARIABLE` emissions.
  - Optional Forth Constant Phase: Emits `<val> CONSTANT nana-const-<name>` for integer/boolean constants to support dictionary inspection.
  - Initialization Phase (`nana-init`): Emits zero initialization instructions for constants.
  - Expression Lowering Phase: When encountering an identifier resolving to a constant, emits the literal representation directly (e.g. `26.0e` or `5000`) without emitting `@` or `F@` fetch words.
- **rfopt Runtime (`rfopt`):**
  - STC code generation directly encodes immediate integer and floating-point literals into register loads (`mov reg, imm` on x86_64 / aarch64), bypassing data cache lines.

## Component Design

### 1. AST Updates (`src/ast.rs`)

```rust
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum StorageClass {
    Local,
    Input,
    Output,
    Constant,
}
```

### 2. Lexer and Parser Block Recognition (`src/lexer.rs`, `src/parser.rs`)

To support standard IEC 61131-3 `VAR CONSTANT ... END_VAR` blocks alongside the `VAR_CONSTANT ... END_VAR` alias:

**Lexer keyword mapping (`src/lexer.rs`):**
```rust
match identifier.to_ascii_uppercase().as_str() {
    // ...
    "CONSTANT" => TokenKind::Constant,
    "VAR_CONSTANT" => TokenKind::VarConstant,
    // ...
}
```

**Parser declaration block dispatch (`src/parser.rs`):**
```rust
// In parse_program:
while matches!(
    self.peek_kind(),
    Some(TokenKind::Var | TokenKind::VarInput | TokenKind::VarOutput | TokenKind::VarConstant)
) {
    declarations.extend(self.parse_declaration_block()?);
}

// In parse_declaration_block:
let storage = match self.advance() {
    Some(Token {
        kind: TokenKind::Var,
        ..
    }) => {
        if self.peek_is(&TokenKind::Constant) {
            self.advance();
            StorageClass::Constant
        } else {
            StorageClass::Local
        }
    }
    Some(Token {
        kind: TokenKind::VarConstant,
        ..
    }) => StorageClass::Constant,
    Some(Token {
        kind: TokenKind::VarInput,
        ..
    }) => StorageClass::Input,
    Some(Token {
        kind: TokenKind::VarOutput,
        ..
    }) => StorageClass::Output,
    _ => return Err(self.error_here("expected a variable declaration block")),
};
```

### 3. Semantic Analysis Invariant Checks (`src/sema.rs`)

```rust
// In statement analysis (assignment check):
let symbol = self.lookup_variable(&target.name)?;
if symbol.storage == StorageClass::Constant {
    return Err(Diagnostic::error(
        target.span,
        format!("cannot assign to constant '{}'", target.name),
    ));
}
```

### 4. Constant Evaluation in Symbol Resolution (`src/sema.rs`)

```rust
pub struct ConstantSymbol {
    pub name: String,
    pub data_type: DataType,
    pub value: ConstantValue,
    pub span: Span,
}

#[derive(Debug, Clone, PartialEq)]
pub enum ConstantValue {
    Bool(bool),
    Int(i16),
    Dint(i32),
    Real(f64),
}
```

### 5. Forth Code Generation Pipeline (`src/forth.rs`)

```rust
// In emit_declarations:
for decl in &program.declarations {
    if decl.storage == StorageClass::Constant {
        match decl.data_type {
            DataType::Bool => {
                let val_str = if decl.constant_value == ConstantValue::Bool(true) { "TRUE" } else { "FALSE" };
                tokens.push(val_str.to_string());
                tokens.push("CONSTANT".to_string());
                tokens.push(format!("nana-const-{}", decl.name));
            }
            DataType::Int | DataType::Dint => {
                let val_str = match &decl.constant_value {
                    ConstantValue::Int(i) => i.to_string(),
                    ConstantValue::Dint(d) => d.to_string(),
                    _ => unreachable!(),
                };
                tokens.push(val_str);
                tokens.push("CONSTANT".to_string());
                tokens.push(format!("nana-const-{}", decl.name));
            }
            DataType::Real => {
                let val_str = match &decl.constant_value {
                    ConstantValue::Real(r) => format!("{}e", r),
                    _ => unreachable!(),
                };
                tokens.push(val_str);
                tokens.push("FCONSTANT".to_string());
                tokens.push(format!("nana-const-{}", decl.name));
            }
            _ => unreachable!(),
        }
        continue;
    }
    // Normal VARIABLE / FVARIABLE emission...
}
```

In expression evaluation, referencing a constant emits its immediate literal directly into the token stream:
```rust
ExpressionKind::Variable(ident) => {
    if let Some(c) = self.lookup_constant(&ident.name) {
        match c.value {
            ConstantValue::Bool(b) => tokens.push(if b { "TRUE" } else { "FALSE" }.to_string()),
            ConstantValue::Int(i) => tokens.push(i.to_string()),
            ConstantValue::Dint(d) => tokens.push(d.to_string()),
            ConstantValue::Real(r) => tokens.push(format!("{}e", r)),
        }
        return Ok(());
    }
    // Otherwise emit variable name and fetch operator (@ or F@)
}
```

## Architectural Invariants

1. **Zero RAM Footprint Invariant:** Constant definitions must never allocate Forth memory cells (`VARIABLE` or `FVARIABLE`).
2. **Zero Init Footprint Invariant:** Constant definitions must never emit store instructions inside `nana-init`.
3. **No Memory Load Invariant:** Expression references to constant symbols must not emit `@` or `F@` memory fetch instructions.
4. **Compile-Time Rejection Invariant:** Any mutation attempt on a constant identifier must be caught and rejected at compile time with a source-located error.
