pub mod ast;
pub mod cli;
pub mod lexer;

use std::error::Error;
use std::fmt;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CompileError {
    NotImplemented,
}

impl fmt::Display for CompileError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::NotImplemented => formatter.write_str("compilation is not implemented yet"),
        }
    }
}

impl Error for CompileError {}

pub fn compile_source(_source: &str) -> Result<Vec<u8>, CompileError> {
    Err(CompileError::NotImplemented)
}
