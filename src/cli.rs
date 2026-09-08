use std::error::Error;
use std::fmt;
use std::fs::{self, OpenOptions};
use std::io::Write;
use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicU64, Ordering};

use crate::{CompileError, compile_source};

static TEMPORARY_OUTPUT_SEQUENCE: AtomicU64 = AtomicU64::new(0);

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
    InspectOutput {
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
            Self::InspectOutput { path, source } => {
                write!(
                    formatter,
                    "failed to inspect output {}: {source}",
                    path.display()
                )
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
            Self::ReadSource { source, .. }
            | Self::InspectOutput { source, .. }
            | Self::WriteOutput { source, .. } => Some(source),
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

    if output_aliases_input(&command.input, &command.output)? {
        return Err(CliError::Usage(
            "input and output paths must refer to different files".to_owned(),
        ));
    }

    let forth = compile_source(&source).map_err(|source| CliError::Compile {
        path: command.input.clone(),
        source,
    })?;
    write_output_atomically(&command.output, forth.as_bytes())
}

fn output_aliases_input(input: &Path, output: &Path) -> Result<bool, CliError> {
    let output_metadata = match fs::metadata(output) {
        Ok(metadata) => metadata,
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => return Ok(false),
        Err(source) => {
            return Err(CliError::InspectOutput {
                path: output.to_owned(),
                source,
            });
        }
    };

    #[cfg(unix)]
    {
        use std::os::unix::fs::MetadataExt;

        let input_metadata = fs::metadata(input).map_err(|source| CliError::ReadSource {
            path: input.to_owned(),
            source,
        })?;
        Ok(input_metadata.dev() == output_metadata.dev()
            && input_metadata.ino() == output_metadata.ino())
    }

    #[cfg(not(unix))]
    {
        let input = fs::canonicalize(input).map_err(|source| CliError::ReadSource {
            path: input.to_owned(),
            source,
        })?;
        let output = fs::canonicalize(output).map_err(|source| CliError::InspectOutput {
            path: output.to_owned(),
            source,
        })?;
        Ok(input == output)
    }
}

fn write_output_atomically(path: &Path, content: &[u8]) -> Result<(), CliError> {
    let directory = path
        .parent()
        .filter(|path| !path.as_os_str().is_empty())
        .unwrap_or_else(|| Path::new("."));

    for _ in 0..100 {
        let temporary = directory.join(format!(
            ".nanastc-{}-{}.tmp",
            std::process::id(),
            TEMPORARY_OUTPUT_SEQUENCE.fetch_add(1, Ordering::Relaxed)
        ));
        let mut file = match OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(&temporary)
        {
            Ok(file) => file,
            Err(error) if error.kind() == std::io::ErrorKind::AlreadyExists => continue,
            Err(source) => {
                return Err(CliError::WriteOutput {
                    path: path.to_owned(),
                    source,
                });
            }
        };

        if let Err(source) = file.write_all(content).and_then(|()| file.sync_all()) {
            drop(file);
            let _ = fs::remove_file(&temporary);
            return Err(CliError::WriteOutput {
                path: path.to_owned(),
                source,
            });
        }
        drop(file);

        if let Err(source) = fs::rename(&temporary, path) {
            let _ = fs::remove_file(&temporary);
            return Err(CliError::WriteOutput {
                path: path.to_owned(),
                source,
            });
        }
        return Ok(());
    }

    Err(CliError::WriteOutput {
        path: path.to_owned(),
        source: std::io::Error::new(
            std::io::ErrorKind::AlreadyExists,
            "could not allocate a temporary output file",
        ),
    })
}

fn usage() -> String {
    "usage: nanastc compile <input.st> --output <output.fs>".to_owned()
}
