---
type: "Detailed Design"
language: "rust"
status: "accepted"
---

# Detailed Design: Named Constants (VAR CONSTANT Profile)

## 1. Type-Anchored Invariants (`type-anchored-spec`)

### AST Representation (`src/ast.rs`)

```rust
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum StorageClass {
    Local,
    Input,
    Output,
    Constant,
}
```

### Lexer Keywords (`src/lexer.rs`)

Matches case-insensitive `"CONSTANT"` and the standard `"VAR_CONSTANT"` alias:

```rust
match identifier.to_ascii_uppercase().as_str() {
    "CONSTANT" => TokenKind::Constant,
    "VAR_CONSTANT" => TokenKind::VarConstant,
    // ...
}
```

### Declaration Block Parser (`src/parser.rs`)

Accepts both `VAR CONSTANT ... END_VAR` and `VAR_CONSTANT ... END_VAR`:

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
    Some(Token { kind: TokenKind::Var, .. }) => {
        if self.peek_is(&TokenKind::Constant) {
            self.advance();
            StorageClass::Constant
        } else {
            StorageClass::Local
        }
    }
    Some(Token { kind: TokenKind::VarConstant, .. }) => StorageClass::Constant,
    Some(Token { kind: TokenKind::VarInput, .. }) => StorageClass::Input,
    Some(Token { kind: TokenKind::VarOutput, .. }) => StorageClass::Output,
    _ => return Err(self.error_here("expected a variable declaration block")),
};
```

### Immutability Protection & Symbol Table (`src/sema.rs`)

Rejects mutation of constant identifiers during statement semantic validation:

```rust
if symbol.storage == StorageClass::Constant {
    return Err(Diagnostic::error(
        target.span,
        format!("cannot assign to constant '{}'", target.name),
    ));
}
```

Evaluates initializer expressions into closed primitive constant values:

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

## 2. Resource & Memory Lifecycle (`design-rust-lifecycles`)

- **Zero Target RAM Footprint:** Constant definitions omit Forth `VARIABLE` and `FVARIABLE` declarations, consuming zero bytes of target data memory.
- **Zero Initialization Overhead:** Compiler generates zero store instructions in `nana-init` reset code.
- **Immediate Machine Literal Lowering:** Referencing a constant identifier emits an immediate literal (`mov reg, imm` / `fmov reg, imm`) in `rfopt` STC emitter, completely bypassing memory bus loads (`@`, `F@`) during cyclic scans (`nana-scan`).
- **Compilation Memory Boundary:** AST declaration nodes and symbol table values exist solely during compilation passes and are dropped before runtime loading.
