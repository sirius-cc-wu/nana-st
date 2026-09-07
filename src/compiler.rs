use std::error::Error;
use std::fmt;

use crate::forth::{ForthError, emit as emit_forth};
use crate::lexer::{LexError, lex};
use crate::parser::{ParseError, parse};
use crate::sema::{AnalyzedProgram, SemanticError, analyze};
use crate::wasm::{WasmError, emit};

#[derive(Debug)]
pub enum CompileError {
    Lex(LexError),
    Parse(ParseError),
    Semantic(SemanticError),
    Forth(ForthError),
    Wasm(WasmError),
}

impl fmt::Display for CompileError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Lex(error) => error.fmt(formatter),
            Self::Parse(error) => error.fmt(formatter),
            Self::Semantic(error) => error.fmt(formatter),
            Self::Forth(error) => error.fmt(formatter),
            Self::Wasm(error) => error.fmt(formatter),
        }
    }
}

impl Error for CompileError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            Self::Lex(error) => Some(error),
            Self::Parse(error) => Some(error),
            Self::Semantic(error) => Some(error),
            Self::Forth(error) => Some(error),
            Self::Wasm(error) => Some(error),
        }
    }
}

pub fn compile_source(source: &str) -> Result<Vec<u8>, CompileError> {
    let program = analyze_source(source)?;
    emit(&program).map_err(CompileError::Wasm)
}

pub fn compile_forth_source(source: &str) -> Result<String, CompileError> {
    let program = analyze_source(source)?;
    emit_forth(&program).map_err(CompileError::Forth)
}

fn analyze_source(source: &str) -> Result<AnalyzedProgram, CompileError> {
    let tokens = lex(source).map_err(CompileError::Lex)?;
    let program = parse(tokens).map_err(CompileError::Parse)?;
    analyze(program).map_err(CompileError::Semantic)
}
