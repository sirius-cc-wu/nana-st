pub mod ast;
pub mod cli;
pub mod compiler;
pub mod lexer;
pub mod parser;
pub mod sema;
pub mod wasm;

pub use compiler::{CompileError, compile_source};
