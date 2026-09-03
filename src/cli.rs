use std::error::Error;
use std::fmt;
use std::fs;
use std::path::PathBuf;

use crate::{CompileError, compile_source};

#[derive(Debug, PartialEq, Eq)]
pub struct CompileCommand {
    pub input: PathBuf,
    pub output: PathBuf,
}

#[derive(Debug)]
pub enum CliError {
    Usage(String),
    ReadSource {
        path: PathBuf,
        source: std::io::Error,
    },
    Compile {
        path: PathBuf,
        source: CompileError,
    },
    WriteOutput {
        path: PathBuf,
        source: std::io::Error,
    },
}

impl fmt::Display for CliError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Usage(message) => formatter.write_str(message),
            Self::ReadSource { path, source } => {
                write!(formatter, "failed to read {}: {source}", path.display())
            }
            Self::Compile { path, source } => {
                write!(formatter, "failed to compile {}: {source}", path.display())
            }
            Self::WriteOutput { path, source } => {
                write!(formatter, "failed to write {}: {source}", path.display())
            }
        }
    }
}

impl Error for CliError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            Self::Usage(_) => None,
            Self::ReadSource { source, .. } | Self::WriteOutput { source, .. } => Some(source),
            Self::Compile { source, .. } => Some(source),
        }
    }
}

pub fn parse_compile_command(args: &[String]) -> Result<CompileCommand, CliError> {
    let [subcommand, input, output_flag, output] = args else {
        return Err(CliError::Usage(usage()));
    };

    if subcommand != "compile" || output_flag != "--output" {
        return Err(CliError::Usage(usage()));
    }

    Ok(CompileCommand {
        input: PathBuf::from(input),
        output: PathBuf::from(output),
    })
}

pub fn run(args: &[String]) -> Result<(), CliError> {
    let command = parse_compile_command(args)?;
    let source = fs::read_to_string(&command.input).map_err(|source| CliError::ReadSource {
        path: command.input.clone(),
        source,
    })?;
    let wasm = compile_source(&source).map_err(|source| CliError::Compile {
        path: command.input.clone(),
        source,
    })?;

    fs::write(&command.output, wasm).map_err(|source| CliError::WriteOutput {
        path: command.output,
        source,
    })
}

fn usage() -> String {
    "usage: nanastc compile <input.st> --output <output.wasm>".to_owned()
}
